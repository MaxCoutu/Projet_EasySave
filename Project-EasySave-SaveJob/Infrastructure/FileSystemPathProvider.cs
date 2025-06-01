using System;
using System.IO;

namespace Projet.Infrastructure
{
    public class FileSystemPathProvider : IPathProvider
    {
        private readonly string _baseDir;

        public FileSystemPathProvider(string baseDir)
        {
            _baseDir = baseDir ?? throw new ArgumentNullException(nameof(baseDir));
            
            // Ensure base directory exists
            Directory.CreateDirectory(_baseDir);
            Directory.CreateDirectory(GetLogDir());
            Directory.CreateDirectory(GetStatusDir());
        }

        public string GetBaseDir()
        {
            return _baseDir;
        }

        public string GetLogDir()
        {
            return Path.Combine(_baseDir, "logs");
        }

        public string GetStatusDir()
        {
            return Path.Combine(_baseDir, "status");
        }
    }
} 