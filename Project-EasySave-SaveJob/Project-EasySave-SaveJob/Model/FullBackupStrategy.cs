using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Projet.Infrastructure;

namespace Projet.Model
{
    public sealed class FullBackupStrategy : BackupStrategyBase
    {
        public override string Type => "Full";

        public override async Task ExecuteAsync(BackupJob job, Action<StatusEntry> progressCallback)
        {
            List<string> files = Directory
                .EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories)
                .ToList();

            long totalSize = files.Sum(f => new FileInfo(f).Length);
            int total = files.Count;
            int done = 0;

            foreach (string src in files)
            {
                string rel = Path.GetRelativePath(job.SourceDir, src);
                string dest = Path.Combine(job.TargetDir, rel);

                await CopyFileAsync(src, dest);
                done++;

                progressCallback?.Invoke(new StatusEntry(
                    job.Name, src, dest, "ACTIVE",
                    total, totalSize, total - done,
                    done / (double)total));
            }
        }
    }
}
