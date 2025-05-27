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
        public string EncryptionKey          { get; set; } = "mySecretKey";

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
            if (_pathProvider == null)
            {
                // Consider logging an error or throwing an exception if pathProvider is crucial for saving
                return; 
            }
            string path = Path.Combine(_pathProvider.GetBaseDir(), "appsettings.json");
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }

        public static Settings Load(IPathProvider paths)
        {
            string path = Path.Combine(paths.GetBaseDir(), "appsettings.json");

            if (!File.Exists(path))
            {
                Directory.CreateDirectory(paths.GetBaseDir());
                var def = new Settings(paths) // Use constructor to set pathProvider
                {
                    CryptoExtensions = new List<string> { ".zip", ".7z" },
                    BlockedPackages  = new List<string> { "calc.exe" },
                    EncryptionKey    = "mySecretKey"
                };
                def.Save(); // Save the default settings
                return def;
            }

            var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path))
                   ?? new Settings();
            settings.Initialize(paths); // Initialize _pathProvider after deserialization
            return settings;
        }
    }
}
