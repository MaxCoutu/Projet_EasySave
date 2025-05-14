using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Projet.Service
{
    public class FileManager
    {
        private readonly string _filePath;
        private readonly string _key;

        public FileManager(string path, string key)
        {
            _filePath = path;
            _key      = key;
        }

        private bool CheckFile()
        {
            if (File.Exists(_filePath)) return true;
            Console.WriteLine("File not found.");
            Thread.Sleep(1000);
            return false;
        }

     
        public int TransformFile()
        {
            if (!CheckFile()) return -1;

            var sw = Stopwatch.StartNew();
            byte[] data = File.ReadAllBytes(_filePath);
            byte[] key  = Encoding.UTF8.GetBytes(_key);

            for (int i = 0; i < data.Length; i++)
                data[i] ^= key[i % key.Length];

            File.WriteAllBytes(_filePath, data);
            sw.Stop();
            return (int)sw.ElapsedMilliseconds;
        }
    }
}
