using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Projet.Model;

namespace Projet.Infrastructure
{
    public class JsonJobRepository : IJobRepository
    {
        private readonly string _filePath;

        public JsonJobRepository(IPathProvider pathProvider)
        {
            _filePath = Path.Combine(pathProvider.GetBaseDir(), "jobs.json");
        }

        public IReadOnlyList<BackupJob> Load()
        {
            if (!File.Exists(_filePath))
            {
                return new List<BackupJob>();
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                var jobs = JsonSerializer.Deserialize<List<BackupJob>>(json);
                return jobs ?? new List<BackupJob>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading jobs from JSON: {ex.Message}");
                return new List<BackupJob>();
            }
        }

        public void Save(IReadOnlyList<BackupJob> jobs)
        {
            try
            {
                string json = JsonSerializer.Serialize(jobs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving jobs to JSON: {ex.Message}");
            }
        }
    }
} 