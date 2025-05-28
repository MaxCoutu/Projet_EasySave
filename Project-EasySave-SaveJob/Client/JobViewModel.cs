using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Projet.Infrastructure;

public class JobViewModel : INotifyPropertyChanged
{
    private StatusEntry _job;
    private bool _isPaused;
    private bool _isStopping;

    public JobViewModel(StatusEntry job)
    {
        _job = job;
        _isPaused = _job.State == "Paused";
        _isStopping = false;

        PlayPauseCommand = new RelayCommand(async _ => await TogglePauseAsync(), _ => !_isStopping);
        StopCommand = new RelayCommand(async _ => await StopJobAsync(), _ => !_isStopping);
    }

    public string Name => _job.Name;

    public string State
    {
        get => _job.State;
        set
        {
            if (_job.State != value)
            {
                _job.State = value;
                OnPropertyChanged();

                _isPaused = _job.State == "Paused";
                OnPropertyChanged(nameof(PlayPauseLabel));
                ((RelayCommand)PlayPauseCommand).RaiseCanExecuteChanged();
                ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public double Progression
    {
        get => _job.Progression;
        set
        {
            if (Math.Abs(_job.Progression - value) > 0.001)
            {
                _job.Progression = value;
                OnPropertyChanged();
            }
        }
    }

    public string PlayPauseLabel => _isPaused ? "Play" : "Pause";

    public ICommand PlayPauseCommand { get; }
    public ICommand StopCommand { get; }

    private async Task TogglePauseAsync()
    {
        if (_isStopping) return;

        string command = _isPaused ? $"RESUME:{_job.Name}" : $"PAUSE:{_job.Name}";

        // Envoi de la commande au serveur
        bool success = await SendCommandAsync(command);

        if (success)
        {
            // Mise à jour locale seulement si succès
            State = _isPaused ? "Running" : "Paused";
        }
        // Sinon tu peux gérer un message d'erreur ou rollback ici
    }

    private async Task StopJobAsync()
    {
        if (_isStopping) return;

        _isStopping = true;
        ((RelayCommand)PlayPauseCommand).RaiseCanExecuteChanged();
        ((RelayCommand)StopCommand).RaiseCanExecuteChanged();

        bool success = await SendCommandAsync($"STOP:{_job.Name}");

        if (success)
        {
            Progression = 0;
            State = "Stopped";
        }

        _isStopping = false;
        ((RelayCommand)PlayPauseCommand).RaiseCanExecuteChanged();
        ((RelayCommand)StopCommand).RaiseCanExecuteChanged();
    }

    private async Task<bool> SendCommandAsync(string command)
    {
        try
        {
            using TcpClient client = new TcpClient();
            await client.ConnectAsync("127.0.0.1", 5555);

            NetworkStream stream = client.GetStream();
            byte[] data = Encoding.UTF8.GetBytes(command);
            await stream.WriteAsync(data, 0, data.Length);

            // Optionnel : lire la réponse serveur (si implémentée)
            // byte[] responseBuffer = new byte[256];
            // int bytesRead = await stream.ReadAsync(responseBuffer, 0, responseBuffer.Length);
            // string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);

            return true;
        }
        catch (Exception ex)
        {
            // Log ou gérer l’erreur ici (ex.Message)
            return false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
}
