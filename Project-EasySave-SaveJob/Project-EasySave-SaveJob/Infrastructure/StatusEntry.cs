using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Projet.Infrastructure
{
    public class StatusEntry : INotifyPropertyChanged
    {
        private string _name = "";
        private string _sourceFilePath = "";
        private string _targetFilePath = "";
        private string _state = "";
        private int _totalFilesToCopy;
        private long _totalFilesSize;
        private int _nbFilesLeftToDo;
        private double _progression;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value);
        }

        public string SourceFilePath
        {
            get => _sourceFilePath;
            set => SetField(ref _sourceFilePath, value);
        }

        public string TargetFilePath
        {
            get => _targetFilePath;
            set => SetField(ref _targetFilePath, value);
        }

        public string State
        {
            get => _state;
            set => SetField(ref _state, value);
        }

        public int TotalFilesToCopy
        {
            get => _totalFilesToCopy;
            set => SetField(ref _totalFilesToCopy, value);
        }

        public long TotalFilesSize
        {
            get => _totalFilesSize;
            set => SetField(ref _totalFilesSize, value);
        }

        public int NbFilesLeftToDo
        {
            get => _nbFilesLeftToDo;
            set => SetField(ref _nbFilesLeftToDo, value);
        }

        public double Progression
        {
            get => _progression;
            set => SetField(ref _progression, value);
        }

        public StatusEntry() { }

        public StatusEntry(string name, string src, string dst, string state,
                           int totalFiles, long totalSize, int left, double progression)
        {
            _name = name;
            _sourceFilePath = src;
            _targetFilePath = dst;
            _state = state;
            _totalFilesToCopy = totalFiles;
            _totalFilesSize = totalSize;
            _nbFilesLeftToDo = left;
            _progression = progression;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
