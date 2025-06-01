using System;
using System.IO;
using System.Threading.Tasks;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;
using Projet.View;
using Projet.ViewModel;

namespace Projet
{
    internal class Program
    {
        private static async Task Main()
        {
            Console.WriteLine("EasySave Backup System");
            Console.WriteLine("1. Start normal application");
            Console.WriteLine("2. Run parallel backup test");
            Console.WriteLine("3. Test large file transfer limitation");
            Console.Write("Choose an option (1-3): ");
            
            string option = Console.ReadLine()?.Trim();
            
            if (option == "2")
            {
                // Run the parallel backup test
                await ParallelBackupTest.RunTest();
                return;
            }
            else if (option == "3")
            {
                // Run the large file transfer test
                await TestLargeFileTransferAsync();
                return;
            }
            
            // Continue with normal application startup
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
            ILogger logger = new DualFormatLogger(paths);
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

        public static async Task TestLargeFileTransferAsync()
        {
            Console.WriteLine("=== Testing Large File Transfer Limitation ===");
            
            // Create test directories
            string baseDir = Path.Combine(Path.GetTempPath(), "EasySaveTest");
            string sourceDir1 = Path.Combine(baseDir, "LargeSource1");
            string sourceDir2 = Path.Combine(baseDir, "LargeSource2");
            string targetDir1 = Path.Combine(baseDir, "LargeTarget1");
            string targetDir2 = Path.Combine(baseDir, "LargeTarget2");
            
            // Clean up any previous test directories
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, true);
            }
            
            // Create directories
            Directory.CreateDirectory(sourceDir1);
            Directory.CreateDirectory(sourceDir2);
            Directory.CreateDirectory(targetDir1);
            Directory.CreateDirectory(targetDir2);
            
            Console.WriteLine("Created test directories");
            
            // Create test paths
            var paths = new FileSystemPathProvider(baseDir);
            
            // Create a settings object with a lower threshold for large files (100KB)
            var settings = new Settings(paths)
            {
                MaxFileSizeKB = 100 // 100KB threshold for large files
            };
            settings.Save();
            
            Console.WriteLine($"Set large file threshold to {settings.MaxFileSizeKB}KB");
            
            // Create large test files (200KB each)
            CreateLargeFile(Path.Combine(sourceDir1, "large1.dat"), 200 * 1024);
            CreateLargeFile(Path.Combine(sourceDir1, "small1.dat"), 50 * 1024);
            CreateLargeFile(Path.Combine(sourceDir2, "large2.dat"), 200 * 1024);
            CreateLargeFile(Path.Combine(sourceDir2, "small2.dat"), 50 * 1024);
            
            Console.WriteLine("Created test files");
            
            // Create logger
            var logger = new DualFormatLogger(paths);
            
            // Create repository
            var repo = new JsonJobRepository(paths);
            
            // Create backup service
            var backupService = new BackupService(logger, repo, settings);
            
            // Subscribe to status updates
            backupService.StatusUpdated += (status) =>
            {
                Console.WriteLine($"Status: {status.Name}, State: {status.State}, Progress: {status.Progression:F2}%");
                if (status.IsLargeFile)
                {
                    Console.WriteLine($"  Large file: {Path.GetFileName(status.SourceFilePath)}, Status: {status.LargeFileTransferStatus}");
                }
            };
            
            // Create backup jobs
            var job1 = new BackupJob
            {
                Name = "LargeFileTest1",
                SourceDir = sourceDir1,
                TargetDir = targetDir1,
                Strategy = new FullBackupStrategy()
            };
            
            var job2 = new BackupJob
            {
                Name = "LargeFileTest2",
                SourceDir = sourceDir2,
                TargetDir = targetDir2,
                Strategy = new FullBackupStrategy()
            };
            
            // Add jobs
            backupService.AddJob(job1);
            backupService.AddJob(job2);
            
            Console.WriteLine("Added backup jobs");
            
            // Run jobs in parallel
            Console.WriteLine("Starting backup jobs in parallel");
            var task1 = backupService.ExecuteBackupAsync(job1.Name);
            var task2 = backupService.ExecuteBackupAsync(job2.Name);
            
            // Wait for both jobs to complete
            await Task.WhenAll(task1, task2);
            
            Console.WriteLine("All backup jobs completed");
            
            // Verify results
            bool success = VerifyFileExists(Path.Combine(targetDir1, "large1.dat")) &&
                          VerifyFileExists(Path.Combine(targetDir1, "small1.dat")) &&
                          VerifyFileExists(Path.Combine(targetDir2, "large2.dat")) &&
                          VerifyFileExists(Path.Combine(targetDir2, "small2.dat"));
            
            Console.WriteLine($"Test result: {(success ? "SUCCESS" : "FAILURE")}");
            Console.WriteLine("=== Large File Transfer Test Completed ===");
        }

        private static void CreateLargeFile(string path, int size)
        {
            Console.WriteLine($"Creating test file: {path} ({size / 1024}KB)");
            using (var fs = new FileStream(path, FileMode.Create))
            {
                var random = new Random();
                var buffer = new byte[4096];
                
                int remaining = size;
                while (remaining > 0)
                {
                    int chunkSize = Math.Min(buffer.Length, remaining);
                    random.NextBytes(buffer);
                    fs.Write(buffer, 0, chunkSize);
                    remaining -= chunkSize;
                }
            }
        }

        private static bool VerifyFileExists(string path)
        {
            bool exists = File.Exists(path);
            Console.WriteLine($"Verifying file: {path} - {(exists ? "EXISTS" : "MISSING")}");
            return exists;
        }
    }
}
