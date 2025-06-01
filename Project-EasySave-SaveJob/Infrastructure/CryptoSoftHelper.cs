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
        // Signature bytes to identify encrypted files (EasySave Encrypted)
        private static readonly byte[] EncryptionSignature = new byte[] { 0x45, 0x53, 0x45, 0x43 }; // ESEC in ASCII

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
                
                // Check if file is already encrypted
                if (IsFileEncrypted(filePath))
                {
                    Console.WriteLine($"File is already encrypted: {filePath}");
                    return 0;
                }
                
                // Read the file content
                byte[] fileBytes = File.ReadAllBytes(filePath);
                byte[] keyBytes = Encoding.UTF8.GetBytes(settings.EncryptionKey);
                
                // Create new array with signature + encrypted content
                byte[] encryptedBytes = new byte[EncryptionSignature.Length + fileBytes.Length];
                
                // Copy signature at the beginning
                EncryptionSignature.CopyTo(encryptedBytes, 0);
                
                // Apply XOR encryption to file content
                for (int i = 0; i < fileBytes.Length; i++)
                {
                    encryptedBytes[i + EncryptionSignature.Length] = (byte)(fileBytes[i] ^ keyBytes[i % keyBytes.Length]);
                }
                
                // Write the encrypted content back to the file
                File.WriteAllBytes(filePath, encryptedBytes);
                
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
        
        /// <summary>
        /// Decrypts a file using the provided encryption key
        /// </summary>
        /// <param name="filePath">Path to the encrypted file</param>
        /// <param name="key">Encryption key</param>
        /// <returns>True if decryption was successful, false otherwise</returns>
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
        
        /// <summary>
        /// Checks if a file is encrypted by looking for the encryption signature
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        /// <returns>True if the file is encrypted, false otherwise</returns>
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
    }
} 