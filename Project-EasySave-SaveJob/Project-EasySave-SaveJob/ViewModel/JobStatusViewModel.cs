using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Timers;
using System.ComponentModel;
using System.Threading;
using Projet.Infrastructure;
using Projet.Model;

namespace Projet.ViewModel
{
    public class JobStatusViewModel : ViewModelBase
    {
        private readonly System.Timers.Timer _refreshTimer;
        private readonly IPathProvider _pathProvider;
        private ConcurrentDictionary<string, StatusEntry> _statusCache = new ConcurrentDictionary<string, StatusEntry>();
        private readonly SemaphoreSlim _statusRefreshLock = new SemaphoreSlim(1, 1);

        public JobStatusViewModel(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            
            // Configurer un timer pour rafraîchir automatiquement l'état des tâches
            _refreshTimer = new System.Timers.Timer(500);
            _refreshTimer.Elapsed += (s, e) => RefreshStatus();
            _refreshTimer.Start();
        }

        // Permet d'attacher un StatusEntry à un BackupJob
        public void ApplyStatus(BackupJob job)
        {
            if (job == null || string.IsNullOrEmpty(job.Name))
                return;

            // Nous n'avons plus besoin d'appeler RefreshStatus() à chaque fois, 
            // car le timer s'en charge périodiquement

            if (_statusCache.TryGetValue(job.Name, out StatusEntry status))
            {
                // Transférer les propriétés du StatusEntry vers le BackupJob
                job.State = status.State;
                job.Progression = status.Progression;
                job.TotalFilesToCopy = status.TotalFilesToCopy;
                job.TotalFilesSize = status.TotalFilesSize;
                job.NbFilesLeftToDo = status.NbFilesLeftToDo;
            }
            else
            {
                // Si aucun statut disponible, mettre les valeurs par défaut
                job.State = "END";
                job.Progression = 0;
                job.TotalFilesToCopy = 0;
                job.TotalFilesSize = 0;
                job.NbFilesLeftToDo = 0;
            }
        }

        private async void RefreshStatus()
        {
            // Use semaphore to prevent multiple simultaneous refreshes
            if (!await _statusRefreshLock.WaitAsync(0))  // Don't wait if locked
                return;
                
            try
            {
                string statusPath = Path.Combine(_pathProvider.GetStatusDir(), "status.json");
                if (!File.Exists(statusPath))
                    return;

                string json = File.ReadAllText(statusPath);
                if (string.IsNullOrWhiteSpace(json))
                    return;

                List<StatusEntry> entries = JsonSerializer.Deserialize<List<StatusEntry>>(json);
                if (entries == null)
                    return;

                // Create a new dictionary to avoid potential issues with concurrent access
                var newStatusCache = new ConcurrentDictionary<string, StatusEntry>();
                
                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry.Name))
                        newStatusCache[entry.Name] = entry;
                }
                
                // Swap the reference atomically
                _statusCache = newStatusCache;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du rafraîchissement du statut : {ex.Message}");
            }
            finally
            {
                _statusRefreshLock.Release();
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _statusRefreshLock?.Dispose();
        }
    }
} 