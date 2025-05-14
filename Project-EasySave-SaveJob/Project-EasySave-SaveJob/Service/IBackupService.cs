using Projet.Infrastructure;
using Projet.Model;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Projet.Service
{
    public interface IBackupService
    {
        event Action<StatusEntry> StatusUpdated;

        void AddBackup(BackupJob job);
        void RemoveBackup(string name);

        Task ExecuteBackupAsync(string name);
        Task ExecuteAllBackupsAsync();

        IReadOnlyList<BackupJob> GetJobs();
    }
}
