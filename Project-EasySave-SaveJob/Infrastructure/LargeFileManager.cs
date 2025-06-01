using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Singleton class that manages the synchronization of large file transfers
    /// to prevent simultaneous transfer of files larger than the configured threshold
    /// </summary>
    public class LargeFileManager
    {
        // Singleton instance
        private static readonly Lazy<LargeFileManager> _instance = 
            new Lazy<LargeFileManager>(() => new LargeFileManager());
        
        public static LargeFileManager Instance => _instance.Value;
        
        // Semaphore to allow only one large file transfer at a time
        private readonly SemaphoreSlim _largeSemaphore = new SemaphoreSlim(1, 1);
        
        // Dictionary to track active file transfers by job
        private readonly Dictionary<string, FileTransferInfo> _activeTransfers = new Dictionary<string, FileTransferInfo>();
        private readonly object _lock = new object();
        
        // Constructor (private for singleton)
        private LargeFileManager()
        {
            Console.WriteLine("LargeFileManager initialized");
        }
        
        /// <summary>
        /// Checks if a file is considered large based on the settings threshold
        /// </summary>
        /// <param name="fileSize">File size in bytes</param>
        /// <param name="settings">Application settings</param>
        /// <returns>True if the file is considered large</returns>
        public bool IsLargeFile(long fileSize, Settings settings)
        {
            // Convert settings threshold from KB to bytes
            long thresholdBytes = settings.MaxFileSizeKB * 1024L;
            return fileSize >= thresholdBytes;
        }
        
        /// <summary>
        /// Request permission to transfer a large file
        /// </summary>
        /// <param name="jobName">Name of the backup job</param>
        /// <param name="filePath">Path of the file to transfer</param>
        /// <param name="fileSize">Size of the file in bytes</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when the transfer is allowed</returns>
        public async Task RequestLargeFileTransferPermissionAsync(
            string jobName, 
            string filePath, 
            long fileSize, 
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"Job {jobName} requesting permission to transfer large file: {filePath} ({fileSize} bytes)");
            
            // Wait for semaphore (only one large file can transfer at a time)
            await _largeSemaphore.WaitAsync(cancellationToken);
            
            lock (_lock)
            {
                // Register this transfer as active
                _activeTransfers[jobName] = new FileTransferInfo
                {
                    FilePath = filePath,
                    FileSize = fileSize,
                    StartTime = DateTime.Now
                };
                
                Console.WriteLine($"Job {jobName} granted permission to transfer large file: {filePath}");
            }
        }
        
        /// <summary>
        /// Release the permission after a large file transfer is complete
        /// </summary>
        /// <param name="jobName">Name of the backup job</param>
        public void ReleaseLargeFileTransferPermission(string jobName)
        {
            lock (_lock)
            {
                if (_activeTransfers.ContainsKey(jobName))
                {
                    var info = _activeTransfers[jobName];
                    _activeTransfers.Remove(jobName);
                    
                    Console.WriteLine($"Job {jobName} completed transfer of large file: {info.FilePath}");
                    
                    // Release the semaphore to allow another large file transfer
                    _largeSemaphore.Release();
                }
            }
        }
        
        /// <summary>
        /// Check if a job is currently transferring a large file
        /// </summary>
        /// <param name="jobName">Name of the backup job</param>
        /// <returns>True if the job is transferring a large file</returns>
        public bool IsTransferringLargeFile(string jobName)
        {
            lock (_lock)
            {
                return _activeTransfers.ContainsKey(jobName);
            }
        }
        
        /// <summary>
        /// Get information about the current large file transfers
        /// </summary>
        /// <returns>List of active large file transfers</returns>
        public List<FileTransferInfo> GetActiveLargeFileTransfers()
        {
            lock (_lock)
            {
                var result = new List<FileTransferInfo>();
                
                foreach (var kvp in _activeTransfers)
                {
                    result.Add(new FileTransferInfo
                    {
                        JobName = kvp.Key,
                        FilePath = kvp.Value.FilePath,
                        FileSize = kvp.Value.FileSize,
                        StartTime = kvp.Value.StartTime
                    });
                }
                
                return result;
            }
        }
        
        /// <summary>
        /// Structure to hold information about a file transfer
        /// </summary>
        public class FileTransferInfo
        {
            public string JobName { get; set; }
            public string FilePath { get; set; }
            public long FileSize { get; set; }
            public DateTime StartTime { get; set; }
            
            public TimeSpan Duration => DateTime.Now - StartTime;
        }
    }
} 