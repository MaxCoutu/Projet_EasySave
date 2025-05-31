using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Moniteur de statut qui fonctionne dans son propre thread et surveille le fichier status.json
    /// </summary>
    public class StatusMonitor : IDisposable
    {
        // Événement déclenché lorsque le statut change
        public event Action<Dictionary<string, StatusEntry>> StatusChanged;
        
        private readonly string _statusFilePath;
        private Dictionary<string, StatusEntry> _statusCache = new Dictionary<string, StatusEntry>();
        private bool _isRunning = false;
        private Thread _monitorThread;
        private readonly int _refreshIntervalMs;
        private long _lastFileSize = 0;
        private DateTime _lastFileWriteTime = DateTime.MinValue;
        private DateTime _lastNotificationTime = DateTime.MinValue;
        private readonly Random _random = new Random();
        
        public StatusMonitor(IPathProvider pathProvider, int refreshIntervalMs = 10) // Réduit à 10ms pour une réactivité maximale
        {
            _statusFilePath = Path.Combine(pathProvider.GetStatusDir(), "status.json");
            _refreshIntervalMs = refreshIntervalMs;
        }
        
        /// <summary>
        /// Démarre le moniteur dans son propre thread
        /// </summary>
        public void Start()
        {
            if (_isRunning)
                return;
                
            _isRunning = true;
            _monitorThread = new Thread(MonitorLoop)
            {
                IsBackground = true,
                Name = "StatusMonitorThread",
                Priority = ThreadPriority.Highest // Priorité maximale pour ne pas rater de mises à jour
            };
            _monitorThread.Start();
            
            Console.WriteLine($"StatusMonitor started in background thread (refresh: {_refreshIntervalMs}ms) with highest priority");
        }
        
        /// <summary>
        /// Arrête le moniteur
        /// </summary>
        public void Stop()
        {
            _isRunning = false;
            
            // Attendre que le thread se termine
            _monitorThread?.Join(1000);
            
            Console.WriteLine("StatusMonitor stopped");
        }
        
        /// <summary>
        /// Boucle principale du moniteur qui s'exécute dans un thread séparé
        /// </summary>
        private void MonitorLoop()
        {
            while (_isRunning)
            {
                try
                {
                    // Vérifier si le fichier existe
                    if (File.Exists(_statusFilePath))
                    {
                        // LECTURE INCONDITIONNELLE du fichier à chaque vérification
                        // Exactement comme ce qui se passe lors d'une transition pause/resume
                        
                        try
                        {
                            // Lire le contenu complet du fichier
                            string json = File.ReadAllText(_statusFilePath);
                            
                            if (!string.IsNullOrWhiteSpace(json))
                            {
                                // Désérialiser les entrées
                                var entries = JsonSerializer.Deserialize<List<StatusEntry>>(json);
                                
                                if (entries != null)
                                {
                                    // Convertir en dictionnaire
                                    var newCache = new Dictionary<string, StatusEntry>();
                                    
                                    foreach (var entry in entries)
                                    {
                                        if (!string.IsNullOrEmpty(entry.Name))
                                        {
                                            // Valider les valeurs
                                            entry.Progression = Math.Min(100, Math.Max(0, entry.Progression));
                                            
                                            // Ajouter au cache
                                            newCache[entry.Name] = entry;
                                        }
                                    }
                                    
                                    // NOTIFIER TOUJOURS, même si rien n'a changé
                                    // C'est exactement ce qui se passe lors d'une transition pause/resume
                                    
                                    // Mettre à jour le cache et notifier les abonnés
                                    _statusCache = newCache;
                                    StatusChanged?.Invoke(newCache);
                                    _lastNotificationTime = DateTime.Now;
                                }
                            }
                        }
                        catch (IOException)
                        {
                            // Fichier probablement en cours d'écriture, on réessaiera à la prochaine itération
                        }
                        catch (JsonException)
                        {
                            // Fichier JSON mal formé, on réessaiera à la prochaine itération
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error reading status file: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in StatusMonitor: {ex.Message}");
                }
                
                // Attendre TRÈS peu avant la prochaine vérification
                // Pour un rafraîchissement ultra-fluide
                Thread.Sleep(5); // Vérifier toutes les 5ms (200 fois par seconde)
            }
        }
        
        /// <summary>
        /// Obtient le statut actuel pour un job spécifique
        /// </summary>
        public StatusEntry GetStatus(string jobName)
        {
            if (string.IsNullOrEmpty(jobName))
                return null;
                
            if (_statusCache.TryGetValue(jobName, out StatusEntry entry))
                return entry;
                
            return null;
        }
        
        /// <summary>
        /// Obtient tous les statuts actuels
        /// </summary>
        public Dictionary<string, StatusEntry> GetAllStatus()
        {
            return new Dictionary<string, StatusEntry>(_statusCache);
        }
        
        /// <summary>
        /// Force une relecture du fichier status.json
        /// Cette méthode simule exactement ce qui se passe lors d'une transition pause/resume
        /// </summary>
        public void ForceRefresh()
        {
            try
            {
                if (File.Exists(_statusFilePath))
                {
                    string json = File.ReadAllText(_statusFilePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var entries = JsonSerializer.Deserialize<List<StatusEntry>>(json);
                        if (entries != null)
                        {
                            var newCache = new Dictionary<string, StatusEntry>();
                            foreach (var entry in entries)
                            {
                                if (!string.IsNullOrEmpty(entry.Name))
                                {
                                    newCache[entry.Name] = entry;
                                }
                            }
                            
                            _statusCache = newCache;
                            StatusChanged?.Invoke(newCache);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error forcing refresh: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            Stop();
        }
    }
} 