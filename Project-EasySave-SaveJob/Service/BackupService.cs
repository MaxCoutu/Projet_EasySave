using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Projet.Infrastructure;
using Projet.Model;

namespace Projet.Service
{
    public class BackupService : IBackupService, IDisposable
    {
        public event Action<StatusEntry> StatusUpdated;

        private readonly ILogger _logger;
        private readonly IJobRepository _repo;
        private readonly Settings _settings;
        private readonly List<BackupJob> _jobs;
        private readonly Dictionary<string, BackupInstance> _backupInstances = new Dictionary<string, BackupInstance>();
        private readonly object _instanceLock = new object();

        public BackupService(ILogger logger, IJobRepository repo, Settings settings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repo = repo ?? throw new ArgumentNullException(nameof(repo));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _jobs = new List<BackupJob>(_repo.Load());
            
            // Debug output to verify if jobs are loaded
            Console.WriteLine($"BackupService initialized with {_jobs.Count} jobs:");
            foreach (var job in _jobs)
            {
                Console.WriteLine($"  - Job: {job.Name}, Source: {job.SourceDir}, Target: {job.TargetDir}");
            }
        }

        public void AddJob(BackupJob job)
        {
            _jobs.Add(job);
            _repo.Save(_jobs);
            Console.WriteLine($"Added job: {job.Name}");
        }

        public void RemoveJob(string name)
        {
            // Cancel any running instance for this job
            StopJob(name);
            
            // Remove from jobs list
            _jobs.RemoveAll(j => j.Name == name);
            _repo.Save(_jobs);
            
            Console.WriteLine($"Removed job: {name}");
        }

        public IReadOnlyList<BackupJob> GetJobs() => _jobs.AsReadOnly();

        public async Task ExecuteBackupAsync(string name)
        {
            Console.WriteLine($"Starting backup for job: {name}");
            
            if (PackageBlocker.IsBlocked(_settings))
            {
                Console.WriteLine("Blocked package running — job skipped.");
                return;
            }

            var job = _jobs.FirstOrDefault(j => j.Name == name);
            if (job == null)
            {
                Console.WriteLine($"Job '{name}' not found.");
                return;
            }

            // Get or create backup instance
            var instance = GetOrCreateBackupInstance(job);
            
            // Start the backup
            await instance.StartAsync();
        }

        public async Task ExecuteAllBackupsAsync()
        {
            Console.WriteLine($"Starting all backups. Total jobs: {_jobs.Count}");
            
            // Check for blocked packages before starting
            if (PackageBlocker.IsBlocked(_settings))
            {
                Console.WriteLine("Blocked package running — all jobs skipped.");
                return;
            }
            
            // Create a list to hold tasks
            var tasks = new List<Task>();
            var jobsToProcess = new List<BackupJob>(_jobs);
            
            // Process all jobs concurrently with a small delay between starts
            foreach (var job in jobsToProcess)
            {
                // Get or create backup instance
                var instance = GetOrCreateBackupInstance(job);
                
                Console.WriteLine($"Queuing job: {job.Name} for parallel execution");
                
                // Add a small delay between starting each job to avoid resource contention
                if (tasks.Count > 0)
                {
                    await Task.Delay(1000);
                }
                
                // Start the backup
                tasks.Add(instance.StartAsync());
            }
            
            // Wait for all tasks to complete
            await Task.WhenAll(tasks);
            
            Console.WriteLine("All backup jobs have completed");
        }
        
        public void PauseJob(string name)
        {
            Console.WriteLine($"Pausing job: {name}");
            
            lock (_instanceLock)
            {
                if (_backupInstances.TryGetValue(name, out var instance))
                {
                    instance.Pause();
                }
                else
                {
                    Console.WriteLine($"Cannot pause job {name}: job not found");
                }
            }
        }
        
        public void ResumeJob(string name)
        {
            Console.WriteLine($"Resuming job: {name}");
            
            lock (_instanceLock)
            {
                if (_backupInstances.TryGetValue(name, out var instance))
                {
                    instance.Resume();
                }
                else
                {
                    Console.WriteLine($"Cannot resume job {name}: job not found");
                }
            }
        }
        
        public void StopJob(string name)
        {
            Console.WriteLine($"Stopping job: {name}");
            
            lock (_instanceLock)
            {
                if (_backupInstances.TryGetValue(name, out var instance))
                {
                    instance.Cancel();
                }
                else
                {
                    Console.WriteLine($"Cannot stop job {name}: job not found");
                }
            }
        }
        
        public void CancelAllBackups()
        {
            Console.WriteLine("Cancelling all backups");
            
            lock (_instanceLock)
            {
                foreach (var instance in _backupInstances.Values)
                {
                    instance.Cancel();
                }
            }
        }
        
        private BackupInstance GetOrCreateBackupInstance(BackupJob job)
        {
            lock (_instanceLock)
            {
                if (_backupInstances.TryGetValue(job.Name, out var existingInstance))
                {
                    // If the instance exists but is not running, we can reuse it
                    if (!existingInstance.IsRunning)
                    {
                        return existingInstance;
                    }
                    
                    // If the instance is running, return it
                    Console.WriteLine($"Job {job.Name} is already running");
                    return existingInstance;
                }
                
                // Ensure job has access to settings
                job.Settings = _settings;
                
                // Create a new instance
                var instance = new BackupInstance(job, _logger, _settings);
                
                // Subscribe to status updates
                instance.StatusUpdated += OnInstanceStatusUpdated;
                
                // Add to dictionary
                _backupInstances[job.Name] = instance;
                
                return instance;
            }
        }
        
        private void OnInstanceStatusUpdated(StatusEntry status)
        {
            try
            {
                // Forward status updates
                StatusUpdated?.Invoke(status);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error forwarding status update: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Cancel all running instances
            CancelAllBackups();
            
            // Clean up instances
            lock (_instanceLock)
            {
                foreach (var instance in _backupInstances.Values)
                {
                    // Unsubscribe from events
                    instance.StatusUpdated -= OnInstanceStatusUpdated;
                }
                
                _backupInstances.Clear();
            }
            
            GC.SuppressFinalize(this);
        }
    }
}