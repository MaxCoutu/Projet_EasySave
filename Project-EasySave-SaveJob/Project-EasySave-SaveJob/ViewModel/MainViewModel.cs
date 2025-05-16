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
        public ObservableCollection<BackupJob> Jobs { get; }
        private BackupJob _selectedJob;
        public BackupJob SelectedJob
        {
            get => _selectedJob;
            set
            {
                _selectedJob = value;
                OnPropertyChanged(nameof(SelectedJob));
            }
        }

        public ICommand RemoveJobCommand { get; }

        public MainViewModel(IBackupService svc)
        {
            _svc = svc;
            Jobs = new ObservableCollection<BackupJob>(_svc.GetJobs());
            RemoveJobCommand = new RelayCommand(
                param => RemoveJob(param as BackupJob),
                param => param is BackupJob);
        }

        private void RemoveJob(BackupJob job)
        {
            if (job != null)
            {
                _svc.RemoveJob(job.Name);
                Jobs.Remove(job);
            }
        }

        public void RefreshJobs()
        {
            Jobs.Clear();
            foreach (var job in _svc.GetJobs())
                Jobs.Add(job);
        }
    }
}
