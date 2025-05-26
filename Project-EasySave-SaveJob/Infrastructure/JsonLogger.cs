using System;
using System.Collections.Generic;
using System.IO;
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
            Directory.CreateDirectory(_statusDir); // S'assurer que le répertoire existe
        }

        public void LogEvent(LogEntry entry)
        {
            string file = Path.Combine(_logDir, $"{DateTime.UtcNow:yyyyMMdd}.log.json");
            AppendJson(file, entry);
        }

        public void UpdateStatus(StatusEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
            {
                return; // Ignore si l'entrée ou le nom est invalide
            }

            string path = Path.Combine(_statusDir, "status.json");
            List<StatusEntry> list = new List<StatusEntry>();

            // Charger les entrées existantes
            if (File.Exists(path))
            {
                string existing = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(existing))
                {
                    list = JsonSerializer.Deserialize<List<StatusEntry>>(existing) ?? new List<StatusEntry>();
                }
            }

            // Mettre à jour ou ajouter l'entrée
            int idx = list.FindIndex(s => string.Equals(s.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                list[idx] = entry; // Remplacer l'entrée existante
            }
            else
            {
                list.Add(entry); // Ajouter une nouvelle entrée
            }

            // Écrire la liste mise à jour dans le fichier
            string json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private static void AppendJson<T>(string file, T obj)
        {
            string json = JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
            File.AppendAllText(file, json + Environment.NewLine);
        }
    }
}