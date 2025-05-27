using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Projet.Infrastructure;
using Projet.Model;

namespace Projet.Service
{
    public class BackupService : IBackupService, IDisposable
    {
        public event Action<StatusEntry> StatusUpdated;

        private readonly ILogger _logger;
        private readonly IJobRepository _repo;
        private readonly Settings _settings;
        private readonly List<BackupJob> _jobs;
        private readonly ThreadPoolManager _threadPool;
        private CancellationTokenSource _cancellationTokenSource;

        public BackupService(ILogger logger, IJobRepository repo, Settings settings)
        {
            _logger = logger;
            _repo = repo;
            _settings = settings;
            _jobs = new List<BackupJob>(_repo.Load());
            _threadPool = ThreadPoolManager.Instance;
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Start the thread pool manager
            _threadPool.Start();
            
            // Debug output to verify if jobs are loaded
            Console.WriteLine($"BackupService initialized with {_jobs.Count} jobs:");
            foreach (var job in _jobs)
            {
                Console.WriteLine($"  - Job: {job.Name}, Source: {job.SourceDir}, Target: {job.TargetDir}");
            }
        }

        public void AddJob(BackupJob job)
        {
            _jobs.Add(job);
            // Use the logging thread pool for saving job data
            _threadPool.EnqueueLoggingTask(async (ct) => 
            {
                _repo.Save(_jobs);
                Console.WriteLine($"Added job: {job.Name}");
            });
        }

        public void RemoveJob(string name)
        {
            _jobs.RemoveAll(j => j.Name == name);
            // Use the logging thread pool for saving job data
            _threadPool.EnqueueLoggingTask(async (ct) => 
            {
                _repo.Save(_jobs);
            });
        }

        public IReadOnlyList<BackupJob> GetJobs() => _jobs.AsReadOnly();

        public async Task ExecuteBackupAsync(string name)
        {
            Console.WriteLine($"Starting backup for job: {name}");
            
            if (PackageBlocker.IsBlocked(_settings))
            {
                Console.WriteLine("Blocked package running — job skipped.");
                return;
            }

            var job = _jobs.FirstOrDefault(j => j.Name == name);
            if (job == null)
            {
                Console.WriteLine($"Job '{name}' not found.");
                return;
            }

            // Use logging thread for status updates
            await _threadPool.EnqueueLoggingTask(async (ct) => 
            {
                Report(new StatusEntry { Name = job.Name, State = "PENDING" });
            });

            try
            {
                await ProcessJobAsync(job);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'exécution du backup '{job.Name}' : {ex.Message}");
            }

            // Use logging thread for status updates
            await _threadPool.EnqueueLoggingTask(async (ct) => 
            {
                Report(new StatusEntry { Name = job.Name, State = "END" });
            });
        }

        public async Task ExecuteAllBackupsAsync()
        {
            Console.WriteLine($"Starting all backups. Total jobs: {_jobs.Count}");
            var tasks = new List<Task>();
            
            // Process all jobs concurrently
            foreach (var job in _jobs)
            {
                if (PackageBlocker.IsBlocked(_settings)) break;
                
                // Use a local variable to avoid closure issues
                var currentJob = job;
                tasks.Add(ExecuteBackupAsync(currentJob.Name));
            }
            
            await Task.WhenAll(tasks);
        }

        private async Task ProcessJobAsync(BackupJob job)
        {
            // Nettoyer les chemins
            string cleanedSourceDir = job.SourceDir.Trim('"').Trim();
            string cleanedTargetDir = job.TargetDir.Trim('"').Trim();
            
            Console.WriteLine($"Processing job: {job.Name}");
            Console.WriteLine($"Source dir: {cleanedSourceDir}");
            Console.WriteLine($"Target dir: {cleanedTargetDir}");

            // Vérifier l'existence du répertoire source
            if (!Directory.Exists(cleanedSourceDir))
            {
                Console.WriteLine($"Source directory does not exist: {cleanedSourceDir}");
                throw new DirectoryNotFoundException($"Le répertoire source '{cleanedSourceDir}' n'existe pas.");
            }

            // Créer le répertoire cible s'il n'existe pas
            if (!Directory.Exists(cleanedTargetDir))
            {
                Console.WriteLine($"Creating target directory: {cleanedTargetDir}");
                Directory.CreateDirectory(cleanedTargetDir);
            }

            try
            {
                var files = Directory.EnumerateFiles(cleanedSourceDir, "*", SearchOption.AllDirectories).ToList();
                
                // Calculate total size for better progress tracking
                long totalSize = files.Sum(f => new FileInfo(f).Length);
                long bytesCopied = 0;
                int totalFiles = files.Count;
                int filesCopied = 0;
                
                Console.WriteLine($"Found {totalFiles} files to copy, total size: {totalSize} bytes");

                // Process files in parallel with controlled concurrency
                var copyTasks = new List<Task>();
                foreach (string src in files)
                {
                    // Skip if job should be cancelled
                    if (PackageBlocker.IsBlocked(_settings) || _cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        Console.WriteLine("Backup interrompu : package bloqué ou annulé.");
                        return;
                    }

                    string rel = Path.GetRelativePath(cleanedSourceDir, src);
                    string dest = Path.Combine(cleanedTargetDir, rel);
                    
                    // Create directories on the logging thread (less resource-intensive)
                    await _threadPool.EnqueueLoggingTask(async (ct) => 
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    });
                    
                    // Use a local copy for the closure
                    string srcLocal = src;
                    string destLocal = dest;
                    
                    // Get file size before copying
                    long fileSize = new FileInfo(srcLocal).Length;
                    
                    var copyTask = _threadPool.EnqueueCopyTask(async (ct) =>
                    {
                        try
                        {
                            Console.WriteLine($"Copying: {srcLocal} -> {destLocal}");

                            var swCopy = System.Diagnostics.Stopwatch.StartNew();
                            File.Copy(srcLocal, destLocal, true);
                            swCopy.Stop();
                            
                            Console.WriteLine($"Copy completed in {swCopy.ElapsedMilliseconds}ms");

                            int encMs = 0;
                            if (_settings.CryptoExtensions.Contains(Path.GetExtension(srcLocal).ToLower()))
                            {
                                Console.WriteLine($"Encrypting file: {destLocal}");
                                encMs = CryptoSoftHelper.Encrypt(destLocal, _settings);
                                Console.WriteLine($"Encryption completed in {encMs}ms");
                            }

                            // Log the event on the logging thread
                            await _threadPool.EnqueueLoggingTask(async (logCt) =>
                            {
                                _logger.LogEvent(new LogEntry
                                {
                                    Timestamp = DateTime.UtcNow,
                                    JobName = job.Name,
                                    SourcePath = srcLocal,
                                    DestPath = destLocal,
                                    FileSize = fileSize,
                                    TransferTimeMs = (int)swCopy.ElapsedMilliseconds,
                                    EncryptionTimeMs = encMs
                                });
                            });

                            // Update progress based on bytes copied rather than files
                            long currentBytesCopied = Interlocked.Add(ref bytesCopied, fileSize);
                            int currentFilesCopied = Interlocked.Increment(ref filesCopied);
                            
                            // Calculate progress based on bytes, not file count
                            double progress = (double)currentBytesCopied / totalSize;
                            
                            // Update progress and report status on logging thread
                            await _threadPool.EnqueueLoggingTask(async (logCt) =>
                            {
                                Report(new StatusEntry
                                {
                                    Name = job.Name,
                                    SourceFilePath = srcLocal,
                                    TargetFilePath = destLocal,
                                    State = "ACTIVE",
                                    TotalFilesToCopy = totalFiles,
                                    TotalFilesSize = totalSize,
                                    NbFilesLeftToDo = totalFiles - currentFilesCopied,
                                    Progression = progress
                                });
                                
                                Console.WriteLine($"Progress: {progress:P2} ({currentBytesCopied}/{totalSize} bytes, {currentFilesCopied}/{totalFiles} files)");
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error copying file {srcLocal}: {ex.Message}");
                            throw;
                        }
                    });
                    
                    copyTasks.Add(copyTask);
                }
                
                // Wait for all copy operations to complete
                await Task.WhenAll(copyTasks);
                
                // Send one final update to ensure we show 100% completion
                // This is helpful in case of rounding errors in the progress calculation
                await _threadPool.EnqueueLoggingTask(async (ct) =>
                {
                    Report(new StatusEntry
                    {
                        Name = job.Name,
                        State = "ACTIVE",
                        TotalFilesToCopy = totalFiles,
                        TotalFilesSize = totalSize,
                        NbFilesLeftToDo = 0,
                        Progression = 1.0 // 100% complete
                    });
                });
                
                Console.WriteLine($"Backup job completed successfully: {job.Name} - {bytesCopied} bytes in {filesCopied} files");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied: {ex.Message}");
                throw new UnauthorizedAccessException($"Accès refusé au répertoire '{cleanedSourceDir}' : {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"I/O error: {ex.Message}");
                throw new IOException($"Erreur d'E/S dans '{cleanedSourceDir}' : {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                throw new Exception($"Erreur inattendue dans '{cleanedSourceDir}' : {ex.Message}", ex);
            }
        }

        private void Report(StatusEntry s)
        {
            _logger.UpdateStatus(s);
            StatusUpdated?.Invoke(s);
        }
        
        public void CancelAllBackups()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            Console.WriteLine("All backup operations have been cancelled");
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
    }
}