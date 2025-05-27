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
                _currentViewModel = value;
                OnPropertyChanged();
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
            LoadRecentJobs();
            
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
                _ => !IsBackupRunning && _selected != null
            );
            
            RunAllCmd = new Infrastructure.RelayCommand(
                async _ => await RunAllJobsAsync(),
                _ => !IsBackupRunning && Jobs.Count > 0
            );
            
            CancelBackupCmd = new Infrastructure.RelayCommand(
                _ => CancelBackup(),
                _ => IsBackupRunning
            );
            
            ShowAddJobViewCommand = new Infrastructure.RelayCommand(_ => ShowAddJobView());
            ShowChooseJobViewCommand = new Infrastructure.RelayCommand(_ => ShowChooseJobView());
            ShowRemoveJobViewCommand = new Infrastructure.RelayCommand(_ => RemoveJobRequested?.Invoke());

            RunJobCmd = new Infrastructure.RelayCommand(async param =>
            {
                if (param is BackupJob job && !string.IsNullOrWhiteSpace(job.Name) && !IsBackupRunning)
                {
                    await RunJobAsync(job);
                }
            });

            SetFrenchCommand = new Infrastructure.RelayCommand(_ =>
                _threadPool.EnqueueGuiTask(async (ct) => {
                    _lang.Load(Path.Combine(_langDir, "fr.json"));
                }));
                
            SetEnglishCommand = new Infrastructure.RelayCommand(_ =>
                _threadPool.EnqueueGuiTask(async (ct) => {
                    _lang.Load(Path.Combine(_langDir, "en.json"));
                }));

            OpenSettingsCommand = new Infrastructure.RelayCommand(_ => ShowSettingsView());
            ReturnToMainViewCommand = new Infrastructure.RelayCommand(_ => CurrentViewModel = this);

            _svc.StatusUpdated += OnStatusUpdated;

            CurrentViewModel = this;
        }

        private void OnStatusUpdated(StatusEntry status)
        {
            // Handle this on the GUI thread
            _threadPool.EnqueueGuiTask(async (ct) => {
                RefreshJobs();
                LoadRecentJobs();
                
                // Check for end of backup
                if (status.State == "END")
                {
                    // If it's the last job, set IsBackupRunning to false
                    bool anyActive = Jobs.Any(j => j.State == "ACTIVE" || j.State == "PENDING");
                    if (!anyActive)
                    {
                        IsBackupRunning = false;
                    }
                }
            });
        }

        private async Task RunSelectedJobAsync()
        {
            if (_selected != null && !string.IsNullOrWhiteSpace(_selected.Name))
            {
                IsBackupRunning = true;
                try
                {
                    // Update UI state immediately
                    _selected.State = "PENDING";
                    _selected.Progression = 0.01;
                    
                    // Execute backup
                    await _svc.ExecuteBackupAsync(_selected.Name);
                }
                finally
                {
                    UpdateJobsStatus();
                    // Will be set to false by the StatusUpdated event handler when complete
                }
            }
        }

        private async Task RunAllJobsAsync()
        {
            if (Jobs.Count > 0)
            {
                IsBackupRunning = true;
                try
                {
                    // Mark all jobs as pending for UI feedback
                    foreach (var job in Jobs)
                    {
                        job.State = "PENDING";
                        job.Progression = 0.01;
                    }
                    
                    // Execute all backups
                    await _svc.ExecuteAllBackupsAsync();
                }
                finally
                {
                    UpdateJobsStatus();
                    // Will be set to false by the StatusUpdated event handler when complete
                }
            }
        }
        
        private async Task RunJobAsync(BackupJob job)
        {
            IsBackupRunning = true;
            try
            {
                // Update UI state immediately
                job.State = "PENDING";
                job.Progression = 0.01;
                
                // Execute backup
                await _svc.ExecuteBackupAsync(job.Name);
            }
            finally
            {
                UpdateJobsStatus();
                // Will be set to false by the StatusUpdated event handler when complete
            }
        }

        private void CancelBackup()
        {
            _svc.CancelAllBackups();
            
            // Update UI
            foreach (var job in Jobs)
            {
                if (job.State == "ACTIVE" || job.State == "PENDING")
                {
                    job.State = "CANCELLED";
                }
            }
            
            IsBackupRunning = false;
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
            // Use the ThreadPoolManager for reading status files
            _threadPool.EnqueueLoggingTask(async (ct) => {
                try
                {
                    string statusPath = Path.Combine(_paths.GetStatusDir(), "status.json");
                    if (!File.Exists(statusPath))
                        return;

                    string json = File.ReadAllText(statusPath);
                    if (string.IsNullOrWhiteSpace(json))
                        return;

                    var statusEntries = JsonSerializer.Deserialize<List<StatusEntry>>(json);
                    if (statusEntries == null || statusEntries.Count == 0)
                        return;

                    var addedJobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var recentJobs = new List<BackupJob>();

                    foreach (var entry in statusEntries)
                    {
                        if (!addedJobs.Contains(entry.Name))
                        {
                            var job = Jobs.FirstOrDefault(j => string.Equals(j.Name?.Trim(), entry.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                            if (job != null)
                            {
                                recentJobs.Add(job);
                                addedJobs.Add(entry.Name);
                                if (recentJobs.Count == 3)
                                    break;
                            }
                        }
                    }

                    recentJobs.Reverse();
                    
                    // Update the ObservableCollection on the GUI thread
                    _threadPool.EnqueueGuiTask(async (guiCt) => {
                        RecentJobs.Clear();
                        foreach (var job in recentJobs)
                        {
                            RecentJobs.Add(job);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading recent jobs: {ex.Message}");
                    
                    // Clear the list on error
                    _threadPool.EnqueueGuiTask(async (guiCt) => {
                        RecentJobs.Clear();
                    });
                }
            });
        }

        private void ShowAddJobView()
        {
            var vm = new AddJobViewModel(_svc);
            vm.JobAdded += () =>
            {
                RefreshJobs();
                LoadRecentJobs();
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
                    UpdateJobStatusInternal();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erreur lors de la mise à jour du statut: {ex.Message}");
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