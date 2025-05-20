using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Projet.Infrastructure;
using Projet.Model;

namespace Projet.Service
{
    public class BackupService : IBackupService
    {
        public event Action<StatusEntry> StatusUpdated;

        private readonly ILogger _logger;
        private readonly IJobRepository _repo;
        private readonly Settings _settings;
        private readonly List<BackupJob> _jobs;

        public BackupService(ILogger logger, IJobRepository repo, Settings settings)
        {
            _logger = logger;
            _repo = repo;
            _settings = settings;
            _jobs = new List<BackupJob>(_repo.Load());
        }

        public void AddJob(BackupJob job)
        {
            _jobs.Add(job);
            _repo.Save(_jobs);
        }

        public void RemoveJob(string name)
        {
            _jobs.RemoveAll(j => j.Name == name);
            _repo.Save(_jobs);
        }

        public IReadOnlyList<BackupJob> GetJobs() => _jobs.AsReadOnly();

        public async Task ExecuteBackupAsync(string name)
        {
            var job = _jobs.FirstOrDefault(j => j.Name == name);
            if (job == null)
                return;

            Report(new StatusEntry { Name = job.Name, State = "PENDING" });

            try
            {
                await ProcessJobAsync(job);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing backup '{name}': {ex.Message}");
            }

            Report(new StatusEntry { Name = job.Name, State = "END" });
        }

        public async Task ExecuteAllBackupsAsync()
        {
            foreach (var job in _jobs)
            {
                await ExecuteBackupAsync(job.Name);
            }
        }

        private async Task ProcessJobAsync(BackupJob job)
        {
            string cleanedSourceDir = job.SourceDir.Trim('"').Trim();
            string cleanedTargetDir = job.TargetDir.Trim('"').Trim();

            if (!Directory.Exists(cleanedSourceDir))
                throw new DirectoryNotFoundException($"Le répertoire source '{cleanedSourceDir}' n'existe pas.");

            if (!Directory.Exists(cleanedTargetDir))
                Directory.CreateDirectory(cleanedTargetDir);

            var files = Directory.EnumerateFiles(cleanedSourceDir, "*", SearchOption.AllDirectories).ToList();
            long totalSize = files.Sum(f => new FileInfo(f).Length);
            long bytesCopied = 0;
            int total = files.Count, left = total;

            foreach (string src in files)
            {
                string rel = Path.GetRelativePath(cleanedSourceDir, src);
                string dest = Path.Combine(cleanedTargetDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));

                var swCopy = System.Diagnostics.Stopwatch.StartNew();
                long fileSize = new FileInfo(src).Length;

                await CopyFileWithProgressAsync(src, dest, (copied, isFinal) =>
                {
                    long currentBytesCopied = bytesCopied + copied;
                    double progression = totalSize > 0 ? (currentBytesCopied * 100.0) / totalSize : 100.0;
                    if (isFinal || swCopy.ElapsedMilliseconds >= 500)
                    {
                        swCopy.Restart();
                        Report(new StatusEntry
                        {
                            Name = job.Name,
                            SourceFilePath = src,
                            TargetFilePath = dest,
                            State = "ACTIVE",
                            TotalFilesToCopy = total,
                            TotalFilesSize = totalSize,
                            NbFilesLeftToDo = left,
                            Progression = progression
                        });
                    }
                });

                swCopy.Stop();

                bytesCopied += fileSize;
                left--;
            }
        }

        private async Task CopyFileWithProgressAsync(string src, string dst, Action<long, bool> progress)
        {
            const int bufferSize = 81920;
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
                    progress?.Invoke(copied, false);
                }
                progress?.Invoke(copied, true);
            }
        }

        private void Report(StatusEntry s)
        {
            Console.WriteLine($"Reporting: Job={s.Name}, State={s.State}, Progression={s.Progression:F2}%");
            _logger.UpdateStatus(s);
            StatusUpdated?.Invoke(s);
        }
    }
}