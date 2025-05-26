using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Projet.Infrastructure;

namespace Projet.Model
{
    public abstract class BackupStrategyBase: IBackupStrategy
    {
        public abstract string Type { get; }

        public abstract Task ExecuteAsync(BackupJob job, Action<StatusEntry> progressCallback);

        protected static async Task CopyFileAsync(string src, string dst)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? ".");
            using FileStream source = File.OpenRead(src);
            using FileStream destination = File.Create(dst);
            await source.CopyToAsync(destination);
        }
    }
}
