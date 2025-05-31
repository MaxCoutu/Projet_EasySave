using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Projet.Model;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Helper class for handling CryptoSoft encryption
    /// </summary>
    public static class CryptoSoftHelper
    {
        /// <summary>
        /// Encrypts a file using a simple XOR encryption with the key from settings
        /// </summary>
        /// <param name="filePath">Path to the file to encrypt</param>
        /// <param name="settings">Application settings</param>
        /// <returns>Encryption time in milliseconds</returns>
        public static int Encrypt(string filePath, Settings settings)
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentNullException(nameof(filePath));
                
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
                
            if (string.IsNullOrEmpty(settings.EncryptionKey))
            {
                Console.WriteLine("Encryption key not set in settings");
                return 0;
            }
            
            try
            {
                var sw = Stopwatch.StartNew();
                
                // Simple XOR encryption using the encryption key from settings
                byte[] fileBytes = File.ReadAllBytes(filePath);
                byte[] keyBytes = Encoding.UTF8.GetBytes(settings.EncryptionKey);
                
                for (int i = 0; i < fileBytes.Length; i++)
                {
                    fileBytes[i] = (byte)(fileBytes[i] ^ keyBytes[i % keyBytes.Length]);
                }
                
                File.WriteAllBytes(filePath, fileBytes);
                
                sw.Stop();
                Console.WriteLine($"File encrypted: {filePath}");
                
                return (int)sw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error encrypting file: {ex.Message}");
                return 0;
            }
        }
    }
} 