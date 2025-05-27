using System.Windows.Input;
using Projet.Infrastructure;
using System.Diagnostics;
using System.Text.Json;

namespace Projet.ViewModel
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly Settings _settings;
        private const string PlaceholderProcessName = "Process Name";
        private const string PlaceholderExtensionName = "File extension name (.txt)";

        public ICommand AddBlockingProgramCommand { get; }
        public ICommand RemoveBlockingProgramCommand { get; }
        public ICommand AddExtensionCommand { get; }
        public ICommand RemoveExtensionCommand { get; }

        public SettingsViewModel(Settings settings)
        {
            _settings = settings;

            AddBlockingProgramCommand = new RelayCommand(param =>
            {
                Debug.WriteLine($"[DEBUG] AddBlockingProgramCommand started. Parameter: '{param}'");
                if (param is string programName && !string.IsNullOrWhiteSpace(programName) && programName != PlaceholderProcessName)
                {
                    Debug.WriteLine($"[DEBUG] Processed program name: '{programName}'");
                    Debug.WriteLine($"[DEBUG] CryptoExtensions BEFORE: {JsonSerializer.Serialize(_settings.CryptoExtensions)}");
                    Debug.WriteLine($"[DEBUG] BlockedPackages BEFORE: {JsonSerializer.Serialize(_settings.BlockedPackages)}");
                    if (!_settings.BlockedPackages.Contains(programName))
                    {
                        _settings.BlockedPackages.Add(programName);
                        Debug.WriteLine($"[DEBUG] Added '{programName}' to BlockedPackages.");
                    }
                    else
                    {
                        Debug.WriteLine($"[DEBUG] BlockedPackages already contains '{programName}'.");
                    }
                    _settings.Save();
                    Debug.WriteLine($"[DEBUG] Settings saved by AddBlockingProgramCommand.");
                    Debug.WriteLine($"[DEBUG] CryptoExtensions AFTER SAVE IN VM: {JsonSerializer.Serialize(_settings.CryptoExtensions)}");
                    Debug.WriteLine($"[DEBUG] BlockedPackages AFTER SAVE IN VM: {JsonSerializer.Serialize(_settings.BlockedPackages)}");
                }
                else
                {
                    Debug.WriteLine($"[DEBUG] AddBlockingProgramCommand: Invalid parameter, whitespace, or placeholder.");
                }
                Debug.WriteLine($"[DEBUG] AddBlockingProgramCommand finished.");
            });

            RemoveBlockingProgramCommand = new RelayCommand(param =>
            {
                if (param is string programName && !string.IsNullOrWhiteSpace(programName) && programName != PlaceholderProcessName)
                {
                    if (_settings.BlockedPackages.Remove(programName))
                    {
                        _settings.Save();
                    }
                }
            });

            AddExtensionCommand = new RelayCommand(param =>
            {
                Debug.WriteLine($"[DEBUG] AddExtensionCommand started. Parameter: '{param}'");
                if (param is string extension && !string.IsNullOrWhiteSpace(extension) && extension != PlaceholderExtensionName)
                {
                    string ext = extension.StartsWith(".") ? extension : "." + extension;
                    Debug.WriteLine($"[DEBUG] Processed extension: '{ext}'");
                    Debug.WriteLine($"[DEBUG] CryptoExtensions BEFORE: {JsonSerializer.Serialize(_settings.CryptoExtensions)}");
                    Debug.WriteLine($"[DEBUG] BlockedPackages BEFORE: {JsonSerializer.Serialize(_settings.BlockedPackages)}");

                    if (!_settings.CryptoExtensions.Contains(ext))
                    {
                        _settings.CryptoExtensions.Add(ext);
                        Debug.WriteLine($"[DEBUG] Added '{ext}' to CryptoExtensions.");
                    }
                    else
                    {
                        Debug.WriteLine($"[DEBUG] CryptoExtensions already contains '{ext}'.");
                    }
                    
                    _settings.Save();
                    Debug.WriteLine($"[DEBUG] Settings saved by AddExtensionCommand.");
                    Debug.WriteLine($"[DEBUG] CryptoExtensions AFTER SAVE IN VM: {JsonSerializer.Serialize(_settings.CryptoExtensions)}");
                    Debug.WriteLine($"[DEBUG] BlockedPackages AFTER SAVE IN VM: {JsonSerializer.Serialize(_settings.BlockedPackages)}");
                }
                else
                {
                    Debug.WriteLine($"[DEBUG] AddExtensionCommand: Invalid parameter, whitespace, or placeholder.");
                }
                Debug.WriteLine($"[DEBUG] AddExtensionCommand finished.");
            });

            RemoveExtensionCommand = new RelayCommand(param =>
            {
                if (param is string extension && !string.IsNullOrWhiteSpace(extension) && extension != PlaceholderExtensionName)
                {
                    string ext = extension.StartsWith(".") ? extension : "." + extension;
                    if (_settings.CryptoExtensions.Remove(ext))
                    {
                        _settings.Save();
                    }
                }
            });
        }
    }
} 