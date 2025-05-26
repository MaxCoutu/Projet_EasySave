using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
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
            
            // Use FileOptions.Asynchronous for better async I/O performance
            using FileStream source = new FileStream(
                src, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read, 
                bufferSize: 4096, 
                FileOptions.Asynchronous | FileOptions.SequentialScan);
                
            using FileStream destination = new FileStream(
                dst, 
                FileMode.Create, 
                FileAccess.Write, 
                FileShare.None, 
                bufferSize: 4096, 
                FileOptions.Asynchronous | FileOptions.SequentialScan);
                
            await source.CopyToAsync(destination);
        }
        
        // Add method to copy files with cancellation support
        protected static async Task CopyFileAsync(string src, string dst, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? ".");
            
            // Use FileOptions.Asynchronous for better async I/O performance
            using FileStream source = new FileStream(
                src, 
                FileMode.Open, 
                FileAccess.Read, 
                FileShare.Read, 
                bufferSize: 4096, 
                FileOptions.Asynchronous | FileOptions.SequentialScan);
                
            using FileStream destination = new FileStream(
                dst, 
                FileMode.Create, 
                FileAccess.Write, 
                FileShare.None, 
                bufferSize: 4096, 
                FileOptions.Asynchronous | FileOptions.SequentialScan);
                
            await source.CopyToAsync(destination, 81920, cancellationToken);
        }
    }
}
