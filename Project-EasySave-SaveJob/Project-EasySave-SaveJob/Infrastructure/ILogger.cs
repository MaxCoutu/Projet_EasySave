using System;
using System.Collections.Generic;
using System.Text;

namespace Projet.Infrastructure
{
    public interface ILogger
    {
        void LogEvent(LogEntry entry);
        void UpdateStatus(StatusEntry entry);
    }
}
