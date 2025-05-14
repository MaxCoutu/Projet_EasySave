using System.Collections.Generic;
using Projet.Model;

namespace Projet.Infrastructure
{
    public interface IJobRepository
    {
        IReadOnlyList<BackupJob> Load();
        void Save(IReadOnlyList<BackupJob> jobs);
    }
}
