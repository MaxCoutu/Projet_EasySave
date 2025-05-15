using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;

namespace Projet.ViewModel
{
    public class RemoveJobViewModel : ViewModelBase
    {
        private readonly IBackupService _svc;
        public ObservableCollection<BackupJob> Jobs { get; }
        public BackupJob SelectedJob { get; set; }

        public ICommand ConfirmRemoveCommand { get; }

        public RemoveJobViewModel(IBackupService svc)
        {
            _svc = svc;
            Jobs = new ObservableCollection<BackupJob>(_svc.GetJobs());

            ConfirmRemoveCommand = new RelayCommand(_ => Remove(), _ => SelectedJob != null);
        }

        public event Action JobRemoved;

        private void Remove()
        {
            if (SelectedJob != null)
            {
                _svc.RemoveJob(SelectedJob.Name);
                Jobs.Remove(SelectedJob);
                JobRemoved?.Invoke(); // Déclenche l'événement
            }
        }
    }
}
