using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Projet.Infrastructure
{
    
    public class Settings
    {
        public List<string> CryptoExtensions { get; set; } = new List<string>();
        public List<string> BlockedPackages  { get; set; } = new List<string>();
        public string EncryptionKey          { get; set; } = "mySecretKey";

        public static Settings Load(IPathProvider paths)
        {
            string path = Path.Combine(paths.GetBaseDir(), "appsettings.json");

            if (!File.Exists(path))
            {
                Directory.CreateDirectory(paths.GetBaseDir());
                var def = new Settings
                {
                    CryptoExtensions = new List<string> { ".zip", ".7z" },
                    BlockedPackages  = new List<string> { "calc.exe" },
                    EncryptionKey    = "mySecretKey"
                };
                File.WriteAllText(path,
                    JsonSerializer.Serialize(def, new JsonSerializerOptions { WriteIndented = true }));
                return def;
            }

            return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path))
                   ?? new Settings();
        }
    }
}
