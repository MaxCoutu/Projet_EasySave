using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Timers;
using System.Xml;
using System.Xml.Serialization;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Logger qui génère des logs à la fois en format JSON et XML
    /// </summary>
    public class DualFormatLogger : ILogger, IDisposable
    {
        private readonly string _logDir;
        private readonly string _statusDir;
        private readonly string _statusJsonFilePath;
        private readonly string _statusXmlFilePath;
        private readonly object _writeLock = new object();
        private Dictionary<string, StatusEntry> _statusCache = new Dictionary<string, StatusEntry>();
        private bool _statusCacheDirty = false;
        private System.Timers.Timer _flushTimer;
        private DateTime _lastWriteTime = DateTime.MinValue;
        private readonly Random _random = new Random();
        
        // XmlSerializers pour les différents types
        private readonly XmlSerializer _statusEntrySerializer;
        private readonly XmlSerializer _logEntrySerializer;
        
        public DualFormatLogger(IPathProvider paths)
        {
            _logDir = paths.GetLogDir();
            _statusDir = paths.GetStatusDir();
            _statusJsonFilePath = Path.Combine(_statusDir, "status.json");
            _statusXmlFilePath = Path.Combine(_statusDir, "status.xml");
            
            // Initialiser les sérialiseurs XML
            _statusEntrySerializer = new XmlSerializer(typeof(List<StatusEntry>));
            _logEntrySerializer = new XmlSerializer(typeof(LogEntry));
            
            // Ensure directories exist
            Directory.CreateDirectory(_logDir);
            Directory.CreateDirectory(_statusDir);
            
            // Initialize status files if they don't exist
            InitializeStatusFiles();
            
            // Configurer un timer pour l'écriture périodique des statuts
            _flushTimer = new System.Timers.Timer(16); // 60fps pour une expérience ultra-fluide
            _flushTimer.Elapsed += (s, e) => FlushStatusCache();
            _flushTimer.Start();
            
            Console.WriteLine("DualFormatLogger initialized - generating both JSON and XML logs");
        }

        public void LogEvent(LogEntry entry)
        {
            if (entry == null)
                return;
                
            try
            {
                // Date du jour au format YYYYMMDD
                string dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
                
                // Fichier de log JSON
                string jsonFile = Path.Combine(_logDir, $"{dateStr}.log.json");
                AppendJson(jsonFile, entry);
                
                // Fichier de log XML
                string xmlFile = Path.Combine(_logDir, $"{dateStr}.log.xml");
                AppendXml(xmlFile, entry);
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
            if (entry.State == "ACTIVE" || entry.State == "PENDING" || entry.State == "PAUSED")
            {
                double microVariation = _random.NextDouble() * 0.001;
                entry.Progression += microVariation;
            }
            
            try
            {
                lock (_writeLock)
                {
                    // Mettre à jour le cache
                    _statusCache[entry.Name] = entry;
                    _statusCacheDirty = true;
                    
                    // Convertir tous les statuts en liste
                    var allEntries = new List<StatusEntry>(_statusCache.Values);
                    
                    // IMPORTANT: Toujours écrire d'abord le fichier JSON pour la compatibilité
                    // avec les composants qui lisent directement ce fichier
                    
                    // Écrire en JSON
                    string json = JsonSerializer.Serialize(allEntries, new JsonSerializerOptions 
                    { 
                        WriteIndented = true // Rétabli pour la lisibilité
                    });
                    
                    // Atomiquement écrire dans le fichier JSON
                    File.WriteAllText(_statusJsonFilePath, json);
                    
                    // Ensuite écrire en XML propre compatible Excel
                    using (var xmlWriter = XmlWriter.Create(_statusXmlFilePath, new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "  ",
                        NewLineChars = "\r\n",
                        NewLineHandling = NewLineHandling.Replace,
                        OmitXmlDeclaration = false,
                        Encoding = System.Text.Encoding.UTF8
                    }))
                    {
                        _statusEntrySerializer.Serialize(xmlWriter, allEntries);
                    }
                    
                    _lastWriteTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing status files: {ex.Message}");
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
                    // IMPORTANT: Toujours écrire d'abord le fichier JSON pour la compatibilité
                    // avec les composants qui lisent directement ce fichier
                    
                    // Écrire en JSON
                    string json = JsonSerializer.Serialize(entriesToWrite, new JsonSerializerOptions 
                    { 
                        WriteIndented = true
                    });
                    
                    File.WriteAllText(_statusJsonFilePath, json);
                    
                    // Ensuite écrire en XML propre compatible Excel
                    using (var xmlWriter = XmlWriter.Create(_statusXmlFilePath, new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "  ",
                        NewLineChars = "\r\n",
                        NewLineHandling = NewLineHandling.Replace,
                        OmitXmlDeclaration = false,
                        Encoding = System.Text.Encoding.UTF8
                    }))
                    {
                        _statusEntrySerializer.Serialize(xmlWriter, entriesToWrite);
                    }
                    
                    _lastWriteTime = DateTime.Now;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error writing status files: {ex.Message}");
                    
                    // Marquer comme sale pour réessayer
                    lock (_writeLock)
                    {
                        _statusCacheDirty = true;
                    }
                }
            }
        }

        private void InitializeStatusFiles()
        {
            // Initialiser le fichier JSON si nécessaire
            if (!File.Exists(_statusJsonFilePath))
            {
                try
                {
                    File.WriteAllText(_statusJsonFilePath, "[]");
                    Console.WriteLine("Initialized empty JSON status file");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing JSON status file: {ex.Message}");
                }
            }
            
            // Initialiser le fichier XML si nécessaire
            if (!File.Exists(_statusXmlFilePath))
            {
                try
                {
                    // Créer un fichier XML vide compatible Excel
                    using (var xmlWriter = XmlWriter.Create(_statusXmlFilePath, new XmlWriterSettings
                    {
                        Indent = true,
                        IndentChars = "  ",
                        NewLineChars = "\r\n",
                        NewLineHandling = NewLineHandling.Replace,
                        OmitXmlDeclaration = false,
                        Encoding = System.Text.Encoding.UTF8
                    }))
                    {
                        _statusEntrySerializer.Serialize(xmlWriter, new List<StatusEntry>());
                    }
                    
                    Console.WriteLine("Initialized empty XML status file");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing XML status file: {ex.Message}");
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
        
        private void AppendXml<T>(string file, T obj)
        {
            try
            {
                // Vérifier si le fichier existe
                bool fileExists = File.Exists(file);
                
                // Create directory if it doesn't exist
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                
                // Initialiser le fichier avec une racine si nécessaire
                if (!fileExists)
                {
                    // Créer un nouveau fichier XML avec une racine compatible Excel
                    using (var xmlWriter = XmlWriter.Create(file, new XmlWriterSettings 
                    { 
                        Indent = true,
                        IndentChars = "  ",
                        NewLineChars = "\r\n",
                        NewLineHandling = NewLineHandling.Replace,
                        OmitXmlDeclaration = false,
                        Encoding = System.Text.Encoding.UTF8
                    }))
                    {
                        xmlWriter.WriteStartDocument();
                        xmlWriter.WriteStartElement("LogEntries");
                        xmlWriter.WriteEndElement();
                        xmlWriter.WriteEndDocument();
                    }
                }
                
                // Ajouter l'entrée au fichier XML existant
                int retries = 0;
                while (retries < 3)
                {
                    try
                    {
                        // Charger le document XML
                        var xmlDoc = new XmlDocument();
                        xmlDoc.Load(file);
                        
                        // Créer un nœud pour l'entrée
                        var serializer = new XmlSerializer(typeof(T));
                        using (var memoryStream = new MemoryStream())
                        {
                            using (var xmlWriter = XmlWriter.Create(memoryStream, new XmlWriterSettings
                            {
                                Indent = true,
                                OmitXmlDeclaration = true,
                                Encoding = System.Text.Encoding.UTF8
                            }))
                            {
                                serializer.Serialize(xmlWriter, obj);
                            }
                            
                            memoryStream.Position = 0;
                            var entryDoc = new XmlDocument();
                            entryDoc.Load(memoryStream);
                            
                            // Importer le nœud racine de l'entrée
                            var importedNode = xmlDoc.ImportNode(entryDoc.DocumentElement, true);
                            
                            // Ajouter à la racine du document
                            xmlDoc.DocumentElement.AppendChild(importedNode);
                        }
                        
                        // Sauvegarder le document en tant que XML pur
                        xmlDoc.Save(file);
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
                Console.WriteLine($"Error appending XML: {ex.Message}");
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