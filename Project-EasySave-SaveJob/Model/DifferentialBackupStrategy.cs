using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Projet.Infrastructure;

namespace Projet.Model
{
    public sealed class DifferentialBackupStrategy : BackupStrategyBase
    {
        public override string Type => "Differential";

        public override async Task ExecuteAsync(BackupJob job, Action<StatusEntry> progressCallback)
        {
            string sourceDir = job.SourceDir.Trim('"').Trim();
            string targetDir = job.TargetDir.Trim('"').Trim();
            
            IEnumerable<string> allFiles =
                Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories);

            List<string> toCopy = new List<string>();
            foreach (string src in allFiles)
            {
                string rel = Path.GetRelativePath(sourceDir, src);
                string dest = Path.Combine(targetDir, rel);

                if (!File.Exists(dest) ||
                    File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dest))
                {
                    toCopy.Add(src);
                }
            }

            long totalSize = toCopy.Sum(f => new FileInfo(f).Length);
            long bytesCopied = 0;
            int totalFiles = toCopy.Count;
            int filesCopied = 0;

            foreach (string src in toCopy)
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
        }
    }
}
