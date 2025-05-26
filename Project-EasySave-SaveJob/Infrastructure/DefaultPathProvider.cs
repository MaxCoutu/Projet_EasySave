using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Projet.Infrastructure
{
    public class DefaultPathProvider: IPathProvider
    {
        private const string Base =
            @"C:\Projet";

        public string GetBaseDir()  => Base;
        public string GetLogDir()
        {
            string dir = Path.Combine(Base, "Logs");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetStatusDir()
        {
            string dir = Path.Combine(Base, "Status");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
