using Projet.Infrastructure;
using Projet.Service;
using System.Windows;

namespace WpfApp
{
    public partial class App : Application
    {
        private static readonly IPathProvider PathProvider = new DefaultPathProvider();
        private static readonly ILogger Logger = new JsonLogger(PathProvider);
        private static readonly IJobRepository JobRepo = new TxJobRepository();
        private static readonly Settings AppSettings = new Settings(); // ou charge tes settings ici

        public static BackupService BackupService { get; } = new BackupService(Logger, JobRepo, AppSettings);
    }
}
