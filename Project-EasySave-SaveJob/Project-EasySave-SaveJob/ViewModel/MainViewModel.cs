using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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

        public void RefreshJobs()
        {
            Jobs.Clear();
            foreach (var job in _svc.GetJobs())
                Jobs.Add(job);
        }

        public ObservableCollection<BackupJob> Jobs { get; }
        private BackupJob _selected;
        public  BackupJob SelectedJob
        {
            get => _selected;
            set { _selected = value; OnPropertyChanged(); }
        }

        public ICommand AddJobCmd      { get; }
        public ICommand RemoveJobCmd   { get; }
        public ICommand RunSelectedCmd { get; }
        public ICommand RunAllCmd      { get; }

        public MainViewModel(IBackupService svc)
        {
            _svc  = svc;
            Jobs  = new ObservableCollection<BackupJob>(_svc.GetJobs());

            AddJobCmd      = new RelayCommand(_ => AddJobRequested?.Invoke());
            RemoveJobCmd   = new RelayCommand(_ => RemoveJobRequested?.Invoke());
            RunSelectedCmd = new RelayCommand(_ => _svc.ExecuteBackupAsync(_selected?.Name));
            RunAllCmd      = new RelayCommand(_ => _svc.ExecuteAllBackupsAsync());

            _svc.StatusUpdated += s => { /* update UI if needed */ };
        }

        public event Action AddJobRequested;
        public event Action RemoveJobRequested;

    }

}

