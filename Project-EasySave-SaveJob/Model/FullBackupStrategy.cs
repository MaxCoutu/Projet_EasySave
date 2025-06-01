using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Projet.Infrastructure;

namespace Projet.Model
{
    public sealed class FullBackupStrategy : BackupStrategyBase
    {
        public override string Type => "Full";

        public override async Task ExecuteAsync(BackupJob job, Action<StatusEntry> progressCallback)
        {
            string sourceDir = job.SourceDir.Trim('"').Trim();
            string targetDir = job.TargetDir.Trim('"').Trim();
            
            // Load the settings to get priority extensions
            Settings settings = job.Settings ?? new Settings();
            
            // Get all files to backup
            List<string> allFiles = Directory
                .EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
                .ToList();
                
            // Split files into priority and non-priority
            List<string> priorityFiles = allFiles
                .Where(file => IsPriorityFile(file, settings.PriorityExtensions))
                .ToList();
                
            List<string> regularFiles = allFiles
                .Where(file => !IsPriorityFile(file, settings.PriorityExtensions))
                .ToList();
                
            // Calculate total size and file counts
            long totalSize = allFiles.Sum(f => new FileInfo(f).Length);
            int totalFiles = allFiles.Count;
            
            // Combine the lists with priority files first
            List<string> orderedFiles = new List<string>();
            orderedFiles.AddRange(priorityFiles);
            orderedFiles.AddRange(regularFiles);
            
            // Variables to track progress
            long bytesCopied = 0;
            int filesCopied = 0;
            
            // First send a status update showing which files are prioritized
            var initialStatus = new StatusEntry
            {
                Name = job.Name,
                SourceFilePath = string.Empty,
                TargetFilePath = string.Empty,
                State = "ACTIVE",
                TotalFilesToCopy = totalFiles,
                PriorityFilesToCopy = priorityFiles.Count,
                TotalFilesSize = totalSize,
                NbFilesLeftToDo = totalFiles,
                Progression = 0.0
            };
            
            progressCallback?.Invoke(initialStatus);

            // Process all files in order (priority first, then regular)
            foreach (string src in orderedFiles)
            {
                string rel = Path.GetRelativePath(sourceDir, src);
                string dest = Path.Combine(targetDir, rel);
                
                // Create directory structure if needed
                string targetDirPath = Path.GetDirectoryName(dest);
                if (!Directory.Exists(targetDirPath))
                {
                    Directory.CreateDirectory(targetDirPath);
                }

                // Get the file size before copying
                long fileSize = new FileInfo(src).Length;
                
                // Check if this is a priority file
                bool isPriority = IsPriorityFile(src, settings.PriorityExtensions);
                
                // Use the base class implementation for file copying with progress tracking
                await CopyFileWithProgressAsync(src, dest, (bytesTransferred, isComplete) =>
                {
                    // Update progress based on actual bytes transferred, not file count
                    long currentBytesCopied = bytesCopied + bytesTransferred;
                    double progress = totalSize > 0 ? (double)currentBytesCopied / totalSize : 1.0;
                    
                    // Create status entry with the new format
                    var status = new StatusEntry
                    {
                        Name = job.Name,
                        SourceFilePath = src,
                        TargetFilePath = dest,
                        State = "ACTIVE",
                        TotalFilesToCopy = totalFiles,
                        PriorityFilesToCopy = priorityFiles.Count,
                        TotalFilesSize = totalSize,
                        NbFilesLeftToDo = totalFiles - filesCopied,
                        Progression = progress * 100.0, // Convert to percentage (0-100)
                        IsPriorityFile = isPriority
                    };
                    
                    progressCallback?.Invoke(status);
                });
                
                // Update counters after file is complete
                bytesCopied += fileSize;
                filesCopied++;
            }
            
            // Send one final update to ensure we show 100% completion
            var finalStatus = new StatusEntry
            {
                Name = job.Name,
                SourceFilePath = string.Empty,
                TargetFilePath = string.Empty,
                State = "ACTIVE",
                TotalFilesToCopy = totalFiles,
                PriorityFilesToCopy = priorityFiles.Count,
                TotalFilesSize = totalSize,
                NbFilesLeftToDo = 0,
                Progression = 100.0 // 100% complete
            };
            
            progressCallback?.Invoke(finalStatus);
            
            Console.WriteLine($"Full Backup completed: {filesCopied} files ({priorityFiles.Count} priority), {bytesCopied} bytes transferred");
        }

        // Use 'new' keyword to indicate this method intentionally hides the base class method
        new private async Task CopyFileWithProgressAsync(string src, string dest, Action<long, bool> progressCallback)
        {
            const int bufferSize = 81920;
            long totalBytesCopied = 0;
            
            using (var sourceStream = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true))
            using (var destStream = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true))
            {
                var buffer = new byte[bufferSize];
                int bytesRead;
                
                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesCopied += bytesRead;
                    
                    // Report progress during copy
                    progressCallback?.Invoke(totalBytesCopied, false);
                }
            }
            
            // Final progress update
            progressCallback?.Invoke(totalBytesCopied, true);
        }
    }
}
