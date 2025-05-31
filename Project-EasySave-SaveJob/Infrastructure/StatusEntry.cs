using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.Infrastructure
{
    public class StatusEntry
    {
        public string Name { get; set; } = "";
        public string SourceFilePath { get; set; } = "";
        public string TargetFilePath { get; set; } = "";
        public string State { get; set; } = "";  
        public int TotalFilesToCopy { get; set; }
        public long TotalFilesSize { get; set; }
        public int NbFilesLeftToDo { get; set; }
        public double Progression { get; set; }        
        public string ErrorMessage { get; set; } = "";
        
        // Memory allocation information
        public int BufferSize { get; set; }
        public int ActiveJobs { get; set; }
        public double MemoryPercentage { get; set; }

        public StatusEntry() { }   

        public StatusEntry(string name, string src, string dst, string state,
                           int totalFiles, long totalSize, int left, double progression)
        {
            Name = name;
            SourceFilePath = src;
            TargetFilePath = dst;
            State = state;
            TotalFilesToCopy = totalFiles;
            TotalFilesSize = totalSize;
            NbFilesLeftToDo = left;
            Progression = progression;
        }
        
        public StatusEntry(string name, string src, string dst, string state,
                           int totalFiles, long totalSize, int left, double progression,
                           string errorMessage) : this(name, src, dst, state, totalFiles, totalSize, left, progression)
        {
            ErrorMessage = errorMessage;
        }
        
        public StatusEntry(string name, string src, string dst, string state,
                           int totalFiles, long totalSize, int left, double progression,
                           int bufferSize, int activeJobs, double memoryPercentage) 
            : this(name, src, dst, state, totalFiles, totalSize, left, progression)
        {
            BufferSize = bufferSize;
            ActiveJobs = activeJobs;
            MemoryPercentage = memoryPercentage;
        }
    }
}
