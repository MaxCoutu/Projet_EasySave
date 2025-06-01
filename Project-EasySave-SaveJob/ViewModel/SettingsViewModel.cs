using System.Windows.Input;
using Projet.Infrastructure;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.ObjectModel;
using System;
using System.Linq;

namespace Projet.ViewModel
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly Settings _settings;
        private const string PlaceholderProcessName = "Process Name";
        private const string PlaceholderExtensionName = "File extension name (.txt)";

        // Observable collections for UI binding
        private ObservableCollection<string> _blockedPackages;
        public ObservableCollection<string> BlockedPackages 
        { 
            get => _blockedPackages;
            private set
            {
                _blockedPackages = value;
                OnPropertyChanged(nameof(BlockedPackages));
            }
        }

        private ObservableCollection<string> _cryptoExtensions;
        public ObservableCollection<string> CryptoExtensions
        {
            get => _cryptoExtensions;
            private set
            {
                _cryptoExtensions = value;
                OnPropertyChanged(nameof(CryptoExtensions));
            }
        }

        private ObservableCollection<string> _priorityExtensions;
        public ObservableCollection<string> PriorityExtensions
        {
            get => _priorityExtensions;
            private set
            {
                _priorityExtensions = value;
                OnPropertyChanged(nameof(PriorityExtensions));
            }
        }

        public ICommand AddBlockingProgramCommand { get; }
        public ICommand RemoveBlockingProgramCommand { get; }
        public ICommand AddExtensionCommand { get; }
        public ICommand RemoveExtensionCommand { get; }
        public ICommand AddPriorityExtensionCommand { get; }
        public ICommand RemovePriorityExtensionCommand { get; }
        public ICommand RemoveItemCommand { get; }

        public SettingsViewModel(Settings settings)
        {
            _settings = settings;
            
            // Initialize collections
            _blockedPackages = new ObservableCollection<string>(_settings.BlockedPackages.OrderBy(p => p));
            _cryptoExtensions = new ObservableCollection<string>(_settings.CryptoExtensions.OrderBy(e => e));
            _priorityExtensions = new ObservableCollection<string>(_settings.PriorityExtensions.OrderBy(e => e));

            AddBlockingProgramCommand = new RelayCommand(param =>
            {
                Debug.WriteLine($"[DEBUG] AddBlockingProgramCommand started. Parameter: '{param}'");
                if (param is string programName && !string.IsNullOrWhiteSpace(programName) && programName != PlaceholderProcessName)
                {
                    Debug.WriteLine($"[DEBUG] Processed program name: '{programName}'");
                    Debug.WriteLine($"[DEBUG] BlockedPackages BEFORE: {JsonSerializer.Serialize(_settings.BlockedPackages)}");
                    
                    if (!_settings.BlockedPackages.Contains(programName))
                    {
                        _settings.BlockedPackages.Add(programName);
                        BlockedPackages.Add(programName);
                        Debug.WriteLine($"[DEBUG] Added '{programName}' to BlockedPackages.");
                    }
                    else
                    {
                        Debug.WriteLine($"[DEBUG] BlockedPackages already contains '{programName}'.");
                    }
                    _settings.Save();
                    Debug.WriteLine($"[DEBUG] Settings saved by AddBlockingProgramCommand.");
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
                        if (BlockedPackages.Contains(programName))
                            BlockedPackages.Remove(programName);
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

                    if (!_settings.CryptoExtensions.Contains(ext))
                    {
                        _settings.CryptoExtensions.Add(ext);
                        CryptoExtensions.Add(ext);
                        Debug.WriteLine($"[DEBUG] Added '{ext}' to CryptoExtensions.");
                    }
                    else
                    {
                        Debug.WriteLine($"[DEBUG] CryptoExtensions already contains '{ext}'.");
                    }
                    
                    _settings.Save();
                    Debug.WriteLine($"[DEBUG] Settings saved by AddExtensionCommand.");
                    Debug.WriteLine($"[DEBUG] CryptoExtensions AFTER SAVE IN VM: {JsonSerializer.Serialize(_settings.CryptoExtensions)}");
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
                        if (CryptoExtensions.Contains(ext))
                            CryptoExtensions.Remove(ext);
                        _settings.Save();
                    }
                }
            });
            
            // Add priority extension command
            AddPriorityExtensionCommand = new RelayCommand(param =>
            {
                Debug.WriteLine($"[DEBUG] AddPriorityExtensionCommand started. Parameter: '{param}'");
                if (param is string extension && !string.IsNullOrWhiteSpace(extension) && extension != PlaceholderExtensionName)
                {
                    string ext = extension.StartsWith(".") ? extension : "." + extension;
                    Debug.WriteLine($"[DEBUG] Processed priority extension: '{ext}'");
                    Debug.WriteLine($"[DEBUG] PriorityExtensions BEFORE: {JsonSerializer.Serialize(_settings.PriorityExtensions)}");

                    if (!_settings.PriorityExtensions.Contains(ext))
                    {
                        _settings.PriorityExtensions.Add(ext);
                        PriorityExtensions.Add(ext);
                        Debug.WriteLine($"[DEBUG] Added '{ext}' to PriorityExtensions.");
                    }
                    else
                    {
                        Debug.WriteLine($"[DEBUG] PriorityExtensions already contains '{ext}'.");
                    }
                    
                    _settings.Save();
                    Debug.WriteLine($"[DEBUG] Settings saved by AddPriorityExtensionCommand.");
                    Debug.WriteLine($"[DEBUG] PriorityExtensions AFTER SAVE: {JsonSerializer.Serialize(_settings.PriorityExtensions)}");
                }
                else
                {
                    Debug.WriteLine($"[DEBUG] AddPriorityExtensionCommand: Invalid parameter, whitespace, or placeholder.");
                }
                Debug.WriteLine($"[DEBUG] AddPriorityExtensionCommand finished.");
            });

            // Remove priority extension command
            RemovePriorityExtensionCommand = new RelayCommand(param =>
            {
                if (param is string extension && !string.IsNullOrWhiteSpace(extension) && extension != PlaceholderExtensionName)
                {
                    string ext = extension.StartsWith(".") ? extension : "." + extension;
                    if (_settings.PriorityExtensions.Remove(ext))
                    {
                        if (PriorityExtensions.Contains(ext))
                            PriorityExtensions.Remove(ext);
                        _settings.Save();
                    }
                }
            });
            
            // General item removal command (for use with list items)
            RemoveItemCommand = new RelayCommand(param => 
            {
                if (param is Tuple<string, string> itemInfo)
                {
                    string itemType = itemInfo.Item1;
                    string value = itemInfo.Item2;
                    
                    switch (itemType)
                    {
                        case "blockedPackage":
                            if (_settings.BlockedPackages.Remove(value))
                            {
                                if (BlockedPackages.Contains(value))
                                    BlockedPackages.Remove(value);
                                _settings.Save();
                            }
                            break;
                            
                        case "cryptoExtension":
                            if (_settings.CryptoExtensions.Remove(value))
                            {
                                if (CryptoExtensions.Contains(value))
                                    CryptoExtensions.Remove(value);
                                _settings.Save();
                            }
                            break;
                            
                        case "priorityExtension":
                            if (_settings.PriorityExtensions.Remove(value))
                            {
                                if (PriorityExtensions.Contains(value))
                                    PriorityExtensions.Remove(value);
                                _settings.Save();
                            }
                            break;
                    }
                }
            });
        }
    }
} 