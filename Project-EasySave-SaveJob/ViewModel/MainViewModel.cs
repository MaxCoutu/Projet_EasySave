using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using System.Timers;
using System.Windows;
using System.Threading.Tasks;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;

namespace Projet.ViewModel
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly IBackupService _svc;
        private readonly ILanguageService _lang;
        private readonly string _langDir;
        private readonly IPathProvider _paths;
        private readonly JobStatusViewModel _statusVM;
        private readonly Timer _refreshTimer;
        private readonly Settings _settings;
        private readonly ThreadPoolManager _threadPool;

        private ViewModelBase _currentViewModel;
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                var previousViewModel = _currentViewModel;
                _currentViewModel = value;
                OnPropertyChanged();
                
                // If we're returning to the main view from a different view, refresh jobs
                if (_currentViewModel == this && previousViewModel != this)
                {
                    Console.WriteLine("Returned to main view - refreshing jobs");
                    RefreshJobs();
                    LoadRecentJobs();
                }
            }
        }

        public ObservableCollection<BackupJob> Jobs { get; }
        private ObservableCollection<BackupJob> _recentJobs;
        public ObservableCollection<BackupJob> RecentJobs
        {
            get => _recentJobs;
            set { _recentJobs = value; OnPropertyChanged(); }
        }

        private BackupJob _selected;
        public BackupJob SelectedJob
        {
            get => _selected;
            set { _selected = value; OnPropertyChanged(); }
        }

        private bool _isBackupRunning;
        public bool IsBackupRunning
        {
            get => _isBackupRunning;
            set
            {
                if (_isBackupRunning != value)
                {
                    _isBackupRunning = value;
                    OnPropertyChanged();
                    // Update command availability
                    ((Infrastructure.RelayCommand)RunSelectedCmd).RaiseCanExecuteChanged();
                    ((Infrastructure.RelayCommand)RunAllCmd).RaiseCanExecuteChanged();
                    ((Infrastructure.RelayCommand)CancelBackupCmd).RaiseCanExecuteChanged();
                }
            }
        }

        public ICommand AddJobCmd { get; }
        public ICommand RemoveJobCmd { get; }
        public ICommand RunSelectedCmd { get; }
        public ICommand RunAllCmd { get; }
        public ICommand CancelBackupCmd { get; }
        public ICommand ShowAddJobViewCommand { get; }
        public ICommand ShowChooseJobViewCommand { get; }
        public ICommand ShowRemoveJobViewCommand { get; }
        public ICommand RunJobCmd { get; }
        public ICommand SetFrenchCommand { get; }
        public ICommand SetEnglishCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand ReturnToMainViewCommand { get; }
        public ICommand RefreshJobsCommand { get; }
        public ICommand PauseResumeJobCommand { get; }
        public ICommand StopJobCommand { get; }
        public ICommand LaunchDecryptorCommand { get; }

        public event Action AddJobRequested;
        public event Action RemoveJobRequested;

        public MainViewModel(IBackupService svc, ILanguageService lang, IPathProvider paths)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            _lang = lang ?? throw new ArgumentNullException(nameof(lang));
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _langDir = Path.Combine(AppContext.BaseDirectory, "Languages");
            _settings = Settings.Load(paths);
            _threadPool = ThreadPoolManager.Instance;

            Jobs = new ObservableCollection<BackupJob>(_svc.GetJobs());
            _recentJobs = new ObservableCollection<BackupJob>();
            
            // Immediately load all jobs into RecentJobs
            foreach (var job in Jobs)
            {
                _recentJobs.Add(job);
            }
            
            // Créer et configurer le JobStatusViewModel
            _statusVM = new JobStatusViewModel(paths);
            
            // Timer pour mettre à jour régulièrement l'état des tâches
            _refreshTimer = new Timer(2000); // 2 secondes
            _refreshTimer.Elapsed += (s, e) => UpdateJobsStatus();
            _refreshTimer.Start();

            // Use the Infrastructure.RelayCommand explicitly to avoid using the ViewModel version
            AddJobCmd = new Infrastructure.RelayCommand(_ => AddJobRequested?.Invoke());
            RemoveJobCmd = new Infrastructure.RelayCommand(_ => RemoveJobRequested?.Invoke());
            
            RunSelectedCmd = new Infrastructure.RelayCommand(
                async _ => await RunSelectedJobAsync(),
                _ => _selected != null
            );
            
            RunAllCmd = new Infrastructure.RelayCommand(async _ =>
            {
                if (Jobs.Count > 0)
                {
                    // Exécuter directement le RunAllJobsAsync sans passer par Task.Run
                    // pour gérer correctement les exceptions
                    try
                    {
                        await RunAllJobsAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in RunAllCmd: {ex.Message}");
                    }
                }
            }, _ => Jobs.Count > 0 && !IsBackupRunning);
            
            CancelBackupCmd = new Infrastructure.RelayCommand(
                _ => CancelBackup(),
                _ => IsBackupRunning
            );
            
            ShowAddJobViewCommand = new Infrastructure.RelayCommand(_ => ShowAddJobView());
            ShowChooseJobViewCommand = new Infrastructure.RelayCommand(_ => ShowChooseJobView());
            ShowRemoveJobViewCommand = new Infrastructure.RelayCommand(_ => RemoveJobRequested?.Invoke());

            RunJobCmd = new Infrastructure.RelayCommand(async param =>
            {
                if (param is BackupJob job && !string.IsNullOrWhiteSpace(job.Name))
                {
                    // Get the job name for reference
                    var jobName = job.Name;
                    Console.WriteLine($"[SIMPLE] Starting job: {jobName}");
                    
                    // Immediately set job state to ACTIVE (not PENDING) so controls are available
                    job.State = "ACTIVE";
                    job.Progression = 0.01;
                    
                    // Notify UI of the change
                    OnPropertyChanged(nameof(Jobs));
                    OnPropertyChanged(nameof(RecentJobs));
                    
                    // Démarrer le job dans un thread séparé
                    Task.Run(async () => 
                    {
                        try
                        {
                            // Start the job
                            await _svc.ExecuteBackupAsync(jobName);
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("blocked application"))
                        {
                            // Une application bloquante est en cours d'exécution
                            Console.WriteLine($"Blocked application error: {ex.Message}");
                            
                            // Update job status to ERROR with message
                            job.State = "ERROR";
                            job.LastError = "Une application bloquante est en cours d'exécution. Veuillez fermer toutes les applications bloquantes et réessayer.";
                            job.Progression = 0;
                            
                            // Notify UI of the change
                            OnPropertyChanged(nameof(Jobs));
                            OnPropertyChanged(nameof(RecentJobs));
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error executing job: {ex.Message}");
                            
                            // Update job status to ERROR
                            job.State = "ERROR";
                            job.LastError = ex.Message;
                            job.Progression = 0;
                            
                            // Notify UI of the change
                            OnPropertyChanged(nameof(Jobs));
                            OnPropertyChanged(nameof(RecentJobs));
                        }
                        finally
                        {
                            // Update job status after completion
                            UpdateJobsStatus();
                        }
                    });
                }
            });

            SetFrenchCommand = new Infrastructure.RelayCommand(_ =>
            {
                try 
                {
                    Console.WriteLine("Changing language to French");
                    _lang.Load(Path.Combine(_langDir, "fr.json"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting French language: {ex.Message}");
                }
            });
                
            SetEnglishCommand = new Infrastructure.RelayCommand(_ =>
            {
                try
                {
                    Console.WriteLine("Changing language to English");
                    _lang.Load(Path.Combine(_langDir, "en.json"));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error setting English language: {ex.Message}");
                }
            });

            OpenSettingsCommand = new Infrastructure.RelayCommand(_ => ShowSettingsView());
            ReturnToMainViewCommand = new Infrastructure.RelayCommand(_ => CurrentViewModel = this);
            RefreshJobsCommand = new Infrastructure.RelayCommand(_ => {
                Console.WriteLine("Manual refresh requested - executing immediately");
                // Use direct call instead of queuing
                try {
                    // Get all jobs directly
                    var allJobs = _svc.GetJobs();
                    
                    // Cache current job states
                    var jobStates = new Dictionary<string, string>();
                    foreach (var job in RecentJobs)
                    {
                        if (job.State == "ACTIVE" || job.State == "PAUSED" || job.State == "PENDING")
                        {
                            jobStates[job.Name] = job.State;
                        }
                    }
                    
                    // Clear collections
                    RecentJobs.Clear();
                    
                    // Re-add all jobs with preserved states
                    foreach (var job in allJobs)
                    {
                        // Preserve active states
                        if (jobStates.ContainsKey(job.Name))
                        {
                            job.State = jobStates[job.Name];
                        }
                        
                        RecentJobs.Add(job);
                    }
                    
                    // Update status
                    UpdateJobStatusInternal();
                    
                    // Notify
                    OnPropertyChanged(nameof(RecentJobs));
                    
                    // Reset UI immediately
                    ResetUIState();
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error in manual refresh: {ex.Message}");
                }
            });

            // Add pause/resume command - SIMPLIFIED
            PauseResumeJobCommand = new Infrastructure.RelayCommand(param =>
            {
                if (param is BackupJob job)
                {
                    // Get job name for specific targeting
                    var jobName = job.Name;
                    Console.WriteLine($"[SIMPLE] Pause/Resume triggered for job: {jobName}");
                    
                    // Check if job is paused to determine whether to pause or resume
                    if (job.State == "PAUSED")
                    {
                        // Resume this specific job
                        Console.WriteLine($"[SIMPLE] Resuming job: {jobName}");
                        job.State = "ACTIVE";
                        _svc.ResumeJob(jobName);
                    }
                    else
                    {
                        // Pause this specific job
                        Console.WriteLine($"[SIMPLE] Pausing job: {jobName}");
                        job.State = "PAUSED";
                        _svc.PauseJob(jobName);
                    }
                    
                    // Simply notify UI of the change
                    OnPropertyChanged(nameof(Jobs));
                    OnPropertyChanged(nameof(RecentJobs));
                }
            }, param => param is BackupJob job && (job.State == "ACTIVE" || job.State == "PAUSED" || job.State == "PENDING"));

            // Add stop command - SIMPLIFIED
            StopJobCommand = new Infrastructure.RelayCommand(param =>
            {
                if (param is BackupJob job)
                {
                    // Get job name for specific targeting
                    var jobName = job.Name;
                    Console.WriteLine($"[SIMPLE] Stop triggered for job: {jobName}");
                    
                    // First set job state to show it's being stopped
                    job.State = "CANCELLING";
                    
                    // Stop this specific job
                    _svc.StopJob(jobName);
                    
                    // Reset the IsBackupRunning flag if no other jobs are running
                    bool anyActive = Jobs.Any(j => j.State == "ACTIVE" || j.State == "PENDING" || j.State == "PAUSED");
                    if (!anyActive)
                    {
                        IsBackupRunning = false;
                    }
                    
                    // Notify UI of the change
                    OnPropertyChanged(nameof(Jobs));
                    OnPropertyChanged(nameof(RecentJobs));
                    
                    // Force additional UI updates using the ForceRefreshJobs method
                    ForceRefreshJobs();
                    
                    // Use a timer to ensure the job state is properly reset after a short delay
                    var timer = new System.Threading.Timer(_ => 
                    {
                        try
                        {
                            // Execute on the UI thread
                            _threadPool.EnqueueGuiTaskPriority(async ct =>
                            {
                                // Find the job in both collections and reset it properly
                                var jobInJobs = Jobs.FirstOrDefault(j => j.Name == jobName);
                                var jobInRecent = RecentJobs.FirstOrDefault(j => j.Name == jobName);
                                
                                // Reset the job in the Jobs collection
                                if (jobInJobs != null)
                                {
                                    // IMPORTANT: Set job state to READY to ensure the play button becomes visible
                                    jobInJobs.State = "READY";
                                    jobInJobs.Progression = 0;
                                    jobInJobs.NbFilesLeftToDo = 0;
                                    jobInJobs.RefreshCounter++; // Force UI refresh
                                }
                                
                                // Reset the job in the RecentJobs collection
                                if (jobInRecent != null)
                                {
                                    // IMPORTANT: Set job state to READY to ensure the play button becomes visible
                                    jobInRecent.State = "READY";
                                    jobInRecent.Progression = 0;
                                    jobInRecent.NbFilesLeftToDo = 0;
                                    jobInRecent.RefreshCounter++; // Force UI refresh
                                }
                                
                                // Notify UI of changes to both collections
                                OnPropertyChanged(nameof(Jobs));
                                OnPropertyChanged(nameof(RecentJobs));
                                
                                // Force a complete UI refresh
                                ForceRefreshJobs();
                                
                                // Delay a bit and do another refresh to ensure UI is updated
                                await Task.Delay(100);
                                ForceRefreshJobs();
                            });
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error in stop job timer: {ex.Message}");
                        }
                    }, null, 300, System.Threading.Timeout.Infinite); // Execute once after 300ms
                }
            }, param => param is BackupJob job && (job.State == "ACTIVE" || job.State == "PAUSED" || job.State == "PENDING"));

            LaunchDecryptorCommand = new Infrastructure.RelayCommand(_ =>
            {
                try
                {
                    // Afficher la vue de déchiffrement intégrée
                    ShowDecryptorView();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lors de l'affichage du déchiffreur : {ex.Message}");
                    Console.WriteLine($"Stack trace : {ex.StackTrace}");
                }
            });

            // Méthode pour afficher la vue de déchiffrement
            void ShowDecryptorView()
            {
                // Créer et afficher le ViewModel de déchiffrement
                CurrentViewModel = new DecryptorViewModel(this);
            }

            _svc.StatusUpdated += OnStatusUpdated;

            CurrentViewModel = this;
        }

        // Method to completely reset the UI state
        private void ResetUIState()
        {
            // Use the ThreadPool to execute on the UI thread
            _threadPool.EnqueueGuiTaskPriority(async (ct) => {
                try
                {
                    // Force all UI updates to happen immediately
                    Console.WriteLine("Resetting UI state");
                    
                    // First clear the collections to force a complete UI reset
                    var tempJobs = new List<BackupJob>(Jobs);
                    var tempRecentJobs = new List<BackupJob>(RecentJobs);
                    
                    // Clear and immediately rebuild collections
                    Jobs.Clear();
                    RecentJobs.Clear();
                    
                    // Notify that collections have changed
                    OnPropertyChanged(nameof(Jobs));
                    OnPropertyChanged(nameof(RecentJobs));
                    
                    // Ensure UI thread updates
                    await Task.Delay(10);
                    
                    // Repopulate collections
                    foreach (var job in tempJobs)
                    {
                        Jobs.Add(job);
                    }
                    
                    foreach (var job in tempRecentJobs)
                    {
                        RecentJobs.Add(job);
                    }
                    
                    // Final notifications
                    OnPropertyChanged(nameof(Jobs));
                    OnPropertyChanged(nameof(RecentJobs));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error resetting UI state: {ex.Message}");
                }
            });
        }

        private void OnStatusUpdated(StatusEntry status)
        {
            // Handle this on the GUI thread with high priority
            _threadPool.EnqueueGuiTask(async (ct) => {
                try {
                    // Skip invalid status updates
                    if (status == null || string.IsNullOrEmpty(status.Name))
                    {
                        return;
                    }
                    
                    Console.WriteLine($"Status update received: Job={status.Name}, State={status.State}, Progress={status.Progression:F2}%");
                    
                    // Find the job in both collections
                    var jobInJobs = Jobs.FirstOrDefault(j => j.Name == status.Name);
                    var jobInRecent = RecentJobs.FirstOrDefault(j => j.Name == status.Name);
                    
                    // Update the job directly without refreshing the entire collection
                    if (jobInJobs != null)
                    {
                        // Update state only if it's a valid transition
                        if (!string.IsNullOrEmpty(status.State))
                        {
                            jobInJobs.State = status.State;
                        }
                        
                        // Ensure progression is valid (0-100)
                        jobInJobs.Progression = Math.Min(100, Math.Max(0, status.Progression));
                        
                        // Update other properties
                        jobInJobs.TotalFilesToCopy = status.TotalFilesToCopy;
                        jobInJobs.TotalFilesSize = status.TotalFilesSize;
                        jobInJobs.NbFilesLeftToDo = status.NbFilesLeftToDo;
                    }
                    
                    // Also update in RecentJobs
                    if (jobInRecent != null)
                    {
                        // Update state only if it's a valid transition
                        if (!string.IsNullOrEmpty(status.State))
                        {
                            jobInRecent.State = status.State;
                        }
                        
                        // Ensure progression is valid (0-100)
                        jobInRecent.Progression = Math.Min(100, Math.Max(0, status.Progression));
                        
                        // Update other properties
                        jobInRecent.TotalFilesToCopy = status.TotalFilesToCopy;
                        jobInRecent.TotalFilesSize = status.TotalFilesSize;
                        jobInRecent.NbFilesLeftToDo = status.NbFilesLeftToDo;
                    }
                    
                    // Notify UI of changes to both collections
                    OnPropertyChanged(nameof(Jobs));
                    OnPropertyChanged(nameof(RecentJobs));
                    
                    // Handle job completion
                    if (status.State == "END" || status.State == "CANCELLED" || status.State == "ERROR")
                    {
                        Console.WriteLine($"Job {status.Name} completed with state {status.State}");
                        
                        // Check if any jobs are still running
                        bool anyActive = Jobs.Any(j => j.State == "ACTIVE" || j.State == "PENDING" || j.State == "PAUSED");
                        
                        // Update IsBackupRunning flag if no jobs are active
                        if (!anyActive && IsBackupRunning)
                        {
                            Console.WriteLine("No active jobs remaining - setting IsBackupRunning to false");
                            IsBackupRunning = false;
                        }
                    }
                    else if (status.State == "ACTIVE" && !IsBackupRunning)
                    {
                        // If any job becomes active, set IsBackupRunning to true
                        Console.WriteLine("Job active - setting IsBackupRunning to true");
                        IsBackupRunning = true;
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error updating status: {ex.Message}");
                }
            });
        }

        private async Task RunSelectedJobAsync()
        {
            if (_selected != null && !string.IsNullOrWhiteSpace(_selected.Name))
            {
                // Get the job name for reference
                var jobName = _selected.Name;
                Console.WriteLine($"[SIMPLE] RunSelectedJobAsync for job: {jobName}");
                
                // Set job state directly to ACTIVE
                _selected.State = "ACTIVE";
                _selected.Progression = 0.01;
                
                // Notify UI of changes
                OnPropertyChanged(nameof(Jobs));
                OnPropertyChanged(nameof(RecentJobs));
                
                // Démarrer le job dans un thread séparé
                Task.Run(async () => 
                {
                    try
                    {
                        // Execute the job
                        await _svc.ExecuteBackupAsync(jobName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in RunSelectedJobAsync: {ex.Message}");
                    }
                    finally
                    {
                        // Update job status
                        UpdateJobsStatus();
                    }
                });
            }
        }

        private async Task RunAllJobsAsync()
        {
            Console.WriteLine("RunAllJobsAsync: Starting");
            try
            {
                // Mark all jobs as pending
                foreach (var job in Jobs)
                {
                    job.State = "PENDING";
                    job.Progression = 0;
                    job.LastError = string.Empty;
                }
                
                // Update UI
                OnPropertyChanged(nameof(Jobs));
                OnPropertyChanged(nameof(RecentJobs));
                
                // Set flag
                IsBackupRunning = true;
                
                // Execute all jobs
                await _svc.ExecuteAllBackupsAsync();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("blocked application"))
            {
                Console.WriteLine($"Blocked application error: {ex.Message}");
                
                // Update all pending jobs to ERROR with message
                foreach (var job in Jobs)
                {
                    if (job.State == "PENDING" || job.State == "ACTIVE")
                    {
                        job.State = "ERROR";
                        job.LastError = "Une application bloquante est en cours d'exécution. Veuillez fermer toutes les applications bloquantes et réessayer.";
                        job.Progression = 0;
                    }
                }
                
                // Update UI
                OnPropertyChanged(nameof(Jobs));
                OnPropertyChanged(nameof(RecentJobs));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"RunAllJobsAsync error: {ex.Message}");
                
                // Mark all pending jobs as errored
                foreach (var job in Jobs)
                {
                    if (job.State == "PENDING" || job.State == "ACTIVE")
                    {
                        job.State = "ERROR";
                        job.LastError = ex.Message;
                        job.Progression = 0;
                    }
                }
                
                // Update UI
                OnPropertyChanged(nameof(Jobs));
                OnPropertyChanged(nameof(RecentJobs));
            }
            finally
            {
                // Reset flag if no jobs are running
                if (!Jobs.Any(j => j.State == "ACTIVE" || j.State == "PENDING" || j.State == "PAUSED"))
                {
                    IsBackupRunning = false;
                }
                
                // Update job status in UI
                UpdateJobsStatus();
                
                Console.WriteLine("RunAllJobsAsync: Completed");
            }
        }

        private void CancelBackup()
        {
            _svc.CancelAllBackups();
            
            // Update UI
            foreach (var job in Jobs)
            {
                if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                {
                    // Use END state instead of CANCELLED to ensure play button visibility
                    job.State = "END";
                }
            }
            
            // Update RecentJobs as well
            foreach (var job in RecentJobs)
            {
                if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                {
                    // Use END state instead of CANCELLED to ensure play button visibility
                    job.State = "END";
                }
            }
            
            // Notify UI of the changes
            OnPropertyChanged(nameof(Jobs));
            OnPropertyChanged(nameof(RecentJobs));
            
            IsBackupRunning = false;
            
            // Force an immediate refresh to update button visibility
            ForceRefreshJobs();
        }

        public void RefreshJobs()
        {
            // This should be called from the UI thread
            try
            {
                var currentJobs = _svc.GetJobs().ToList();
                
                // Remove jobs that no longer exist
                for (int i = Jobs.Count - 1; i >= 0; i--)
                {
                    if (!currentJobs.Any(j => j.Name == Jobs[i].Name))
                    {
                        Jobs.RemoveAt(i);
                    }
                }
                
                // Update or add jobs
                foreach (var job in currentJobs)
                {
                    var existingJob = Jobs.FirstOrDefault(j => j.Name == job.Name);
                    if (existingJob == null)
                    {
                        Jobs.Add(job);
                    }
                    else
                    {
                        // Update properties if needed
                        existingJob.SourceDir = job.SourceDir;
                        existingJob.TargetDir = job.TargetDir;
                        existingJob.Strategy = job.Strategy;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing jobs: {ex.Message}");
            }
        }

        private void LoadRecentJobs()
        {
            // Run on the GUI thread for UI updates
            _threadPool.EnqueueGuiTask(async (ct) => {
                try
                {
                    // Clear existing recent jobs
                    RecentJobs.Clear();
                    
                    // Get all jobs from the service
                    var allJobs = _svc.GetJobs().ToList();
                    Console.WriteLine($"LoadRecentJobs found {allJobs.Count} jobs");
                    
                    // Add all jobs to the RecentJobs collection
                    foreach (var job in allJobs)
                    {
                        RecentJobs.Add(job);
                        Console.WriteLine($"LoadRecentJobs added: {job.Name}");
                    }
                    
                    // Update job status
                    UpdateJobStatusInternal();
                    
                    // Ensure UI is notified of changes
                    OnPropertyChanged(nameof(RecentJobs));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading recent jobs: {ex.Message}");
                }
            });
        }

        private void ShowAddJobView()
        {
            var vm = new AddJobViewModel(_svc);
            vm.JobAdded += () =>
            {
                // Force a full refresh of jobs
                RefreshJobs();
                
                // Reload all jobs in the UI
                LoadRecentJobs();
                
                // Return to main view
                CurrentViewModel = this;
            };
            CurrentViewModel = vm;
        }

        private void ShowChooseJobView()
        {
            CurrentViewModel = new ChooseJobViewModel(_svc);
        }

        private void UpdateJobsStatus()
        {
            // Use the GUI thread allocation for UI updates
            _threadPool.EnqueueGuiTask(async (ct) => {
                try
                {
                    // Debug: print jobs count
                    Console.WriteLine($"Periodic job status update: Jobs.Count={Jobs.Count}, RecentJobs.Count={RecentJobs.Count}");
                    
                    // Check if any jobs need to be added/removed from collections
                    var allJobs = _svc.GetJobs().ToList();
                    bool collectionChanged = false;
                    
                    // Add missing jobs to Jobs collection
                    foreach (var job in allJobs)
                    {
                        if (!Jobs.Any(j => j.Name == job.Name))
                        {
                            Jobs.Add(job);
                            collectionChanged = true;
                        }
                        
                        if (!RecentJobs.Any(j => j.Name == job.Name))
                        {
                            RecentJobs.Add(job);
                            collectionChanged = true;
                        }
                    }
                    
                    // Remove jobs that no longer exist
                    for (int i = Jobs.Count - 1; i >= 0; i--)
                    {
                        if (!allJobs.Any(j => j.Name == Jobs[i].Name))
                        {
                            Jobs.RemoveAt(i);
                            collectionChanged = true;
                        }
                    }
                    
                    for (int i = RecentJobs.Count - 1; i >= 0; i--)
                    {
                        if (!allJobs.Any(j => j.Name == RecentJobs[i].Name))
                        {
                            RecentJobs.RemoveAt(i);
                            collectionChanged = true;
                        }
                    }
                    
                    // If collections changed, notify UI
                    if (collectionChanged)
                    {
                        OnPropertyChanged(nameof(Jobs));
                        OnPropertyChanged(nameof(RecentJobs));
                    }
                    
                    // Update status for all jobs
                    UpdateJobStatusInternal();
                    
                    // Check if we need to update IsBackupRunning flag
                    bool anyActive = Jobs.Any(j => j.State == "ACTIVE" || j.State == "PENDING" || j.State == "PAUSED");
                    if (!anyActive && IsBackupRunning)
                    {
                        Console.WriteLine("Periodic check: No active jobs - resetting IsBackupRunning flag");
                        IsBackupRunning = false;
                    }
                    else if (anyActive && !IsBackupRunning)
                    {
                        Console.WriteLine("Periodic check: Active jobs found - setting IsBackupRunning flag");
                        IsBackupRunning = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating job status: {ex.Message}");
                }
            });
        }

        private void UpdateJobStatusInternal()
        {
            foreach (var job in Jobs)
            {
                _statusVM.ApplyStatus(job);
            }
            
            foreach (var job in RecentJobs)
            {
                _statusVM.ApplyStatus(job);
            }
        }

        private void ShowSettingsView()
        {
            CurrentViewModel = new SettingsViewModel(_settings);
        }

        // Public method that can be called from external code to force jobs to display
        public void ForceRefreshJobs()
        {
            Console.WriteLine("[SIMPLE] Force refresh called");
            
            try {
                // Get all jobs directly
                var allJobs = _svc.GetJobs();
                
                // Create dictionary of current jobs with their states
                var currentJobStates = new Dictionary<string, string>();
                foreach (var job in RecentJobs)
                {
                    // Store all states we want to preserve
                    // IMPORTANT: We specifically want to preserve END state to keep play button visible
                    if (job.State == "ACTIVE" || job.State == "PAUSED" || job.State == "PENDING" || 
                        job.State == "END" || job.State == "CANCELLED")
                    {
                        currentJobStates[job.Name] = job.State;
                        Console.WriteLine($"Preserving state '{job.State}' for job '{job.Name}'");
                    }
                }
                
                // Clear and rebuild the RecentJobs collection
                RecentJobs.Clear();
                
                // Add all jobs directly, preserving important states
                foreach (var job in allJobs)
                {
                    // If job was already in the collection, preserve its state
                    if (currentJobStates.ContainsKey(job.Name))
                    {
                        // Preserve job state including END state
                        job.State = currentJobStates[job.Name];
                        Console.WriteLine($"Setting preserved state '{job.State}' for job '{job.Name}'");
                    }
                    else
                    {
                        // For jobs that don't have a preserved state, make sure they're in READY state
                        // so the play button will be visible
                        if (string.IsNullOrEmpty(job.State) || job.State == "CANCELLED")
                        {
                            job.State = "READY";
                            Console.WriteLine($"Setting default READY state for job '{job.Name}'");
                        }
                    }
                    
                    // Add the job to the collection
                    RecentJobs.Add(job);
                }
                
                // Update job status immediately
                UpdateJobStatusInternal();
                
                // Notify UI
                OnPropertyChanged(nameof(RecentJobs));
                
                // Check if we need to update IsBackupRunning flag
                bool anyActive = RecentJobs.Any(j => j.State == "ACTIVE" || j.State == "PENDING" || j.State == "PAUSED");
                if (!anyActive && IsBackupRunning)
                {
                    Console.WriteLine("No active jobs - resetting IsBackupRunning flag");
                    IsBackupRunning = false;
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error in ForceRefreshJobs: {ex.Message}");
            }
        }

        // Helper method to clear UI focus - simplified to avoid WPF dependencies
        private void ClearFocus()
        {
            // Use the ThreadPool to execute on the UI thread
            _threadPool.EnqueueGuiTask(async (ct) => {
                try
                {
                    // Just log the intent for now
                    Console.WriteLine("Clearing focus");
                    
                    // Note: We can't directly clear focus here because this is in the core library
                    // The WPF app will handle this via the FocusBehavior attached property
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error clearing focus: {ex.Message}");
                }
            });
        }

        // Helper method to refresh jobs while preserving specified states
        private void RefreshJobsWithPreservedStates(Dictionary<string, string> jobStates)
        {
            try
            {
                // Get all jobs directly
                var allJobs = _svc.GetJobs();
                
                // Clear collections
                RecentJobs.Clear();
                
                // Re-add all jobs with preserved states
                foreach (var refreshedJob in allJobs)
                {
                    // Preserve active states
                    if (jobStates.ContainsKey(refreshedJob.Name))
                    {
                        refreshedJob.State = jobStates[refreshedJob.Name];
                    }
                    
                    RecentJobs.Add(refreshedJob);
                }
                
                // Update status
                UpdateJobStatusInternal();
                
                // Notify UI
                OnPropertyChanged(nameof(RecentJobs));
                
                // Reset UI state
                ResetUIState();
                
                // Clear focus
                ClearFocus();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in RefreshJobsWithPreservedStates: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _statusVM?.Dispose();
            
            // Unregister from events
            _svc.StatusUpdated -= OnStatusUpdated;
        }
    }
}