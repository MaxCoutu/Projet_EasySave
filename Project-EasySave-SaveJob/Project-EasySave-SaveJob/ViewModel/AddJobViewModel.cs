using Projet.Model;
using Projet.Service;
using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.ViewModel
{
    public class AddJobViewModel
    {
        private readonly IBackupService _svc;

        public BackupJobBuilder Builder { get; } = new BackupJobBuilder();

        public AddJobViewModel(IBackupService svc) => _svc = svc;

        public void AddJob() => _svc.AddBackup(Builder.Build());
    }
}
