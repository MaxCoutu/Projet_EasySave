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
        public event Action JobSelected;

        public ChooseJobViewModel(IBackupService svc)
        {
            _svc = svc;
            Jobs = new ObservableCollection<BackupJob>(_svc.GetJobs());
            RunJobCmd = new RelayCommand(param =>
            {
                if (param is BackupJob job && !string.IsNullOrWhiteSpace(job.Name))
                    _svc.ExecuteBackupAsync(job.Name);
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