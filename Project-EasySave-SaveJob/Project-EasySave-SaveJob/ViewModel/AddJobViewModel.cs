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

            // Utilisation explicite du namespace pour éviter tout conflit
            ConfirmCommand = new Projet.Infrastructure.RelayCommand(
                _ => AddJob(),
                _ => CanAddJob()
            );
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
            var job = Builder?.Build();
            return job != null
                && !string.IsNullOrWhiteSpace(job.Name)
                && !string.IsNullOrWhiteSpace(job.SourceDir)
                && !string.IsNullOrWhiteSpace(job.TargetDir)
                && job.Strategy != null;
        }
    }
}
