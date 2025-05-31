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

            ConfirmCommand = new Projet.Infrastructure.RelayCommand(
                param => AddJob(),
                param => CanAddJob()
            );

            Builder.PropertyChanged += (s, e) =>
            {
                (ConfirmCommand as Projet.Infrastructure.RelayCommand)?.RaiseCanExecuteChanged();
            };
        }

        public void AddJob()
        {
            var job = Builder.Build();
            _svc.AddJob(job);
            JobAdded?.Invoke();
        }

        public bool CanAddJob()
        {
            var job = Builder?.Build();
            return job != null
                && !string.IsNullOrWhiteSpace(job.Name)
                && !string.IsNullOrWhiteSpace(job.SourceDir)
                && !string.IsNullOrWhiteSpace(job.TargetDir)
                && (job.Strategy != null || !string.IsNullOrWhiteSpace(Builder.Type));
        }
    }
}