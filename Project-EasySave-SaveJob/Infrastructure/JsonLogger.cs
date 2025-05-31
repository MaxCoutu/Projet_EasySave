using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Timers;

namespace Projet.Infrastructure
{
    public class JsonLogger : ILogger, IDisposable
    {
        private readonly string _logDir;
        private readonly string _statusDir;
        private readonly string _statusFilePath;
        private readonly object _writeLock = new object();
        private Dictionary<string, StatusEntry> _statusCache = new Dictionary<string, StatusEntry>();
        private bool _statusCacheDirty = false;
        private System.Timers.Timer _flushTimer;
        private DateTime _lastWriteTime = DateTime.MinValue;
        private readonly Random _random = new Random();
        
        public JsonLogger(IPathProvider paths)
        {
            _logDir = paths.GetLogDir();
            _statusDir = paths.GetStatusDir();
            _statusFilePath = Path.Combine(_statusDir, "status.json");
            
            // Ensure directories exist
            Directory.CreateDirectory(_logDir);
            Directory.CreateDirectory(_statusDir);
            
            // Initialize status file if it doesn't exist
            InitializeStatusFile();
            
            // Configurer un timer pour l'écriture périodique des statuts (beaucoup plus fréquent)
            _flushTimer = new System.Timers.Timer(16); // 60fps pour une expérience ultra-fluide
            _flushTimer.Elapsed += (s, e) => FlushStatusCache();
            _flushTimer.Start();
            
            Console.WriteLine("JsonLogger initialized with 16ms flush interval (60 FPS updates)");
        }

        public void LogEvent(LogEntry entry)
        {
            if (entry == null)
                return;
                
            try
            {
                string file = Path.Combine(_logDir, $"{DateTime.UtcNow:yyyyMMdd}.log.json");
                AppendJson(file, entry);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging event: {ex.Message}");
            }
        }

        public void UpdateStatus(StatusEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                return;

            // Make sure timestamp is set
            if (entry.Timestamp == default)
                entry.Timestamp = DateTime.Now;
            
            // Ensure values are valid
            entry.TotalFilesToCopy = Math.Max(0, entry.TotalFilesToCopy);
            entry.NbFilesLeftToDo = Math.Max(0, entry.NbFilesLeftToDo);
            entry.Progression = Math.Min(100, Math.Max(0, entry.Progression));
            
            // Ajouter une micro-variation pour forcer le rafraîchissement visuel
            // comme avec pause/resume
            if (entry.State == "ACTIVE" || entry.State == "PENDING" || entry.State == "PAUSED")
            {
                // Ajouter une micro-variation à la progression (même technique que pause/resume)
                double microVariation = _random.NextDouble() * 0.001;
                entry.Progression += microVariation;
            }
            
            // CHANGEMENT CRITIQUE: Nous voulons maintenant écrire IMMÉDIATEMENT dans le fichier
            // à chaque mise à jour, exactement comme ce qui se passe lors d'une transition pause/resume
            
            try
            {
                lock (_writeLock)
                {
                    // Mettre à jour le cache
                    _statusCache[entry.Name] = entry;
                    
                    // Convertir tous les statuts en liste
                    var allEntries = new List<StatusEntry>(_statusCache.Values);
                    
                    // Sérialiser en JSON
                    string json = JsonSerializer.Serialize(allEntries, new JsonSerializerOptions 
                    { 
                        WriteIndented = false // Compact pour des écritures plus rapides
                    });
                    
                    // ÉCRIRE IMMÉDIATEMENT dans le fichier
                    File.WriteAllText(_statusFilePath, json);
                    _lastWriteTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing status file: {ex.Message}");
            }
        }
        
        private void FlushStatusCache()
        {
            bool needsFlush = false;
            List<StatusEntry> entriesToWrite = null;
            
            lock (_writeLock)
            {
                if (_statusCacheDirty && _statusCache.Count > 0)
                {
                    needsFlush = true;
                    entriesToWrite = new List<StatusEntry>(_statusCache.Values);
                    _statusCacheDirty = false;
                }
            }
            
            if (needsFlush && entriesToWrite != null)
            {
                try
                {
                    // Sérialiser les données
                    string json = JsonSerializer.Serialize(entriesToWrite, new JsonSerializerOptions 
                    { 
                        WriteIndented = false // Plus compact pour des écritures plus rapides
                    });
                    
                    // Écrire dans le fichier
                    File.WriteAllText(_statusFilePath, json);
                    _lastWriteTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing status file: {ex.Message}");
                    
                    // Remarquer comme sale pour réessayer
                    lock (_writeLock)
                    {
                        _statusCacheDirty = true;
                    }
                }
            }
        }

        private void InitializeStatusFile()
        {
            if (!File.Exists(_statusFilePath))
            {
                try
                {
                    // Créer un fichier de statut vide
                    File.WriteAllText(_statusFilePath, "[]");
                    Console.WriteLine("Initialized empty status file");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing status file: {ex.Message}");
                }
            }
        }

        private static void AppendJson<T>(string file, T obj)
        {
            try
            {
                string json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
                
                // Create directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                
                // Append with retry logic
                int retries = 0;
                while (retries < 3)
                {
                    try
                    {
                        File.AppendAllText(file, json + Environment.NewLine);
                        break;
                    }
                    catch (IOException)
                    {
                        // File might be in use, retry after a short delay
                        retries++;
                        Thread.Sleep(5);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error appending JSON: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Ensure any pending changes are written
            FlushStatusCache();
            
            _flushTimer?.Stop();
            _flushTimer?.Dispose();
        }
    }
}