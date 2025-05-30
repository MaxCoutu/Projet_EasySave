using System;
using System.Diagnostics;
using System.IO;
using Projet.Infrastructure;

namespace Projet.Service
{
    public static class PackageBlocker
    {
        private static IBackupService _backupService;

        public static void Initialize(IBackupService backupService)
        {
            _backupService = backupService;
        }

        public static bool IsBlocked(Settings s)
        {
            var procs = Process.GetProcesses();
            foreach (string exe in s.BlockedPackages)
            {
                string name = Path.GetFileNameWithoutExtension(exe);
                if (Array.Exists(procs, p =>
                        p.ProcessName.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    // Si un processus bloquant est détecté, on met en pause tous les jobs
                    if (_backupService != null)
                    {
                        _backupService.PauseAllJobs();
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
