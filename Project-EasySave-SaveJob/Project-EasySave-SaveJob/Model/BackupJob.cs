using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using Projet.Infrastructure;

namespace Projet.Model
{
    public class BackupJob : INotifyPropertyChanged
    {
        public string Name { get; set; } = string.Empty;
        public string SourceDir { get; set; } = string.Empty;
        public string TargetDir { get; set; } = string.Empty;
        public IBackupStrategy Strategy { get; set; } = null!;

        // Propriétés pour le suivi de la progression
        private string _state = "END";
        public string State 
        { 
            get => _state; 
            set 
            { 
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged(nameof(State));
                }
            } 
        }

        private double _progression;
        public double Progression 
        { 
            get => _progression; 
            set 
            { 
                if (_progression != value)
                {
                    _progression = value;
                    OnPropertyChanged(nameof(Progression));
                }
            } 
        }

        private int _totalFilesToCopy;
        public int TotalFilesToCopy 
        { 
            get => _totalFilesToCopy; 
            set 
            { 
                if (_totalFilesToCopy != value)
                {
                    _totalFilesToCopy = value;
                    OnPropertyChanged(nameof(TotalFilesToCopy));
                }
            } 
        }

        private long _totalFilesSize;
        public long TotalFilesSize 
        { 
            get => _totalFilesSize; 
            set 
            { 
                if (_totalFilesSize != value)
                {
                    _totalFilesSize = value;
                    OnPropertyChanged(nameof(TotalFilesSize));
                }
            } 
        }

        private int _nbFilesLeftToDo;
        public int NbFilesLeftToDo 
        { 
            get => _nbFilesLeftToDo; 
            set 
            { 
                if (_nbFilesLeftToDo != value)
                {
                    _nbFilesLeftToDo = value;
                    OnPropertyChanged(nameof(NbFilesLeftToDo));
                }
            } 
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Task RunAsync(Action<StatusEntry> cb) => Strategy.ExecuteAsync(this, cb);
    }
}
