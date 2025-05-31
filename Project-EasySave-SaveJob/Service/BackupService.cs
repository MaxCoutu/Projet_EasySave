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
        private readonly MemoryAllocationManager _memoryManager;
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
            _memoryManager = MemoryAllocationManager.Instance;
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
            
            // Register job with memory manager
            _memoryManager.RegisterJob(name);
            
            // Use logging thread for status updates
            await _threadPool.EnqueueLoggingTask((ct) => 
            {
                // Get memory allocation information
                int bufferSize = _memoryManager.GetBufferSize(job.Name);
                int activeJobs = _memoryManager.GetActiveJobCount();
                double memoryPercentage = _memoryManager.GetMemoryPercentage(job.Name);
                
                Report(new StatusEntry 
                { 
                    Name = job.Name, 
                    State = "PENDING",
                    TotalFilesToCopy = 0,
                    TotalFilesSize = 0,
                    NbFilesLeftToDo = 0,
                    Progression = 0,
                    BufferSize = bufferSize,
                    ActiveJobs = activeJobs,
                    MemoryPercentage = memoryPercentage
                });
                
                return Task.CompletedTask;
            });

            try
            {
                await ProcessJobAsync(job);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Job '{job.Name}' was cancelled.");
                
                // Use logging thread for status updates
                await _threadPool.EnqueueLoggingTask((ct) => 
                {
                    // Get memory allocation information
                    int bufferSize = _memoryManager.GetBufferSize(job.Name);
                    int activeJobs = _memoryManager.GetActiveJobCount();
                    double memoryPercentage = _memoryManager.GetMemoryPercentage(job.Name);
                    
                    Report(new StatusEntry 
                    { 
                        Name = job.Name, 
                        State = "CANCELLED",
                        BufferSize = bufferSize,
                        ActiveJobs = activeJobs,
                        MemoryPercentage = memoryPercentage
                    });
                    
                    return Task.CompletedTask;
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
                await _threadPool.EnqueueLoggingTask((ct) => 
                {
                    // Get memory allocation information
                    int bufferSize = _memoryManager.GetBufferSize(job.Name);
                    int activeJobs = _memoryManager.GetActiveJobCount();
                    double memoryPercentage = _memoryManager.GetMemoryPercentage(job.Name);
                    
                    Report(new StatusEntry 
                    { 
                        Name = job.Name, 
                        State = "ERROR",
                        ErrorMessage = ex.Message,
                        BufferSize = bufferSize,
                        ActiveJobs = activeJobs,
                        MemoryPercentage = memoryPercentage
                    });
                    
                    return Task.CompletedTask;
                });
            }
            finally
            {
                // Unregister job from memory manager when finished
                _memoryManager.UnregisterJob(job.Name);
            }

            // If the job completed successfully (not cancelled or error)
            if (_jobStates.ContainsKey(name) && _jobStates[name].State != "CANCELLED" && _jobStates[name].State != "ERROR")
            {
                // Use logging thread for status updates
                await _threadPool.EnqueueLoggingTask((ct) => 
                {
                    // Get memory allocation information
                    int bufferSize = _memoryManager.GetBufferSize(job.Name);
                    int activeJobs = _memoryManager.GetActiveJobCount();
                    double memoryPercentage = _memoryManager.GetMemoryPercentage(job.Name);
                    
                    Report(new StatusEntry 
                    { 
                        Name = job.Name, 
                        State = "END",
                        BufferSize = bufferSize,
                        ActiveJobs = activeJobs,
                        MemoryPercentage = memoryPercentage
                    });
                    
                    return Task.CompletedTask;
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
                
                // Démarrer chaque job dans sa propre tâche sans attendre qu'elle se termine
                var jobTask = Task.Run(async () => 
                {
                    try 
                    {
                        Console.WriteLine($"Starting job {currentJob.Name} in a separate thread");
                        await ExecuteBackupAsync(currentJob.Name);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in job {currentJob.Name}: {ex.Message}");
                    }
                });
                
                // Ajouter à la liste pour pouvoir attendre la fin si nécessaire
                tasks.Add(jobTask);
            }
            
            // Facultatif: attendre que toutes les tâches soient terminées
            // await Task.WhenAll(tasks);
            
            // On n'attend pas forcément que toutes les tâches soient terminées ici
            // pour permettre à l'interface utilisateur de rester réactive
        }
        
        // New methods for job control
        
        public void PauseJob(string name)
        {
            Console.WriteLine($"Pausing job: {name}");
            
            if (_jobStates.ContainsKey(name) && !_jobStates[name].IsPaused)
            {
                _jobStates[name].IsPaused = true;
                _jobStates[name].PauseEvent.Reset(); // Block threads waiting on this event
                _jobStates[name].State = "PAUSED";
                
                // Report the paused state
                _threadPool.EnqueueLoggingTask((ct) => 
                {
                    Report(new StatusEntry { 
                        Name = name, 
                        State = "PAUSED" 
                    });
                    
                    return Task.CompletedTask;
                });
            }
        }
        
        public void ResumeJob(string name)
        {
            Console.WriteLine($"Resuming job: {name}");
            
            if (_jobStates.ContainsKey(name) && _jobStates[name].IsPaused)
            {
                _jobStates[name].IsPaused = false;
                _jobStates[name].PauseEvent.Set(); // Unblock threads waiting on this event
                _jobStates[name].State = "ACTIVE";
                
                // Report the resumed state
                _threadPool.EnqueueLoggingTask((ct) => 
                {
                    Report(new StatusEntry { 
                        Name = name, 
                        State = "ACTIVE" 
                    });
                    
                    return Task.CompletedTask;
                });
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
                _threadPool.EnqueueLoggingTask((ct) => 
                {
                    Report(new StatusEntry { 
                        Name = name, 
                        State = "CANCELLED" 
                    });
                    
                    return Task.CompletedTask;
                });
            }
        }

        private async Task ProcessJobAsync(BackupJob job)
        {
            Console.WriteLine($"Processing backup job: {job.Name}");
            
            // Update job state
            _jobStates[job.Name].State = "ACTIVE";
            
            var pauseEvent = _jobStates[job.Name].PauseEvent;
            var cancellationToken = _jobStates[job.Name].CancellationTokenSource.Token;
            
            // Use logging thread for status updates
            await _threadPool.EnqueueLoggingTask((ct) => 
            {
                // Get memory allocation information
                int bufferSize = _memoryManager.GetBufferSize(job.Name);
                int activeJobs = _memoryManager.GetActiveJobCount();
                double memoryPercentage = _memoryManager.GetMemoryPercentage(job.Name);
                
                Report(new StatusEntry 
                { 
                    Name = job.Name, 
                    State = "ACTIVE",
                    TotalFilesToCopy = 0,
                    TotalFilesSize = 0,
                    NbFilesLeftToDo = 0,
                    Progression = 0,
                    BufferSize = bufferSize,
                    ActiveJobs = activeJobs,
                    MemoryPercentage = memoryPercentage
                });
                
                return Task.CompletedTask;
            });
            
            // Build list of files to process
            string sourceDir = job.SourceDir;
            string targetDir = job.TargetDir;
            
            if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(targetDir))
            {
                throw new InvalidOperationException("Source or target directory is not set");
            }
            
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }
            
            // Create target directory if it doesn't exist
            Directory.CreateDirectory(targetDir);
            
            // Clean the paths
            string cleanedSourceDir = sourceDir.Trim('"').Trim();
            string cleanedTargetDir = targetDir.Trim('"').Trim();
            
            Console.WriteLine($"Scanning source directory for files: {cleanedSourceDir}");
            
            // Analyse complète du répertoire source AVANT de commencer la sauvegarde
            List<(string src, string dest, long size, DateTime lastModified)> sourceFiles = new List<(string, string, long, DateTime)>();
            
            try
            {
                // Analyser tous les fichiers de manière récursive
                foreach (string srcFilePath in Directory.EnumerateFiles(cleanedSourceDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        // Calculer le chemin relatif
                        string relPath = Path.GetRelativePath(cleanedSourceDir, srcFilePath);
                        string destFilePath = Path.Combine(cleanedTargetDir, relPath);
                        
                        // Obtenir les infos du fichier
                        var fileInfo = new FileInfo(srcFilePath);
                        
                        // Vérifier que le fichier est accessible
                        if (!fileInfo.Exists)
                            continue;
                            
                        // Vérifier que ce n'est pas un fichier système ou caché
                        if ((fileInfo.Attributes & (FileAttributes.System | FileAttributes.Hidden)) != 0)
                            continue;
                        
                        // Ajouter à la liste avec taille et date de modification
                        sourceFiles.Add((srcFilePath, destFilePath, fileInfo.Length, fileInfo.LastWriteTime));
                        
                        // Afficher les informations de chaque fichier trouvé (limité aux 20 premiers pour éviter le spam)
                        if (sourceFiles.Count <= 20)
                        {
                            Console.WriteLine($"Found file: {relPath}, Size: {fileInfo.Length} bytes, Last modified: {fileInfo.LastWriteTime}");
                        }
                        else if (sourceFiles.Count == 21)
                        {
                            Console.WriteLine("More files found...");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error scanning file {srcFilePath}: {ex.Message}");
                    }
                }
                
                Console.WriteLine($"Scan completed. Found {sourceFiles.Count} files to copy.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during directory scan: {ex.Message}");
                throw new Exception($"Failed to scan source directory: {ex.Message}", ex);
            }
            
            // Vérifier qu'on a trouvé des fichiers
            if (sourceFiles.Count == 0)
            {
                Console.WriteLine($"No files found in source directory: {cleanedSourceDir}");
                
                // Si la stratégie a spécifié des fichiers, essayer d'utiliser cette méthode à la place
                var collectingCallback = new Action<StatusEntry>(status => {
                    Console.WriteLine($">>> Strategy callback received status: Job={status.Name}, State={status.State}, Source={status.SourceFilePath}, Target={status.TargetFilePath}");
                    
                    if (!string.IsNullOrEmpty(status.SourceFilePath) && !string.IsNullOrEmpty(status.TargetFilePath))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(status.SourceFilePath);
                            if (fileInfo.Exists)
                            {
                                sourceFiles.Add((status.SourceFilePath, status.TargetFilePath, fileInfo.Length, fileInfo.LastWriteTime));
                                Console.WriteLine($">>> Added file from strategy: {status.SourceFilePath} -> {status.TargetFilePath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing file from strategy: {ex.Message}");
                        }
                    }
                    
                    Report(status);
                });
                
                // Exécuter la stratégie de sauvegarde
                Console.WriteLine($">>> Executing backup strategy for job: {job.Name}");
                await job.Strategy.ExecuteAsync(job, collectingCallback);
                Console.WriteLine($">>> Strategy execution completed. Collected {sourceFiles.Count} files to copy.");
            }
            
            // Si on n'a toujours pas de fichiers, terminer
            if (sourceFiles.Count == 0)
            {
                Console.WriteLine("No files to copy after strategy execution.");
                
                // Mise à jour finale avec 100% de complétion puisqu'il n'y a rien à faire
                await _threadPool.EnqueueLoggingTask((ct) =>
                {
                    Report(new StatusEntry
                    {
                        Name = job.Name,
                        State = "ACTIVE",
                        TotalFilesToCopy = 0,
                        TotalFilesSize = 0,
                        NbFilesLeftToDo = 0,
                        Progression = 1.0, // 100% puisque rien à faire
                        BufferSize = _memoryManager.GetBufferSize(job.Name),
                        ActiveJobs = _memoryManager.GetActiveJobCount(),
                        MemoryPercentage = _memoryManager.GetMemoryPercentage(job.Name)
                    });
                    
                    return Task.CompletedTask;
                });
                
                return;
            }
            
            // Calculer le total des fichiers et leur taille
            int totalFiles = sourceFiles.Count;
            long totalSize = sourceFiles.Sum(f => f.size);
            
            // Mettre à jour les informations de progression
            job.TotalFilesToCopy = totalFiles;
            job.TotalFilesSize = totalSize;
            job.NbFilesLeftToDo = totalFiles;
            job.Progression = 0;
            job.State = "ACTIVE";
            
            Console.WriteLine($"Starting backup: {job.Name} - {totalFiles} files, {totalSize} bytes");
            
            // Utiliser le thread de logging pour les mises à jour de statut
            await _threadPool.EnqueueLoggingTask((ct) => 
            {
                // Obtenir les informations d'allocation mémoire
                int bufferSize = _memoryManager.GetBufferSize(job.Name);
                int activeJobs = _memoryManager.GetActiveJobCount();
                double memoryPercentage = _memoryManager.GetMemoryPercentage(job.Name);
                
                Report(new StatusEntry 
                { 
                    Name = job.Name, 
                    State = "ACTIVE",
                    TotalFilesToCopy = totalFiles,
                    TotalFilesSize = totalSize,
                    NbFilesLeftToDo = totalFiles,
                    Progression = 0,
                    BufferSize = bufferSize,
                    ActiveJobs = activeJobs,
                    MemoryPercentage = memoryPercentage
                });
                
                return Task.CompletedTask;
            });
            
            // Copier les fichiers
            var copyTasks = new List<Task>();
            int filesCopied = 0;
            long bytesCopied = 0;
            int filesFailed = 0;
            
            foreach (var (src, dest, fileSize, lastModified) in sourceFiles)
            {
                // Vérifier une dernière fois que le fichier source existe
                if (!File.Exists(src))
                {
                    Console.WriteLine($"Source file no longer exists: {src}");
                    continue;
                }
                
                // Copies locales pour la closure
                string srcLocal = src;
                string destLocal = dest;
                long fileSizeLocal = fileSize;
                DateTime lastModifiedLocal = lastModified;
                
                // Copier le fichier
                var copyTask = _threadPool.EnqueueCopyTask(async (ct) =>
                {
                    try
                    {
                        // Vérifier l'annulation pendant la copie du fichier
                        if (cancellationToken.IsCancellationRequested)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                        
                        // Vérifier la pause avant de commencer la copie
                        pauseEvent.WaitOne();
                        
                        Console.WriteLine($"Copying: {srcLocal} -> {destLocal} ({fileSizeLocal} bytes)");
                        
                        // S'assurer que le répertoire de destination existe
                        Directory.CreateDirectory(Path.GetDirectoryName(destLocal));

                        var swCopy = System.Diagnostics.Stopwatch.StartNew();
                        
                        // Utiliser un try-catch spécifique pour la copie de fichier
                        try 
                        {
                            await CopyFileWithPauseAndCancellationSupportAsync(srcLocal, destLocal, pauseEvent, cancellationToken, job.Name);
                            swCopy.Stop();
                            Console.WriteLine($"Copy completed in {swCopy.ElapsedMilliseconds}ms");
                            
                            // Vérifier l'intégrité du fichier copié
                            bool integrityOk = await VerifyFileIntegrityAsync(srcLocal, destLocal, cancellationToken);
                            if (!integrityOk)
                            {
                                throw new Exception("File integrity check failed");
                            }
                            
                            // Copier l'horodatage du fichier source
                            try
                            {
                                File.SetLastWriteTime(destLocal, lastModifiedLocal);
                            }
                            catch (Exception timeEx)
                            {
                                Console.WriteLine($"Warning: Could not set file timestamp: {timeEx.Message}");
                            }
                        }
                        catch (Exception copyEx)
                        {
                            swCopy.Stop();
                            Console.WriteLine($"Error during file copy: {copyEx.Message}");
                            
                            // Si c'est une annulation, propager l'exception
                            if (copyEx is OperationCanceledException)
                                throw;
                                
                            // Pour les autres erreurs, on les enregistre mais on continue le traitement des autres fichiers
                            await _threadPool.EnqueueLoggingTask((logCt) =>
                            {
                                _logger.LogEvent(new LogEntry
                                {
                                    Timestamp = DateTime.UtcNow,
                                    JobName = job.Name,
                                    SourcePath = srcLocal,
                                    DestPath = destLocal,
                                    FileSize = fileSizeLocal,
                                    TransferTimeMs = (int)swCopy.ElapsedMilliseconds,
                                    EncryptionTimeMs = 0,
                                    ErrorMessage = copyEx.Message
                                });
                                
                                return Task.CompletedTask;
                            });
                            
                            Interlocked.Increment(ref filesFailed);
                            
                            // Continuer avec les autres fichiers
                            return;
                        }

                        // Traiter l'encryption si nécessaire
                        int encMs = 0;
                        if (_settings.CryptoExtensions.Contains(Path.GetExtension(srcLocal).ToLower()))
                        {
                            // Vérifier l'annulation avant l'encryption
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // Vérifier la pause avant l'encryption
                            pauseEvent.WaitOne();
                            
                            try
                            {
                                Console.WriteLine($"Encrypting file: {destLocal}");
                                encMs = CryptoSoftHelper.Encrypt(destLocal, _settings);
                                Console.WriteLine($"Encryption completed in {encMs}ms");
                            }
                            catch (Exception encEx)
                            {
                                Console.WriteLine($"Error during encryption: {encEx.Message}");
                                
                                // Si c'est une annulation, propager l'exception
                                if (encEx is OperationCanceledException)
                                    throw;
                                    
                                // Pour les autres erreurs, on les enregistre mais on continue
                                encMs = -1; // Marquer l'échec de l'encryption
                            }
                        }

                        // Enregistrer l'événement
                        await _threadPool.EnqueueLoggingTask((logCt) =>
                        {
                            _logger.LogEvent(new LogEntry
                            {
                                Timestamp = DateTime.UtcNow,
                                JobName = job.Name,
                                SourcePath = srcLocal,
                                DestPath = destLocal,
                                FileSize = fileSizeLocal,
                                TransferTimeMs = (int)swCopy.ElapsedMilliseconds,
                                EncryptionTimeMs = encMs
                            });
                            
                            return Task.CompletedTask;
                        });

                        // Mettre à jour la progression
                        long currentBytesCopied = Interlocked.Add(ref bytesCopied, fileSizeLocal);
                        int currentFilesCopied = Interlocked.Increment(ref filesCopied);
                        
                        // Calculer la progression
                        double progress = totalSize > 0 ? (double)currentBytesCopied / totalSize : 1.0;
                        
                        // Rapporter le statut
                        await _threadPool.EnqueueLoggingTask((logCt) =>
                        {
                            // Obtenir les informations d'allocation mémoire
                            int bufferSize = _memoryManager.GetBufferSize(job.Name);
                            int activeJobs = _memoryManager.GetActiveJobCount();
                            double memoryPercentage = _memoryManager.GetMemoryPercentage(job.Name);
                            
                            Report(new StatusEntry
                            {
                                Name = job.Name,
                                SourceFilePath = srcLocal,
                                TargetFilePath = destLocal,
                                State = _jobStates[job.Name].State,
                                TotalFilesToCopy = totalFiles,
                                TotalFilesSize = totalSize,
                                NbFilesLeftToDo = totalFiles - currentFilesCopied,
                                Progression = progress,
                                BufferSize = bufferSize,
                                ActiveJobs = activeJobs,
                                MemoryPercentage = memoryPercentage
                            });
                            
                            Console.WriteLine($"Progress: {progress:P2} ({currentBytesCopied}/{totalSize} bytes, {currentFilesCopied}/{totalFiles} files)");
                            Console.WriteLine($"Memory allocation: {memoryPercentage:F1}% ({bufferSize} bytes buffer, {activeJobs} active jobs)");
                            
                            return Task.CompletedTask;
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"File copy cancelled: {srcLocal}");
                        throw; // Rethrow to propagate cancellation
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in copy task for {srcLocal}: {ex.Message}");
                        Console.WriteLine($"Stack trace: {ex.StackTrace}");
                        
                        Interlocked.Increment(ref filesFailed);
                        
                        // On ne propage pas les exceptions pour éviter d'arrêter tous les autres fichiers
                        // On enregistre simplement l'erreur
                        await _threadPool.EnqueueLoggingTask((logCt) =>
                        {
                            _logger.LogEvent(new LogEntry
                            {
                                Timestamp = DateTime.UtcNow,
                                JobName = job.Name,
                                SourcePath = srcLocal,
                                DestPath = destLocal,
                                FileSize = fileSizeLocal,
                                TransferTimeMs = 0,
                                EncryptionTimeMs = 0,
                                ErrorMessage = ex.Message
                            });
                            
                            return Task.CompletedTask;
                        });
                    }
                });
                
                copyTasks.Add(copyTask);
            }
            
            try
            {
                // Attendre toutes les opérations de copie
                if (copyTasks.Count > 0)
                {
                    Console.WriteLine($"Waiting for {copyTasks.Count} copy tasks to complete for job {job.Name}");
                    
                    // Attendre toutes les tâches avec une surveillance de timeout
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(30)); // 30 minutes timeout
                    var completedTask = await Task.WhenAny(Task.WhenAll(copyTasks), timeoutTask);
                    
                    if (completedTask == timeoutTask)
                    {
                        Console.WriteLine($"WARNING: Timeout waiting for copy tasks to complete for job {job.Name}");
                        
                        // Vérifier combien de tâches sont encore en cours
                        int pendingTasks = copyTasks.Count(t => !t.IsCompleted);
                        Console.WriteLine($"There are {pendingTasks} tasks still pending out of {copyTasks.Count}");
                    }
                    else
                    {
                        Console.WriteLine($"All copy tasks completed for job {job.Name}");
                    }
                }
                else
                {
                    Console.WriteLine($"No files to copy for job {job.Name}");
                }
                
                // Ajouter un petit délai pour s'assurer que toutes les opérations sont correctement terminées
                await Task.Delay(500);
                
                // Envoyer la mise à jour finale pour 100% de complétion
                await _threadPool.EnqueueLoggingTask((ct) =>
                {
                    // Obtenir les informations d'allocation mémoire
                    int bufferSize = _memoryManager.GetBufferSize(job.Name);
                    int activeJobs = _memoryManager.GetActiveJobCount();
                    double memoryPercentage = _memoryManager.GetMemoryPercentage(job.Name);
                    
                    Report(new StatusEntry
                    {
                        Name = job.Name,
                        State = "ACTIVE",
                        TotalFilesToCopy = totalFiles,
                        TotalFilesSize = totalSize,
                        NbFilesLeftToDo = 0,
                        Progression = 1.0, // 100% complet
                        BufferSize = bufferSize,
                        ActiveJobs = activeJobs,
                        MemoryPercentage = memoryPercentage
                    });
                    
                    return Task.CompletedTask;
                });
                
                Console.WriteLine($"Backup job completed successfully: {job.Name} - {bytesCopied} bytes in {filesCopied} files");
                if (filesFailed > 0)
                {
                    Console.WriteLine($"WARNING: {filesFailed} files failed to copy");
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Backup job was cancelled: {job.Name}");
                throw; // Rethrow to let calling code handle cancellation
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in backup job {job.Name}: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Propager l'exception pour permettre au code appelant de la gérer
                throw;
            }
        }
        
        /// <summary>
        /// Vérifie l'intégrité d'un fichier copié en comparant la taille et le hash MD5
        /// </summary>
        private async Task<bool> VerifyFileIntegrityAsync(string source, string destination, CancellationToken cancellationToken)
        {
            try
            {
                // Vérifier que les deux fichiers existent
                if (!File.Exists(source) || !File.Exists(destination))
                {
                    Console.WriteLine($"File integrity check failed: Source or destination file not found");
                    return false;
                }
                
                // Vérifier la taille des fichiers
                var sourceInfo = new FileInfo(source);
                var destInfo = new FileInfo(destination);
                
                if (sourceInfo.Length != destInfo.Length)
                {
                    Console.WriteLine($"File integrity check failed: Size mismatch. Source: {sourceInfo.Length}, Destination: {destInfo.Length}");
                    return false;
                }
                
                // Pour les petits fichiers (moins de 10MB), calculer et comparer les hash MD5
                if (sourceInfo.Length < 10 * 1024 * 1024)
                {
                    using (var md5 = System.Security.Cryptography.MD5.Create())
                    {
                        // Calculer le hash du fichier source
                        byte[] sourceHash;
                        using (var stream = File.OpenRead(source))
                        {
                            sourceHash = await Task.Run(() => md5.ComputeHash(stream), cancellationToken);
                        }
                        
                        // Calculer le hash du fichier destination
                        byte[] destHash;
                        using (var stream = File.OpenRead(destination))
                        {
                            destHash = await Task.Run(() => md5.ComputeHash(stream), cancellationToken);
                        }
                        
                        // Comparer les hash
                        bool hashesMatch = sourceHash.SequenceEqual(destHash);
                        if (!hashesMatch)
                        {
                            Console.WriteLine("File integrity check failed: Hash mismatch");
                            return false;
                        }
                    }
                }
                else
                {
                    // Pour les gros fichiers, vérifier juste quelques segments
                    using (var sourceStream = File.OpenRead(source))
                    using (var destStream = File.OpenRead(destination))
                    {
                        // Vérifier le début du fichier (first 64KB)
                        if (!await CompareFileSegmentAsync(sourceStream, destStream, 0, 64 * 1024, cancellationToken))
                        {
                            Console.WriteLine("File integrity check failed: Beginning segment mismatch");
                            return false;
                        }
                        
                        // Vérifier le milieu du fichier
                        long middle = sourceInfo.Length / 2;
                        if (!await CompareFileSegmentAsync(sourceStream, destStream, middle, 64 * 1024, cancellationToken))
                        {
                            Console.WriteLine("File integrity check failed: Middle segment mismatch");
                            return false;
                        }
                        
                        // Vérifier la fin du fichier
                        long end = Math.Max(0, sourceInfo.Length - 64 * 1024);
                        if (!await CompareFileSegmentAsync(sourceStream, destStream, end, 64 * 1024, cancellationToken))
                        {
                            Console.WriteLine("File integrity check failed: End segment mismatch");
                            return false;
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during file integrity check: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Compare un segment de deux fichiers
        /// </summary>
        private async Task<bool> CompareFileSegmentAsync(FileStream source, FileStream dest, long offset, int length, CancellationToken cancellationToken)
        {
            byte[] sourceBuffer = new byte[length];
            byte[] destBuffer = new byte[length];
            
            // Positionner les flux
            source.Position = offset;
            dest.Position = offset;
            
            // Lire les segments
            int sourceBytesRead = await source.ReadAsync(sourceBuffer, 0, length, cancellationToken);
            int destBytesRead = await dest.ReadAsync(destBuffer, 0, length, cancellationToken);
            
            // Vérifier la longueur lue
            if (sourceBytesRead != destBytesRead)
                return false;
                
            // Comparer les données
            for (int i = 0; i < sourceBytesRead; i++)
            {
                if (sourceBuffer[i] != destBuffer[i])
                    return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Copie un fichier avec support de pause et d'annulation
        /// </summary>
        private async Task CopyFileWithPauseAndCancellationSupportAsync(string source, string destination, ManualResetEvent pauseEvent, CancellationToken cancellationToken, string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
            {
                throw new ArgumentException("Job name cannot be null or empty", nameof(jobName));
            }
            
            // Get dynamic buffer size based on current memory allocation
            int bufferSize = _memoryManager.GetBufferSize(jobName);
            
            // Ensure buffer size is at least 4KB and not more than 8MB
            bufferSize = Math.Max(4096, Math.Min(bufferSize, 8 * 1024 * 1024));
            
            Console.WriteLine($"Copying file with buffer size: {bufferSize} bytes for job: {jobName}");
            
            // Create destination directory if it doesn't exist
            string destDir = Path.GetDirectoryName(destination);
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }
            
            // Vérifier si le fichier existe
            if (!File.Exists(source))
            {
                throw new FileNotFoundException($"Source file not found: {source}");
            }

            // Si le fichier de destination existe déjà, le supprimer
            if (File.Exists(destination))
            {
                try
                {
                    File.Delete(destination);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Could not delete existing destination file: {ex.Message}");
                }
            }
            
            long fileSize = new FileInfo(source).Length;
            
            try
            {
                // Créer les streams avec un timeout plus court pour détecter les blocages
                using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan))
                using (var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous | FileOptions.WriteThrough))
                {
                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    long totalBytesRead = 0;
                    
                    // Utiliser des délais de surveillance pour éviter les blocages
                    var lastProgressTime = DateTime.Now;

                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        // Vérifier si l'opération est en pause
                        pauseEvent.WaitOne();
                        
                        // Vérifier si l'opération est annulée
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // Mise à jour du temps de dernière progression
                        lastProgressTime = DateTime.Now;
                        
                        // Vérifier périodiquement (tous les 10MB) si la taille du buffer a changé
                        totalBytesRead += bytesRead;
                        if (totalBytesRead % (10 * 1024 * 1024) < bytesRead)
                        {
                            // Vérifier si la taille du buffer a changé (un autre job a démarré/terminé)
                            int currentBufferSize = _memoryManager.GetBufferSize(jobName);
                            currentBufferSize = Math.Max(4096, Math.Min(currentBufferSize, 8 * 1024 * 1024));
                            
                            if (currentBufferSize != buffer.Length)
                            {
                                Console.WriteLine($"Buffer size changed for job {jobName}: {buffer.Length} -> {currentBufferSize}");
                                buffer = new byte[currentBufferSize];
                            }
                        }
                        
                        // Écrire dans le fichier de destination
                        await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        
                        // Log progression périodiquement
                        if (fileSize > 0 && totalBytesRead % (5 * 1024 * 1024) < bytesRead)
                        {
                            double progress = (double)totalBytesRead / fileSize;
                            Console.WriteLine($"File copy progress for {Path.GetFileName(source)}: {progress:P0}");
                        }
                        
                        // Vérifier si le processus est bloqué
                        if ((DateTime.Now - lastProgressTime).TotalSeconds > 30)
                        {
                            Console.WriteLine($"WARNING: Copy operation seems stuck for {Path.GetFileName(source)}");
                            throw new TimeoutException("Copy operation timed out - no progress for 30 seconds");
                        }
                    }
                    
                    // Flush des données sur le disque
                    await destStream.FlushAsync(cancellationToken);
                }
                
                // Vérifier que le fichier a été correctement écrit
                if (new FileInfo(destination).Length != fileSize)
                {
                    Console.WriteLine($"WARNING: Destination file size ({new FileInfo(destination).Length}) doesn't match source file size ({fileSize})");
                }
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                Console.WriteLine($"Error during file copy: {ex.Message}");
                Console.WriteLine($"Source: {source}");
                Console.WriteLine($"Destination: {destination}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                // Si le fichier de destination existe et est partiellement écrit, le supprimer
                if (File.Exists(destination))
                {
                    try
                    {
                        File.Delete(destination);
                        Console.WriteLine($"Deleted partial file: {destination}");
                    }
                    catch (Exception deleteEx)
                    {
                        Console.WriteLine($"Failed to delete partial file: {deleteEx.Message}");
                    }
                }
                
                // Rethrow to handle at higher level
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