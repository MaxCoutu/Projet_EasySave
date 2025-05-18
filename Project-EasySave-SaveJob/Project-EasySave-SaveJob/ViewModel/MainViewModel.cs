using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Input;
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

        private ViewModelBase _currentViewModel;
        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set { _currentViewModel = value; OnPropertyChanged(); }
        }

        public event Action AddJobRequested;
        public event Action RemoveJobRequested;

        public MainViewModel(
            IBackupService svc,
            ILanguageService langService,
            IPathProvider paths)
        {
            _svc = svc;
            _lang = langService;
            _paths = paths;
            _langDir = Path.Combine(paths.GetBaseDir(), "Languages");

            Jobs = new ObservableCollection<BackupJob>(_svc.GetJobs());
            RecentJobs = new ObservableCollection<BackupJob>();
            LoadRecentJobs();

            AddJobCmd = new RelayCommand(_ => AddJobRequested?.Invoke());
            RemoveJobCmd = new RelayCommand(_ => RemoveJobRequested?.Invoke());

            // Commande pour lancer le job sélectionné
            RunSelectedCmd = new RelayCommand(async _ =>
            {
                if (_selected != null && !string.IsNullOrWhiteSpace(_selected.Name))
                {
                    await _svc.ExecuteBackupAsync(_selected.Name);
                }
            });

            // Commande pour lancer tous les jobs
            RunAllCmd = new RelayCommand(async _ =>
            {
                await _svc.ExecuteAllBackupsAsync();
            });

            ShowAddJobViewCommand = new RelayCommand(_ => ShowAddJobView());
            ShowChooseJobViewCommand = new RelayCommand(_ => ShowChooseJobView());
            ShowRemoveJobViewCommand = new RelayCommand(_ => RemoveJobRequested?.Invoke());

            // Commande pour lancer un job spécifique
            RunJobCmd = new RelayCommand(async param =>
            {
                if (param is BackupJob job && !string.IsNullOrWhiteSpace(job.Name))
                {
                    await _svc.ExecuteBackupAsync(job.Name);
                }
            });

            SetFrenchCommand = new RelayCommand(_ =>
                _lang.Load(Path.Combine(_langDir, "fr.json")));
            SetEnglishCommand = new RelayCommand(_ =>
                _lang.Load(Path.Combine(_langDir, "en.json")));

            OpenSettingsCommand = new RelayCommand(_ => { /* Logique pour ouvrir les paramètres */ });
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
            if (!File.Exists(statusPath)) return;

            string json = File.ReadAllText(statusPath);
            if (string.IsNullOrWhiteSpace(json)) return;

            var statusEntries = JsonSerializer.Deserialize<List<StatusEntry>>(json);
            if (statusEntries == null || statusEntries.Count == 0) return;

            var distinctEntries = statusEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.Name))
                .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Take(3)
                .ToList();

            foreach (var entry in distinctEntries)
            {
                var job = Jobs.FirstOrDefault(j => string.Equals(j.Name?.Trim(), entry.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                if (job != null)
                {
                    RecentJobs.Add(job);
                }
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
    }
}