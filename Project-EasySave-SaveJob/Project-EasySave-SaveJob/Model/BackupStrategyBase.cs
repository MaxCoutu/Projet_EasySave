using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Projet.Infrastructure;

namespace Projet.Model
{
    public abstract class BackupStrategyBase: IBackupStrategy
    {
        public abstract string Type { get; }

        public abstract Task ExecuteAsync(BackupJob job, Action<StatusEntry> progressCallback);

        protected static async Task CopyFileAsync(string src, string dst)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? ".");
            using FileStream source = File.OpenRead(src);
            using FileStream destination = File.Create(dst);
            await source.CopyToAsync(destination);
        }
        
        /// <summary>
        /// Copies a file while reporting progress during the operation
        /// </summary>
        /// <param name="src">Source file path</param>
        /// <param name="dst">Destination file path</param>
        /// <param name="progressCallback">Callback that receives bytes copied so far and whether copy is complete</param>
        protected static async Task CopyFileWithProgressAsync(string src, string dst, Action<long, bool> progressCallback)
        {
            // Reduced buffer size for better memory usage on low-end machines
            const int bufferSize = 32768; // 32KB instead of 80KB
            long totalBytesCopied = 0;
            
            // Create the directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? ".");
            
            using (var sourceStream = new FileStream(
                src, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read, 
                bufferSize, 
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var destStream = new FileStream(
                dst, 
                FileMode.Create, 
                FileAccess.Write, 
                FileShare.None, 
                bufferSize, 
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[bufferSize];
                int bytesRead;
                
                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesCopied += bytesRead;
                    
                    // Report progress less frequently to reduce overhead
                    if (totalBytesCopied % (bufferSize * 16) == 0)
                    {
                        progressCallback?.Invoke(totalBytesCopied, false);
                    }
                }
            }
            
            // Final progress update
            progressCallback?.Invoke(totalBytesCopied, true);
        }
    }
}
