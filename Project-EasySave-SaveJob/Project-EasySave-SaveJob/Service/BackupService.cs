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
            
            // Debug output to verify if jobs are loaded
            Console.WriteLine($"BackupService initialized with {_jobs.Count} jobs:");
            foreach (var job in _jobs)
            {
                Console.WriteLine($"  - Job: {job.Name}, Source: {job.SourceDir}, Target: {job.TargetDir}");
            }
        }

        public void AddJob(BackupJob job)
        {
            _jobs.Add(job);
            _repo.Save(_jobs);
            Console.WriteLine($"Added job: {job.Name}");
        }

        public void RemoveJob(string name)
        {
            _jobs.RemoveAll(j => j.Name == name);
            _repo.Save(_jobs);
        }

        public IReadOnlyList<BackupJob> GetJobs() => _jobs.AsReadOnly();

        public async Task ExecuteBackupAsync(string name)
        {
            Console.WriteLine($"Starting backup for job: {name}");
            
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

            Report(new StatusEntry { Name = job.Name, State = "PENDING" });

            try
            {
                await ProcessJobAsync(job);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de l'exécution du backup '{job.Name}' : {ex.Message}");
            }

            Report(new StatusEntry { Name = job.Name, State = "END" });
        }

        public async Task ExecuteAllBackupsAsync()
        {
            Console.WriteLine($"Starting all backups. Total jobs: {_jobs.Count}");
            foreach (var job in _jobs)
            {
                if (PackageBlocker.IsBlocked(_settings)) break;
                await ExecuteBackupAsync(job.Name);
            }
        }

        private async Task ProcessJobAsync(BackupJob job)
        {
            // Nettoyer les chemins
            string cleanedSourceDir = job.SourceDir.Trim('"').Trim();
            string cleanedTargetDir = job.TargetDir.Trim('"').Trim();
            
            Console.WriteLine($"Processing job: {job.Name}");
            Console.WriteLine($"Source dir: {cleanedSourceDir}");
            Console.WriteLine($"Target dir: {cleanedTargetDir}");

            // Vérifier l'existence du répertoire source
            if (!Directory.Exists(cleanedSourceDir))
            {
                Console.WriteLine($"Source directory does not exist: {cleanedSourceDir}");
                throw new DirectoryNotFoundException($"Le répertoire source '{cleanedSourceDir}' n'existe pas.");
            }

            // Créer le répertoire cible s'il n'existe pas
            if (!Directory.Exists(cleanedTargetDir))
            {
                Console.WriteLine($"Creating target directory: {cleanedTargetDir}");
                Directory.CreateDirectory(cleanedTargetDir);
            }

            try
            {
                var files = Directory.EnumerateFiles(cleanedSourceDir, "*", SearchOption.AllDirectories).ToList();
                long totalSize = files.Sum(f => new FileInfo(f).Length);
                int total = files.Count, left = total;
                
                Console.WriteLine($"Found {total} files to copy, total size: {totalSize} bytes");

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
                    
                    Console.WriteLine($"Copying: {src} -> {dest}");

                    var swCopy = System.Diagnostics.Stopwatch.StartNew();
                    await Task.Run(() => File.Copy(src, dest, true));
                    swCopy.Stop();
                    
                    Console.WriteLine($"Copy completed in {swCopy.ElapsedMilliseconds}ms");

                    int encMs = 0;
                    if (_settings.CryptoExtensions.Contains(Path.GetExtension(src).ToLower()))
                    {
                        Console.WriteLine($"Encrypting file: {dest}");
                        encMs = CryptoSoftHelper.Encrypt(dest, _settings);
                        Console.WriteLine($"Encryption completed in {encMs}ms");
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
                    
                    Console.WriteLine($"Progress: {(total - left)}/{total} files ({(total - left) / (double)total:P0})");
                }
                
                Console.WriteLine($"Backup job completed successfully: {job.Name}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Access denied: {ex.Message}");
                throw new UnauthorizedAccessException($"Accès refusé au répertoire '{cleanedSourceDir}' : {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"I/O error: {ex.Message}");
                throw new IOException($"Erreur d'E/S dans '{cleanedSourceDir}' : {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error: {ex.Message}");
                throw new Exception($"Erreur inattendue dans '{cleanedSourceDir}' : {ex.Message}", ex);
            }
        }

        private void Report(StatusEntry s)
        {
            _logger.UpdateStatus(s);
            StatusUpdated?.Invoke(s);
        }
    }
}