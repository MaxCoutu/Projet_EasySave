using Projet.Infrastructure;
using Projet.Service;
using Projet.Model;
using Projet.ViewModel;
using System.Windows;

namespace WpfApp
{
    public partial class App : Application
    {
        private static readonly IPathProvider PathProvider = new DefaultPathProvider();
        private static readonly ILogger Logger = new JsonLogger(PathProvider);
        private static readonly IJobRepository JobRepo = new TxtJobRepository(PathProvider);
        private static readonly Settings AppSettings = Settings.Load(PathProvider);

        public static BackupService BackupService { get; } = new BackupService(Logger, JobRepo, AppSettings);
    }
}
