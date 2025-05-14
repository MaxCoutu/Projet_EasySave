using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.Infrastructure
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string JobName { get; set; } = "";
        public string SourcePath { get; set; } = "";
        public string DestPath { get; set; } = "";
        public long FileSize { get; set; }
        public int TransferTimeMs { get; set; }
        public int EncryptionTimeMs { get; set; }

        public LogEntry() { }

        public LogEntry(DateTime ts, string job, string src, string dst, long size, int ms)
        {
            Timestamp = ts;
            JobName = job;
            SourcePath = src;
            DestPath = dst;
            FileSize = size;
            TransferTimeMs = ms;
        }
    }
}
