using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Projet.Model;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Represents a single backup instance that runs independently
    /// </summary>
    public class BackupInstance
    {
        /// <summary>
        /// Event fired when status changes
        /// </summary>
        public event Action<StatusEntry> StatusUpdated;
        
        // Properties
        public string Name { get; }
        public BackupJob Job { get; }
        public string State { get; private set; } = "READY";
        public double Progress { get; private set; } = 0;
        public int TotalFiles { get; private set; } = 0;
        public long TotalSize { get; private set; } = 0;
        public int FilesRemaining { get; private set; } = 0;
        public bool IsRunning => State == "ACTIVE" || State == "PAUSED" || State == "PENDING";
        
        // Private fields
        private readonly ILogger _logger;
        private readonly Settings _settings;
        private readonly MemoryAllocationManager _memoryManager;
        private CancellationTokenSource _cancellationTokenSource;
        private ManualResetEvent _pauseEvent = new ManualResetEvent(true);
        private Task _backupTask;
        private DateTime _startTime;
        private DateTime _lastStatusUpdate = DateTime.MinValue;
        private System.Timers.Timer _progressUpdateTimer;
        
        public BackupInstance(BackupJob job, ILogger logger, Settings settings)
        {
            Job = job ?? throw new ArgumentNullException(nameof(job));
            Name = job.Name;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _memoryManager = MemoryAllocationManager.Instance;
            
            // Ensure the job has a reference to settings
            if (job.Settings == null)
            {
                job.Settings = settings;
            }
            
            // Configurer un timer pour forcer des mises à jour de statut régulières
            // Exactement comme le font les actions pause/resume
            _progressUpdateTimer = new System.Timers.Timer(50); // Intervalle très court (50ms)
            _progressUpdateTimer.Elapsed += (s, e) => {
                if (State == "ACTIVE" || State == "PENDING") {
                    // Simuler exactement ce qui se passe dans Pause/Resume
                    // pour forcer un rafraîchissement de l'UI
                    ReportStatus();
                }
            };
            _progressUpdateTimer.AutoReset = true;
            
            Console.WriteLine($"BackupInstance created for job: {Name}");
        }
        
        /// <summary>
        /// Starts the backup process
        /// </summary>
        public Task StartAsync()
        {
            if (IsRunning)
            {
                Console.WriteLine($"Job {Name} is already running");
                return _backupTask;
            }
            
            // Create new cancellation token and reset pause event
            _cancellationTokenSource = new CancellationTokenSource();
            _pauseEvent.Set(); // Ensure not paused
            
            // Update state
            State = "PENDING";
            Progress = 0;
            _startTime = DateTime.Now;
            
            // Log priority file information if available
            if (Job.Settings?.PriorityExtensions?.Count > 0)
            {
                Console.WriteLine($"Priority file extensions for job {Name}: {string.Join(", ", Job.Settings.PriorityExtensions)}");
            }
            
            // Configurer le timer de mise à jour de progression avec un intervalle plus court (30ms)
            _progressUpdateTimer = new System.Timers.Timer(30);
            _progressUpdateTimer.Elapsed += (s, e) => ReportStatus();
            _progressUpdateTimer.Start();
            
            // Report initial status
            ReportStatus();
            
            // Start the backup task
            _backupTask = Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine($"Starting backup job: {Name}");
                    State = "ACTIVE";
                    ReportStatus();
                    
                    // Register with memory manager
                    _memoryManager.RegisterJob(Name);
                    
                    // Process the backup
                    await ProcessBackupAsync(_cancellationTokenSource.Token);
                    
                    // Update state on completion
                    State = "END";
                    Progress = 100;
                    ReportStatus();
                    
                    Console.WriteLine($"Backup job completed: {Name}");
                }
                catch (OperationCanceledException)
                {
                    // Job was cancelled
                    State = "CANCELLED";
                    ReportStatus();
                    Console.WriteLine($"Backup job cancelled: {Name}");
                }
                catch (Exception ex)
                {
                    // Job failed
                    State = "ERROR";
                    ReportStatus(ex.Message);
                    Console.WriteLine($"Backup job failed: {Name}, Error: {ex.Message}");
                }
                finally
                {
                    // Clean up resources
                    _memoryManager.UnregisterJob(Name);
                    _progressUpdateTimer?.Stop();
                    _progressUpdateTimer?.Dispose();
                }
            });
            
            return _backupTask;
        }
        
        /// <summary>
        /// Pauses the backup process
        /// </summary>
        public void Pause()
        {
            if (State == "ACTIVE")
            {
                _pauseEvent.Reset();
                State = "PAUSED";
                ReportStatus();
                Console.WriteLine($"Backup job paused: {Name}");
            }
        }
        
        /// <summary>
        /// Resumes the backup process
        /// </summary>
        public void Resume()
        {
            if (State == "PAUSED")
            {
                _pauseEvent.Set();
                State = "ACTIVE";
                ReportStatus();
                Console.WriteLine($"Backup job resumed: {Name}");
            }
        }
        
        /// <summary>
        /// Cancels the backup process
        /// </summary>
        public void Cancel()
        {
            if (IsRunning)
            {
                _cancellationTokenSource?.Cancel();
                _pauseEvent.Set(); // Ensure not paused so cancellation can proceed
                Console.WriteLine($"Backup job cancellation requested: {Name}");
            }
        }
        
        /// <summary>
        /// Processes the backup job
        /// </summary>
        private async Task ProcessBackupAsync(CancellationToken cancellationToken)
        {
            // Build list of files to process
            string sourceDir = Job.SourceDir;
            string targetDir = Job.TargetDir;
            
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
            
            // Get files to copy
            var files = Directory.EnumerateFiles(cleanedSourceDir, "*", SearchOption.AllDirectories).ToList();
            
            // Create source/destination pairs
            var sourceFiles = new List<(string src, string dest, bool isPriority)>();
            foreach (string src in files)
            {
                string rel = Path.GetRelativePath(cleanedSourceDir, src);
                string dest = Path.Combine(cleanedTargetDir, rel);
                
                // Check if this is a priority file
                bool isPriority = false;
                if (Job.Settings != null && Job.Settings.PriorityExtensions.Count > 0)
                {
                    string extension = Path.GetExtension(src).ToLower();
                    isPriority = Job.Settings.PriorityExtensions.Contains(extension);
                }
                
                sourceFiles.Add((src, dest, isPriority));
            }
            
            // Sort files: priority files first, then non-priority files
            sourceFiles = sourceFiles
                .OrderByDescending(f => f.isPriority) // Priority files first
                .ToList();
                
            // Count priority files for reporting
            int priorityFileCount = sourceFiles.Count(f => f.isPriority);
            
            // Log priority file information
            if (priorityFileCount > 0)
            {
                Console.WriteLine($"Found {priorityFileCount} priority files out of {sourceFiles.Count} total files");
                
                // Print first few priority files for debugging
                var priorityFileSamples = sourceFiles
                    .Where(f => f.isPriority)
                    .Take(5)
                    .Select(f => Path.GetFileName(f.src));
                    
                Console.WriteLine($"Priority files (sample): {string.Join(", ", priorityFileSamples)}");
            }
            
            // Update progress information
            TotalFiles = sourceFiles.Count;
            TotalSize = sourceFiles.Sum(f => new FileInfo(f.src).Length);
            FilesRemaining = TotalFiles;
            
            // Rapport d'état initial pour afficher 0%
            Progress = 0;
            ReportStatus();
            
            // Pour les jobs très petits, créer des étapes artificielles pour montrer une progression
            bool artificialSteps = false;
            int artificialStepCount = 10;
            int currentArtificialStep = 0;
            
            // Si moins de 3 fichiers, nous utiliserons des étapes artificielles
            if (TotalFiles < 3) {
                artificialSteps = true;
                Console.WriteLine($"Using artificial steps for small job: {Name}");
            }

            // Informations de progression
            Console.WriteLine($"Starting backup: {Name} - {TotalFiles} files, {TotalSize} bytes");
            
            try
            {
                // Copy files
                int filesCopied = 0;
                
                if (artificialSteps) {
                    // Pour les petits jobs, simuler une progression par étapes
                    for (int step = 1; step <= artificialStepCount; step++) {
                        if (step == 1) {
                            // Étape initiale
                            currentArtificialStep = step;
                            Progress = (step * 100.0) / artificialStepCount;
                            ReportStatus();
                            await Task.Delay(50, cancellationToken);
                        }
                    }
                }
                
                foreach (var (src, dest, isPriority) in sourceFiles)
                {
                    // Check for cancellation
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    // Check for pause
                    WaitIfPaused(cancellationToken);
                    
                    // Skip files that don't exist
                    if (!File.Exists(src))
                    {
                        Console.WriteLine($"Source file does not exist: {src}");
                        continue;
                    }
                    
                    // Get file size
                    long fileSize = new FileInfo(src).Length;
                    
                    try
                    {
                        // Pour les jobs avec étapes artificielles, progression graduelle
                        if (artificialSteps) {
                            currentArtificialStep++;
                            Progress = Math.Min(99, (currentArtificialStep * 100.0) / artificialStepCount);
                            ReportStatus(src, dest);
                            await Task.Delay(30, cancellationToken);
                        }
                        
                        // Ensure the destination directory exists
                        Directory.CreateDirectory(Path.GetDirectoryName(dest));
                        
                        Console.WriteLine($"Copying: {src} -> {dest}");
                        
                        // Copy the file
                        await CopyFileAsync(src, dest, fileSize, cancellationToken);
                        
                        // Process file encryption if needed
                        if (_settings.CryptoExtensions.Contains(Path.GetExtension(src).ToLower()))
                        {
                            // Check for cancellation before encryption
                            cancellationToken.ThrowIfCancellationRequested();
                            
                            // Check for pause
                            WaitIfPaused(cancellationToken);
                            
                            Console.WriteLine($"Encrypting file: {dest}");
                            int encMs = CryptoSoftHelper.Encrypt(dest, _settings);
                            Console.WriteLine($"Encryption completed in {encMs}ms");
                        }
                        
                        // Log the event
                        _logger.LogEvent(new LogEntry
                        {
                            Timestamp = DateTime.UtcNow,
                            JobName = Name,
                            SourcePath = src,
                            DestPath = dest,
                            FileSize = fileSize,
                            TransferTimeMs = 0,
                            EncryptionTimeMs = 0
                        });
                        
                        // Update progress
                        filesCopied++;
                        FilesRemaining = TotalFiles - filesCopied;
                        
                        // Mise à jour de la progression réelle
                        if (!artificialSteps) {
                            // Calcul basé sur le nombre de fichiers
                            Progress = Math.Min(99, (filesCopied * 100.0) / TotalFiles);
                        }
                        
                        // Report status after each file, toujours
                        ReportStatus(src, dest);
                        
                        Console.WriteLine($"Progress: {Progress:F1}% ({filesCopied}/{TotalFiles} files)");
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"File copy cancelled: {src}");
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error copying file {src}: {ex.Message}");
                        throw;
                    }
                }
                
                // Si nous avons utilisé des étapes artificielles, petit délai avant 100%
                if (artificialSteps) {
                    await Task.Delay(100, cancellationToken);
                }
                
                // Send final update for 100% completion
                Progress = 100;
                FilesRemaining = 0;
                ReportStatus();
                
                Console.WriteLine($"Backup job completed successfully: {Name} - {TotalSize} bytes in {TotalFiles} files");
            }
            finally
            {
                // Le timer de progression est arrêté dans le bloc finally du _backupTask
            }
        }
        
        /// <summary>
        /// Copies a file with progress reporting
        /// </summary>
        private async Task CopyFileAsync(string source, string destination, long fileSize, CancellationToken cancellationToken)
        {
            // Ensure buffer size is valid (minimum 4KB)
            int bufferSize = Math.Max(4096, _memoryManager.GetBufferSize(Name));
            
            // Track progress for large files
            long bytesTransferred = 0;
            DateTime lastReportTime = DateTime.MinValue;
            
            // Pour les petits fichiers, définir une fréquence de mise à jour en fonction de la taille
            int updateIntervalMs = 50; // Par défaut, toutes les 50ms
            
            // Déterminer un intervalle approprié en fonction de la taille
            if (fileSize > 10 * 1024 * 1024) // >10MB
                updateIntervalMs = 200;
            else if (fileSize > 1 * 1024 * 1024) // >1MB
                updateIntervalMs = 100;
            else 
                updateIntervalMs = 50; // Petits fichiers
            
            try
            {
                using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, FileOptions.Asynchronous))
                using (var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous))
                {
                    var buffer = new byte[bufferSize];
                    int bytesRead;

                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        // Check for pause
                        WaitIfPaused(cancellationToken);
                        
                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // Write the data
                        await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        
                        // Update progress for large files (more frequent updates)
                        bytesTransferred += bytesRead;
                        
                        // Mettre à jour la progression en fonction de l'intervalle défini
                        if ((DateTime.Now - lastReportTime).TotalMilliseconds > updateIntervalMs)
                        {
                            lastReportTime = DateTime.Now;
                            ReportStatus(source, destination);
                        }
                        
                        // Yield control to allow other tasks to run
                        await Task.Yield();
                    }
                    
                    // Ensure all data is written to disk
                    await destStream.FlushAsync(cancellationToken);
                }
            }
            catch (Exception)
            {
                // Clean up partial file
                try
                {
                    if (File.Exists(destination))
                    {
                        File.Delete(destination);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
                
                throw;
            }
        }
        
        /// <summary>
        /// Waits if the backup is paused
        /// </summary>
        private void WaitIfPaused(CancellationToken cancellationToken)
        {
            if (!_pauseEvent.WaitOne(0))
            {
                Console.WriteLine($"Job {Name} is paused");
                
                // Wait until resumed or cancelled
                while (!_pauseEvent.WaitOne(100))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
                
                Console.WriteLine($"Job {Name} resumed");
            }
        }
        
        /// <summary>
        /// Reports the current status
        /// </summary>
        private void ReportStatus(string sourcePath = "", string targetPath = "", string errorMessage = "")
        {
            // Clamp between 0-100
            double progressValue = Math.Min(100, Math.Max(0, Progress));
            
            // Force à 100% pour les états terminaux
            if (State == "END" || State == "CANCELLED" || State == "ERROR")
            {
                progressValue = 100;
            }
            
            // Pour les sauvegardes terminées trop rapidement, simuler une progression
            if (State == "ACTIVE" && TotalFiles > 0 && (DateTime.Now - _startTime).TotalMilliseconds < 500)
            {
                // Pour les tâches très rapides, assurer une animation minimale
                double elapsedRatio = (DateTime.Now - _startTime).TotalMilliseconds / 500.0;
                
                // S'assurer que la progression augmente de manière linéaire
                if (elapsedRatio < 1.0 && progressValue > elapsedRatio * 100)
                {
                    progressValue = elapsedRatio * 99; // Limiter à 99% jusqu'à ce que ce soit vraiment fini
                }
            }
            
            // Ajouter une micro-variation pour forcer un rafraîchissement visuel
            // similaire à ce qui se passe lors d'un pause/resume
            if (State == "ACTIVE" || State == "PENDING")
            {
                // Micro-variation de la progression pour forcer le rafraîchissement
                Random rnd = new Random();
                double microVariation = rnd.NextDouble() * 0.001; // Variation imperceptible
                progressValue += microVariation;
            }
            
            // TOUJOURS mettre à jour l'horodatage pour forcer une détection de changement
            _lastStatusUpdate = DateTime.Now;
            
            // Check if current file is a priority file
            bool isPriorityFile = false;
            if (!string.IsNullOrEmpty(sourcePath) && Job.Settings?.PriorityExtensions != null)
            {
                string extension = Path.GetExtension(sourcePath).ToLower();
                isPriorityFile = Job.Settings.PriorityExtensions.Contains(extension);
            }
            
            // Count priority files in the job
            int priorityFileCount = 0;
            if (Job.Settings?.PriorityExtensions != null && Job.Settings.PriorityExtensions.Count > 0)
            {
                // Try to calculate from our known files if possible
                if (sourcePath == string.Empty && targetPath == string.Empty)
                {
                    // This is a status update without a specific file, just use the known count
                    var filesToProcess = Directory.EnumerateFiles(Job.SourceDir, "*", SearchOption.AllDirectories);
                    priorityFileCount = filesToProcess.Count(f => 
                        Job.Settings.PriorityExtensions.Contains(Path.GetExtension(f).ToLower()));
                }
            }
            
            var status = new StatusEntry
            {
                Name = Name,
                State = State,
                SourceFilePath = sourcePath,
                TargetFilePath = targetPath,
                TotalFilesToCopy = TotalFiles,
                TotalFilesSize = TotalSize,
                NbFilesLeftToDo = FilesRemaining,
                Progression = progressValue,
                ErrorMessage = errorMessage,
                // Add priority information
                IsPriorityFile = isPriorityFile,
                PriorityFilesToCopy = priorityFileCount,
                // Ajouter un timestamp unique à chaque fois pour forcer la détection de changement
                Timestamp = DateTime.Now 
            };
            
            // Log the status - ceci va écrire dans le fichier status.json
            _logger.UpdateStatus(status);
            
            // Notify subscribers
            StatusUpdated?.Invoke(status);
            
            // Log some extra info about priority files in the console
            if (!string.IsNullOrEmpty(sourcePath) && isPriorityFile)
            {
                Console.WriteLine($"[PRIORITY] Processing priority file: {Path.GetFileName(sourcePath)}");
            }
        }
    }
} 