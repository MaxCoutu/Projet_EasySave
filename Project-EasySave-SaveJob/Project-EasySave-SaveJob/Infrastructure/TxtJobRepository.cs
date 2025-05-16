using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Projet.Model;

namespace Projet.Infrastructure
{
   
    public class TxtJobRepository : IJobRepository
    {
        private readonly string _file;

        public TxtJobRepository(IPathProvider paths)
        {
            
            _file = Path.Combine(
                Path.GetDirectoryName(paths.GetLogDir()) ?? "C:\\Projet",
                "jobs.txt");
        }

      
        public IReadOnlyList<BackupJob> Load()
        {
            if (!File.Exists(_file)) return Array.Empty<BackupJob>();

            var list = new List<BackupJob>();
            foreach (string line in File.ReadAllLines(_file))
            {
                string[] p = line.Split('|');
                if (p.Length != 4) continue;

                IBackupStrategy strat = p[3].Trim().ToLower() == "diff"
                    ? (IBackupStrategy)new DifferentialBackupStrategy()
                    : new FullBackupStrategy();

                list.Add(new BackupJob
                {
                    Name = p[0],
                    SourceDir = p[1],
                    TargetDir = p[2],
                    Strategy = strat
                });
            }
            return list;
        }


        public void Save(IReadOnlyList<BackupJob> jobs)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);

            var lines = jobs.Select(j =>
                $"{j.Name}|{j.SourceDir}|{j.TargetDir}|{(j.Strategy?.Type?.ToLower() ?? "full")}"
            );

            File.WriteAllLines(_file, lines);
        }
    }
}
