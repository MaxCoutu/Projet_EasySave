using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Manages parallel execution of backup jobs
    /// </summary>
    public class ParallelJobExecutor
    {
        // Singleton instance
        private static readonly Lazy<ParallelJobExecutor> _instance = new Lazy<ParallelJobExecutor>(() => new ParallelJobExecutor());
        
        // Memory allocation manager
        private readonly MemoryAllocationManager _memoryManager;
        
        // Thread pool for executing jobs
        private readonly ConcurrentDictionary<string, Task> _runningJobs = new ConcurrentDictionary<string, Task>();
        
        // Cancellation tokens for each job
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _jobCancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
        
        // Pause events for each job
        private readonly ConcurrentDictionary<string, ManualResetEvent> _jobPauseEvents = new ConcurrentDictionary<string, ManualResetEvent>();
        
        // Job states
        private readonly ConcurrentDictionary<string, string> _jobStates = new ConcurrentDictionary<string, string>();
        
        // Public singleton accessor
        public static ParallelJobExecutor Instance => _instance.Value;
        
        // Event for status updates
        public event Action<StatusEntry> StatusUpdated;
        
        private ParallelJobExecutor()
        {
            _memoryManager = MemoryAllocationManager.Instance;
            Console.WriteLine("ParallelJobExecutor initialized");
        }
        
        /// <summary>
        /// Executes a job in parallel with other running jobs
        /// </summary>
        /// <param name="jobName">Name of the job</param>
        /// <param name="jobAction">Action to execute for the job</param>
        /// <returns>Task representing the job execution</returns>
        public Task ExecuteJobAsync(string jobName, Func<CancellationToken, ManualResetEvent, Task> jobAction)
        {
            Console.WriteLine($"Starting parallel execution of job: {jobName}");
            
            // Check if job is already running
            if (_runningJobs.TryGetValue(jobName, out var existingTask))
            {
                Console.WriteLine($"Job {jobName} is already running, returning existing task");
                return existingTask;
            }
            
            // Create cancellation token and pause event for this job
            var cancellationTokenSource = new CancellationTokenSource();
            var pauseEvent = new ManualResetEvent(true); // Initially not paused
            
            // Store the token and event
            _jobCancellationTokens[jobName] = cancellationTokenSource;
            _jobPauseEvents[jobName] = pauseEvent;
            
            // Set initial state
            _jobStates[jobName] = "PENDING";
            
            // Register with memory manager - but don't hold any locks during the job execution
            _memoryManager.RegisterJob(jobName);
            
            // Create a task to execute the job
            var jobTask = Task.Run(async () =>
            {
                try
                {
                    // Update state
                    _jobStates[jobName] = "ACTIVE";
                    
                    // Report status
                    ReportStatus(new StatusEntry
                    {
                        Name = jobName,
                        State = "ACTIVE",
                        BufferSize = _memoryManager.GetBufferSize(jobName),
                        ActiveJobs = _memoryManager.GetActiveJobCount(),
                        MemoryPercentage = _memoryManager.GetMemoryPercentage(jobName)
                    });
                    
                    // Execute the job action with its own cancellation token and pause event
                    await jobAction(cancellationTokenSource.Token, pauseEvent);
                    
                    // Update state on completion
                    _jobStates[jobName] = "END";
                    
                    // Report final status
                    ReportStatus(new StatusEntry
                    {
                        Name = jobName,
                        State = "END",
                        Progression = 100,
                        BufferSize = _memoryManager.GetBufferSize(jobName),
                        ActiveJobs = _memoryManager.GetActiveJobCount(),
                        MemoryPercentage = _memoryManager.GetMemoryPercentage(jobName)
                    });
                }
                catch (OperationCanceledException)
                {
                    // Job was cancelled
                    _jobStates[jobName] = "CANCELLED";
                    
                    // Report cancelled status
                    ReportStatus(new StatusEntry
                    {
                        Name = jobName,
                        State = "CANCELLED",
                        BufferSize = _memoryManager.GetBufferSize(jobName),
                        ActiveJobs = _memoryManager.GetActiveJobCount(),
                        MemoryPercentage = _memoryManager.GetMemoryPercentage(jobName)
                    });
                }
                catch (Exception ex)
                {
                    // Job failed
                    Console.WriteLine($"Error in job {jobName}: {ex.Message}");
                    _jobStates[jobName] = "ERROR";
                    
                    // Report error status
                    ReportStatus(new StatusEntry
                    {
                        Name = jobName,
                        State = "ERROR",
                        ErrorMessage = ex.Message,
                        BufferSize = _memoryManager.GetBufferSize(jobName),
                        ActiveJobs = _memoryManager.GetActiveJobCount(),
                        MemoryPercentage = _memoryManager.GetMemoryPercentage(jobName)
                    });
                }
                finally
                {
                    // Clean up resources - use a separate try-catch to ensure cleanup happens
                    try
                    {
                        // Unregister from memory manager
                        _memoryManager.UnregisterJob(jobName);
                        
                        // Remove from tracking collections
                        _jobCancellationTokens.TryRemove(jobName, out _);
                        _jobPauseEvents.TryRemove(jobName, out _);
                        _runningJobs.TryRemove(jobName, out _);
                        
                        Console.WriteLine($"Job {jobName} completed with status: {_jobStates[jobName]}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error during cleanup for job {jobName}: {ex.Message}");
                    }
                }
            });
            
            // Store the task
            _runningJobs[jobName] = jobTask;
            
            return jobTask;
        }
        
        /// <summary>
        /// Pauses a running job
        /// </summary>
        /// <param name="jobName">Name of the job to pause</param>
        public void PauseJob(string jobName)
        {
            Console.WriteLine($"Pausing job: {jobName}");
            
            if (_jobPauseEvents.TryGetValue(jobName, out var pauseEvent))
            {
                // Only affect this specific job's pause event
                pauseEvent.Reset(); // Block execution
                _jobStates[jobName] = "PAUSED";
                
                // Report paused status
                ReportStatus(new StatusEntry
                {
                    Name = jobName,
                    State = "PAUSED",
                    BufferSize = _memoryManager.GetBufferSize(jobName),
                    ActiveJobs = _memoryManager.GetActiveJobCount(),
                    MemoryPercentage = _memoryManager.GetMemoryPercentage(jobName)
                });
                
                // Log the pause to help with debugging
                Console.WriteLine($"Job {jobName} is now PAUSED");
            }
            else
            {
                Console.WriteLine($"Cannot pause job {jobName}: job not found");
            }
        }
        
        /// <summary>
        /// Resumes a paused job
        /// </summary>
        /// <param name="jobName">Name of the job to resume</param>
        public void ResumeJob(string jobName)
        {
            Console.WriteLine($"Resuming job: {jobName}");
            
            if (_jobPauseEvents.TryGetValue(jobName, out var pauseEvent))
            {
                // Only affect this specific job's pause event
                pauseEvent.Set(); // Allow execution to continue
                _jobStates[jobName] = "ACTIVE";
                
                // Report active status
                ReportStatus(new StatusEntry
                {
                    Name = jobName,
                    State = "ACTIVE",
                    BufferSize = _memoryManager.GetBufferSize(jobName),
                    ActiveJobs = _memoryManager.GetActiveJobCount(),
                    MemoryPercentage = _memoryManager.GetMemoryPercentage(jobName)
                });
                
                // Log the resume to help with debugging
                Console.WriteLine($"Job {jobName} is now ACTIVE");
            }
            else
            {
                Console.WriteLine($"Cannot resume job {jobName}: job not found");
            }
        }
        
        /// <summary>
        /// Cancels a running job
        /// </summary>
        /// <param name="jobName">Name of the job to cancel</param>
        public void CancelJob(string jobName)
        {
            Console.WriteLine($"Cancelling job: {jobName}");
            
            if (_jobCancellationTokens.TryGetValue(jobName, out var tokenSource))
            {
                tokenSource.Cancel();
                
                // If job is paused, resume it so it can process the cancellation
                if (_jobPauseEvents.TryGetValue(jobName, out var pauseEvent))
                {
                    pauseEvent.Set();
                }
                
                _jobStates[jobName] = "CANCELLING";
                
                // Report cancelling status
                ReportStatus(new StatusEntry
                {
                    Name = jobName,
                    State = "CANCELLING",
                    BufferSize = _memoryManager.GetBufferSize(jobName),
                    ActiveJobs = _memoryManager.GetActiveJobCount(),
                    MemoryPercentage = _memoryManager.GetMemoryPercentage(jobName)
                });
            }
        }
        
        /// <summary>
        /// Cancels all running jobs
        /// </summary>
        public void CancelAllJobs()
        {
            Console.WriteLine("Cancelling all jobs");
            
            foreach (var jobName in _jobCancellationTokens.Keys)
            {
                CancelJob(jobName);
            }
        }
        
        /// <summary>
        /// Gets the current state of a job
        /// </summary>
        /// <param name="jobName">Name of the job</param>
        /// <returns>Current state of the job</returns>
        public string GetJobState(string jobName)
        {
            if (_jobStates.TryGetValue(jobName, out var state))
            {
                return state;
            }
            
            return "UNKNOWN";
        }
        
        /// <summary>
        /// Gets a list of all running job names
        /// </summary>
        /// <returns>List of job names</returns>
        public List<string> GetRunningJobs()
        {
            return new List<string>(_runningJobs.Keys);
        }
        
        /// <summary>
        /// Reports a status update for a job
        /// </summary>
        /// <param name="status">Status entry to report</param>
        private void ReportStatus(StatusEntry status)
        {
            // Ensure valid status
            if (status == null || string.IsNullOrEmpty(status.Name))
            {
                return;
            }
            
            // Update buffer size, active jobs, and memory percentage
            status.BufferSize = _memoryManager.GetBufferSize(status.Name);
            status.ActiveJobs = _memoryManager.GetActiveJobCount();
            status.MemoryPercentage = _memoryManager.GetMemoryPercentage(status.Name);
            
            // Ensure progression is valid (0-100)
            if (status.Progression < 0)
            {
                status.Progression = 0;
            }
            else if (status.Progression > 100)
            {
                status.Progression = 100;
            }
            
            // Update job state in our tracking dictionary for consistency
            if (!string.IsNullOrEmpty(status.State))
            {
                _jobStates[status.Name] = status.State;
            }
            
            // Log status update
            Console.WriteLine($"Reporting status for job {status.Name}: State={status.State}, Progress={status.Progression:F2}%, ActiveJobs={status.ActiveJobs}");
            
            // Invoke the event
            StatusUpdated?.Invoke(status);
        }
    }
} 