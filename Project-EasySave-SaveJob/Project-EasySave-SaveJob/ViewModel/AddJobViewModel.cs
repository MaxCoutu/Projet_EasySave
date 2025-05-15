using System;
using System.Windows.Input;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;

namespace Projet.ViewModel
{
    public class AddJobViewModel : ViewModelBase
    {
        private readonly IBackupService _svc;
        public BackupJobBuilder Builder { get; } = new BackupJobBuilder();

        public ICommand ConfirmCommand { get; }

        public event Action JobAdded;

        public AddJobViewModel(IBackupService svc)
        {
            _svc = svc;

            ConfirmCommand = new RelayCommand(_ => AddJob(), _ => CanAddJob());
        }

        public void AddJob()
        {
            var job = Builder.Build();
            _svc.AddJob(job);
            JobAdded?.Invoke();
        }

        public bool CanAddJob()
        {
            // Vérifie que tous les champs sont remplis
            return !string.IsNullOrWhiteSpace(Builder?.Build().Name)
                && !string.IsNullOrWhiteSpace(Builder?.Build().SourceDir)
                && !string.IsNullOrWhiteSpace(Builder?.Build().TargetDir)
                && Builder?.Build().Strategy != null;
        }
    }
}
