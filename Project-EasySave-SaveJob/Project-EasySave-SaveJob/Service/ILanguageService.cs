using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.Service
{
    public interface ILanguageService
    {
        string Translate(string key);
        event Action LanguageChanged;
        void Load(string fullPath);
    }
}
