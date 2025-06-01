using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;

namespace Projet
{
    public class BlockingProcessTest
    {
        public static async Task RunTest()
        {
            Console.WriteLine("=== Starting Blocking Process Monitor Test ===");
            
            // Create test directories
            string baseDir = Path.Combine(Path.GetTempPath(), "EasySaveTest");
            string sourceDir = Path.Combine(baseDir, "Source");
            string targetDir = Path.Combine(baseDir, "Target");
            
            // Clean up any previous test directories
            if (Directory.Exists(baseDir))
            {
                Directory.Delete(baseDir, true);
            }
            
            // Create directories
            Directory.CreateDirectory(sourceDir);
            Directory.CreateDirectory(targetDir);
            
            Console.WriteLine("Created test directories:");
            Console.WriteLine($"- Source: {sourceDir}");
            Console.WriteLine($"- Target: {targetDir}");
            
            // Create test files
            CreateTestFiles(sourceDir, 5, 512 * 1024); // 5 files of 512KB each
            
            Console.WriteLine("Created test files");
            
            // Initialize services
            var logger = new ConsoleLogger();
            var repo = new InMemoryJobRepository();
            var settings = new Settings();
            
            // Add a blocking process name for testing
            string testProcessName = "notepad";
            settings.BlockedPackages.Add(testProcessName);
            
            // Configure monitoring settings
            settings.AutoMonitoringEnabled = true;
            settings.ProcessMonitoringIntervalMs = 2000; // Check every 2 seconds
            
            // Create backup service
            var backupService = new BackupService(logger, repo, settings);
            
            // Subscribe to status updates
            backupService.StatusUpdated += (status) =>
            {
                Console.WriteLine($"Status: {status.Name}, State: {status.State}, Progress: {status.Progression:F2}%");
            };
            
            // Create backup job
            var job = new BackupJob
            {
                Name = "TestJob",
                SourceDir = sourceDir,
                TargetDir = targetDir,
                Strategy = new FullBackupStrategy()
            };
            
            // Add job
            backupService.AddJob(job);
            
            Console.WriteLine("Added backup job");
            
            // Start the job in the background
            Task backupTask = Task.Run(() => backupService.ExecuteBackupAsync(job.Name));
            
            // Wait a moment for the job to start
            await Task.Delay(1000);
            
            // Launch the blocking process
            Console.WriteLine($"Launching blocking process: {testProcessName}");
            var process = Process.Start(testProcessName);
            
            // Wait to demonstrate pausing
            Console.WriteLine("Wait a few seconds to see backup job pause...");
            await Task.Delay(5000);
            
            // Close the process
            Console.WriteLine("Closing blocking process...");
            process.CloseMainWindow();
            
            // Wait for process to exit
            process.WaitForExit(5000);
            if (!process.HasExited)
            {
                process.Kill();
            }
            
            // Wait for backup to complete
            Console.WriteLine("Wait for backup job to resume and complete...");
            await Task.WhenAny(backupTask, Task.Delay(30000)); // Max wait 30 seconds
            
            if (!backupTask.IsCompleted)
            {
                Console.WriteLine("Backup task taking too long, cancelling...");
                backupService.CancelAllBackups();
            }
            
            // Verify results
            VerifyBackupResults(sourceDir, targetDir);
            
            Console.WriteLine("=== Blocking Process Monitor Test Completed ===");
        }
        
        private static void CreateTestFiles(string directory, int count, int size)
        {
            var random = new Random();
            var buffer = new byte[size];
            
            for (int i = 0; i < count; i++)
            {
                string filePath = Path.Combine(directory, $"file{i:D3}.dat");
                random.NextBytes(buffer);
                
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    fileStream.Write(buffer, 0, size);
                }
            }
        }
        
        private static void VerifyBackupResults(string sourceDir, string targetDir)
        {
            var sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            var targetFiles = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories);
            
            Console.WriteLine($"Verifying backup: {sourceDir} -> {targetDir}");
            Console.WriteLine($"Source files: {sourceFiles.Length}, Target files: {targetFiles.Length}");
            
            if (sourceFiles.Length != targetFiles.Length)
            {
                Console.WriteLine("ERROR: File count mismatch");
                return;
            }
            
            foreach (var sourceFile in sourceFiles)
            {
                string relativePath = Path.GetRelativePath(sourceDir, sourceFile);
                string targetFile = Path.Combine(targetDir, relativePath);
                
                if (!File.Exists(targetFile))
                {
                    Console.WriteLine($"ERROR: Target file missing: {relativePath}");
                    continue;
                }
                
                var sourceInfo = new FileInfo(sourceFile);
                var targetInfo = new FileInfo(targetFile);
                
                if (sourceInfo.Length != targetInfo.Length)
                {
                    Console.WriteLine($"ERROR: Size mismatch for {relativePath}: {sourceInfo.Length} vs {targetInfo.Length}");
                }
            }
            
            Console.WriteLine("Verification completed");
        }
        
        // Simple console logger for testing
        private class ConsoleLogger : ILogger
        {
            public void LogEvent(LogEntry entry)
            {
                Console.WriteLine($"LOG: {entry.JobName} - {entry.SourcePath} -> {entry.DestPath}, Size: {entry.FileSize} bytes");
            }
            
            public void UpdateStatus(StatusEntry status)
            {
                // Status updates are handled by the event in the main test
            }
        }
        
        // Simple in-memory job repository for testing
        private class InMemoryJobRepository : IJobRepository
        {
            private List<BackupJob> _jobs = new List<BackupJob>();
            
            public IReadOnlyList<BackupJob> Load()
            {
                return _jobs.AsReadOnly();
            }
            
            public void Save(IReadOnlyList<BackupJob> jobs)
            {
                _jobs = new List<BackupJob>(jobs);
            }
        }
    }
} 