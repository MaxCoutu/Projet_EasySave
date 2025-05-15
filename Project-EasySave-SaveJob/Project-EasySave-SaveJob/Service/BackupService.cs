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

        // Renommé ici en AddJob
        public void AddJob(BackupJob job)
        {
            _jobs.Add(job);
            _repo.Save(_jobs);
        }

        // Renommé ici en RemoveJob
        public void RemoveJob(string name)
        {
            _jobs.RemoveAll(j => j.Name == name);
            _repo.Save(_jobs);
        }

        public IReadOnlyList<BackupJob> GetJobs() => _jobs.AsReadOnly();

        public async Task ExecuteBackupAsync(string name)
        {
            if (PackageBlocker.IsBlocked(_settings))
            {
                Console.WriteLine("Blocked package running — job skipped.");
                return;
            }

            var job = _jobs.First(j => j.Name == name);
            Report(new StatusEntry { Name = job.Name, State = "PENDING" });
            await ProcessJobAsync(job);
            Report(new StatusEntry { Name = job.Name, State = "END" });
        }

        public async Task ExecuteAllBackupsAsync()
        {
            foreach (var j in _jobs)
            {
                if (PackageBlocker.IsBlocked(_settings)) break;
                await ExecuteBackupAsync(j.Name);
            }
        }

        private async Task ProcessJobAsync(BackupJob job)
        {
            var files = Directory.EnumerateFiles(job.SourceDir, "*", SearchOption.AllDirectories).ToList();
            long totalSize = files.Sum(f => new FileInfo(f).Length);
            int total = files.Count, left = total;

            foreach (string src in files)
            {
                if (PackageBlocker.IsBlocked(_settings))
                {
                    Environment.Exit(1);
                }
                string rel = Path.GetRelativePath(job.SourceDir, src);
                string dest = Path.Combine(job.TargetDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));

                var swCopy = System.Diagnostics.Stopwatch.StartNew();
                await Task.Run(() => File.Copy(src, dest, true));
                swCopy.Stop();

                int encMs = 0;
                if (_settings.CryptoExtensions.Contains(Path.GetExtension(src).ToLower()))
                {
                    encMs = CryptoSoftHelper.Encrypt(dest, _settings);
                    _ = CryptoSoftHelper.Encrypt(dest, _settings);
                }

                _logger.LogEvent(new LogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    JobName = job.Name,
                    SourcePath = src,
                    DestPath = dest,
                    FileSize = new FileInfo(src).Length,
                    TransferTimeMs = (int)swCopy.ElapsedMilliseconds,
                    EncryptionTimeMs = encMs
                });

                left--;
                Report(new StatusEntry
                {
                    Name = job.Name,
                    SourceFilePath = src,
                    TargetFilePath = dest,
                    State = "ACTIVE",
                    TotalFilesToCopy = total,
                    TotalFilesSize = totalSize,
                    NbFilesLeftToDo = left,
                    Progression = (total - left) / (double)total
                });
            }
        }

        private void Report(StatusEntry s)
        {
            _logger.UpdateStatus(s);
            StatusUpdated?.Invoke(s);
        }
    }
}
