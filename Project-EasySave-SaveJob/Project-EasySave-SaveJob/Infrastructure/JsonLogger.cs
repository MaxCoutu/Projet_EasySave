using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Projet.Infrastructure
{
    public class JsonLogger : ILogger
    {
        private readonly string _logDir;
        private readonly string _statusDir;

        public JsonLogger(IPathProvider paths)
        {
            _logDir = paths.GetLogDir();
            _statusDir = paths.GetStatusDir();
        }

       
        public void LogEvent(LogEntry entry)
        {
            string file = Path.Combine(_logDir, $"{DateTime.UtcNow:yyyyMMdd}.log.json");
            AppendJson(file, entry);
        }

        
        public void UpdateStatus(StatusEntry entry)
        {
            string path = Path.Combine(_statusDir, "status.json");

            List<StatusEntry> list;
            if (File.Exists(path))
            {
                string existing = File.ReadAllText(path);
                list = JsonSerializer.Deserialize<List<StatusEntry>>(existing) ?? new List<StatusEntry>();
            }
            else
            {
                list = new List<StatusEntry>();
            }

            int idx = list.FindIndex(s => s.Name == entry.Name);
            if (idx >= 0) list[idx] = entry; else list.Add(entry);

            string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json + Environment.NewLine);   
        }

        
        private static void AppendJson<T>(string file, T obj)
        {
            string json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
            File.AppendAllText(file, json + Environment.NewLine);
        }
    }
}
