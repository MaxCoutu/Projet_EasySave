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

            // Create base directories for testing
            string testSourceDir = Path.Combine(Environment.CurrentDirectory, "TestSourceDir");
            string testTargetDir = Path.Combine(Environment.CurrentDirectory, "TestTargetDir");
            
            try
            {
                // Create test directory and file if they don't exist
                if (!Directory.Exists(testSourceDir))
                {
                    Directory.CreateDirectory(testSourceDir);
                    File.WriteAllText(Path.Combine(testSourceDir, "testfile.txt"), "This is a test file content");
                    Console.WriteLine($"Created test directory and file at: {testSourceDir}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating test directory: {ex.Message}");
            }
            
            IPathProvider paths = new DefaultPathProvider();
            Settings set = Settings.Load(paths);
            ILogger logger = new JsonLogger(paths);
            IJobRepository repo = new TxtJobRepository(paths);

            IBackupService backup = new BackupService(logger, repo, set);
            ILanguageService lang = new JsonLanguageService(dictPath);
            
            // Create a test job if no jobs exist
            if (backup.GetJobs().Count == 0)
            {
                Console.WriteLine("No jobs found. Creating a test job...");
                try
                {
                    var testJob = new BackupJob
                    {
                        Name = "TestJob",
                        SourceDir = testSourceDir,
                        TargetDir = testTargetDir,
                        Strategy = new FullBackupStrategy()
                    };
                    
                    backup.AddJob(testJob);
                    Console.WriteLine($"Test job created: {testJob.Name}, Source: {testJob.SourceDir}, Target: {testJob.TargetDir}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating test job: {ex.Message}");
                }
            }

            var mainVm = new MainViewModel(backup, lang, paths);
            var addVm = new AddJobViewModel(backup);
            var remVm = new RemoveJobViewModel(backup);

            IAddJobView addView = new ConsoleAddJobView(addVm, lang);
            IRemoveJobView remView = new ConsoleRemoveJobView(remVm, backup);

            Console.WriteLine("Starting main view...");
            IMainView mainView = new ConsoleMainView(mainVm, lang, addView, remView, backup);
            mainView.Show();
        }
    }
}
