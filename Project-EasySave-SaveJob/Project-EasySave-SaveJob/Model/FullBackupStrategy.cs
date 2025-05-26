using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Projet.Infrastructure;

namespace Projet.Model
{
    public sealed class FullBackupStrategy : BackupStrategyBase
    {
        public override string Type => "Full";

        public override async Task ExecuteAsync(BackupJob job, Action<StatusEntry> progressCallback)
        {
            List<string> files = Directory
                .EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories)
                .ToList();

            long totalSize = files.Sum(f => new FileInfo(f).Length);
            int total = files.Count;
            
            // Use a concurrent counter for thread-safe incrementing
            int doneCount = 0;
            object lockObj = new object();
            
            // Create a cancellation token source to allow cancellation
            using var cts = new CancellationTokenSource();
            
            // Create a list to track all running tasks
            var tasks = new List<Task>();
            
            // Create a semaphore to limit parallel operations
            using var semaphore = new SemaphoreSlim(4); // Limit to 4 concurrent operations
            
            try
            {
                foreach (string src in files)
                {
                    string rel = Path.GetRelativePath(job.SourceDir, src);
                    string dest = Path.Combine(job.TargetDir, rel);
                    
                    // Wait for a semaphore slot
                    await semaphore.WaitAsync(cts.Token);
                    
                    // Start a new task for this file
                    var task = Task.Run(async () =>
                    {
                        try
                        {
                            await CopyFileAsync(src, dest, cts.Token);
                            
                            // Thread-safe increment of progress counter
                            int newDoneCount;
                            lock (lockObj)
                            {
                                doneCount++;
                                newDoneCount = doneCount;
                            }
                            
                            // Report progress
                            progressCallback?.Invoke(new StatusEntry(
                                job.Name, src, dest, "ACTIVE",
                                total, totalSize, total - newDoneCount,
                                newDoneCount / (double)total));
                        }
                        finally
                        {
                            // Always release the semaphore
                            semaphore.Release();
                        }
                    }, cts.Token);
                    
                    tasks.Add(task);
                }
                
                // Wait for all tasks to complete
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Handle cancellation
                progressCallback?.Invoke(new StatusEntry(
                    job.Name, "", "", "CANCELED",
                    total, totalSize, total - doneCount,
                    doneCount / (double)total));
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                Console.WriteLine($"Error during backup: {ex.Message}");
                progressCallback?.Invoke(new StatusEntry(
                    job.Name, "", "", "ERROR",
                    total, totalSize, total - doneCount,
                    doneCount / (double)total));
                throw;
            }
        }
    }
}
