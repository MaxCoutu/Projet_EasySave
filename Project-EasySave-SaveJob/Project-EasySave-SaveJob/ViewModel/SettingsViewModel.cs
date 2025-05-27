using System.Windows.Input;
using Projet.Infrastructure;

namespace Projet.ViewModel
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly Settings _settings;

        public ICommand AddBlockingProgramCommand { get; }
        public ICommand RemoveBlockingProgramCommand { get; }
        public ICommand AddExtensionCommand { get; }
        public ICommand RemoveExtensionCommand { get; }

        public SettingsViewModel(Settings settings)
        {
            _settings = settings;

            AddBlockingProgramCommand = new RelayCommand(param =>
            {
                if (param is string programName && !string.IsNullOrWhiteSpace(programName))
                {
                    if (!_settings.BlockedPackages.Contains(programName))
                    {
                        _settings.BlockedPackages.Add(programName);
                    }
                }
            });

            RemoveBlockingProgramCommand = new RelayCommand(param =>
            {
                if (param is string programName && !string.IsNullOrWhiteSpace(programName))
                {
                    _settings.BlockedPackages.Remove(programName);
                }
            });

            AddExtensionCommand = new RelayCommand(param =>
            {
                if (param is string extension && !string.IsNullOrWhiteSpace(extension))
                {
                    string ext = extension.StartsWith(".") ? extension : "." + extension;
                    if (!_settings.CryptoExtensions.Contains(ext))
                    {
                        _settings.CryptoExtensions.Add(ext);
                    }
                }
            });

            RemoveExtensionCommand = new RelayCommand(param =>
            {
                if (param is string extension && !string.IsNullOrWhiteSpace(extension))
                {
                    string ext = extension.StartsWith(".") ? extension : "." + extension;
                    _settings.CryptoExtensions.Remove(ext);
                }
            });
        }
    }
} 