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

        void AddJob(BackupJob job);              
        void RemoveJob(string name);             

        Task ExecuteBackupAsync(string name);
        Task ExecuteAllBackupsAsync();
        void CancelAllBackups();
        
        // New methods for job control
        void PauseJob(string name);
        void ResumeJob(string name);
        void StopJob(string name);

        IReadOnlyList<BackupJob> GetJobs();      
    }
}
