using Projet.Infrastructure;

namespace Projet.Service
{
   
    internal static class CryptoSoftHelper
    {
        public static int Encrypt(string filePath, Settings settings)
        {
            try
            {
                var fm = new FileManager(filePath, settings.EncryptionKey);
                return fm.TransformFile();  
            }
            catch
            {
                return -999;              
            }
        }
    }
}
