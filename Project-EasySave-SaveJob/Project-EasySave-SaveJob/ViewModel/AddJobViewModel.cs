using Projet.Model;
using Projet.Service;
using System;
using System.Windows.Input;
using Projet.Infrastructure;

namespace Projet.ViewModel
{
    public class AddJobViewModel
    {
        private readonly IBackupService _svc;
        public BackupJobBuilder Builder { get; } = new BackupJobBuilder();

        public AddJobViewModel(IBackupService svc) => _svc = svc;

        public void AddJob() => _svc.AddBackup(Builder.Build());

        public event Action JobAdded;

        public ICommand AddJobCmd => new RelayCommand(_ =>
        {
            AddJob();
            JobAdded?.Invoke();
        });
    }
}
