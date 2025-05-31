using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace Projet.Infrastructure
{
    public class JsonLogger : ILogger
    {
        private readonly string _logDir;
        private readonly string _statusDir;
        private readonly object _statusLock = new object();
        private readonly int _maxRetries = 5;
        private readonly int _retryDelayMs = 50;

        public JsonLogger(IPathProvider paths)
        {
            _logDir = paths.GetLogDir();
            _statusDir = paths.GetStatusDir();
            
            // Ensure directories exist
            Directory.CreateDirectory(_logDir);
            Directory.CreateDirectory(_statusDir);
            
            // Initialize status file if it doesn't exist
            InitializeStatusFile();
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
            {
                return; // Ignore invalid entries
            }

            // Set timestamp to now
            entry.Timestamp = DateTime.Now;
            
            // Ensure progression is within bounds
            entry.Progression = Math.Min(100, Math.Max(0, entry.Progression));
            
            string path = Path.Combine(_statusDir, "status.json");
            int retries = 0;
            bool success = false;
            
            while (!success && retries < _maxRetries)
            {
                try
                {
                    // Use a lock to prevent concurrent access from the same process
                    lock (_statusLock)
                    {
                        // Load existing entries
                        List<StatusEntry> list = LoadStatusEntries(path);
                        
                        // Update or add the entry
                        int idx = list.FindIndex(s => string.Equals(s.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
                        if (idx >= 0)
                        {
                            // Update existing entry
                            list[idx] = entry;
                            Console.WriteLine($"Updated status for job '{entry.Name}': State={entry.State}, Progress={entry.Progression:F2}%");
                        }
                        else
                        {
                            // Add new entry
                            list.Add(entry);
                            Console.WriteLine($"Added new status for job '{entry.Name}': State={entry.State}, Progress={entry.Progression:F2}%");
                        }

                        // Write updated list to file
                        SaveStatusEntries(path, list);
                    }
                    
                    success = true;
                }
                catch (IOException ex)
                {
                    // File might be locked by another process
                    retries++;
                    Console.WriteLine($"Status file locked, retry {retries}/{_maxRetries}: {ex.Message}");
                    Thread.Sleep(_retryDelayMs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating status: {ex.Message}");
                    
                    // Try a fallback approach for the last retry
                    if (retries == _maxRetries - 1)
                    {
                        try
                        {
                            // Create a temporary file with just this entry
                            string tempPath = Path.Combine(_statusDir, $"status_{entry.Name}_{Guid.NewGuid()}.json");
                            SaveStatusEntries(tempPath, new List<StatusEntry> { entry });
                            
                            // Try to merge it later
                            Console.WriteLine($"Created temporary status file: {tempPath}");
                        }
                        catch
                        {
                            // Ignore errors in fallback
                        }
                    }
                    
                    retries++;
                    Thread.Sleep(_retryDelayMs);
                }
            }
            
            if (!success)
            {
                Console.WriteLine($"Failed to update status for job '{entry.Name}' after {_maxRetries} attempts");
            }
        }

        private List<StatusEntry> LoadStatusEntries(string path)
        {
            List<StatusEntry> list = new List<StatusEntry>();
            
            if (!File.Exists(path))
                return list;
                
            try
            {
                string json = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    list = JsonSerializer.Deserialize<List<StatusEntry>>(json) ?? new List<StatusEntry>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading status entries: {ex.Message}");
            }
            
            return list;
        }
        
        private void SaveStatusEntries(string path, List<StatusEntry> entries)
        {
            try
            {
                string json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
                
                // Write to a temporary file first
                string tempFile = path + ".tmp";
                File.WriteAllText(tempFile, json);
                
                // Then move it to the destination (atomic operation)
                if (File.Exists(path))
                    File.Delete(path);
                    
                File.Move(tempFile, path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving status entries: {ex.Message}");
                throw;
            }
        }

        private void InitializeStatusFile()
        {
            string path = Path.Combine(_statusDir, "status.json");
            
            if (!File.Exists(path))
            {
                try
                {
                    // Create an empty status file
                    SaveStatusEntries(path, new List<StatusEntry>());
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
                        Thread.Sleep(50);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error appending JSON: {ex.Message}");
            }
        }
    }
}