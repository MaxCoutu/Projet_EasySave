using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Manages memory allocation for backup jobs
    /// </summary>
    public class MemoryAllocationManager
    {
        // Singleton instance
        private static readonly Lazy<MemoryAllocationManager> _instance = 
            new Lazy<MemoryAllocationManager>(() => new MemoryAllocationManager());
        
        public static MemoryAllocationManager Instance => _instance.Value;
        
        // Default buffer sizes in bytes
        private const int DEFAULT_BUFFER_SIZE = 4 * 1024 * 1024; // 4MB
        private const int MIN_BUFFER_SIZE = 64 * 1024; // 64KB
        private const int MAX_BUFFER_SIZE = 16 * 1024 * 1024; // 16MB
        
        // Dictionary to track active jobs and their memory allocations
        private readonly Dictionary<string, JobMemoryInfo> _activeJobs = new Dictionary<string, JobMemoryInfo>();
        private readonly object _lock = new object();
        
        // Constructor (private for singleton)
        private MemoryAllocationManager()
        {
            Console.WriteLine("MemoryAllocationManager initialized");
        }
        
        /// <summary>
        /// Register a job with the memory manager
        /// </summary>
        public void RegisterJob(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
                throw new ArgumentNullException(nameof(jobName));
                
            lock (_lock)
            {
                if (_activeJobs.ContainsKey(jobName))
                {
                    Console.WriteLine($"Job {jobName} is already registered");
                    return;
                }
                
                // Calculate initial buffer size based on active job count
                int bufferSize = CalculateBufferSize(_activeJobs.Count + 1);
                
                _activeJobs[jobName] = new JobMemoryInfo
                {
                    BufferSize = bufferSize,
                    StartTime = DateTime.Now
                };
                
                Console.WriteLine($"Registered job: {jobName}, Buffer size: {bufferSize} bytes, Active jobs: {_activeJobs.Count}");
                
                // Recalculate buffer sizes for all jobs
                RecalculateBufferSizes();
            }
        }
        
        /// <summary>
        /// Unregister a job from the memory manager
        /// </summary>
        public void UnregisterJob(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
                return;
                
            lock (_lock)
            {
                if (_activeJobs.Remove(jobName))
                {
                    Console.WriteLine($"Unregistered job: {jobName}, Active jobs remaining: {_activeJobs.Count}");
                    
                    // Recalculate buffer sizes for remaining jobs
                    RecalculateBufferSizes();
                }
            }
        }
        
        /// <summary>
        /// Get the current buffer size for a job
        /// </summary>
        public int GetBufferSize(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
                return DEFAULT_BUFFER_SIZE;
                
            lock (_lock)
            {
                if (_activeJobs.TryGetValue(jobName, out var info))
                {
                    return info.BufferSize;
                }
                
                return DEFAULT_BUFFER_SIZE;
            }
        }
        
        /// <summary>
        /// Get the number of active jobs
        /// </summary>
        public int GetActiveJobCount()
        {
            lock (_lock)
            {
                return _activeJobs.Count;
            }
        }
        
        /// <summary>
        /// Get the memory percentage allocated to a job (out of total available)
        /// </summary>
        public double GetMemoryPercentage(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
                return 0;
                
            lock (_lock)
            {
                if (_activeJobs.Count == 0)
                    return 0;
                    
                if (_activeJobs.TryGetValue(jobName, out var info))
                {
                    long totalBufferSize = _activeJobs.Values.Sum(j => j.BufferSize);
                    if (totalBufferSize > 0)
                    {
                        return (double)info.BufferSize / totalBufferSize * 100;
                    }
                }
                
                return 0;
            }
        }
        
        /// <summary>
        /// Calculate an appropriate buffer size based on the number of active jobs
        /// </summary>
        private int CalculateBufferSize(int jobCount)
        {
            if (jobCount <= 0)
                return DEFAULT_BUFFER_SIZE;
                
            // Start with default buffer size
            int bufferSize = DEFAULT_BUFFER_SIZE;
            
            // Adjust based on job count
            if (jobCount == 1)
            {
                // Single job gets maximum buffer
                bufferSize = MAX_BUFFER_SIZE;
            }
            else if (jobCount <= 3)
            {
                // 2-3 jobs get medium buffer
                bufferSize = MAX_BUFFER_SIZE / 2;
            }
            else if (jobCount <= 6)
            {
                // 4-6 jobs get smaller buffer
                bufferSize = MAX_BUFFER_SIZE / 4;
            }
            else
            {
                // 7+ jobs get minimum buffer
                bufferSize = MIN_BUFFER_SIZE;
            }
            
            return bufferSize;
        }
        
        /// <summary>
        /// Recalculate buffer sizes for all active jobs
        /// </summary>
        private void RecalculateBufferSizes()
        {
            if (_activeJobs.Count == 0)
                return;
                
            int jobCount = _activeJobs.Count;
            int newBufferSize = CalculateBufferSize(jobCount);
            
            foreach (var job in _activeJobs.Keys.ToList())
            {
                if (_activeJobs.TryGetValue(job, out var info))
                {
                    info.BufferSize = newBufferSize;
                    _activeJobs[job] = info;
                }
            }
            
            Console.WriteLine($"Recalculated buffer sizes: {newBufferSize} bytes for {jobCount} jobs");
        }
        
        /// <summary>
        /// Structure to hold memory information for a job
        /// </summary>
        private struct JobMemoryInfo
        {
            public int BufferSize;
            public DateTime StartTime;
        }
    }
} 