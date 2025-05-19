using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            long bytesCopied = 0;
            int total = toCopy.Count;
            int done = 0;

            foreach (string src in toCopy)
            {
                string rel = Path.GetRelativePath(job.SourceDir, src);
                string dest = Path.Combine(job.TargetDir, rel);

                long fileSize = new FileInfo(src).Length;
                await CopyFileWithProgressAsync(src, dest, copied =>
                {
                    long currentBytesCopied = bytesCopied + copied;
                    double progression = totalSize > 0 ? (currentBytesCopied * 100.0) / totalSize : 100.0;
                    progressCallback?.Invoke(new StatusEntry(
                        job.Name, src, dest, "ACTIVE",
                        total, totalSize, total - done,
                        progression
                    ));
                });
                bytesCopied += fileSize;
                done++;
            }
        }

        private async Task CopyFileWithProgressAsync(string src, string dst, Action<long> progress)
        {
            const int bufferSize = 81920;
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            long copied = 0;
            using (var source = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true))
            using (var dest = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true))
            {
                var buffer = new byte[bufferSize];
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await dest.WriteAsync(buffer, 0, read);
                    copied += read;
                    progress?.Invoke(copied);
                }
            }
        }
    }
}
