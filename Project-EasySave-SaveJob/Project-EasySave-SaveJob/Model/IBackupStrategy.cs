using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Projet.Infrastructure;

namespace Projet.Model
{
    public interface IBackupStrategy
    {
        string Type { get; }
        Task ExecuteAsync(BackupJob job, Action<StatusEntry> progressCallback);
    }
}
