using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.Model
{
    public class BackupJobBuilder
    {
        private  BackupJob _job = new BackupJob();

        public BackupJobBuilder WithName(string name) { _job.Name = name; return this; }
        public BackupJobBuilder WithSource(string dir) { _job.SourceDir = dir; return this; }
        public BackupJobBuilder WithTarget(string dir) { _job.TargetDir = dir; return this; }
        public BackupJobBuilder WithStrategy(IBackupStrategy strategy) { _job.Strategy = strategy; return this; }

        public BackupJob Build()
        {
            BackupJob job = _job;
            _job = new BackupJob();
            return job;
        }
    }
}
