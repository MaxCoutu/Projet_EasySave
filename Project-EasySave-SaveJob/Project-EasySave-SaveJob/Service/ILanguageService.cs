using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.Service
{
    public interface ILanguageService
    {
        string Translate(string key);
        void Load(string fullPath);
        event Action LanguageChanged;

    }
}
