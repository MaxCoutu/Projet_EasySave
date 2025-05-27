using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
using System.Timers;
using System.Windows;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;

namespace Projet.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IBackupService _svc;
        private readonly ILanguageService _lang;
        private readonly string _langDir;
        private readonly IPathProvider _paths;
        private readonly JobStatusViewModel _statusVM;
        private readonly Timer _refreshTimer;
        private readonly Settings _settings;

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

        public ICommand AddJobCmd { get; }
        public ICommand RemoveJobCmd { get; }
        public ICommand RunSelectedCmd { get; }
        public ICommand RunAllCmd { get; }
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

            Jobs = new ObservableCollection<BackupJob>(_svc.GetJobs());
            _recentJobs = new ObservableCollection<BackupJob>();
            LoadRecentJobs();
            
            // Créer et configurer le JobStatusViewModel
            _statusVM = new JobStatusViewModel(paths);
            
            // Timer pour mettre à jour régulièrement l'état des tâches
            _refreshTimer = new Timer(5000); // 5 secondes
            _refreshTimer.Elapsed += (s, e) => UpdateJobsStatus();
            _refreshTimer.Start();

            AddJobCmd = new RelayCommand(_ => AddJobRequested?.Invoke());
            RemoveJobCmd = new RelayCommand(_ => RemoveJobRequested?.Invoke());
            RunSelectedCmd = new RelayCommand(async _ =>
            {
                if (_selected != null && !string.IsNullOrWhiteSpace(_selected.Name))
                {
                    await _svc.ExecuteBackupAsync(_selected.Name);
                }
            });
            RunAllCmd = new RelayCommand(async _ =>
            {
                await _svc.ExecuteAllBackupsAsync();
            });
            ShowAddJobViewCommand = new RelayCommand(_ => ShowAddJobView());
            ShowChooseJobViewCommand = new RelayCommand(_ => ShowChooseJobView());
            ShowRemoveJobViewCommand = new RelayCommand(_ => RemoveJobRequested?.Invoke());

            RunJobCmd = new RelayCommand(async param =>
            {
                if (param is BackupJob job && !string.IsNullOrWhiteSpace(job.Name))
                {
                    // Mettre immédiatement à jour l'état pour une meilleure réactivité de l'UI
                    job.State = "PENDING";
                    job.Progression = 0.01; // Démarre la progression à 1% pour montrer visuellement que le job a démarré
                    
                    // Exécuter la sauvegarde
                    await _svc.ExecuteBackupAsync(job.Name);
                    
                    // Forcer le rafraîchissement des tâches après l'exécution
                    UpdateJobsStatus();
                }
            });

            SetFrenchCommand = new RelayCommand(_ =>
                _lang.Load(Path.Combine(_langDir, "fr.json")));
            SetEnglishCommand = new RelayCommand(_ =>
                _lang.Load(Path.Combine(_langDir, "en.json")));

            OpenSettingsCommand = new RelayCommand(_ => ShowSettingsView());

            ReturnToMainViewCommand = new RelayCommand(_ => CurrentViewModel = this);

            _svc.StatusUpdated += s => { RefreshJobs(); LoadRecentJobs(); };

            CurrentViewModel = this;
        }

        public void RefreshJobs()
        {
            Jobs.Clear();
            foreach (var job in _svc.GetJobs())
                Jobs.Add(job);
        }

        private void LoadRecentJobs()
        {
            RecentJobs.Clear();
            string statusPath = Path.Combine(_paths.GetStatusDir(), "status.json");
            if (!File.Exists(statusPath))
                return;

            try
            {
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
                foreach (var job in recentJobs)
                {
                    RecentJobs.Add(job);
                }
            }
            catch
            {
                RecentJobs.Clear();
            }
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
            try
            {
                // Simple mise à jour directe sans passer par le dispatcher
                UpdateJobStatusInternal();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la mise à jour du statut: {ex.Message}");
            }
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
        }
    }
}