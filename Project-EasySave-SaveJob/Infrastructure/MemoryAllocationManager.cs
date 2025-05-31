using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Manages dynamic memory allocation for backup jobs based on the number of active jobs
    /// </summary>
    public class MemoryAllocationManager
    {
        // Singleton instance
        private static readonly Lazy<MemoryAllocationManager> _instance = new Lazy<MemoryAllocationManager>(() => new MemoryAllocationManager());
        
        // Default buffer size for file copies (can be adjusted based on memory allocation)
        private const int DefaultBufferSize = 81920;
        
        // Maximum memory allocation per job in bytes (80MB default)
        private const long MaxMemoryPerJob = 83886080;
        
        // Lock for thread safety when updating allocations
        private readonly object _allocationLock = new object();
        
        // Track active jobs and their memory allocations
        private readonly ConcurrentDictionary<string, long> _activeJobs = new ConcurrentDictionary<string, long>();
        
        // Public singleton accessor
        public static MemoryAllocationManager Instance => _instance.Value;
        
        private MemoryAllocationManager()
        {
            Console.WriteLine("MemoryAllocationManager initialized");
        }
        
        /// <summary>
        /// Registers a job as active and recalculates memory allocations for all jobs
        /// </summary>
        /// <param name="jobName">Name of the job to register</param>
        /// <returns>Buffer size allocated for this job</returns>
        public int RegisterJob(string jobName)
        {
            Console.WriteLine($">>> Registering job '{jobName}' for memory allocation");
            
            lock (_allocationLock)
            {
                // Add job if not already present
                if (_activeJobs.TryAdd(jobName, 0))
                {
                    Console.WriteLine($">>> Job '{jobName}' added to active jobs list");
                }
                else
                {
                    Console.WriteLine($">>> Job '{jobName}' was already registered");
                }
                
                // Recalculate allocations for all jobs
                RecalculateAllocations();
                
                // Return the buffer size for this job
                int bufferSize = CalculateBufferSize(_activeJobs[jobName]);
                Console.WriteLine($">>> Allocated buffer size for '{jobName}': {bufferSize} bytes");
                return bufferSize;
            }
        }
        
        /// <summary>
        /// Unregisters a job when it completes and recalculates memory allocations
        /// </summary>
        /// <param name="jobName">Name of the job to unregister</param>
        public void UnregisterJob(string jobName)
        {
            Console.WriteLine($">>> Unregistering job '{jobName}' from memory allocation");
            
            lock (_allocationLock)
            {
                // Remove the job
                if (_activeJobs.TryRemove(jobName, out _))
                {
                    Console.WriteLine($">>> Job '{jobName}' removed from active jobs list");
                }
                else
                {
                    Console.WriteLine($">>> Job '{jobName}' was not registered");
                }
                
                // Recalculate allocations for remaining jobs
                if (_activeJobs.Count > 0)
                {
                    RecalculateAllocations();
                }
                else
                {
                    Console.WriteLine(">>> No active jobs remain");
                }
            }
        }
        
        /// <summary>
        /// Gets the current buffer size for a specific job
        /// </summary>
        /// <param name="jobName">Name of the job</param>
        /// <returns>Buffer size in bytes</returns>
        public int GetBufferSize(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
            {
                Console.WriteLine(">>> WARNING: Attempted to get buffer size for a null or empty job name");
                return DefaultBufferSize;
            }

            if (_activeJobs.TryGetValue(jobName, out long allocation))
            {
                int bufferSize = CalculateBufferSize(allocation);
                Console.WriteLine($">>> Getting buffer size for '{jobName}': {bufferSize} bytes");
                return bufferSize;
            }
            
            // If job is not registered, register it now
            Console.WriteLine($">>> Job '{jobName}' not found when getting buffer size, registering it now");
            return RegisterJob(jobName);
        }
        
        /// <summary>
        /// Gets the current count of active jobs
        /// </summary>
        /// <returns>Number of active jobs</returns>
        public int GetActiveJobCount()
        {
            int count = _activeJobs.Count;
            Console.WriteLine($">>> Current active job count: {count}");
            return count;
        }
        
        /// <summary>
        /// Gets the memory percentage allocated to a specific job
        /// </summary>
        /// <param name="jobName">Name of the job</param>
        /// <returns>Memory percentage (0-100)</returns>
        public double GetMemoryPercentage(string jobName)
        {
            if (_activeJobs.Count == 0)
                return 0;
                
            if (_activeJobs.TryGetValue(jobName, out long allocation))
            {
                // Calculate percentage of total memory
                double percentage = (double)allocation / MaxMemoryPerJob * 100;
                Console.WriteLine($">>> Memory percentage for '{jobName}': {percentage:F1}%");
                return percentage;
            }
            
            Console.WriteLine($">>> Job '{jobName}' not found when getting memory percentage");
            return 0;
        }
        
        /// <summary>
        /// Dumps the current state of all jobs and their allocations
        /// </summary>
        public void DumpState()
        {
            Console.WriteLine(">>> ======== MEMORY ALLOCATION STATE ========");
            Console.WriteLine($">>> Total active jobs: {_activeJobs.Count}");
            
            if (_activeJobs.Count > 0)
            {
                Console.WriteLine(">>> Active jobs:");
                foreach (var job in _activeJobs)
                {
                    double percentage = (double)job.Value / MaxMemoryPerJob * 100;
                    int bufferSize = CalculateBufferSize(job.Value);
                    Console.WriteLine($">>>   - {job.Key}: {job.Value} bytes ({percentage:F1}%), buffer: {bufferSize} bytes");
                }
            }
            
            Console.WriteLine(">>> ========================================");
        }
        
        /// <summary>
        /// Recalculates memory allocations for all active jobs, dividing available memory equally
        /// </summary>
        private void RecalculateAllocations()
        {
            int jobCount = _activeJobs.Count;
            
            if (jobCount == 0) return;
            
            // Calculate per-job allocation
            long totalAvailableMemory = Math.Max(MaxMemoryPerJob, Environment.SystemPageSize * 1024); // Au moins 4 MB
            
            // Calculer la mémoire totale disponible en fonction du nombre de processeurs
            int procCount = Environment.ProcessorCount;
            totalAvailableMemory = Math.Min(
                MaxMemoryPerJob * Math.Max(1, procCount / 2), // Utiliser la moitié des processeurs disponibles
                totalAvailableMemory * jobCount * 2            // Mais pas plus du double par job
            );
            
            long memoryPerJob = totalAvailableMemory / jobCount;
            
            // Set a minimum allocation to ensure jobs have enough memory
            const long minimumAllocation = 4 * 1024 * 1024; // 4 MB minimum
            
            if (memoryPerJob < minimumAllocation)
            {
                Console.WriteLine($">>> WARNING: Calculated memory per job ({memoryPerJob} bytes) is below minimum threshold. Using {minimumAllocation} bytes per job instead.");
                memoryPerJob = minimumAllocation;
            }
            
            Console.WriteLine($">>> Recalculating memory allocations for {jobCount} active jobs: {memoryPerJob} bytes per job (total: {totalAvailableMemory} bytes)");
            
            // Update allocations for all jobs
            foreach (string jobName in _activeJobs.Keys)
            {
                // Vérifier si la clé existe toujours (peut avoir été supprimée par un autre thread)
                if (_activeJobs.ContainsKey(jobName))
                {
                    _activeJobs[jobName] = memoryPerJob;
                    Console.WriteLine($">>>   - Updated allocation for '{jobName}' to {memoryPerJob} bytes");
                }
            }
            
            // Dump the current state
            DumpState();
        }
        
        /// <summary>
        /// Calculates the optimal buffer size based on memory allocation
        /// </summary>
        /// <param name="allocation">Memory allocation in bytes</param>
        /// <returns>Buffer size in bytes</returns>
        private int CalculateBufferSize(long allocation)
        {
            // Use a portion of the allocation for buffer size (25%)
            int bufferSize = (int)Math.Min(int.MaxValue, Math.Max(4096, allocation / 4));
            
            // Round to nearest multiple of 4096 (common page size)
            bufferSize = (bufferSize / 4096) * 4096;
            
            return bufferSize;
        }
    }
} 