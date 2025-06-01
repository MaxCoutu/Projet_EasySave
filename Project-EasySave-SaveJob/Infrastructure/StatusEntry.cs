using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.Infrastructure
{
    /// <summary>
    /// Represents the status of a backup job
    /// </summary>
    public class StatusEntry
    {
        // Job identification
        public string Name { get; set; }
        
        // State information
        public string State { get; set; } = "READY"; // READY, ACTIVE, PAUSED, END, ERROR, CANCELLED
        
        // File information
        public string SourceFilePath { get; set; } = "";
        public string TargetFilePath { get; set; } = "";
        
        // Progress information
        public int TotalFilesToCopy { get; set; } = 0;
        public long TotalFilesSize { get; set; } = 0;
        public int NbFilesLeftToDo { get; set; } = 0;
        public double Progression { get; set; } = 0; // 0-100
        
        // Priority file information
        public int PriorityFilesToCopy { get; set; } = 0;
        public bool IsPriorityFile { get; set; } = false;
        
        // Large file transfer information
        public bool IsLargeFile { get; set; } = false;
        public string LargeFileTransferStatus { get; set; } = ""; // WAITING, TRANSFERRING, COMPLETED
        
        // Error information
        public string ErrorMessage { get; set; } = "";
        
        // Resource allocation information
        public int BufferSize { get; set; } = 0;
        public int ActiveJobs { get; set; } = 0;
        public double MemoryPercentage { get; set; } = 0;
        
        // Timestamp
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        // Default constructor
        public StatusEntry()
        {
        }
        
        // Copy constructor
        public StatusEntry(StatusEntry other)
        {
            if (other != null)
            {
                Name = other.Name;
                State = other.State;
                SourceFilePath = other.SourceFilePath;
                TargetFilePath = other.TargetFilePath;
                TotalFilesToCopy = other.TotalFilesToCopy;
                TotalFilesSize = other.TotalFilesSize;
                NbFilesLeftToDo = other.NbFilesLeftToDo;
                Progression = other.Progression;
                PriorityFilesToCopy = other.PriorityFilesToCopy;
                IsPriorityFile = other.IsPriorityFile;
                IsLargeFile = other.IsLargeFile;
                LargeFileTransferStatus = other.LargeFileTransferStatus;
                ErrorMessage = other.ErrorMessage;
                BufferSize = other.BufferSize;
                ActiveJobs = other.ActiveJobs;
                MemoryPercentage = other.MemoryPercentage;
                Timestamp = other.Timestamp;
            }
        }
        
        public override string ToString()
        {
            return $"{Name}: {State} - {Progression:F2}% ({NbFilesLeftToDo}/{TotalFilesToCopy} files remaining)";
        }
    }
}
