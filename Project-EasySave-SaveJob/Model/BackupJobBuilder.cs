using System.ComponentModel;
using Projet.Model;

public class BackupJobBuilder : INotifyPropertyChanged
{
    private string _name;
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    private string _sourceDir;
    public string SourceDir
    {
        get => _sourceDir;
        set { _sourceDir = value; OnPropertyChanged(nameof(SourceDir)); }
    }

    private string _targetDir;
    public string TargetDir
    {
        get => _targetDir;
        set { _targetDir = value; OnPropertyChanged(nameof(TargetDir)); }
    }

    private string _type;
    public string Type
    {
        get => _type;
        set
        {
            _type = value;
            OnPropertyChanged(nameof(Type));
            // Met à jour la stratégie automatiquement selon le type choisi
            if (!string.IsNullOrWhiteSpace(_type))
            {
                Strategy = _type.ToLower() == "diff"
                    ? (IBackupStrategy)new DifferentialBackupStrategy()
                    : new FullBackupStrategy();
            }
            else
            {
                Strategy = null;
            }
        }
    }

    private IBackupStrategy _strategy;
    public IBackupStrategy Strategy
    {
        get => _strategy;
        set { _strategy = value; OnPropertyChanged(nameof(Strategy)); }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public BackupJob Build()
    {
        return new BackupJob
        {
            Name = this.Name,
            SourceDir = this.SourceDir,
            TargetDir = this.TargetDir,
            Strategy = this.Strategy
        };
    }

    public BackupJobBuilder WithName(string name)
    {
        this.Name = name;
        return this;
    }

    public BackupJobBuilder WithSource(string sourceDir)
    {
        this.SourceDir = sourceDir;
        return this;
    }

    public BackupJobBuilder WithTarget(string targetDir)
    {
        this.TargetDir = targetDir;
        return this;
    }

    public BackupJobBuilder WithStrategy(IBackupStrategy strategy)
    {
        this.Strategy = strategy;
        return this;
    }
}
