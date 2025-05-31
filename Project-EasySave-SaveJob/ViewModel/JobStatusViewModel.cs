using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Timers;
using System.ComponentModel;
using System.Threading;
using Projet.Infrastructure;
using Projet.Model;

namespace Projet.ViewModel
{
    public class JobStatusViewModel : ViewModelBase, IDisposable
    {
        private readonly IPathProvider _pathProvider;
        private readonly StatusMonitor _statusMonitor;
        private Dictionary<string, StatusEntry> _statusCache = new Dictionary<string, StatusEntry>();
        private readonly System.Timers.Timer _uiUpdateTimer;
        private readonly Dictionary<string, double> _lastProgressValues = new Dictionary<string, double>();
        
        // Pour le lissage des animations
        private readonly Dictionary<string, double> _targetProgress = new Dictionary<string, double>();
        private readonly Dictionary<string, DateTime> _lastUpdateTime = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, bool> _isAnimating = new Dictionary<string, bool>();
        
        // Compteur pour forcer la relecture complète
        private int _forceRefreshCounter = 0;
        
        // Utilisé pour la synchronisation des mises à jour entre threads
        private readonly SynchronizationContext _syncContext;

        public JobStatusViewModel(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
            
            // Capturer le contexte de synchronisation courant
            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
            
            // Créer et démarrer le moniteur de statut dans son propre thread
            _statusMonitor = new StatusMonitor(pathProvider, 10); // Vérifier toutes les 10ms
            _statusMonitor.StatusChanged += OnStatusChanged;
            _statusMonitor.Start();
            
            // Créer un timer pour les mises à jour d'interface plus fréquentes
            _uiUpdateTimer = new System.Timers.Timer(8); // Rafraîchir l'UI à ~120 FPS
            _uiUpdateTimer.Elapsed += (s, e) => {
                // Forcer une relecture complète régulière du fichier JSON
                // C'est EXACTEMENT ce qui se passe lors d'une transition pause/resume
                _forceRefreshCounter++;
                
                // Forcer un refresh complet à intervalle régulier (comme lors des transitions pause/resume)
                if (_forceRefreshCounter % 10 == 0) // Toutes les 80ms (10 * 8ms)
                {
                    _statusMonitor.ForceRefresh(); // Relire le fichier JSON complètement
                }
                
                // Rafraîchir l'interface entre les lectures complètes
                RefreshJobsFromCache();
            };
            _uiUpdateTimer.Start();
            
            Console.WriteLine("JobStatusViewModel initialized with ultra-fast UI updates and forced refreshes");
        }

        // Appelé lorsque le moniteur détecte un changement de statut
        private void OnStatusChanged(Dictionary<string, StatusEntry> newStatus)
        {
            // Mettre à jour le cache avec les nouvelles valeurs (thread background)
            lock (_statusCache)
            {
                // IMPORTANT: Remplacer complètement le cache à chaque fois
                // pour imiter exactement ce qui se passe lors d'une transition pause/resume
                _statusCache = new Dictionary<string, StatusEntry>(newStatus);
                
                // Mettre à jour les cibles d'animation pour chaque job
                foreach (var entry in newStatus)
                {
                    string jobName = entry.Key;
                    
                    // Mise à jour des valeurs cibles pour l'animation fluide
                    double targetValue = Math.Min(100, Math.Max(0, entry.Value.Progression));
                    _targetProgress[jobName] = targetValue;
                    
                    // Si le job n'est pas encore dans notre liste, initialiser ses valeurs
                    if (!_lastProgressValues.ContainsKey(jobName))
                    {
                        _lastProgressValues[jobName] = 0;
                        _lastUpdateTime[jobName] = DateTime.Now;
                        _isAnimating[jobName] = true;
                    }
                    
                    // Pour les fins de job, mettre à jour immédiatement
                    if (entry.Value.State == "END" || entry.Value.State == "ERROR" || 
                        entry.Value.State == "CANCELLED")
                    {
                        _lastProgressValues[jobName] = 100;
                        _isAnimating[jobName] = false;
                    }
                    // Pour les jobs ACTIFS ou en ATTENTE, toujours animer
                    else if (entry.Value.State == "ACTIVE" || entry.Value.State == "PENDING")
                    {
                        _isAnimating[jobName] = true;
                        
                        // Si la progression cible est à 0, mettre une valeur minimale pour montrer une activité
                        if (targetValue < 0.1 && entry.Value.State == "ACTIVE")
                        {
                            _lastProgressValues[jobName] = Math.Max(_lastProgressValues[jobName], 0.5);
                        }
                    }
                }
            }
            
            // Forcer le rafraîchissement UI via le contexte de synchronisation
            _syncContext.Post(_ => {
                // Notifier le changement global pour forcer le rafraîchissement de l'UI
                NotifyPropertyChanged("StatusUpdated");
            }, null);
        }
        
        // Rafraîchit les jobs à partir du cache (appelé par le timer)
        private void RefreshJobsFromCache()
        {
            // Rien à faire si le cache est vide
            if (_statusCache.Count == 0)
                return;
                
            // Mettre à jour tous les jobs qui sont en cours d'animation
            lock (_statusCache)
            {
                foreach (var job in _isAnimating.Where(j => j.Value).ToList())
                {
                    string jobName = job.Key;
                    
                    // Si le job n'est plus dans le cache, arrêter l'animation
                    if (!_targetProgress.ContainsKey(jobName))
                    {
                        _isAnimating[jobName] = false;
                        continue;
                    }
                    
                    // Calculer le temps écoulé depuis la dernière mise à jour
                    double currentValue = _lastProgressValues[jobName];
                    double targetValue = _targetProgress[jobName];
                    
                    // Si on a atteint la cible, arrêter l'animation
                    if (Math.Abs(currentValue - targetValue) < 0.1)
                    {
                        _lastProgressValues[jobName] = targetValue;
                        continue;
                    }
                    
                    // Calculer une progression lissée avec animation plus rapide
                    double progress = currentValue + (targetValue - currentValue) * 0.25;
                    
                    // Assurer une progression minimale
                    if (targetValue > currentValue && progress < currentValue + 0.1)
                        progress = currentValue + 0.1;
                        
                    // Mettre à jour la valeur en cache
                    _lastProgressValues[jobName] = progress;
                    _lastUpdateTime[jobName] = DateTime.Now;
                }
            }
            
            // Utiliser le contexte de synchronisation pour les mises à jour UI
            _syncContext.Post(_ => {
                // Notifier le changement global pour forcer le rafraîchissement de l'UI
                NotifyPropertyChanged("StatusUpdated");
            }, null);
        }

        // Permet d'attacher un StatusEntry à un BackupJob
        public void ApplyStatus(BackupJob job)
        {
            if (job == null || string.IsNullOrEmpty(job.Name))
                return;

            // Store the current state before updating
            string previousState = job.State;
            
            // Obtenir le statut depuis le cache thread-safe
            StatusEntry status = null;
            double animatedProgress = 0;
            
            lock (_statusCache)
            {
                _statusCache.TryGetValue(job.Name, out status);
                _lastProgressValues.TryGetValue(job.Name, out animatedProgress);
            }

            if (status != null)
            {
                // CRUCIAL: Appliquer directement les valeurs du status.json
                // C'est exactement ce qui se passe lors d'une transition pause/resume
                
                // Mettre à jour l'état du job
                job.State = status.State;
                
                // Calculer une progression lissée
                double progressToDisplay;
                
                // Pour les états terminaux, mettre immédiatement à 100%
                if (status.State == "END" || status.State == "ERROR" || status.State == "CANCELLED")
                {
                    progressToDisplay = 100;
                    _isAnimating[job.Name] = false;
                }
                // Pour les jobs actifs, prendre la valeur directement du status.json
                // comme lors d'une transition pause/resume
                else
                {
                    // Utiliser directement la valeur du fichier status.json
                    progressToDisplay = status.Progression;
                    
                    // Ajouter une micro-variation pour forcer le rafraîchissement visuel
                    Random rnd = new Random();
                    progressToDisplay += rnd.NextDouble() * 0.001; // Variation imperceptible
                    
                    _isAnimating[job.Name] = true;
                }
                
                // Assurer que la valeur est dans les limites 0-100%
                progressToDisplay = Math.Min(100, Math.Max(0, progressToDisplay));
                
                // Mettre à jour la progression avec la valeur calculée
                job.Progression = progressToDisplay;
                
                // Mettre à jour les compteurs de fichiers directement depuis le status.json
                job.TotalFilesToCopy = status.TotalFilesToCopy;
                job.TotalFilesSize = status.TotalFilesSize;
                job.NbFilesLeftToDo = status.NbFilesLeftToDo;
                
                // CRUCIAL: Forcer le rafraîchissement complet des propriétés
                // exactement comme lors d'une transition pause/resume
                job.ForceProgressRefresh();
                
                // Utiliser le contexte de synchronisation pour les mises à jour UI
                _syncContext.Post(_ => {
                    job.ForceProgressRefresh(); // Appel supplémentaire sur le thread UI
                }, null);
            }
            else
            {
                // Si pas de statut, maintenir l'état actuel
                if (previousState != "END" && previousState != "CANCELLED")
                {
                    job.State = "READY";
                    job.Progression = 0;
                }
            }
        }

        public void Dispose()
        {
            _uiUpdateTimer?.Stop();
            _uiUpdateTimer?.Dispose();
            _statusMonitor?.Dispose();
        }
    }
} 