using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Projet.Model;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Represents a single backup instance that runs independently
    /// </summary>
    public class BackupInstance
    {
        // Events
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
        
        public BackupInstance(BackupJob job, ILogger logger, Settings settings)
        {
            Job = job ?? throw new ArgumentNullException(nameof(job));
            Name = job.Name;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _memoryManager = MemoryAllocationManager.Instance;
            
            Console.WriteLine($"Created backup instance for job: {Name}");
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
            var sourceFiles = new List<(string src, string dest)>();
            foreach (string src in files)
            {
                string rel = Path.GetRelativePath(cleanedSourceDir, src);
                string dest = Path.Combine(cleanedTargetDir, rel);
                sourceFiles.Add((src, dest));
            }
            
            // Update progress information
            TotalFiles = sourceFiles.Count;
            TotalSize = sourceFiles.Sum(f => new FileInfo(f.src).Length);
            FilesRemaining = TotalFiles;
            
            Console.WriteLine($"Starting backup: {Name} - {TotalFiles} files, {TotalSize} bytes");
            ReportStatus();
            
            // Copy files
            int filesCopied = 0;
            long bytesCopied = 0;
            
            foreach (var (src, dest) in sourceFiles)
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
                        TransferTimeMs = 0, // Not tracking individual file times
                        EncryptionTimeMs = 0 // Not tracking individual file times
                    });
                    
                    // Update progress
                    filesCopied++;
                    bytesCopied += fileSize;
                    FilesRemaining = TotalFiles - filesCopied;
                    
                    // Calculate progress percentage (0-100)
                    Progress = TotalSize > 0 ? Math.Min(99.9, Math.Max(0, (double)bytesCopied / TotalSize * 100)) : 0;
                    
                    // Report status
                    ReportStatus(src, dest);
                    
                    Console.WriteLine($"Progress: {Progress:F2}% ({bytesCopied}/{TotalSize} bytes, {filesCopied}/{TotalFiles} files)");
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
            
            // Send final update for 100% completion
            Progress = 100;
            FilesRemaining = 0;
            ReportStatus();
            
            Console.WriteLine($"Backup job completed successfully: {Name} - {bytesCopied} bytes in {filesCopied} files");
        }
        
        /// <summary>
        /// Copies a file with progress reporting
        /// </summary>
        private async Task CopyFileAsync(string source, string destination, long fileSize, CancellationToken cancellationToken)
        {
            // Ensure buffer size is valid (minimum 4KB)
            int bufferSize = Math.Max(4096, _memoryManager.GetBufferSize(Name));
            DateTime lastProgressUpdate = DateTime.MinValue;
            
            try
            {
                using (var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, FileOptions.Asynchronous))
                using (var destStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, FileOptions.Asynchronous))
                {
                    var buffer = new byte[bufferSize];
                    int bytesRead;
                    long totalBytesRead = 0;

                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        // Check for pause
                        WaitIfPaused(cancellationToken);
                        
                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        // Write the data
                        await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                        totalBytesRead += bytesRead;
                        
                        // Report progress periodically (every 500ms)
                        if ((DateTime.Now - lastProgressUpdate).TotalMilliseconds > 500)
                        {
                            lastProgressUpdate = DateTime.Now;
                            
                            // Update file progress
                            double fileProgress = fileSize > 0 ? (double)totalBytesRead / fileSize : 0;
                            Console.WriteLine($"File progress: {fileProgress:P2} ({totalBytesRead}/{fileSize} bytes)");
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
            var status = new StatusEntry
            {
                Name = Name,
                State = State,
                SourceFilePath = sourcePath,
                TargetFilePath = targetPath,
                TotalFilesToCopy = TotalFiles,
                TotalFilesSize = TotalSize,
                NbFilesLeftToDo = FilesRemaining,
                Progression = Progress,
                ErrorMessage = errorMessage,
                BufferSize = _memoryManager.GetBufferSize(Name),
                ActiveJobs = _memoryManager.GetActiveJobCount(),
                MemoryPercentage = _memoryManager.GetMemoryPercentage(Name)
            };
            
            // Log the status
            _logger.UpdateStatus(status);
            
            // Notify subscribers
            StatusUpdated?.Invoke(status);
        }
    }
} 