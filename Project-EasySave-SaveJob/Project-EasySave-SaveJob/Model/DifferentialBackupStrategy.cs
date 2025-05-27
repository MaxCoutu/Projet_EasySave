using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Projet.Infrastructure;

namespace Projet.Model
{
    public sealed class DifferentialBackupStrategy : BackupStrategyBase
    {
        public override string Type => "Diff";

        public override async Task ExecuteAsync(BackupJob job, Action<StatusEntry> progressCallback)
        {
            string sourceDir = job.SourceDir.Trim('"').Trim();
            string targetDir = job.TargetDir.Trim('"').Trim();
            
            if (!Directory.Exists(sourceDir))
            {
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // Get all source files
            List<string> sourceFiles = Directory
                .EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories)
                .ToList();

            // Calculate total size for progress tracking
            long totalSize = sourceFiles.Sum(f => new FileInfo(f).Length);
            long bytesCopied = 0; // Tracks actual bytes copied
            long bytesProcessed = 0; // Tracks total bytes processed (copied + skipped)
            int totalFiles = sourceFiles.Count;
            int filesProcessed = 0;
            int filesCopied = 0;

            // Process each source file
            foreach (string sourceFile in sourceFiles)
            {
                // Calculate relative path
                string relPath = Path.GetRelativePath(sourceDir, sourceFile);
                string targetFile = Path.Combine(targetDir, relPath);
                
                bool needsCopy = true;
                var sourceInfo = new FileInfo(sourceFile);
                long fileSize = sourceInfo.Length;

                // Check if the target file exists
                if (File.Exists(targetFile))
                {
                    var targetInfo = new FileInfo(targetFile);

                    // Compare last write time and size to determine if it needs to be copied
                    // Only copy if the source file is newer or different size
                    if (sourceInfo.LastWriteTime <= targetInfo.LastWriteTime &&
                        sourceInfo.Length == targetInfo.Length)
                    {
                        needsCopy = false;
                    }
                }

                if (needsCopy)
                {
                    // Create directory structure if needed
                    string targetDirPath = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetDirPath))
                    {
                        Directory.CreateDirectory(targetDirPath);
                    }
                    
                    // Copy the file
                    await CopyFileAsync(sourceFile, targetFile);
                    filesCopied++;
                    bytesCopied += fileSize;
                }

                // Update progress based on bytes processed (regardless of whether they were copied)
                bytesProcessed += fileSize;
                filesProcessed++;
                
                // Calculate progress based on bytes, not file count
                double progress = (double)bytesProcessed / totalSize;

                // Report progress
                progressCallback?.Invoke(new StatusEntry(
                    job.Name,
                    sourceFile,
                    needsCopy ? targetFile : string.Empty,
                    "ACTIVE",
                    totalFiles,
                    totalSize,
                    totalFiles - filesProcessed,
                    progress
                ));
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

            Console.WriteLine($"Differential Backup completed: {filesCopied} of {totalFiles} files copied, {bytesCopied} of {totalSize} bytes transferred");
        }
    }
}
