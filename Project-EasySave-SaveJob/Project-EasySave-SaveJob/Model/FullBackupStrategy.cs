using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
                
                // Copy the file
                await CopyFileAsync(src, dest);
                filesCopied++;
                
                // Update total bytes copied
                bytesCopied += fileSize;
                
                // Calculate progress based on bytes, not file count
                double progress = (double)bytesCopied / totalSize;

                progressCallback?.Invoke(new StatusEntry(
                    job.Name, 
                    src, 
                    dest, 
                    "ACTIVE",
                    totalFiles, 
                    totalSize, 
                    totalFiles - filesCopied,
                    progress));
                    
                Console.WriteLine($"Progress: {progress:P2} ({bytesCopied}/{totalSize} bytes, {filesCopied}/{totalFiles} files)");
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
                1.0 // 100% complete
            ));
            
            Console.WriteLine($"Full Backup completed: {filesCopied} files, {bytesCopied} bytes transferred");
        }
    }
}
