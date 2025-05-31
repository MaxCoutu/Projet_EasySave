using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Thread pool manager that allocates CPU resources based on task categories
    /// </summary>
    public class ThreadPoolManager
    {
        // Singleton instance
        private static readonly Lazy<ThreadPoolManager> _instance = new Lazy<ThreadPoolManager>(() => new ThreadPoolManager());
        
        // CPU allocation percentages
        private const int CopyTasksPercent = 70;
        private const int LoggingTasksPercent = 10;
        private const int GuiTasksPercent = 20;

        // Thread pools for different task categories
        private readonly int _maxConcurrentCopyTasks;
        private readonly int _maxConcurrentLoggingTasks;
        private readonly int _maxConcurrentGuiTasks;
        private readonly SemaphoreSlim _copyTasksSemaphore;
        private readonly SemaphoreSlim _loggingTasksSemaphore;
        private readonly SemaphoreSlim _guiTasksSemaphore;

        // Cancellation source for stopping all tasks
        private CancellationTokenSource _cancellationSource;

        // Task queues for each category
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _copyTasks;
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _loggingTasks;
        private readonly ConcurrentQueue<Func<CancellationToken, Task>> _guiTasks;
        
        // Counters for task tracking
        private int _copyTasksProcessed = 0;
        private int _copyTasksQueued = 0;
        private int _loggingTasksProcessed = 0;
        private int _loggingTasksQueued = 0;
        private int _guiTasksProcessed = 0;
        private int _guiTasksQueued = 0;

        public static ThreadPoolManager Instance => _instance.Value;

        private ThreadPoolManager()
        {
            // Calculate the number of CPU cores to use (50% of total)
            int totalCores = Environment.ProcessorCount;
            int coresToUse = Math.Max(2, totalCores / 2); // Ensure at least 2 cores

            // Allocate threads for each category based on percentages
            _maxConcurrentCopyTasks = Math.Max(2, (coresToUse * CopyTasksPercent) / 100); // Ensure at least 2 threads
            _maxConcurrentLoggingTasks = Math.Max(1, (coresToUse * LoggingTasksPercent) / 100);
            _maxConcurrentGuiTasks = Math.Max(1, (coresToUse * GuiTasksPercent) / 100);

            // Initialize semaphores to control thread counts
            _copyTasksSemaphore = new SemaphoreSlim(_maxConcurrentCopyTasks);
            _loggingTasksSemaphore = new SemaphoreSlim(_maxConcurrentLoggingTasks);
            _guiTasksSemaphore = new SemaphoreSlim(_maxConcurrentGuiTasks);

            // Initialize task queues
            _copyTasks = new ConcurrentQueue<Func<CancellationToken, Task>>();
            _loggingTasks = new ConcurrentQueue<Func<CancellationToken, Task>>();
            _guiTasks = new ConcurrentQueue<Func<CancellationToken, Task>>();

            _cancellationSource = new CancellationTokenSource();

            Console.WriteLine($"ThreadPoolManager initialized with:");
            Console.WriteLine($"- Total cores: {totalCores}, Using: {coresToUse}");
            Console.WriteLine($"- Copy tasks: {_maxConcurrentCopyTasks} threads ({CopyTasksPercent}%)");
            Console.WriteLine($"- Logging tasks: {_maxConcurrentLoggingTasks} threads ({LoggingTasksPercent}%)");
            Console.WriteLine($"- GUI tasks: {_maxConcurrentGuiTasks} threads ({GuiTasksPercent}%)");
        }

        public void Start()
        {
            Console.WriteLine("ThreadPoolManager starting task processors");
            
            // Create task processor for each category
            Task.Run(() => ProcessTaskQueue(_copyTasks, _copyTasksSemaphore, "Copy"));
            Task.Run(() => ProcessTaskQueue(_loggingTasks, _loggingTasksSemaphore, "Logging"));
            Task.Run(() => ProcessTaskQueue(_guiTasks, _guiTasksSemaphore, "GUI"));
        }

        public void Stop()
        {
            Console.WriteLine("ThreadPoolManager stopping, cancelling all tasks");
            _cancellationSource.Cancel();
            _cancellationSource = new CancellationTokenSource();
        }

        public Task EnqueueCopyTask(Func<CancellationToken, Task> task)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            _copyTasks.Enqueue(async (cancellationToken) => 
            {
                try 
                {
                    await task(cancellationToken);
                    tcs.TrySetResult(true);
                }
                catch (Exception ex) 
                {
                    tcs.TrySetException(ex);
                }
            });
            
            return tcs.Task;
        }

        public Task EnqueueLoggingTask(Func<CancellationToken, Task> task)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            _loggingTasks.Enqueue(async (cancellationToken) => 
            {
                try 
                {
                    await task(cancellationToken);
                    tcs.TrySetResult(true);
                }
                catch (Exception ex) 
                {
                    tcs.TrySetException(ex);
                }
            });
            
            return tcs.Task;
        }

        public Task EnqueueGuiTask(Func<CancellationToken, Task> task)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            _guiTasks.Enqueue(async (cancellationToken) => 
            {
                try 
                {
                    await task(cancellationToken);
                    tcs.TrySetResult(true);
                }
                catch (Exception ex) 
                {
                    tcs.TrySetException(ex);
                }
            });
            
            return tcs.Task;
        }

        /// <summary>
        /// Enqueues a GUI task with high priority, attempting to execute it immediately
        /// on the current thread if possible, otherwise queueing it with priority
        /// </summary>
        public Task EnqueueGuiTaskPriority(Func<CancellationToken, Task> task)
        {
            var tcs = new TaskCompletionSource<bool>();
            
            // Increment queued count
            Interlocked.Increment(ref _guiTasksQueued);
            
            // For high priority GUI tasks, try to run immediately if we can acquire the semaphore
            if (_guiTasksSemaphore.Wait(0))
            {
                try
                {
                    Console.WriteLine(">>> Executing priority GUI task immediately");
                    // Execute the task immediately on the current thread
                    Task.Run(async () => 
                    {
                        try 
                        {
                            await task(_cancellationSource.Token);
                            tcs.TrySetResult(true);
                        }
                        catch (Exception ex) 
                        {
                            Console.WriteLine($">>> Error in priority GUI task: {ex.Message}");
                            tcs.TrySetException(ex);
                        }
                        finally
                        {
                            _guiTasksSemaphore.Release();
                            Interlocked.Increment(ref _guiTasksProcessed);
                        }
                    });
                }
                catch (Exception ex)
                {
                    _guiTasksSemaphore.Release();
                    tcs.TrySetException(ex);
                    Console.WriteLine($">>> Error starting priority GUI task: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine(">>> Adding priority GUI task to front of queue");
                // If we can't run immediately, add to the front of the queue
                Func<CancellationToken, Task> priorityTask = async (cancellationToken) => 
                {
                    try 
                    {
                        await task(cancellationToken);
                        tcs.TrySetResult(true);
                    }
                    catch (Exception ex) 
                    {
                        Console.WriteLine($">>> Error in queued priority GUI task: {ex.Message}");
                        tcs.TrySetException(ex);
                    }
                    finally
                    {
                        Interlocked.Increment(ref _guiTasksProcessed);
                    }
                };
                
                // Add to a new queue and then merge with existing queue to prioritize
                var tempQueue = new ConcurrentQueue<Func<CancellationToken, Task>>();
                tempQueue.Enqueue(priorityTask);
                
                // Drain current tasks to temporary collection
                var existingTasks = new List<Func<CancellationToken, Task>>();
                while (_guiTasks.TryDequeue(out var existingTask))
                {
                    existingTasks.Add(existingTask);
                }
                
                // Re-add all tasks with priority task first
                _guiTasks.Enqueue(priorityTask);
                foreach (var existingTask in existingTasks)
                {
                    _guiTasks.Enqueue(existingTask);
                }
            }
            
            Console.WriteLine($">>> Priority GUI task enqueued (queued: {_guiTasksQueued}, processed: {_guiTasksProcessed})");
            return tcs.Task;
        }

        /// <summary>
        /// Monitors task queues and reports statistics
        /// </summary>
        private async Task MonitorTaskQueuesAsync()
        {
            while (!_cancellationSource.IsCancellationRequested)
            {
                // Log current queue status every few seconds
                await Task.Delay(5000, _cancellationSource.Token);
                
                Console.WriteLine(">>> ======== THREAD POOL STATS ========");
                Console.WriteLine($">>> Copy tasks: {_copyTasks.Count} queued, {_copyTasksQueued} total queued, {_copyTasksProcessed} processed");
                Console.WriteLine($">>> Logging tasks: {_loggingTasks.Count} queued, {_loggingTasksQueued} total queued, {_loggingTasksProcessed} processed");
                Console.WriteLine($">>> GUI tasks: {_guiTasks.Count} queued, {_guiTasksQueued} total queued, {_guiTasksProcessed} processed");
                Console.WriteLine(">>> ==================================");
            }
        }

        private async Task ProcessTaskQueue(
            ConcurrentQueue<Func<CancellationToken, Task>> taskQueue, 
            SemaphoreSlim semaphore, 
            string queueName)
        {
            Console.WriteLine($"Starting task processor for {queueName} queue");
            
            while (!_cancellationSource.Token.IsCancellationRequested)
            {
                if (taskQueue.TryDequeue(out var taskToProcess))
                {
                    await semaphore.WaitAsync(_cancellationSource.Token);
                    try
                    {
                        await taskToProcess(_cancellationSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"{queueName} task was cancelled");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in {queueName} task: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
                else
                {
                    // No tasks in queue, wait a bit before checking again
                    await Task.Delay(50, _cancellationSource.Token);
                }
            }
            
            Console.WriteLine($"Task processor for {queueName} queue is shutting down");
        }
    }
}