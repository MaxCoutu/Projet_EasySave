using Projet.Infrastructure;
using System;

namespace Projet.Service
{
   
    internal static class CryptoSoftHelper
    {
        public static int Encrypt(string filePath, Settings settings)
        {
            try
            {
                // Utiliser l'implémentation avec signature dans Infrastructure.CryptoSoftHelper
                return Infrastructure.CryptoSoftHelper.Encrypt(filePath, settings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CryptoSoftHelper.Encrypt: {ex.Message}");
                return -999;              
            }
        }
        
        public static bool Decrypt(string filePath, string key)
        {
            try
            {
                // Utiliser l'implémentation de déchiffrement dans Infrastructure.CryptoSoftHelper
                return Infrastructure.CryptoSoftHelper.Decrypt(filePath, key);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CryptoSoftHelper.Decrypt: {ex.Message}");
                return false;
            }
        }
        
        public static bool IsFileEncrypted(string filePath)
        {
            try
            {
                // Utiliser l'implémentation de vérification dans Infrastructure.CryptoSoftHelper
                return Infrastructure.CryptoSoftHelper.IsFileEncrypted(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in CryptoSoftHelper.IsFileEncrypted: {ex.Message}");
                return false;
            }
        }
    }
}
