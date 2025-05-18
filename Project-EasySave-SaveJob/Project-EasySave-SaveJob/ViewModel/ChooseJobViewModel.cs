using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;

namespace Projet.ViewModel
{
    public class ChooseJobViewModel : ViewModelBase
    {
        private readonly IBackupService _svc;
        public ObservableCollection<BackupJob> Jobs { get; }
        public ICommand RunJobCmd { get; }
        public ICommand RemoveJobCmd { get; }

        public ChooseJobViewModel(IBackupService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            Jobs = new ObservableCollection<BackupJob>(_svc.GetJobs());

            // Commande pour lancer un job
            RunJobCmd = new RelayCommand(async param =>
            {
                if (param is BackupJob job && !string.IsNullOrWhiteSpace(job.Name))
                {
                    await _svc.ExecuteBackupAsync(job.Name);
                }
            });

            RemoveJobCmd = new RelayCommand(param =>
            {
                if (param is BackupJob job && !string.IsNullOrWhiteSpace(job.Name))
                {
                    _svc.RemoveJob(job.Name);
                    RefreshJobs();
                }
            });
        }

        public void RefreshJobs()
        {
            Jobs.Clear();
            foreach (var job in _svc.GetJobs())
                Jobs.Add(job);
        }
    }
}