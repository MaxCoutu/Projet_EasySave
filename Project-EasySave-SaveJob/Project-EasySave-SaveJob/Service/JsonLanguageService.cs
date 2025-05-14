using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Projet.Service
{
  
    public class JsonLanguageService : ILanguageService
    {
        private readonly Dictionary<string, string> _dict;

        public JsonLanguageService(string fullPath)
        {
           
            if (!File.Exists(fullPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                File.WriteAllText(fullPath,
                    fullPath.Contains("fr") ? DefaultFrJson : DefaultEnJson);
            }

            
            string json = File.ReadAllText(fullPath);
            _dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                    ?? new Dictionary<string, string>();
        }

        public string Translate(string key) =>
            _dict.TryGetValue(key, out var value) ? value : key;

       
        private const string DefaultEnJson = @"{
  ""menu_title""       : ""=== EasySave ==="",
  ""menu_list""        : ""1. List jobs"",
  ""menu_runselected"" : ""2. Run selected job"",
  ""menu_runall""      : ""3. Run all jobs"",
  ""menu_add""         : ""4. Add job"",
  ""menu_remove""      : ""5. Remove job"",
  ""menu_exit""        : ""0. Exit"",
  ""menu_choice""      : ""Choice: "",
  ""invalid_choice""   : ""Invalid choice"",
  ""press_key""        : ""Press any key…"",
  ""jobs_label""       : ""Jobs:""
}";

        private const string DefaultFrJson = @"{
  ""menu_title""       : ""=== EasySave ==="",
  ""menu_list""        : ""1. Lister les sauvegardes"",
  ""menu_runselected"" : ""2. Lancer la sauvegarde sélectionnée"",
  ""menu_runall""      : ""3. Lancer toutes les sauvegardes"",
  ""menu_add""         : ""4. Ajouter une sauvegarde"",
  ""menu_remove""      : ""5. Supprimer une sauvegarde"",
  ""menu_exit""        : ""0. Quitter"",
  ""menu_choice""      : ""Choix : "",
  ""invalid_choice""   : ""Choix invalide"",
  ""press_key""        : ""Appuyez sur une touche…"",
  ""jobs_label""       : ""Sauvegardes :""
}";
    }
}
