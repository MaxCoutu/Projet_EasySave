using Projet.Infrastructure;
using Projet.Service;
using Projet.ViewModel;
using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Client
{
    public partial class App : Application
    {
        public static IPathProvider PathProvider { get; private set; }
        public static IBackupService BackupService { get; private set; }
        public static ILanguageService LanguageService { get; private set; }

        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "BackupMonitorApp_Mutex";
            bool createdNew;

            _mutex = new Mutex(true, mutexName, out createdNew);

            if (!createdNew)
            {
                MessageBox.Show("L'application est déjà en cours d'exécution.", "Instance existante", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            PathProvider = new DefaultPathProvider();
            string baseDir = PathProvider.GetBaseDir();

            var settings = Settings.Load(PathProvider);
            var logger = new JsonLogger(PathProvider);
            var repo = new TxtJobRepository(PathProvider);
            BackupService = new BackupService(logger, repo, settings);

            Directory.CreateDirectory(Path.Combine(baseDir, "Languages"));
            string en = Path.Combine(baseDir, "Languages", "en.json");
            LanguageService = new JsonLanguageService(en);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
