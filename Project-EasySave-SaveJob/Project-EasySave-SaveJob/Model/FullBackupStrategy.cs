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
            
            List<string> files = Directory
                .EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
                .ToList();

            long totalSize = files.Sum(f => new FileInfo(f).Length);
            long bytesCopied = 0;
            int totalFiles = files.Count;
            int filesCopied = 0;

            foreach (string src in files)
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
                
                // Use the base class implementation for file copying with progress tracking
                await CopyFileWithProgressAsync(src, dest, (bytesTransferred, isComplete) =>
                {
                    // Update progress based on actual bytes transferred, not file count
                    long currentBytesCopied = bytesCopied + bytesTransferred;
                    double progress = totalSize > 0 ? (double)currentBytesCopied / totalSize : 1.0;
                    
                    progressCallback?.Invoke(new StatusEntry(
                        job.Name, 
                        src, 
                        dest, 
                        "ACTIVE",
                        totalFiles, 
                        totalSize, 
                        totalFiles - filesCopied,
                        progress * 100.0)); // Convert to percentage (0-100)
                });
                
                // Update counters after file is complete
                bytesCopied += fileSize;
                filesCopied++;
            }
            
            // Send one final update to ensure we show 100% completion
            progressCallback?.Invoke(new StatusEntry(
                job.Name,
                string.Empty,
                string.Empty,
                "ACTIVE",
                totalFiles,
                totalSize,
                0,
                100.0 // 100% complete
            ));
            
            Console.WriteLine($"Full Backup completed: {filesCopied} files, {bytesCopied} bytes transferred");
        }

        private async Task CopyFileWithProgressAsync(string src, string dest, Action<long, bool> progressCallback)
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
