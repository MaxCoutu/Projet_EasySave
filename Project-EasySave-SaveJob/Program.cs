using System;
using System.IO;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;
using Projet.View;
using Projet.ViewModel;

namespace Projet
{
    internal class Program
    {
        private static void Main()
        {
            
            Console.Write("Choose language (en/fr) [en] : ");
            string code = Console.ReadLine()?.Trim().ToLower();
            if (code != "fr") code = "en";

            string dictPath = Path.Combine(AppContext.BaseDirectory,
                                           "Languages", $"{code}.json");

        
            IPathProvider  paths  = new DefaultPathProvider();
            Settings       set    = Settings.Load(paths);
            ILogger        logger = new JsonLogger(paths);
            IJobRepository repo   = new TxtJobRepository(paths);

            
            IBackupService backup = new BackupService(logger, repo, set);
            ILanguageService lang = new JsonLanguageService(dictPath);

           
            //var mainVm = new MainViewModel(backup);
            var addVm  = new AddJobViewModel(backup);
            var remVm  = new RemoveJobViewModel(backup);

            IAddJobView    addView = new ConsoleAddJobView(addVm, lang);
            IRemoveJobView remView = new ConsoleRemoveJobView(remVm, backup);

            //IMainView mainView = new ConsoleMainView(mainVm, lang, addView, remView, backup);
            //mainView.Show();
        }
    }
}
