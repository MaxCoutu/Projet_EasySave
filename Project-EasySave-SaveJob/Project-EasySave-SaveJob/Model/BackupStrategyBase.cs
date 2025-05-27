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
            const int bufferSize = 81920;
            long totalBytesCopied = 0;
            
            // Create the directory if it doesn't exist
            Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? ".");
            
            using (var sourceStream = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true))
            using (var destStream = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true))
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
