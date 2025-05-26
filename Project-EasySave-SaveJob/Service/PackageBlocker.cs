using System;
using System.Diagnostics;
using System.IO;
using Projet.Infrastructure;

namespace Projet.Service
{
    public static class PackageBlocker
    {
        public static bool IsBlocked(Settings s)
        {
            var procs = Process.GetProcesses();
            foreach (string exe in s.BlockedPackages)
            {
                string name = Path.GetFileNameWithoutExtension(exe);
                if (Array.Exists(procs, p =>
                        p.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }
    }
}
