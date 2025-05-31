using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Projet.Infrastructure;
using Projet.Model;
using Projet.Service;

namespace Projet
{
    public class ParallelBackupTest
    {
        public static async Task RunTest()
        {
            Console.WriteLine("=== Starting Parallel Backup Test ===");
            
            // Create test directories
            string baseDir = Path.Combine(Path.GetTempPath(), "EasySaveTest");
            string sourceDir1 = Path.Combine(baseDir, "Source1");
            string sourceDir2 = Path.Combine(baseDir, "Source2");
            string targetDir1 = Path.Combine(baseDir, "Target1");
            string targetDir2 = Path.Combine(baseDir, "Target2");
            
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
            
            Console.WriteLine("Created test directories:");
            Console.WriteLine($"- Source 1: {sourceDir1}");
            Console.WriteLine($"- Source 2: {sourceDir2}");
            Console.WriteLine($"- Target 1: {targetDir1}");
            Console.WriteLine($"- Target 2: {targetDir2}");
            
            // Create test files
            CreateTestFiles(sourceDir1, 10, 1024 * 1024); // 10 files of 1MB each
            CreateTestFiles(sourceDir2, 5, 2 * 1024 * 1024); // 5 files of 2MB each
            
            Console.WriteLine("Created test files");
            
            // Initialize services
            var logger = new ConsoleLogger();
            var repo = new InMemoryJobRepository();
            var settings = new Settings();
            
            // Create backup service
            var backupService = new BackupService(logger, repo, settings);
            
            // Subscribe to status updates
            backupService.StatusUpdated += (status) =>
            {
                Console.WriteLine($"Status: {status.Name}, State: {status.State}, Progress: {status.Progression:F2}%");
                if (status.ActiveJobs > 0)
                {
                    Console.WriteLine($"  Memory: {status.MemoryPercentage:F1}%, Buffer: {status.BufferSize} bytes, Active Jobs: {status.ActiveJobs}");
                }
            };
            
            // Create backup jobs
            var job1 = new BackupJob
            {
                Name = "TestJob1",
                SourceDir = sourceDir1,
                TargetDir = targetDir1,
                Strategy = new FullBackupStrategy()
            };
            
            var job2 = new BackupJob
            {
                Name = "TestJob2",
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
            VerifyBackupResults(sourceDir1, targetDir1);
            VerifyBackupResults(sourceDir2, targetDir2);
            
            Console.WriteLine("=== Parallel Backup Test Completed ===");
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
                return _jobs;
            }
            
            public void Save(IReadOnlyList<BackupJob> jobs)
            {
                _jobs = new List<BackupJob>(jobs);
            }
        }
    }
} 