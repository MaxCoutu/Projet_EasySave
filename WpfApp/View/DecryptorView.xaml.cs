using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Projet.Infrastructure;
using Projet.ViewModel;

namespace Projet.Wpf.View
{
    public partial class DecryptorView : UserControl
    {
        private string _selectedFilePath;
        private MainViewModel _mainViewModel;
        
        // Signature bytes to identify encrypted files (EasySave Encrypted)
        private static readonly byte[] EncryptionSignature = new byte[] { 0x45, 0x53, 0x45, 0x43 }; // ESEC in ASCII

        public DecryptorView()
        {
            InitializeComponent();
            
            // Le DataContext sera défini après l'initialisation, donc on ne peut pas l'utiliser ici
            // On va plutôt mettre à jour le bouton de retour dans la méthode Loaded
            this.Loaded += DecryptorView_Loaded;
            
            // Charger la clé de chiffrement par défaut
            string defaultKey = LoadEncryptionKeyFromSettings();
            if (!string.IsNullOrEmpty(defaultKey))
            {
                keyPasswordBox.Password = defaultKey;
            }
        }
        
        private void DecryptorView_Loaded(object sender, RoutedEventArgs e)
        {
            // Récupérer la référence au MainViewModel via le DataContext
            // On remonte jusqu'au ContentControl parent qui a le DataContext défini
            var parent = this.Parent;
            while (parent != null)
            {
                if (parent is FrameworkElement element && element.DataContext is DecryptorViewModel viewModel)
                {
                    _mainViewModel = viewModel.MainViewModel;
                    break;
                }
                
                if (parent is FrameworkElement elementWithContext && elementWithContext.DataContext != null)
                {
                    Console.WriteLine($"Parent DataContext type: {elementWithContext.DataContext.GetType().Name}");
                }
                
                if (parent is DependencyObject depObj)
                {
                    parent = VisualTreeHelper.GetParent(depObj);
                }
                else
                {
                    break;
                }
            }
            
            Console.WriteLine($"MainViewModel found: {_mainViewModel != null}");
        }
        
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Sélectionnez un fichier chiffré",
                Filter = "Tous les fichiers (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                _selectedFilePath = dialog.FileName;
                filePathTextBox.Text = _selectedFilePath;
                
                // Vérifier si le fichier est chiffré
                bool isEncrypted = IsFileEncrypted(_selectedFilePath);
                
                // Mettre à jour l'interface
                statusTextBlock.Text = isEncrypted 
                    ? "Fichier chiffré détecté. Entrez la clé de déchiffrement."
                    : "Ce fichier n'est pas chiffré avec EasySave.";
                
                statusTextBlock.Foreground = isEncrypted 
                    ? new SolidColorBrush(Colors.Green) 
                    : new SolidColorBrush(Colors.Red);
                
                decryptButton.IsEnabled = isEncrypted;
                keyPasswordBox.IsEnabled = isEncrypted;
                
                if (isEncrypted)
                {
                    keyPasswordBox.Focus();
                }
            }
        }

        private void DecryptButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                MessageBox.Show("Veuillez d'abord sélectionner un fichier valide.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string key = keyPasswordBox.Password;
            
            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("Veuillez entrer une clé de déchiffrement.", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                // Déchiffrer le fichier
                bool success = Decrypt(_selectedFilePath, key);
                
                if (success)
                {
                    statusTextBlock.Text = "Fichier déchiffré avec succès!";
                    statusTextBlock.Foreground = new SolidColorBrush(Colors.Green);
                    MessageBox.Show("Le fichier a été déchiffré avec succès!", "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    // Réinitialiser l'interface
                    _selectedFilePath = null;
                    filePathTextBox.Text = "";
                    keyPasswordBox.Password = "";
                    decryptButton.IsEnabled = false;
                    keyPasswordBox.IsEnabled = false;
                }
                else
                {
                    statusTextBlock.Text = "Échec du déchiffrement. Vérifiez que la clé est correcte.";
                    statusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                }
            }
            catch (Exception ex)
            {
                statusTextBlock.Text = $"Erreur: {ex.Message}";
                statusTextBlock.Foreground = new SolidColorBrush(Colors.Red);
                MessageBox.Show($"Une erreur est survenue lors du déchiffrement: {ex.Message}", "Erreur", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Checks if a file is encrypted by looking for the encryption signature
        /// </summary>
        public static bool IsFileEncrypted(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;
                    
                // Check file size
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length < EncryptionSignature.Length)
                    return false;
                    
                // Read just the beginning of the file to check for signature
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] signatureBuffer = new byte[EncryptionSignature.Length];
                    int bytesRead = stream.Read(signatureBuffer, 0, signatureBuffer.Length);
                    
                    if (bytesRead < EncryptionSignature.Length)
                        return false;
                        
                    // Compare signature
                    for (int i = 0; i < EncryptionSignature.Length; i++)
                    {
                        if (signatureBuffer[i] != EncryptionSignature[i])
                            return false;
                    }
                    
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Decrypts a file using the provided encryption key
        /// </summary>
        public static bool Decrypt(string filePath, string key)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;
                
            if (string.IsNullOrEmpty(key))
                return false;
                
            try
            {
                // Check if file is encrypted
                if (!IsFileEncrypted(filePath))
                {
                    Console.WriteLine($"File is not encrypted: {filePath}");
                    return false;
                }
                
                // Read the file content
                byte[] fileBytes = File.ReadAllBytes(filePath);
                byte[] keyBytes = Encoding.UTF8.GetBytes(key);
                
                // Skip the signature and decrypt only the content
                byte[] contentBytes = new byte[fileBytes.Length - EncryptionSignature.Length];
                
                // Apply XOR decryption (same as encryption)
                for (int i = 0; i < contentBytes.Length; i++)
                {
                    contentBytes[i] = (byte)(fileBytes[i + EncryptionSignature.Length] ^ keyBytes[i % keyBytes.Length]);
                }
                
                // Write the decrypted content back to the file
                File.WriteAllBytes(filePath, contentBytes);
                
                Console.WriteLine($"File decrypted: {filePath}");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decrypting file: {ex.Message}");
                return false;
            }
        }

        // Gestionnaire de l'événement pour le bouton retour
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // Trouver le ViewModel parent
            var window = Window.GetWindow(this);
            if (window != null && window.DataContext != null)
            {
                // Récupérer le MainViewModel directement depuis le DataContext de la fenêtre
                var mainViewModel = window.DataContext as MainViewModel;
                if (mainViewModel != null)
                {
                    // Définir la vue courante comme étant le MainViewModel lui-même
                    mainViewModel.CurrentViewModel = mainViewModel;
                    Console.WriteLine("Retour à la vue principale via le bouton");
                }
                else
                {
                    Console.WriteLine($"Le DataContext de la fenêtre n'est pas un MainViewModel: {window.DataContext.GetType().Name}");
                }
            }
            else
            {
                Console.WriteLine("Impossible de trouver la fenêtre parente ou son DataContext");
            }
        }
        
        /// <summary>
        /// Charge la clé de chiffrement depuis le fichier de paramètres
        /// </summary>
        private string LoadEncryptionKeyFromSettings()
        {
            try
            {
                // Chemin vers le fichier de paramètres
                string settingsPath = Path.Combine("C:\\Projet", "appsettings.json");
                
                if (File.Exists(settingsPath))
                {
                    // Lire le contenu du fichier
                    string jsonContent = File.ReadAllText(settingsPath);
                    
                    // Désérialiser le JSON
                    using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                    {
                        // Vérifier si la propriété EncryptionKey existe
                        if (doc.RootElement.TryGetProperty("EncryptionKey", out JsonElement keyElement))
                        {
                            string key = keyElement.GetString();
                            Console.WriteLine("Clé de chiffrement chargée depuis les paramètres");
                            return key;
                        }
                    }
                }
                
                Console.WriteLine("Impossible de charger la clé de chiffrement depuis les paramètres");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement de la clé de chiffrement: {ex.Message}");
                return null;
            }
        }
    }
} 