using System;
using System.ComponentModel;
using Projet.Service;
using WpfApp;

namespace Projet.Wpf.Localization
{
    public class LocExtensionProvider : INotifyPropertyChanged
    {
        private static readonly Lazy<LocExtensionProvider> _lazy =
            new Lazy<LocExtensionProvider>(() => new LocExtensionProvider());
        public static LocExtensionProvider Instance => _lazy.Value;

        private readonly ILanguageService _lang;

        private LocExtensionProvider()
        {
            
            _lang = App.LanguageService;

            if (_lang != null)
            {
                _lang.LanguageChanged += () =>
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

       
        public string this[string key]
            => _lang != null ? _lang.Translate(key) : key;
    }
}
