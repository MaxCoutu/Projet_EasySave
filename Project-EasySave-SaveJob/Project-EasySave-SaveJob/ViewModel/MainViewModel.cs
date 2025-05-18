using System;
using System.Collections.ObjectModel;
using System.IO;
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

        public ObservableCollection<BackupJob> Jobs { get; }
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
        public ICommand ShowRemoveJobViewCommand { get; }
        public ICommand RunJobCmd { get; }
        public ICommand SetFrenchCommand { get; }
        public ICommand SetEnglishCommand { get; }

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
            _langDir = Path.Combine(paths.GetBaseDir(), "Languages");

            Jobs = new ObservableCollection<BackupJob>(_svc.GetJobs());

            AddJobCmd = new RelayCommand(_ => AddJobRequested?.Invoke());
            RemoveJobCmd = new RelayCommand(_ => RemoveJobRequested?.Invoke());
            RunSelectedCmd = new RelayCommand(_ => _svc.ExecuteBackupAsync(_selected?.Name));
            RunAllCmd = new RelayCommand(_ => _svc.ExecuteAllBackupsAsync());
            ShowAddJobViewCommand = new RelayCommand(_ => AddJobRequested?.Invoke());
            ShowRemoveJobViewCommand = new RelayCommand(_ => RemoveJobRequested?.Invoke());

            RunJobCmd = new RelayCommand(param =>
            {
                if (param is BackupJob job && !string.IsNullOrWhiteSpace(job.Name))
                    _svc.ExecuteBackupAsync(job.Name);
            });

            SetFrenchCommand = new RelayCommand(_ =>
                _lang.Load(Path.Combine(_langDir, "fr.json")));
            SetEnglishCommand = new RelayCommand(_ =>
                _lang.Load(Path.Combine(_langDir, "en.json")));

            _svc.StatusUpdated += s => { /* UI progress if needed */ };

            CurrentViewModel = this;
        }

        public void RefreshJobs()
        {
            Jobs.Clear();
            foreach (var job in _svc.GetJobs())
                Jobs.Add(job);
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

        private void ShowRemoveJobView()
        {
            var vm = new RemoveJobViewModel(_svc);
            vm.JobRemoved += () =>
            {
                RefreshJobs();
                CurrentViewModel = this;
            };
            CurrentViewModel = vm;
        }
    }
}