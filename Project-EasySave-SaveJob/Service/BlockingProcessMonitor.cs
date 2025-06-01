using System;
using System.Threading;
using System.Threading.Tasks;
using Projet.Infrastructure;

namespace Projet.Service
{
    /// <summary>
    /// Monitors for blocking processes and automatically pauses/resumes backup jobs
    /// </summary>
    public class BlockingProcessMonitor : IDisposable
    {
        private readonly IBackupService _backupService;
        private readonly Settings _settings;
        private Timer _monitorTimer;
        private bool _isMonitoring;
        private bool _jobsPaused;
        private readonly object _lock = new object();
        
        // Default check interval is 5 seconds
        private const int DEFAULT_CHECK_INTERVAL = 5000;
        private int _checkIntervalMs;
        
        /// <summary>
        /// Gets the current check interval in milliseconds
        /// </summary>
        public int CheckIntervalMs => _checkIntervalMs;
        
        /// <summary>
        /// Creates a new instance of the BlockingProcessMonitor
        /// </summary>
        /// <param name="backupService">The backup service to control</param>
        /// <param name="settings">Application settings containing blocked packages</param>
        /// <param name="checkIntervalMs">Optional interval between checks in milliseconds</param>
        public BlockingProcessMonitor(IBackupService backupService, Settings settings, int checkIntervalMs = DEFAULT_CHECK_INTERVAL)
        {
            _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _checkIntervalMs = checkIntervalMs;
            _isMonitoring = false;
            _jobsPaused = false;
        }
        
        /// <summary>
        /// Starts monitoring for blocking processes
        /// </summary>
        public void Start()
        {
            lock (_lock)
            {
                if (_isMonitoring)
                    return;
                
                _monitorTimer = new Timer(CheckBlockingProcesses, null, 0, _checkIntervalMs);
                _isMonitoring = true;
                
                Console.WriteLine("Blocking process monitor started");
            }
        }
        
        /// <summary>
        /// Stops monitoring for blocking processes
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                if (!_isMonitoring)
                    return;
                
                _monitorTimer?.Dispose();
                _monitorTimer = null;
                _isMonitoring = false;
                
                // Resume any paused jobs when stopping the monitor
                if (_jobsPaused)
                {
                    ResumeAllJobs();
                }
                
                Console.WriteLine("Blocking process monitor stopped");
            }
        }
        
        /// <summary>
        /// Changes the check interval
        /// </summary>
        /// <param name="intervalMs">New interval in milliseconds</param>
        public void SetCheckInterval(int intervalMs)
        {
            if (intervalMs <= 0)
                throw new ArgumentException("Interval must be greater than zero", nameof(intervalMs));
            
            lock (_lock)
            {
                _checkIntervalMs = intervalMs;
                
                // Reset timer with new interval if monitoring is active
                if (_isMonitoring)
                {
                    _monitorTimer?.Dispose();
                    _monitorTimer = new Timer(CheckBlockingProcesses, null, 0, _checkIntervalMs);
                }
            }
        }
        
        /// <summary>
        /// Checks for blocking processes and pauses/resumes jobs as needed
        /// </summary>
        private void CheckBlockingProcesses(object state)
        {
            lock (_lock)
            {
                bool isBlocked = PackageBlocker.IsBlocked(_settings);
                
                // If blocked and jobs are not already paused
                if (isBlocked && !_jobsPaused)
                {
                    PauseAllJobs();
                    _jobsPaused = true;
                    Console.WriteLine("Blocking process detected - all backup jobs paused");
                }
                // If not blocked and jobs are paused
                else if (!isBlocked && _jobsPaused)
                {
                    ResumeAllJobs();
                    _jobsPaused = false;
                    Console.WriteLine("No blocking processes detected - resuming backup jobs");
                }
            }
        }
        
        /// <summary>
        /// Pauses all active backup jobs
        /// </summary>
        private void PauseAllJobs()
        {
            // Get current jobs to pause
            var jobs = _backupService.GetJobs();
            foreach (var job in jobs)
            {
                _backupService.PauseJob(job.Name);
            }
        }
        
        /// <summary>
        /// Resumes all paused backup jobs
        /// </summary>
        private void ResumeAllJobs()
        {
            // Get current jobs to resume
            var jobs = _backupService.GetJobs();
            foreach (var job in jobs)
            {
                _backupService.ResumeJob(job.Name);
            }
        }
        
        /// <summary>
        /// Disposes of resources
        /// </summary>
        public void Dispose()
        {
            _monitorTimer?.Dispose();
            _monitorTimer = null;
            _isMonitoring = false;
        }
    }
} 