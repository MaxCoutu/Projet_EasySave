using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

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

        public static ThreadPoolManager Instance => _instance.Value;

        private ThreadPoolManager()
        {
            // Calculate the number of CPU cores to use (50% of total)
            int totalCores = Environment.ProcessorCount;
            int coresToUse = Math.Max(1, totalCores / 2);

            // Allocate threads for each category based on percentages
            _maxConcurrentCopyTasks = Math.Max(1, (coresToUse * CopyTasksPercent) / 100);
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
            // Create task processor for each category
            Task.Run(() => ProcessTaskQueue(_copyTasks, _copyTasksSemaphore, "Copy"));
            Task.Run(() => ProcessTaskQueue(_loggingTasks, _loggingTasksSemaphore, "Logging"));
            Task.Run(() => ProcessTaskQueue(_guiTasks, _guiTasksSemaphore, "GUI"));
        }

        public void Stop()
        {
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

        private async Task ProcessTaskQueue(
            ConcurrentQueue<Func<CancellationToken, Task>> taskQueue, 
            SemaphoreSlim semaphore, 
            string queueName)
        {
            while (!_cancellationSource.Token.IsCancellationRequested)
            {
                if (taskQueue.TryDequeue(out var taskToProcess))
                {
                    await semaphore.WaitAsync(_cancellationSource.Token);
                    try
                    {
                        await taskToProcess(_cancellationSource.Token);
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
                    await Task.Delay(50, _cancellationSource.Token);
                }
            }
        }
    }
}