using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;

namespace Projet.ViewModel
{
    public class ChooseJobViewModel : ViewModelBase, IDisposable
    {
        private readonly IBackupService _svc;
        private readonly IPathProvider _pathProvider;
        private string _statusFilePath;
        private CancellationTokenSource _refreshTokenSource;
        private Task _autoRefreshTask;
        
        public ObservableCollection<BackupJob> Jobs { get; }
        public ICommand RunJobCmd { get; }
        public ICommand RemoveJobCmd { get; }
        
        // Pour simuler le comportement pause/resume
        private int _forceRefreshCounter = 0;
        private Random _random = new Random();

        public ChooseJobViewModel(IBackupService svc)
        {
            _svc = svc ?? throw new ArgumentNullException(nameof(svc));
            Jobs = new ObservableCollection<BackupJob>(_svc.GetJobs());
            
            // Obtenir le chemin du fichier status.json directement
            try
            {
                // On peut utiliser un chemin fixe qui correspond à la structure du projet
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string easySavePath = Path.Combine(appDataPath, "EasySave");
                string statusDir = Path.Combine(easySavePath, "status");
                _statusFilePath = Path.Combine(statusDir, "status.json");
                
                // S'assurer que le répertoire existe
                if (!Directory.Exists(statusDir))
                {
                    Console.WriteLine($"Status directory not found: {statusDir}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting status file path: {ex.Message}");
            }

            RunJobCmd = new RelayCommand(async param =>
            {
                if (param is BackupJob job && !string.IsNullOrWhiteSpace(job.Name))
                {
                    await _svc.ExecuteBackupAsync(job.Name);
                    
                    // Démarrer le rafraîchissement automatique après avoir lancé un job
                    StartAutoRefresh();
                }
            });

            RemoveJobCmd = new RelayCommand(param =>
            {
                if (param is BackupJob job && !string.IsNullOrWhiteSpace(job.Name))
                {
                    _svc.RemoveJob(job.Name);
                    RefreshJobs();
                }
            });
            
            // Démarrer le rafraîchissement automatique de l'interface
            StartAutoRefresh();
        }
        
        // Démarre un thread séparé pour forcer le rafraîchissement continu des données
        private void StartAutoRefresh()
        {
            // Arrêter un éventuel thread existant
            StopAutoRefresh();
            
            // Créer un nouveau token de cancellation
            _refreshTokenSource = new CancellationTokenSource();
            var token = _refreshTokenSource.Token;
            
            // Démarrer une tâche de rafraîchissement automatique
            _autoRefreshTask = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        // Vérifier s'il y a des jobs actifs qui nécessitent un rafraîchissement
                        bool hasActiveJobs = false;
                        
                        // Cette lecture est thread-safe car ObservableCollection est synchronisé en lecture
                        foreach (var job in Jobs)
                        {
                            if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                            {
                                hasActiveJobs = true;
                                break;
                            }
                        }
                        
                        if (hasActiveJobs)
                        {
                            // Fréquence de rafraîchissement plus élevée pour les jobs actifs
                            await Task.Delay(50, token); // 20 fois par seconde
                            
                            // Forcer une mise à jour depuis le thread de l'interface
                            SynchronizationContext.Current?.Post(_ => 
                            {
                                ForceProgressUpdate();
                                
                                // Forcer les jobs à notifier leurs changements
                                foreach (var job in Jobs)
                                {
                                    if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                                    {
                                        job.RefreshCounter++;
                                        job.ForceProgressRefresh();
                                    }
                                }
                                
                                // Notifier un changement global qui forcera la mise à jour des vues
                                NotifyPropertyChanged("Jobs");
                            }, null);
                        }
                        else
                        {
                            // Fréquence réduite s'il n'y a pas de jobs actifs
                            await Task.Delay(500, token); // Toutes les 500ms
                            
                            // Rafraîchir la liste des jobs au cas où
                            SynchronizationContext.Current?.Post(_ => RefreshJobs(), null);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Tâche annulée normalement, rien à faire
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in auto-refresh task: {ex.Message}");
                }
            }, token);
        }
        
        // Arrête le thread de rafraîchissement automatique
        private void StopAutoRefresh()
        {
            _refreshTokenSource?.Cancel();
            _refreshTokenSource?.Dispose();
            _refreshTokenSource = null;
        }

        public void RefreshJobs()
        {
            Jobs.Clear();
            foreach (var job in _svc.GetJobs())
                Jobs.Add(job);
            
            // Notifier un changement global qui forcera la mise à jour de l'interface
            NotifyPropertyChanged("Jobs");
        }
        
        /// <summary>
        /// Force une relecture complète du fichier status.json exactement comme
        /// ce qui se passe lors d'une transition pause/resume
        /// </summary>
        public void ForceRefreshFromJsonFile()
        {
            try
            {
                // Si le chemin n'est pas défini ou le fichier n'existe pas, sortir
                if (string.IsNullOrEmpty(_statusFilePath) || !File.Exists(_statusFilePath))
                    return;
                
                // Lire le contenu du fichier status.json
                string json = File.ReadAllText(_statusFilePath);
                if (string.IsNullOrWhiteSpace(json))
                    return;
                
                // Désérialiser les entrées
                var statusEntries = JsonSerializer.Deserialize<List<StatusEntry>>(json);
                if (statusEntries == null)
                    return;
                
                // Mettre à jour tous les jobs avec les données fraîches du fichier
                foreach (var job in Jobs)
                {
                    // Trouver l'entrée correspondante
                    var entry = statusEntries.Find(e => e.Name == job.Name);
                    
                    if (entry != null)
                    {
                        // COMPORTEMENT CRUCIAL: Appliquer directement les valeurs
                        // du fichier status.json aux objets BackupJob
                        // C'est exactement ce qui se passe lors d'une transition pause/resume
                        
                        // Mettre à jour l'état
                        job.State = entry.State;
                        
                        // Mettre à jour la progression
                        job.Progression = Math.Min(100, Math.Max(0, entry.Progression));
                        
                        // Mettre à jour les autres propriétés
                        job.TotalFilesToCopy = entry.TotalFilesToCopy;
                        job.TotalFilesSize = entry.TotalFilesSize;
                        job.NbFilesLeftToDo = entry.NbFilesLeftToDo;
                        
                        // Forcer un rafraîchissement complet des propriétés
                        job.ForceProgressRefresh();
                        
                        // Augmenter le compteur de rafraîchissement pour forcer l'actualisation
                        job.RefreshCounter++;
                    }
                }
                
                // Notifier un changement global qui forcera la mise à jour des vues
                NotifyPropertyChanged("Jobs");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading status file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Méthode pour forcer une relecture complète des données de progression
        /// Cette méthode simule exactement ce qui se passe lors d'une transition pause/resume
        /// </summary>
        public void ForceProgressUpdate()
        {
            // Appeler la nouvelle méthode qui lit directement le fichier status.json
            ForceRefreshFromJsonFile();
            
            _forceRefreshCounter++;
            
            // Forcer un rafraîchissement complet de tous les jobs
            foreach (var job in Jobs)
            {
                // Simuler exactement ce qui se passe lors d'une transition pause/resume
                if (job.State == "ACTIVE" || job.State == "PENDING" || job.State == "PAUSED")
                {
                    // Forcer un rafraîchissement complet
                    job.ForceProgressRefresh();
                    
                    // Augmenter le compteur de rafraîchissement pour forcer l'actualisation
                    job.RefreshCounter++;
                }
            }
            
            // Notifier un changement global qui forcera la mise à jour des vues
            NotifyPropertyChanged("Jobs");
        }
        
        public void Dispose()
        {
            StopAutoRefresh();
        }
    }
}