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
        // Propriété spéciale pour forcer la mise à jour de l'UI
        private int _refreshCounter = 0;
        public int RefreshCounter 
        { 
            get => _refreshCounter; 
            set 
            { 
                _refreshCounter = value;
                OnPropertyChanged(nameof(RefreshCounter));
            } 
        }

        public string Name { get; set; } = string.Empty;
        public string SourceDir { get; set; } = string.Empty;
        public string TargetDir { get; set; } = string.Empty;
        public IBackupStrategy Strategy { get; set; } = null!;
        public Settings Settings { get; set; } = null!;

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
                    // Forcer un rafraîchissement global
                    IncrementRefreshCounter();
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
                    // Forcer un rafraîchissement global
                    IncrementRefreshCounter();
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

        private string _lastError = string.Empty;
        public string LastError 
        { 
            get => _lastError; 
            set 
            { 
                if (_lastError != value)
                {
                    _lastError = value;
                    OnPropertyChanged(nameof(LastError));
                    // Forcer un rafraîchissement global
                    IncrementRefreshCounter();
                }
            } 
        }

        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        // Incrémenter le compteur de rafraîchissement pour forcer la mise à jour
        private void IncrementRefreshCounter()
        {
            RefreshCounter++;
        }
        
        // Méthode publique pour forcer le rafraîchissement de l'interface
        public void OnProgressPropertyChanged()
        {
            // Cette méthode force le rafraîchissement des propriétés sans changer leur valeur
            OnPropertyChanged(nameof(Progression));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(TotalFilesToCopy));
            OnPropertyChanged(nameof(NbFilesLeftToDo));
            OnPropertyChanged(nameof(TotalFilesSize));
            
            // Forcer un rafraîchissement global
            IncrementRefreshCounter();
        }
        
        // Méthode spéciale pour forcer un rafraîchissement très agressif
        public void ForceProgressRefresh()
        {
            // Ajouter une petite variation à la progression pour forcer un rafraîchissement
            // même si la valeur réelle n'a pas changé - exactement comme ce qui se passe
            // lors d'une transition pause/resume
            var originalValue = _progression;
            
            // Si l'état est actif ou en pause, faire un rafraîchissement agressif
            if (State == "ACTIVE" || State == "PENDING" || State == "PAUSED")
            {
                // Simuler le cycle complet pause/resume pour forcer l'UI à se rafraîchir
                // 1. Notification complète de toutes les propriétés
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(Progression));
                OnPropertyChanged(nameof(TotalFilesToCopy));
                OnPropertyChanged(nameof(NbFilesLeftToDo));
                OnPropertyChanged(nameof(TotalFilesSize));
                
                // 2. Micro-oscillation de la valeur (comme dans pause/resume)
                if (_progression > 0 && _progression < 99.9)
                {
                    // Créer une micro-oscillation plus importante pour forcer le rafraîchissement
                    Random rnd = new Random();
                    double oscillation = rnd.NextDouble() * 0.05; // Jusqu'à 0.05% de variation
                    
                    // Micro-modification temporaire pour forcer la notification
                    _progression += oscillation;
                    OnPropertyChanged(nameof(Progression));
                    
                    // Restaurer la valeur d'origine
                    _progression = originalValue;
                    OnPropertyChanged(nameof(Progression));
                    
                    // Notification finale pour s'assurer que tous les écouteurs sont notifiés
                    OnPropertyChanged(nameof(Progression));
                }
                // Cas spécial: traitement quand la progression est à 100%
                else if (_progression >= 99.9)
                {
                    // Forcer la valeur exactement à 100% pour garantir un affichage parfait
                    _progression = 100.0;
                    OnPropertyChanged(nameof(Progression));
                    
                    // Si l'état est "END", s'assurer que les fichiers sont marqués comme totalement copiés
                    if (State == "END")
                    {
                        // S'assurer que le nombre de fichiers restants est à 0
                        _nbFilesLeftToDo = 0;
                        OnPropertyChanged(nameof(NbFilesLeftToDo));
                    }
                }
                
                // Si on est dans l'état PAUSED, simuler un cycle pause-resume-pause
                // pour forcer une mise à jour complète de l'interface
                if (State == "PAUSED")
                {
                    string originalState = State;
                    
                    // Simuler un passage temporaire à ACTIVE pour forcer le rafraîchissement
                    State = "ACTIVE";
                    OnPropertyChanged(nameof(State));
                    
                    // Revenir à l'état d'origine
                    State = originalState;
                    OnPropertyChanged(nameof(State));
                }
                
                // Forcer un rafraîchissement global
                IncrementRefreshCounter();
                IncrementRefreshCounter(); // Double incrémentation pour s'assurer que la valeur a changé
            }
            else
            {
                // Pour les autres états, simplement notifier
                OnPropertyChanged(nameof(Progression));
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(TotalFilesToCopy));
                OnPropertyChanged(nameof(NbFilesLeftToDo));
                OnPropertyChanged(nameof(TotalFilesSize));
                
                // Forcer un rafraîchissement global
                IncrementRefreshCounter();
            }
        }

        public Task RunAsync(Action<StatusEntry> cb) => Strategy.ExecuteAsync(this, cb);
    }
}
