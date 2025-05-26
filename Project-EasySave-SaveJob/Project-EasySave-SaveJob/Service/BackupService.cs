using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Projet.Infrastructure;
using Projet.Model;

namespace Projet.Service
{
    public class BackupService : IBackupService
    {
        public event Action<StatusEntry> StatusUpdated;

        private readonly ILogger _logger;
        private readonly IJobRepository _repo;
        private readonly Settings _settings;
        private readonly List<BackupJob> _jobs;
        private readonly ConcurrentDictionary<string, Task> _runningJobs;
        private readonly SemaphoreSlim _semaphore;
        private readonly int _maxConcurrentJobs;

        public BackupService(ILogger logger, IJobRepository repo, Settings settings)
        {
            _logger = logger;
            _repo = repo;
            _settings = settings;
            _jobs = new List<BackupJob>(_repo.Load());
            _runningJobs = new ConcurrentDictionary<string, Task>();
            _maxConcurrentJobs = 5; // Maximum number of concurrent jobs
            _semaphore = new SemaphoreSlim(_maxConcurrentJobs, _maxConcurrentJobs);
        }

        public void AddJob(BackupJob job)
        {
            _jobs.Add(job);
            _repo.Save(_jobs);
        }

        public void RemoveJob(string name)
        {
            _jobs.RemoveAll(j => j.Name == name);
            _repo.Save(_jobs);
        }

        public IReadOnlyList<BackupJob> GetJobs() => _jobs.AsReadOnly();

        public IReadOnlyCollection<string> GetRunningJobs() => _runningJobs.Keys.ToList().AsReadOnly();

        public bool IsJobRunning(string name) => _runningJobs.ContainsKey(name);

        public async Task ExecuteBackupAsync(string name)
        {
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

            // If job is already running, don't start it again
            if (_runningJobs.ContainsKey(name))
            {
                Console.WriteLine($"Job '{name}' is already running.");
                return;
            }

            Report(new StatusEntry { Name = job.Name, State = "PENDING" });

            // Create a task for this job
            Task jobTask = Task.Run(async () =>
            {
                try
                {
                    // Acquire semaphore to limit concurrent jobs
                    await _semaphore.WaitAsync();
                    
                    try
                    {
                        await ProcessJobAsync(job);
                    }
                    finally
                    {
                        // Release the semaphore when done
                        _semaphore.Release();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lors de l'exécution du backup '{job.Name}' : {ex.Message}");
                }
                finally
                {
                    // Remove from running jobs dictionary when complete
                    _runningJobs.TryRemove(name, out _);
                    Report(new StatusEntry { Name = job.Name, State = "END" });
                }
            });

            // Add to running jobs
            _runningJobs[name] = jobTask;
        }

        public async Task ExecuteAllBackupsAsync()
        {
            List<Task> tasks = new List<Task>();
            
            foreach (var job in _jobs)
            {
                if (PackageBlocker.IsBlocked(_settings)) break;
                
                // Don't run jobs that are already running
                if (!_runningJobs.ContainsKey(job.Name))
                {
                    tasks.Add(ExecuteBackupAsync(job.Name));
                }
            }
            
            // Wait for all jobs to start (not to complete)
            await Task.WhenAll(tasks);
        }

        private async Task ProcessJobAsync(BackupJob job)
        {
            // Nettoyer les chemins
            string cleanedSourceDir = job.SourceDir.Trim('"').Trim();
            string cleanedTargetDir = job.TargetDir.Trim('"').Trim();

            // Vérifier l'existence du répertoire source
            if (!Directory.Exists(cleanedSourceDir))
            {
                throw new DirectoryNotFoundException($"Le répertoire source '{cleanedSourceDir}' n'existe pas.");
            }

            // Créer le répertoire cible s'il n'existe pas
            if (!Directory.Exists(cleanedTargetDir))
            {
                Directory.CreateDirectory(cleanedTargetDir);
            }

            try
            {
                var files = Directory.EnumerateFiles(cleanedSourceDir, "*", SearchOption.AllDirectories).ToList();
                long totalSize = files.Sum(f => new FileInfo(f).Length);
                int total = files.Count;
                
                // Use a concurrent counter for thread-safe incrementing
                int doneCount = 0;
                object lockObj = new object();
                
                // Create a cancellation token source to allow cancellation
                using var cts = new CancellationTokenSource();
                
                // Create a list to track all running tasks
                var tasks = new List<Task>();
                
                // Create a semaphore to limit parallel operations
                using var fileSemaphore = new SemaphoreSlim(4); // Limit to 4 concurrent file operations
                
                foreach (string src in files)
                {
                    if (PackageBlocker.IsBlocked(_settings))
                    {
                        Console.WriteLine("Backup interrompu : package bloqué.");
                        cts.Cancel();
                        break;
                    }

                    string rel = Path.GetRelativePath(cleanedSourceDir, src);
                    string dest = Path.Combine(cleanedTargetDir, rel);
                    
                    // Make sure destination directory exists
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    
                    // Wait for a semaphore slot
                    await fileSemaphore.WaitAsync(cts.Token);
                    
                    // Start a new task for this file
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            // Copy the file using optimized method
                            var swCopy = System.Diagnostics.Stopwatch.StartNew();
                            
                            // Use asynchronous file operations with buffer settings for better performance
                            using FileStream source = new FileStream(
                                src, 
                                FileMode.Open, 
                                FileAccess.Read, 
                                FileShare.Read, 
                                bufferSize: 4096, 
                                FileOptions.Asynchronous | FileOptions.SequentialScan);
                                
                            using FileStream destination = new FileStream(
                                dest, 
                                FileMode.Create, 
                                FileAccess.Write, 
                                FileShare.None, 
                                bufferSize: 4096, 
                                FileOptions.Asynchronous | FileOptions.SequentialScan);
                                
                            await source.CopyToAsync(destination, 81920, cts.Token);
                            
                            swCopy.Stop();
                            
                            // Handle encryption if needed
                            int encMs = 0;
                            if (_settings.CryptoExtensions.Contains(Path.GetExtension(src).ToLower()))
                            {
                                encMs = CryptoSoftHelper.Encrypt(dest, _settings);
                            }
                            
                            // Log the file operation
                            _logger.LogEvent(new LogEntry
                            {
                                Timestamp = DateTime.UtcNow,
                                JobName = job.Name,
                                SourcePath = src,
                                DestPath = dest,
                                FileSize = new FileInfo(src).Length,
                                TransferTimeMs = (int)swCopy.ElapsedMilliseconds,
                                EncryptionTimeMs = encMs
                            });
                            
                            // Thread-safe increment of progress counter
                            int newDoneCount, newLeft;
                            lock (lockObj)
                            {
                                doneCount++;
                                newDoneCount = doneCount;
                                newLeft = total - newDoneCount;
                            }
                            
                            // Report progress
                            Report(new StatusEntry
                            {
                                Name = job.Name,
                                SourceFilePath = src,
                                TargetFilePath = dest,
                                State = "ACTIVE",
                                TotalFilesToCopy = total,
                                TotalFilesSize = totalSize,
                                NbFilesLeftToDo = newLeft,
                                Progression = newDoneCount / (double)total
                            });
                        }
                        finally
                        {
                            // Always release the semaphore
                            fileSemaphore.Release();
                        }
                    }, cts.Token);
                    
                    tasks.Add(task);
                }
                
                // Wait for all file operations to complete
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation gracefully
                Report(new StatusEntry 
                { 
                    Name = job.Name, 
                    State = "CANCELED",
                    Progression = 0
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException($"Accès refusé au répertoire '{cleanedSourceDir}' : {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new IOException($"Erreur d'E/S dans '{cleanedSourceDir}' : {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur inattendue dans '{cleanedSourceDir}' : {ex.Message}", ex);
            }
        }

        private void Report(StatusEntry s)
        {
            _logger.UpdateStatus(s);
            StatusUpdated?.Invoke(s);
        }
    }
}