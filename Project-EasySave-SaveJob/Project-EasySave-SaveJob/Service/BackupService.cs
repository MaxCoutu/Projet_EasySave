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
        
        // Dictionary to track individual job states and cancellation tokens
        private readonly Dictionary<string, JobStateInfo> _jobStates = new Dictionary<string, JobStateInfo>();

        // Class to track job state information
        private class JobStateInfo
        {
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public bool IsPaused { get; set; }
            public string State { get; set; }
            public ManualResetEvent PauseEvent { get; set; }

            public JobStateInfo()
            {
                CancellationTokenSource = new CancellationTokenSource();
                IsPaused = false;
                State = "READY";
                PauseEvent = new ManualResetEvent(true); // Initially not paused (signaled)
            }
        }

        public BackupService(ILogger logger, IJobRepository repo, Settings settings)
        {
            _logger = logger;
            _repo = repo;
            _settings = settings;
            _jobs = new List<BackupJob>(_repo.Load());
            _threadPool = ThreadPoolManager.Instance;
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Initialize job states for all jobs
            foreach (var job in _jobs)
            {
                _jobStates[job.Name] = new JobStateInfo();
            }
            
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
            
            // Initialize job state
            _jobStates[job.Name] = new JobStateInfo();
            
            // Use the logging thread pool for saving job data
            _threadPool.EnqueueLoggingTask(async (ct) => 
            {
                _repo.Save(_jobs);
                Console.WriteLine($"Added job: {job.Name}");
            });
        }

        public void RemoveJob(string name)
        {
            // Cancel any running task for this job
            if (_jobStates.ContainsKey(name))
            {
                _jobStates[name].CancellationTokenSource.Cancel();
                _jobStates.Remove(name);
            }
            
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

            // Reset job state if it was stopped or completed
            if (!_jobStates.ContainsKey(name) || 
                _jobStates[name].State == "END" || 
                _jobStates[name].State == "CANCELLED")
            {
                _jobStates[name] = new JobStateInfo();
            }
            
            // Set job to pending
            _jobStates[name].State = "PENDING";
            
            // Use logging thread for status updates
            await _threadPool.EnqueueLoggingTask(async (ct) => 
            {
                Report(new StatusEntry { Name = job.Name, State = "PENDING" });
            });

            try
            {
                await ProcessJobAsync(job);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Job '{job.Name}' was cancelled.");
                
                // Use logging thread for status updates
                await _threadPool.EnqueueLoggingTask(async (ct) => 
                {
                    Report(new StatusEntry { Name = job.Name, State = "CANCELLED" });
                });
                
                // Update job state
                _jobStates[job.Name].State = "CANCELLED";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing backup '{job.Name}': {ex.Message}");
                
                // Update job state
                _jobStates[job.Name].State = "ERROR";
                
                // Report error
                await _threadPool.EnqueueLoggingTask(async (ct) => 
                {
                    Report(new StatusEntry { 
                        Name = job.Name, 
                        State = "ERROR",
                        ErrorMessage = ex.Message
                    });
                });
            }

            // If the job completed successfully (not cancelled or error)
            if (_jobStates.ContainsKey(name) && _jobStates[name].State != "CANCELLED" && _jobStates[name].State != "ERROR")
            {
                // Use logging thread for status updates
                await _threadPool.EnqueueLoggingTask(async (ct) => 
                {
                    Report(new StatusEntry { Name = job.Name, State = "END" });
                });
                
                // Update job state
                _jobStates[job.Name].State = "END";
            }
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
        
        // New methods for job control
        
        public void PauseJob(string name)
        {
            Console.WriteLine($"Pausing job: {name}");
            
            try
            {
                if (_jobStates.ContainsKey(name) && !_jobStates[name].IsPaused)
                {
                    _jobStates[name].IsPaused = true;
                    _jobStates[name].State = "PAUSED";
                    
                    // Reset the pause event in a try-catch to handle any potential issues
                    try
                    {
                        _jobStates[name].PauseEvent.Reset();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error resetting pause event: {ex.Message}");
                        // Create a new pause event if the current one is corrupted
                        _jobStates[name].PauseEvent = new ManualResetEvent(false);
                    }
                    
                    // Report the paused state
                    _threadPool.EnqueueLoggingTask(async (ct) => 
                    {
                        Report(new StatusEntry { 
                            Name = name, 
                            State = "PAUSED" 
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pausing job {name}: {ex.Message}");
                // Ensure the job can be resumed even if pause fails
                if (_jobStates.ContainsKey(name))
                {
                    _jobStates[name].IsPaused = false;
                    _jobStates[name].PauseEvent?.Set();
                }
            }
        }
        
        public void ResumeJob(string name)
        {
            Console.WriteLine($"Resuming job: {name}");
            
            try
            {
                if (_jobStates.ContainsKey(name) && _jobStates[name].IsPaused)
                {
                    _jobStates[name].IsPaused = false;
                    _jobStates[name].State = "ACTIVE";
                    
                    // Set the pause event in a try-catch to handle any potential issues
                    try
                    {
                        _jobStates[name].PauseEvent.Set();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error setting pause event: {ex.Message}");
                        // Create a new pause event if the current one is corrupted
                        _jobStates[name].PauseEvent = new ManualResetEvent(true);
                    }
                    
                    // Report the resumed state
                    _threadPool.EnqueueLoggingTask(async (ct) => 
                    {
                        Report(new StatusEntry { 
                            Name = name, 
                            State = "ACTIVE" 
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resuming job {name}: {ex.Message}");
                // Ensure the job can continue even if resume fails
                if (_jobStates.ContainsKey(name))
                {
                    _jobStates[name].IsPaused = false;
                    _jobStates[name].PauseEvent?.Set();
                }
            }
        }
        
        public void StopJob(string name)
        {
            Console.WriteLine($"Stopping job: {name}");
            
            if (_jobStates.ContainsKey(name))
            {
                // Cancel the job's token
                _jobStates[name].CancellationTokenSource.Cancel();
                
                // If it's paused, unpause it so it can process the cancellation
                if (_jobStates[name].IsPaused)
                {
                    _jobStates[name].IsPaused = false;
                    _jobStates[name].PauseEvent.Set();
                }
                
                _jobStates[name].State = "CANCELLED";
                
                // Report the cancelled state
                _threadPool.EnqueueLoggingTask(async (ct) => 
                {
                    Report(new StatusEntry { 
                        Name = name, 
                        State = "CANCELLED" 
                    });
                });
            }
        }

        private async Task ProcessJobAsync(BackupJob job)
        {
            // Check if job state exists, create if not
            if (!_jobStates.ContainsKey(job.Name))
            {
                _jobStates[job.Name] = new JobStateInfo();
            }
            
            // Get the job's cancellation token
            var cancellationToken = _jobStates[job.Name].CancellationTokenSource.Token;
            
            // Get the job's pause event
            var pauseEvent = _jobStates[job.Name].PauseEvent;
            
            // Update job state to active
            _jobStates[job.Name].State = "ACTIVE";
            
            try
            {
                // Clean the paths
                string cleanedSourceDir = job.SourceDir.Trim('"').Trim();
                string cleanedTargetDir = job.TargetDir.Trim('"').Trim();
                
                Console.WriteLine($"Processing job: {job.Name}");
                Console.WriteLine($"Source dir: {cleanedSourceDir}");
                Console.WriteLine($"Target dir: {cleanedTargetDir}");

                // Check source directory
                if (!Directory.Exists(cleanedSourceDir))
                {
                    Console.WriteLine($"Source directory does not exist: {cleanedSourceDir}");
                    throw new DirectoryNotFoundException($"Le répertoire source '{cleanedSourceDir}' n'existe pas.");
                }

                // Create target directory if needed
                if (!Directory.Exists(cleanedTargetDir))
                {
                    Console.WriteLine($"Creating target directory: {cleanedTargetDir}");
                    Directory.CreateDirectory(cleanedTargetDir);
                }

                // Get files to copy with non-blocking pause check
                var files = new List<string>();
                await Task.Run(() =>
                {
                    foreach (var file in Directory.EnumerateFiles(cleanedSourceDir, "*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        // Non-blocking pause check
                        if (pauseEvent != null && !pauseEvent.WaitOne(0))
                        {
                            Task.Delay(50, cancellationToken).Wait();
                        }
                        files.Add(file);
                    }
                }, cancellationToken);
                
                // Calculate total size with non-blocking pause check
                long totalSize = 0;
                await Task.Run(() =>
                {
                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        // Non-blocking pause check
                        if (pauseEvent != null && !pauseEvent.WaitOne(0))
                        {
                            Task.Delay(50, cancellationToken).Wait();
                        }
                        totalSize += new FileInfo(file).Length;
                    }
                }, cancellationToken);

                long bytesCopied = 0;
                int totalFiles = files.Count;
                int filesCopied = 0;
                
                Console.WriteLine($"Found {totalFiles} files to copy, total size: {totalSize} bytes");

                // Process files
                var copyTasks = new List<Task>();
                foreach (string src in files)
                {
                    // Non-blocking pause check
                    if (pauseEvent != null && !pauseEvent.WaitOne(0))
                    {
                        await Task.Delay(50, cancellationToken);
                        continue;
                    }
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Skip if package blocker
                    if (PackageBlocker.IsBlocked(_settings))
                    {
                        Console.WriteLine("Backup interrupted: package blocked.");
                        return;
                    }

                    string rel = Path.GetRelativePath(cleanedSourceDir, src);
                    string dest = Path.Combine(cleanedTargetDir, rel);
                    
                    // Create directories
                    await _threadPool.EnqueueLoggingTask(async (ct) => 
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    });
                    
                    // Local copies for closure
                    string srcLocal = src;
                    string destLocal = dest;
                    
                    // Get file size
                    long fileSize = new FileInfo(srcLocal).Length;
                    
                    // Copy file
                    var copyTask = _threadPool.EnqueueCopyTask(async (ct) =>
                    {
                        try
                        {
                            // Process cancellation during file copy
                            if (cancellationToken.IsCancellationRequested)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                            
                            // Check for pause before starting copy
                            pauseEvent.WaitOne();
                            
                            Console.WriteLine($"Copying: {srcLocal} -> {destLocal}");

                            var swCopy = System.Diagnostics.Stopwatch.StartNew();
                            await CopyFileWithPauseAndCancellationSupportAsync(srcLocal, destLocal, pauseEvent, cancellationToken);
                            swCopy.Stop();
                            
                            Console.WriteLine($"Copy completed in {swCopy.ElapsedMilliseconds}ms");

                            // Process file encryption if needed
                            int encMs = 0;
                            if (_settings.CryptoExtensions.Contains(Path.GetExtension(srcLocal).ToLower()))
                            {
                                // Check for cancellation before encryption
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                // Check for pause before encryption
                                pauseEvent.WaitOne();
                                
                                Console.WriteLine($"Encrypting file: {destLocal}");
                                encMs = CryptoSoftHelper.Encrypt(destLocal, _settings);
                                Console.WriteLine($"Encryption completed in {encMs}ms");
                            }

                            // Log the event
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

                            // Update progress
                            long currentBytesCopied = Interlocked.Add(ref bytesCopied, fileSize);
                            int currentFilesCopied = Interlocked.Increment(ref filesCopied);
                            
                            // Calculate progress
                            double progress = (double)currentBytesCopied / totalSize;
                            
                            // Report status
                            await _threadPool.EnqueueLoggingTask(async (logCt) =>
                            {
                                Report(new StatusEntry
                                {
                                    Name = job.Name,
                                    SourceFilePath = srcLocal,
                                    TargetFilePath = destLocal,
                                    State = _jobStates[job.Name].State,
                                    TotalFilesToCopy = totalFiles,
                                    TotalFilesSize = totalSize,
                                    NbFilesLeftToDo = totalFiles - currentFilesCopied,
                                    Progression = progress
                                });
                                
                                Console.WriteLine($"Progress: {progress:P2} ({currentBytesCopied}/{totalSize} bytes, {currentFilesCopied}/{totalFiles} files)");
                            });
                        }
                        catch (OperationCanceledException)
                        {
                            Console.WriteLine($"File copy cancelled: {srcLocal}");
                            throw; // Rethrow to propagate cancellation
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error copying file {srcLocal}: {ex.Message}");
                            throw;
                        }
                    });
                    
                    copyTasks.Add(copyTask);
                }
                
                try
                {
                    // Wait for all copy operations to complete, with support for cancellation
                    await Task.WhenAll(copyTasks);
                    
                    // Send final update for 100% completion
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
                catch (OperationCanceledException)
                {
                    Console.WriteLine($"Backup job was cancelled: {job.Name}");
                    throw; // Rethrow to let calling code handle cancellation
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Backup job was cancelled: {job.Name}");
                throw; // Rethrow to let calling code handle cancellation
            }
        }
        
        // Helper method to copy files with pause and cancellation support
        private async Task CopyFileWithPauseAndCancellationSupportAsync(string source, string destination, ManualResetEvent pauseEvent, CancellationToken cancellationToken)
        {
            const int bufferSize = 32768; // Reduced buffer size for better memory management
            
            try
            {
                using (var sourceStream = new FileStream(
                    source, 
                    FileMode.Open, 
                    FileAccess.Read, 
                    FileShare.Read, 
                    bufferSize, 
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                using (var destStream = new FileStream(
                    destination, 
                    FileMode.Create, 
                    FileAccess.Write, 
                    FileShare.None, 
                    bufferSize, 
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    
                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        try
                        {
                            // Non-blocking pause check
                            while (pauseEvent != null && !pauseEvent.WaitOne(0))
                            {
                                await Task.Delay(50, cancellationToken);
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                            
                            await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error during file copy: {ex.Message}");
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CopyFileWithPauseAndCancellationSupportAsync: {ex.Message}");
                throw;
            }
        }

        private void Report(StatusEntry s)
        {
            Console.WriteLine($"Reporting: Job={s.Name}, State={s.State}, Progression={s.Progression:F2}%");
            _logger.UpdateStatus(s);
            StatusUpdated?.Invoke(s);
        }

        public void CancelAllBackups()
        {
            Console.WriteLine("Cancelling all backups");
            
            // Cancel the global token
            _cancellationTokenSource.Cancel();
            
            // Create a new token for future operations
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Also cancel all individual job tokens
            foreach (var jobState in _jobStates.Values)
            {
                jobState.CancellationTokenSource.Cancel();
                jobState.CancellationTokenSource = new CancellationTokenSource();
                jobState.State = "CANCELLED";
                jobState.IsPaused = false;
                jobState.PauseEvent.Set(); // Ensure it's not stuck in pause
            }
        }

        public void Dispose()
        {
            // Cancel all running tasks
            CancelAllBackups();
            
            // Dispose all job state resources
            foreach (var jobState in _jobStates.Values)
            {
                jobState.CancellationTokenSource.Dispose();
                jobState.PauseEvent.Dispose();
            }
            
            // Dispose other resources
            _cancellationTokenSource.Dispose();
            _threadPool.Stop(); // Stop the thread pool instead of trying to dispose it
            
            GC.SuppressFinalize(this);
        }
    }
}