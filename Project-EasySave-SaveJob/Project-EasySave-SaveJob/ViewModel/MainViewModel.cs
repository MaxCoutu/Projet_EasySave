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
                
                if (_currentViewModel == this)
                {
                    RefreshJobs();
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
                    UpdateCommandsCanExecute();
                }
            }
        }

        // Commandes
        private ICommand _addJobCmd;
        public ICommand AddJobCmd { get => _addJobCmd; private set => _addJobCmd = value; }

        private ICommand _removeJobCmd;
        public ICommand RemoveJobCmd { get => _removeJobCmd; private set => _removeJobCmd = value; }

        private ICommand _runSelectedCmd;
        public ICommand RunSelectedCmd { get => _runSelectedCmd; private set => _runSelectedCmd = value; }

        private ICommand _runAllCmd;
        public ICommand RunAllCmd { get => _runAllCmd; private set => _runAllCmd = value; }

        private ICommand _cancelBackupCmd;
        public ICommand CancelBackupCmd { get => _cancelBackupCmd; private set => _cancelBackupCmd = value; }

        private ICommand _showAddJobViewCommand;
        public ICommand ShowAddJobViewCommand { get => _showAddJobViewCommand; private set => _showAddJobViewCommand = value; }

        private ICommand _showChooseJobViewCommand;
        public ICommand ShowChooseJobViewCommand { get => _showChooseJobViewCommand; private set => _showChooseJobViewCommand = value; }

        private ICommand _showRemoveJobViewCommand;
        public ICommand ShowRemoveJobViewCommand { get => _showRemoveJobViewCommand; private set => _showRemoveJobViewCommand = value; }

        private ICommand _runJobCmd;
        public ICommand RunJobCmd { get => _runJobCmd; private set => _runJobCmd = value; }

        private ICommand _setFrenchCommand;
        public ICommand SetFrenchCommand { get => _setFrenchCommand; private set => _setFrenchCommand = value; }

        private ICommand _setEnglishCommand;
        public ICommand SetEnglishCommand { get => _setEnglishCommand; private set => _setEnglishCommand = value; }

        private ICommand _openSettingsCommand;
        public ICommand OpenSettingsCommand { get => _openSettingsCommand; private set => _openSettingsCommand = value; }

        private ICommand _returnToMainViewCommand;
        public ICommand ReturnToMainViewCommand { get => _returnToMainViewCommand; private set => _returnToMainViewCommand = value; }

        private ICommand _refreshJobsCommand;
        public ICommand RefreshJobsCommand { get => _refreshJobsCommand; private set => _refreshJobsCommand = value; }

        private ICommand _pauseResumeJobCommand;
        public ICommand PauseResumeJobCommand { get => _pauseResumeJobCommand; private set => _pauseResumeJobCommand = value; }

        private ICommand _stopJobCommand;
        public ICommand StopJobCommand { get => _stopJobCommand; private set => _stopJobCommand = value; }

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

            Jobs = new ObservableCollection<BackupJob>();
            _recentJobs = new ObservableCollection<BackupJob>();
            
            _statusVM = new JobStatusViewModel(paths);
            
            // Timer de rafraîchissement
            _refreshTimer = new Timer(2000);
            _refreshTimer.Elapsed += (s, e) => RefreshJobs();
            _refreshTimer.Start();

            // Initialisation des commandes
            InitializeCommands();

            _svc.StatusUpdated += OnStatusUpdated;
            CurrentViewModel = this;
            RefreshJobs();
        }

        private void InitializeCommands()
        {
            AddJobCmd = new Infrastructure.RelayCommand(_ => AddJobRequested?.Invoke());
            RemoveJobCmd = new Infrastructure.RelayCommand(_ => RemoveJobRequested?.Invoke());
            
            RunSelectedCmd = new Infrastructure.RelayCommand(
                async _ => await RunJobAsync(_selected),
                _ => !IsBackupRunning && _selected != null
            );
            
            RunAllCmd = new Infrastructure.RelayCommand(
                async _ => await RunAllJobsAsync(),
                _ => !IsBackupRunning && Jobs.Count > 0
            );
            
            CancelBackupCmd = new Infrastructure.RelayCommand(
                _ => CancelAllBackups(),
                _ => IsBackupRunning
            );
            
            ShowAddJobViewCommand = new Infrastructure.RelayCommand(_ => ShowAddJobView());
            ShowChooseJobViewCommand = new Infrastructure.RelayCommand(_ => ShowChooseJobView());
            ShowRemoveJobViewCommand = new Infrastructure.RelayCommand(_ => RemoveJobRequested?.Invoke());

            RunJobCmd = new Infrastructure.RelayCommand(async param =>
            {
                if (param is BackupJob job)
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
            RefreshJobsCommand = new Infrastructure.RelayCommand(_ => RefreshJobs());

            PauseResumeJobCommand = new Infrastructure.RelayCommand(async param =>
            {
                if (param is BackupJob job)
                {
                    await TogglePauseJobAsync(job);
                }
            }, param => param is BackupJob job && (job.State == "ACTIVE" || job.State == "PAUSED"));

            StopJobCommand = new Infrastructure.RelayCommand(param =>
            {
                if (param is BackupJob job)
                {
                    StopJob(job);
                }
            }, param => param is BackupJob job && (job.State == "ACTIVE" || job.State == "PAUSED"));
        }

        private void UpdateCommandsCanExecute()
        {
            ((Infrastructure.RelayCommand)RunSelectedCmd).RaiseCanExecuteChanged();
            ((Infrastructure.RelayCommand)RunAllCmd).RaiseCanExecuteChanged();
            ((Infrastructure.RelayCommand)CancelBackupCmd).RaiseCanExecuteChanged();
        }

        private async Task RunJobAsync(BackupJob job)
        {
            if (job == null || string.IsNullOrWhiteSpace(job.Name)) return;

            job.State = "ACTIVE";
            job.Progression = 0.01;
            IsBackupRunning = true;
            
            try
            {
                await _svc.ExecuteBackupAsync(job.Name);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing job {job.Name}: {ex.Message}");
                job.State = "ERROR";
            }
        }

        private async Task RunAllJobsAsync()
        {
            if (Jobs.Count == 0) return;

            IsBackupRunning = true;
            foreach (var job in Jobs)
            {
                job.State = "PENDING";
                job.Progression = 0.01;
            }
            
            try
            {
                await _svc.ExecuteAllBackupsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing all jobs: {ex.Message}");
            }
        }

        private async Task TogglePauseJobAsync(BackupJob job)
        {
            if (job == null) return;

            // Ajout de log pour vérifier le job traité
            Console.WriteLine($"TogglePauseJobAsync appelé pour le job : {job.Name}, état actuel : {job.State}");

            try
            {
                if (job.State == "PAUSED")
                {
                    job.State = "ACTIVE";
                    _svc.ResumeJob(job.Name);
                    Console.WriteLine($"Reprise du job : {job.Name}");
                }
                else if (job.State == "ACTIVE")
                {
                    job.State = "PAUSED";
                    _svc.PauseJob(job.Name);
                    Console.WriteLine($"Pause du job : {job.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur pour le job {job.Name} : {ex.Message}");
            }
        }

        private void StopJob(BackupJob job)
        {
            if (job == null) return;

            try
            {
                job.State = "END";
                _svc.StopJob(job.Name);
                
                // Vérifier si d'autres jobs sont en cours
                IsBackupRunning = Jobs.Any(j => j.State == "ACTIVE" || j.State == "PENDING" || j.State == "PAUSED");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping job {job.Name}: {ex.Message}");
            }
        }

        private void CancelAllBackups()
        {
            try
            {
                _svc.CancelAllBackups();
                
                foreach (var job in Jobs.Concat(RecentJobs))
                {
                    if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                    {
                        job.State = "END";
                    }
                }
                
                IsBackupRunning = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error canceling all backups: {ex.Message}");
            }
        }

        private void OnStatusUpdated(StatusEntry status)
        {
            RefreshJobs();
        }

        // Méthode publique pour le rafraîchissement des jobs
        public void RefreshJobs()
        {
            _threadPool.EnqueueGuiTask(async (ct) =>
            {
                try
                {
                    var currentJobs = _svc.GetJobs().ToList();
                    
                    // Mettre à jour Jobs
                    UpdateJobCollection(Jobs, currentJobs);
                    
                    // Mettre à jour RecentJobs
                    UpdateJobCollection(RecentJobs, currentJobs);
                    
                    // Mettre à jour l'état des jobs
                    foreach (var job in Jobs.Concat(RecentJobs))
                    {
                        _statusVM.ApplyStatus(job);
                    }
                    
                    // Vérifier si des jobs sont en cours
                    IsBackupRunning = Jobs.Any(j => j.State == "ACTIVE" || j.State == "PENDING" || j.State == "PAUSED");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error refreshing jobs: {ex.Message}");
                }
            });
        }

        // Méthode pour forcer le rafraîchissement des jobs
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
                    if (job.State == "ACTIVE" || job.State == "PAUSED" || job.State == "PENDING" || 
                        job.State == "END" || job.State == "CANCELLED")
                    {
                        currentJobStates[job.Name] = job.State;
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
                        job.State = currentJobStates[job.Name];
                    }
                    else
                    {
                        // For jobs that don't have a preserved state, make sure they're in READY state
                        if (string.IsNullOrEmpty(job.State) || job.State == "CANCELLED")
                        {
                            job.State = "READY";
                        }
                    }
                    
                    // Add the job to the collection
                    RecentJobs.Add(job);
                }
                
                // Update job status immediately
                foreach (var job in RecentJobs)
                {
                    _statusVM.ApplyStatus(job);
                }
                
                // Notify UI
                OnPropertyChanged(nameof(RecentJobs));
                
                // Check if we need to update IsBackupRunning flag
                bool anyActive = RecentJobs.Any(j => j.State == "ACTIVE" || j.State == "PENDING" || j.State == "PAUSED");
                if (!anyActive && IsBackupRunning)
                {
                    IsBackupRunning = false;
                }
            }
            catch (Exception ex) {
                Console.WriteLine($"Error in ForceRefreshJobs: {ex.Message}");
            }
        }

        private void UpdateJobCollection(ObservableCollection<BackupJob> collection, List<BackupJob> currentJobs)
        {
            // Supprimer les jobs qui n'existent plus
            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (!currentJobs.Any(j => j.Name == collection[i].Name))
                {
                    collection.RemoveAt(i);
                }
            }
            
            // Mettre à jour ou ajouter les jobs
            foreach (var job in currentJobs)
            {
                var existingJob = collection.FirstOrDefault(j => j.Name == job.Name);
                if (existingJob == null)
                {
                    collection.Add(job);
                }
                else
                {
                    // Préserver l'état si le job est en cours
                    if (existingJob.State != "ACTIVE" && existingJob.State != "PAUSED" && existingJob.State != "PENDING")
                    {
                        existingJob.State = job.State;
                    }
                    existingJob.SourceDir = job.SourceDir;
                    existingJob.TargetDir = job.TargetDir;
                    existingJob.Strategy = job.Strategy;
                }
            }
        }

        private void ShowAddJobView()
        {
            var vm = new AddJobViewModel(_svc);
            vm.JobAdded += () =>
            {
                RefreshJobs();
                CurrentViewModel = this;
            };
            CurrentViewModel = vm;
        }

        private void ShowChooseJobView()
        {
            CurrentViewModel = new ChooseJobViewModel(_svc);
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
            _svc.StatusUpdated -= OnStatusUpdated;
        }
    }
}