using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Projet.Infrastructure
{
    public class Settings
    {
        private IPathProvider _pathProvider;

        public List<string> CryptoExtensions { get; set; } = new List<string>();
        public List<string> BlockedPackages  { get; set; } = new List<string>();
        public List<string> PriorityExtensions { get; set; } = new List<string>();
        public string EncryptionKey          { get; set; } = "mySecretKey";
        // Maximum file size in KB that can be transferred simultaneously
        public int MaxFileSizeKB { get; set; } = 1024; // Default to 1MB (1024 KB)
        // Process monitoring settings
        public bool AutoMonitoringEnabled { get; set; } = true;
        public int ProcessMonitoringIntervalMs { get; set; } = 5000; // Default: check every 5 seconds

        // Parameterless constructor for JsonSerializer
        public Settings() {}

        public Settings(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
        }

        public void Initialize(IPathProvider pathProvider)
        {
            _pathProvider = pathProvider;
        }

        public void Save()
        {
            // Utiliser le chemin spécifique C:\Projet pour le fichier de paramètres
            string rootDir = "C:\\Projet";
            string path = Path.Combine(rootDir, "appsettings.json");
            
            // S'assurer que le répertoire existe
            Directory.CreateDirectory(rootDir);
            
            // Sérialiser et sauvegarder les paramètres
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Settings saved to {path}");
        }

        public static Settings Load(IPathProvider paths)
        {
            // Utiliser le chemin spécifique C:\Projet pour le fichier de paramètres
            string rootDir = "C:\\Projet";
            string path = Path.Combine(rootDir, "appsettings.json");
            Console.WriteLine($"Attempting to load settings from: {path}");

            if (!File.Exists(path))
            {
                Console.WriteLine("Settings file not found in C:\\Projet. Creating default settings.");
                Directory.CreateDirectory(rootDir);
                var def = new Settings(paths) // Use constructor to set pathProvider
                {
                    CryptoExtensions = new List<string> { ".txt", ".zip", ".7z" },
                    BlockedPackages  = new List<string> { "calc.exe" },
                    PriorityExtensions = new List<string> { ".txt", ".docx", ".xlsx" },
                    EncryptionKey    = "mySecretKey",
                    MaxFileSizeKB    = 1024 // Default to 1MB
                };
                
                // Sauvegarder les paramètres dans C:\Projet
                string jsonContent = JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, jsonContent);
                
                Console.WriteLine($"Default settings saved to {path} with extensions: {string.Join(", ", def.CryptoExtensions)}");
                return def;
            }

            try
            {
                Console.WriteLine("Loading settings from existing file.");
                string jsonContent = File.ReadAllText(path);
                var settings = JsonSerializer.Deserialize<Settings>(jsonContent) ?? new Settings();
                settings.Initialize(paths); // Initialize _pathProvider after deserialization
                
                // Ensure CryptoExtensions contains ".txt" extension
                if (!settings.CryptoExtensions.Contains(".txt"))
                {
                    Console.WriteLine("Adding .txt to CryptoExtensions as it was missing");
                    settings.CryptoExtensions.Add(".txt");
                    settings.Save(); // Save the updated settings
                }
                
                Console.WriteLine($"Loaded settings with encryption key: {settings.EncryptionKey}");
                Console.WriteLine($"Loaded settings with encryption extensions: {string.Join(", ", settings.CryptoExtensions)}");
                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings from {path}: {ex.Message}. Creating default settings.");
                
                // Créer les paramètres par défaut
                var def = new Settings(paths)
                {
                    CryptoExtensions = new List<string> { ".txt", ".zip", ".7z" },
                    BlockedPackages  = new List<string> { "calc.exe" },
                    PriorityExtensions = new List<string> { ".txt", ".docx", ".xlsx" },
                    EncryptionKey    = "mySecretKey",
                    MaxFileSizeKB    = 1024
                };
                
                // Sauvegarder les paramètres dans C:\Projet
                Directory.CreateDirectory(rootDir);
                string jsonContent = JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, jsonContent);
                
                Console.WriteLine($"Default settings saved to {path}");
                return def;
            }
        }
    }
}
