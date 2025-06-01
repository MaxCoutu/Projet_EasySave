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
        
        // File size units
        public ObservableCollection<string> FileSizeUnits { get; } = new ObservableCollection<string> { "KB", "MB", "GB" };
        
        // Max file size properties
        private int _maxFileSizeKB;
        public int MaxFileSizeKB
        {
            get => _maxFileSizeKB;
            set
            {
                if (_maxFileSizeKB != value)
                {
                    _maxFileSizeKB = value;
                    OnPropertyChanged(nameof(MaxFileSizeKB));
                    OnPropertyChanged(nameof(MaxFileSizeDisplay));
                }
            }
        }
        
        // Slider value (1-1000)
        private double _fileSizeValue = 100;
        public double FileSizeValue
        {
            get => _fileSizeValue;
            set
            {
                if (_fileSizeValue != value && value > 0)
                {
                    _fileSizeValue = value;
                    OnPropertyChanged(nameof(FileSizeValue));
                    // Ne plus mettre à jour automatiquement
                    // UpdateMaxFileSizeFromInput();
                }
            }
        }
        
        // Selected unit index
        private int _selectedUnitIndex = 0;
        public int SelectedUnitIndex
        {
            get => _selectedUnitIndex;
            set
            {
                if (_selectedUnitIndex != value)
                {
                    _selectedUnitIndex = value;
                    OnPropertyChanged(nameof(SelectedUnitIndex));
                    // Ne plus mettre à jour automatiquement
                    // UpdateMaxFileSizeFromInput();
                }
            }
        }
        
        // Human-readable display of the max file size
        public string MaxFileSizeDisplay
        {
            get
            {
                if (MaxFileSizeKB >= 1048576) // 1 GB in KB
                {
                    return $"{MaxFileSizeKB / 1048576.0:F2} GB";
                }
                else if (MaxFileSizeKB >= 1024) // 1 MB in KB
                {
                    return $"{MaxFileSizeKB / 1024.0:F2} MB";
                }
                else
                {
                    return $"{MaxFileSizeKB} KB";
                }
            }
        }

        public ICommand AddBlockingProgramCommand { get; }
        public ICommand RemoveBlockingProgramCommand { get; }
        public ICommand AddExtensionCommand { get; }
        public ICommand RemoveExtensionCommand { get; }
        public ICommand AddPriorityExtensionCommand { get; }
        public ICommand RemovePriorityExtensionCommand { get; }
        public ICommand RemoveItemCommand { get; }
        public ICommand UpdateMaxFileSizeCommand { get; }
        public ICommand ApplyFileSizeCommand { get; }

        public SettingsViewModel(Settings settings)
        {
            _settings = settings;
            
            // Initialize collections
            _blockedPackages = new ObservableCollection<string>(_settings.BlockedPackages.OrderBy(p => p));
            _cryptoExtensions = new ObservableCollection<string>(_settings.CryptoExtensions.OrderBy(e => e));
            _priorityExtensions = new ObservableCollection<string>(_settings.PriorityExtensions.OrderBy(e => e));
            _maxFileSizeKB = _settings.MaxFileSizeKB;
            
            // Initialize slider value and unit based on current MaxFileSizeKB
            InitializeFileSizeControls();

            // Add max file size update command
            UpdateMaxFileSizeCommand = new RelayCommand(param =>
            {
                if (param is string sizeStr && !string.IsNullOrWhiteSpace(sizeStr))
                {
                    Debug.WriteLine($"[DEBUG] UpdateMaxFileSizeCommand started. Parameter: '{sizeStr}'");
                    
                    if (int.TryParse(sizeStr, out int size) && size > 0)
                    {
                        _settings.MaxFileSizeKB = size;
                        MaxFileSizeKB = size;
                        _settings.Save();
                        Debug.WriteLine($"[DEBUG] Updated MaxFileSizeKB to {size}KB");
                    }
                    else
                    {
                        Debug.WriteLine($"[DEBUG] Failed to parse '{sizeStr}' as a valid file size");
                    }
                }
            });
            
            // Command to apply file size from slider and unit selection
            ApplyFileSizeCommand = new RelayCommand(_ =>
            {
                Debug.WriteLine($"[DEBUG] ApplyFileSizeCommand executed. Value: {_fileSizeValue}, Unit: {FileSizeUnits[_selectedUnitIndex]}");
                
                // Appliquer et sauvegarder la nouvelle valeur
                UpdateMaxFileSizeFromInput(true);
                
                Debug.WriteLine($"[DEBUG] MaxFileSizeKB updated to: {_maxFileSizeKB} KB");
            });

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
        
        // Initialize slider value and unit based on current MaxFileSizeKB
        private void InitializeFileSizeControls()
        {
            if (MaxFileSizeKB >= 1048576) // 1 GB in KB
            {
                _selectedUnitIndex = 2; // GB
                _fileSizeValue = Math.Round(MaxFileSizeKB / 1048576.0, 2);
            }
            else if (MaxFileSizeKB >= 1024) // 1 MB in KB
            {
                _selectedUnitIndex = 1; // MB
                _fileSizeValue = Math.Round(MaxFileSizeKB / 1024.0, 2);
            }
            else
            {
                _selectedUnitIndex = 0; // KB
                _fileSizeValue = MaxFileSizeKB;
            }
            
            // Notify UI
            OnPropertyChanged(nameof(FileSizeValue));
            OnPropertyChanged(nameof(SelectedUnitIndex));
        }
        
        // Update MaxFileSizeKB based on input value and selected unit
        private void UpdateMaxFileSizeFromInput(bool saveToSettings = false)
        {
            // Ensure the value is positive
            if (_fileSizeValue <= 0)
            {
                _fileSizeValue = 1;
                OnPropertyChanged(nameof(FileSizeValue));
            }
            
            int newSizeKB;
            
            switch (SelectedUnitIndex)
            {
                case 1: // MB
                    newSizeKB = (int)(_fileSizeValue * 1024);
                    break;
                case 2: // GB
                    newSizeKB = (int)(_fileSizeValue * 1048576);
                    break;
                default: // KB
                    newSizeKB = (int)_fileSizeValue;
                    break;
            }
            
            // Ensure minimum size of 1 KB
            newSizeKB = Math.Max(1, newSizeKB);
            
            MaxFileSizeKB = newSizeKB;
            
            if (saveToSettings)
            {
                _settings.MaxFileSizeKB = newSizeKB;
                _settings.Save();
                Debug.WriteLine($"[DEBUG] Saved MaxFileSizeKB to {newSizeKB}KB");
            }
            
            OnPropertyChanged(nameof(MaxFileSizeDisplay));
        }
    }
} 