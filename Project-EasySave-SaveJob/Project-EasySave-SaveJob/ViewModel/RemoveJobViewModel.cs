using Projet.Model;
using Projet.Service;
using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.ViewModel
{
    public class RemoveJobViewModel
    {
        private readonly IBackupService _svc;
        public RemoveJobViewModel(IBackupService svc) => _svc = svc;

        public BackupJob SelectedJob { get; set; }

        public void Remove()
        {
            if (SelectedJob != null)
                _svc.RemoveBackup(SelectedJob.Name);
        }
    }
}
