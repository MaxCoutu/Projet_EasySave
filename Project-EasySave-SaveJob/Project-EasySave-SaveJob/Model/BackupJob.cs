using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Projet.Infrastructure;

namespace Projet.Model
{
    public class BackupJob
    {
        public string Name { get; set; } = string.Empty;
        public string SourceDir { get; set; } = string.Empty;
        public string TargetDir { get; set; } = string.Empty;
        public IBackupStrategy Strategy { get; set; } = null!;

        public Task RunAsync(Action<StatusEntry> cb) => Strategy.ExecuteAsync(this, cb);
    }
}
