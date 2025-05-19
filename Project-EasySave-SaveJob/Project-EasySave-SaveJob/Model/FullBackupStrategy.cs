using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            long bytesCopied = 0;
            int total = files.Count;
            int done = 0;

            foreach (string src in files)
            {
                string rel = Path.GetRelativePath(job.SourceDir, src);
                string dest = Path.Combine(job.TargetDir, rel);

                long fileSize = new FileInfo(src).Length;
                await CopyFileWithProgressAsync(src, dest, copied =>
                {
                    // Mise à jour de la progression à chaque chunk copié
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
