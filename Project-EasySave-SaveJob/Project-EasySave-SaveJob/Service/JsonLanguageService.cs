using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Projet.Service
{
    public class JsonLanguageService : ILanguageService
    {
        private Dictionary<string, string> _dict = new Dictionary<string, string>();
        private readonly string _defaultEn = @"{
  ""menu_title"":         ""Easy Save"",
  ""menu_create"":        ""Create a\nBackup job"",
  ""menu_choose"":        ""Choose a\nBackup job to run"",
  ""menu_runall"":        ""Run all\nBackup jobs"",
  ""menu_settings"":      ""Settings"",
  ""menu_choice"":        ""Choice:"",

  ""recent_title"":       ""Recent Backup jobs Launched"",
  ""col_name"":           ""Backup Name"",
  ""col_type"":           ""Type"",
  ""src_label"":          ""Source directory:"",
  ""dst_label"":          ""Target directory:"",
  ""play_tooltip"":       ""Run this job""
}";
        private readonly string _defaultFr = @"{
  ""menu_title"":         ""Easy Save"",
  ""menu_create"":        ""Créer une\nsauvegarde"",
  ""menu_choose"":        ""Choisir une\nsauvegarde à lancer"",
  ""menu_runall"":        ""Lancer toutes\nles sauvegardes"",
  ""menu_settings"":      ""Paramètres"",
  ""menu_choice"":        ""Choix :"",

  ""recent_title"":       ""Sauvegardes récentes lancées"",
  ""col_name"":           ""Nom"",
  ""col_type"":           ""Type"",
  ""src_label"":          ""Dossier source :"",
  ""dst_label"":          ""Dossier cible :"",
  ""play_tooltip"":       ""Lancer ce job""
}";

        public event Action LanguageChanged;

        public JsonLanguageService(string fullPath)
        {
            Load(fullPath);
        }

        public void Load(string fullPath)
        {
            
            if (!File.Exists(fullPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                bool isFr = fullPath.EndsWith("fr.json", StringComparison.OrdinalIgnoreCase);
                File.WriteAllText(fullPath,
                    isFr ? _defaultFr : _defaultEn);
            }

          
            var text = File.ReadAllText(fullPath);
            _dict = JsonSerializer.Deserialize<Dictionary<string, string>>(text)
                    ?? new Dictionary<string, string>();

            
            var master = JsonSerializer.Deserialize<Dictionary<string, string>>(
                          fullPath.EndsWith("fr.json") ? _defaultFr : _defaultEn)
                         ?? new Dictionary<string, string>();

            bool updated = false;
            foreach (var kv in master)
            {
                if (!_dict.ContainsKey(kv.Key))
                {
                    _dict[kv.Key] = kv.Value;
                    updated = true;
                }
            }
            if (updated)
            {
                File.WriteAllText(fullPath,
                    JsonSerializer.Serialize(_dict, new JsonSerializerOptions { WriteIndented = true }));
            }

           
            LanguageChanged?.Invoke();
        }

        public string Translate(string key)
            => _dict.TryGetValue(key, out var v) ? v : key;
    }
}
