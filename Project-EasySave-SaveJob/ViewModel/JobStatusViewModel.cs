using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Timers;
using System.ComponentModel;
using Projet.Infrastructure;
using Projet.Model;

namespace Projet.ViewModel
{
    public class JobStatusViewModel : ViewModelBase
    {
        private readonly Timer _refreshTimer;
        private readonly IPathProvider _pathProvider;
        private Dictionary<string, StatusEntry> _statusCache = new Dictionary<string, StatusEntry>();

        public JobStatusViewModel(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            
            // Configurer un timer pour rafraîchir automatiquement l'état des tâches
            _refreshTimer = new Timer(500);
            _refreshTimer.Elapsed += (s, e) => RefreshStatus();
            _refreshTimer.Start();
        }

        // Permet d'attacher un StatusEntry à un BackupJob
        public void ApplyStatus(BackupJob job)
        {
            if (job == null || string.IsNullOrEmpty(job.Name))
                return;

            // Store the current state before updating
            string previousState = job.State;
            
            // Check if we need to preserve this state
            bool preserveState = previousState == "END" || previousState == "CANCELLED";
            
            // Refresh status data
            RefreshStatus();

            if (_statusCache.TryGetValue(job.Name, out StatusEntry status))
            {
                // If job was manually stopped/cancelled and we want to preserve that state
                if (preserveState && (status.State == "READY" || string.IsNullOrEmpty(status.State)))
                {
                    // Preserve the END state but update other properties
                    job.Progression = status.Progression;
                    job.TotalFilesToCopy = status.TotalFilesToCopy;
                    job.TotalFilesSize = status.TotalFilesSize;
                    job.NbFilesLeftToDo = status.NbFilesLeftToDo;
                    
                    // Log this special case
                    Console.WriteLine($"Preserving '{previousState}' state for job '{job.Name}' instead of '{status.State}'");
                }
                else
                {
                    // Transfer all properties from the StatusEntry to the BackupJob
                    job.State = status.State;
                    job.Progression = status.Progression;
                    job.TotalFilesToCopy = status.TotalFilesToCopy;
                    job.TotalFilesSize = status.TotalFilesSize;
                    job.NbFilesLeftToDo = status.NbFilesLeftToDo;
                }
            }
            else
            {
                // If no status is available, use default values
                // But only change state if we're not preserving a special state
                if (!preserveState)
                {
                    job.State = "READY"; // Changed from END to READY to ensure play button appears
                }
                
                job.Progression = 0;
                job.TotalFilesToCopy = 0;
                job.TotalFilesSize = 0;
                job.NbFilesLeftToDo = 0;
            }
        }

        private void RefreshStatus()
        {
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

                // Mettre à jour le cache de statut
                _statusCache.Clear();
                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry.Name))
                        _statusCache[entry.Name] = entry;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du rafraîchissement du statut : {ex.Message}");
            }
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
        }
    }
} 