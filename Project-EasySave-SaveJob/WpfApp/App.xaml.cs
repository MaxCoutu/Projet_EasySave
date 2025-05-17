using Projet.Infrastructure;
using Projet.Service;
using Projet.Model;
using Projet.ViewModel;
using System.Windows;
using System.IO;

namespace WpfApp
{
    public partial class App : Application
    {
        public static IPathProvider PathProvider { get; private set; }
        public static IBackupService BackupService { get; private set; }
        public static ILanguageService LanguageService { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
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
    }
}
