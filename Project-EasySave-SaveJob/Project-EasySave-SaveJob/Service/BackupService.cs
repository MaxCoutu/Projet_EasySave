using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Projet.Infrastructure;
using Projet.Model;

namespace Projet.Service
{
    public class BackupService : IBackupService, IDisposable
    {
        public event Action<StatusEntry> StatusUpdated;

        private readonly ILogger _logger;
        private readonly IJobRepository _repo;
        private readonly Settings _settings;
        private readonly List<BackupJob> _jobs;

        // File thread-safe pour les status
        private readonly BlockingCollection<StatusEntry> _statusQueue = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _statusReporterTask;

        public BackupService(ILogger logger, IJobRepository repo, Settings settings)
        {
            _logger = logger;
            _repo = repo;
            _settings = settings;
            _jobs = new List<BackupJob>(_repo.Load());

            // Lancer le thread de reporting
            _statusReporterTask = Task.Run(() => StatusReporterLoop(_cts.Token));
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
            if (PackageBlocker.IsBlocked(_settings))
            {
                Console.WriteLine("Blocked package running — job skipped.");
                return;
            }

            var job = _jobs.FirstOrDefault(j => j.Name == name);
            if (job == null)
            {
                Console.WriteLine($"Job '{name}' not found.");
                return;
            }

            EnqueueStatus(new StatusEntry { Name = job.Name, State = "PENDING" });

            try
            {
                await ProcessJobAsync(job);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'exécution du backup '{job.Name}' : {ex.Message}");
            }

            EnqueueStatus(new StatusEntry { Name = job.Name, State = "END" });
        }

        public async Task ExecuteAllBackupsAsync()
        {
            foreach (var job in _jobs)
            {
                if (PackageBlocker.IsBlocked(_settings)) break;
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

            try
            {
                var files = Directory.EnumerateFiles(cleanedSourceDir, "*", SearchOption.AllDirectories).ToList();
                long totalSize = files.Sum(f => new FileInfo(f).Length);
                long bytesCopied = 0;
                int total = files.Count, left = total;

                foreach (string src in files)
                {
                    if (PackageBlocker.IsBlocked(_settings))
                    {
                        Console.WriteLine("Backup interrompu : package bloqué.");
                        Environment.Exit(1);
                    }

                    string rel = Path.GetRelativePath(cleanedSourceDir, src);
                    string dest = Path.Combine(cleanedTargetDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));

                    var swCopy = System.Diagnostics.Stopwatch.StartNew();
                    long fileSize = new FileInfo(src).Length;

                    await CopyFileWithProgressAsync(src, dest, (copied, isFinal) =>
                    {
                        long currentBytesCopied = bytesCopied + copied;
                        double progression = totalSize > 0 ? (currentBytesCopied * 100.0) / totalSize : 100.0;
                        // On ne reporte que toutes les 500ms ou à la fin du fichier
                        if (isFinal || swCopy.ElapsedMilliseconds >= 500)
                        {
                            swCopy.Restart();
                            EnqueueStatus(new StatusEntry
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

                    int encMs = 0;
                    if (_settings.CryptoExtensions.Contains(Path.GetExtension(src).ToLower()))
                    {
                        encMs = CryptoSoftHelper.Encrypt(dest, _settings);
                    }

                    _logger.LogEvent(new LogEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        JobName = job.Name,
                        SourcePath = src,
                        DestPath = dest,
                        FileSize = fileSize,
                        TransferTimeMs = (int)swCopy.ElapsedMilliseconds,
                        EncryptionTimeMs = encMs
                    });

                    bytesCopied += fileSize;
                    left--;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new UnauthorizedAccessException($"Accès refusé au répertoire '{cleanedSourceDir}' : {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new IOException($"Erreur d'E/S dans '{cleanedSourceDir}' : {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"Erreur inattendue dans '{cleanedSourceDir}' : {ex.Message}", ex);
            }
        }

        // Callback : (octets copiés, bool isFinal)
        private async Task CopyFileWithProgressAsync(string src, string dst, Action<long, bool> progress)
        {
            const int bufferSize = 81920;
            long copied = 0;
            var lastReport = DateTime.UtcNow;
            using (var source = new FileStream(src, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true))
            using (var dest = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true))
            {
                var buffer = new byte[bufferSize];
                int read;
                while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await dest.WriteAsync(buffer, 0, read);
                    copied += read;
                    var now = DateTime.UtcNow;
                    if ((now - lastReport).TotalMilliseconds >= 500)
                    {
                        progress?.Invoke(copied, false);
                        lastReport = now;
                    }
                }
                // Report final à la fin du fichier
                progress?.Invoke(copied, true);
            }
        }

        // Ajoute un status à la file (thread-safe)
        private void EnqueueStatus(StatusEntry s)
        {
            _statusQueue.Add(s);
        }

        // Thread de reporting : consomme la file et écrit le status
        private void StatusReporterLoop(CancellationToken token)
        {
            try
            {
                foreach (var status in _statusQueue.GetConsumingEnumerable(token))
                {
                    _logger.UpdateStatus(status);
                    StatusUpdated?.Invoke(status);
                    // Optionnel : Thread.Sleep(50); // pour lisser la charge disque
                }
            }
            catch (OperationCanceledException)
            {
                // Arrêt normal
            }
        }

        // Pour bien libérer le thread à la fermeture
        public void Dispose()
        {
            _cts.Cancel();
            _statusQueue.CompleteAdding();
            try { _statusReporterTask.Wait(); } catch { }
            _cts.Dispose();
            _statusQueue.Dispose();
        }
    }
}

