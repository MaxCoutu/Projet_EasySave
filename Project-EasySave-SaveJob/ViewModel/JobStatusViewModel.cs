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
        private Dictionary<string, DateTime> _lastJobUpdate = new Dictionary<string, DateTime>();
        private DateTime _lastFileModified = DateTime.MinValue;
        private int _consecutiveEmptyReads = 0;

        public JobStatusViewModel(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            
            // Configurer un timer pour rafraîchir automatiquement l'état des tâches
            // Réduire l'intervalle à 100ms pour une mise à jour plus fréquente
            _refreshTimer = new Timer(100);
            _refreshTimer.Elapsed += (s, e) => RefreshStatus();
            _refreshTimer.Start();
            
            // Force une première lecture immédiate
            RefreshStatus();
        }

        // Permet d'attacher un StatusEntry à un BackupJob
        public void ApplyStatus(BackupJob job)
        {
            if (job == null || string.IsNullOrEmpty(job.Name))
                return;

            // Store the current state before updating
            string previousState = job.State;
            double previousProgress = job.Progression;
            
            // Check if we need to preserve this state
            bool preserveState = previousState == "END" || previousState == "CANCELLED";
            
            // Refresh status data
            RefreshStatus();

            if (_statusCache.TryGetValue(job.Name, out StatusEntry status))
            {
                // Check if this status update is recent enough
                bool isRecentUpdate = IsRecentUpdate(job.Name, status);
                
                // Log status data for debugging
                Console.WriteLine($"Applying status for job '{job.Name}': State={status.State}, Progress={status.Progression:F2}%, Recent={isRecentUpdate}");
                
                // If job was manually stopped/cancelled and we want to preserve that state
                if (preserveState && (status.State == "READY" || string.IsNullOrEmpty(status.State)))
                {
                    // Preserve the END state but update other properties
                    job.Progression = Math.Min(100, Math.Max(0, status.Progression));
                    job.TotalFilesToCopy = status.TotalFilesToCopy;
                    job.TotalFilesSize = status.TotalFilesSize;
                    job.NbFilesLeftToDo = status.NbFilesLeftToDo;
                    
                    // Log this special case
                    Console.WriteLine($"Preserving '{previousState}' state for job '{job.Name}' instead of '{status.State}'");
                }
                else
                {
                    // Validate progression value before updating
                    double newProgress = Math.Min(100, Math.Max(0, status.Progression));
                    
                    // Check for invalid progress jumps (prevent sudden jumps to high values)
                    if (status.State == "ACTIVE" && previousState == "ACTIVE" && 
                        newProgress > previousProgress + 20 && previousProgress > 0)
                    {
                        // If progress jumps by more than 20% in one update, it might be an error
                        Console.WriteLine($"Warning: Large progress jump detected for job '{job.Name}': {previousProgress}% -> {newProgress}%");
                        
                        // Use a more reasonable progress value (previous + small increment)
                        newProgress = Math.Min(100, previousProgress + 2);
                        Console.WriteLine($"Limiting progress to {newProgress}%");
                    }
                    
                    // If job is complete (100%), ensure state is END
                    if (newProgress >= 99.9 && status.State == "ACTIVE")
                    {
                        Console.WriteLine($"Job '{job.Name}' reached 100% progress, setting state to END");
                        job.State = "END";
                        job.Progression = 100;
                    }
                    else
                    {
                        // Transfer all properties from the StatusEntry to the BackupJob
                        job.State = status.State;
                        job.Progression = newProgress;
                    }
                    
                    job.TotalFilesToCopy = status.TotalFilesToCopy;
                    job.TotalFilesSize = status.TotalFilesSize;
                    job.NbFilesLeftToDo = status.NbFilesLeftToDo;
                    
                    // Log significant state changes
                    if (previousState != job.State)
                    {
                        Console.WriteLine($"Job '{job.Name}' state changed: {previousState} -> {job.State}");
                    }
                    
                    // Log significant progress changes
                    if (Math.Abs(previousProgress - job.Progression) > 1)
                    {
                        Console.WriteLine($"Job '{job.Name}' progress changed: {previousProgress:F2}% -> {job.Progression:F2}%");
                    }
                    
                    // Update last update time for this job
                    _lastJobUpdate[job.Name] = DateTime.Now;
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
                
                // Keep previous progress if job is complete
                if (job.State != "END" && job.State != "CANCELLED")
                {
                    job.Progression = 0;
                }
                
                job.TotalFilesToCopy = 0;
                job.TotalFilesSize = 0;
                job.NbFilesLeftToDo = 0;
            }
        }

        private bool IsRecentUpdate(string jobName, StatusEntry status)
        {
            // If we have a timestamp in the status, use it
            if (status.Timestamp != default)
            {
                // Consider updates within the last 5 seconds as recent
                return (DateTime.Now - status.Timestamp).TotalSeconds <= 5;
            }
            
            // If we have a record of the last time we updated this job, check that
            if (_lastJobUpdate.TryGetValue(jobName, out DateTime lastUpdate))
            {
                // Consider updates within the last 5 seconds as recent
                return (DateTime.Now - lastUpdate).TotalSeconds <= 5;
            }
            
            // If we have no record, assume it's recent
            return true;
        }

        private void RefreshStatus()
        {
            try
            {
                string statusPath = Path.Combine(_pathProvider.GetStatusDir(), "status.json");
                if (!File.Exists(statusPath))
                {
                    _consecutiveEmptyReads++;
                    if (_consecutiveEmptyReads > 5)
                    {
                        Console.WriteLine("Status file does not exist after multiple attempts");
                    }
                    return;
                }
                
                // Vérifier si le fichier a été modifié depuis la dernière lecture
                var fileInfo = new FileInfo(statusPath);
                if (fileInfo.LastWriteTime <= _lastFileModified && _statusCache.Count > 0)
                {
                    // Le fichier n'a pas changé, pas besoin de le relire
                    return;
                }
                
                _lastFileModified = fileInfo.LastWriteTime;

                // Essayer de lire le fichier avec un accès partagé
                string json = "";
                int retryCount = 0;
                bool success = false;
                
                while (!success && retryCount < 3)
                {
                    try
                    {
                        using (var fileStream = new FileStream(statusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (var reader = new StreamReader(fileStream))
                        {
                            json = reader.ReadToEnd();
                        }
                        success = true;
                    }
                    catch (IOException)
                    {
                        // File might be locked by another process, retry after a short delay
                        retryCount++;
                        System.Threading.Thread.Sleep(50);
                    }
                }
                
                if (!success)
                {
                    Console.WriteLine("Failed to read status file after multiple attempts");
                    return;
                }
                
                if (string.IsNullOrWhiteSpace(json))
                {
                    _consecutiveEmptyReads++;
                    if (_consecutiveEmptyReads > 5)
                    {
                        Console.WriteLine("Status file is empty after multiple attempts");
                    }
                    return;
                }
                
                _consecutiveEmptyReads = 0; // Reset counter on successful read

                try
                {
                    var entries = JsonSerializer.Deserialize<List<StatusEntry>>(json);
                    if (entries == null)
                        return;

                    // Mettre à jour le cache de statut
                    var newCache = new Dictionary<string, StatusEntry>();
                    foreach (var entry in entries)
                    {
                        if (!string.IsNullOrEmpty(entry.Name))
                        {
                            // Ensure progression is within bounds
                            entry.Progression = Math.Min(100, Math.Max(0, entry.Progression));
                            
                            // Add to new cache
                            newCache[entry.Name] = entry;
                            
                            // Log status
                            Console.WriteLine($"Read status for job '{entry.Name}': State={entry.State}, Progress={entry.Progression:F2}%");
                        }
                    }
                    
                    // Replace cache with new data
                    _statusCache = newCache;
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Error parsing status JSON: {ex.Message}");
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