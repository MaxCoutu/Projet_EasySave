using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Projet.Infrastructure;

namespace Projet.Model
{
    public sealed class DifferentialBackupStrategy : BackupStrategyBase
    {
        public override string Type => "Differential";

        public override async Task ExecuteAsync(BackupJob job, Action<StatusEntry> progressCallback)
        {
            IEnumerable<string> allFiles =
                Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories);

            List<string> toCopy = new List<string>();
            foreach (string src in allFiles)
            {
                string rel = Path.GetRelativePath(job.SourceDir, src);
                string dest = Path.Combine(job.TargetDir, rel);

                if (!File.Exists(dest) ||
                    File.GetLastWriteTimeUtc(src) > File.GetLastWriteTimeUtc(dest))
                {
                    toCopy.Add(src);
                }
            }

            long totalSize = toCopy.Sum(f => new FileInfo(f).Length);
            int total = toCopy.Count;
            int done = 0;

            foreach (string src in toCopy)
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
