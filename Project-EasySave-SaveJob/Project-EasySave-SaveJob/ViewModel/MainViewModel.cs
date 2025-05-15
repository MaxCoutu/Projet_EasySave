using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;

namespace Projet.ViewModel
{
    public class MainViewModel : ViewModelBase
    {
        private readonly IBackupService _svc;

        public IBackupService Svc => _svc;

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

        public event Action AddJobRequested;
        public event Action RemoveJobRequested;

        public MainViewModel(IBackupService svc)
        {
            _svc = svc;
            Jobs = new ObservableCollection<BackupJob>(_svc.GetJobs());

            AddJobCmd = new RelayCommand(_ => AddJobRequested?.Invoke());
            RemoveJobCmd = new RelayCommand(_ => RemoveJobRequested?.Invoke());
            RunSelectedCmd = new RelayCommand(_ => _svc.ExecuteBackupAsync(_selected?.Name));
            RunAllCmd = new RelayCommand(_ => _svc.ExecuteAllBackupsAsync());

            ShowAddJobViewCommand = new RelayCommand(_ => ShowAddJobView());
            ShowRemoveJobViewCommand = new RelayCommand(_ => ShowRemoveJobView());

            _svc.StatusUpdated += s => { /* Optionnel : mise à jour de l’UI */ };

            // Vue par défaut
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
