using Projet.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

public class MonitorViewModel
{
    public ObservableCollection<JobViewModel> Jobs { get; } = new ObservableCollection<JobViewModel>();

    private bool _isPaused = false;

    public async Task LoadJobsAsync()
    {
        while (true)
        {
            if (!_isPaused)
            {
                try
                {
                    using var client = new TcpClient();
                    await client.ConnectAsync("127.0.0.1", 5555);

                    using NetworkStream stream = client.GetStream();

                    // Envoi de la requête
                    byte[] request = Encoding.UTF8.GetBytes("GET_JOBS");
                    await stream.WriteAsync(request, 0, request.Length);

                    using var ms = new MemoryStream();
                    byte[] buffer = new byte[8192];
                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        ms.Write(buffer, 0, bytesRead);
                    }

                    string json = Encoding.UTF8.GetString(ms.ToArray());
                    var jobsFromServer = JsonSerializer.Deserialize<List<StatusEntry>>(json);

                    if (jobsFromServer != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            SyncJobsCollection(jobsFromServer);
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log erreur si besoin, par ex. Console.WriteLine(ex.Message);
                }
            }

            await Task.Delay(1000);
        }
    }

    private void SyncJobsCollection(List<StatusEntry> newJobs)
    {
        // Supprimer les jobs absents dans la nouvelle liste
        for (int i = Jobs.Count - 1; i >= 0; i--)
        {
            var existingJob = Jobs[i];
            if (!newJobs.Any(j => j.Name == existingJob.Name))
            {
                Jobs.RemoveAt(i);
            }
        }

        // Mettre à jour ou ajouter les jobs
        foreach (var newJob in newJobs)
        {
            var existingJob = Jobs.FirstOrDefault(j => j.Name == newJob.Name);
            if (existingJob == null)
            {
                Jobs.Add(new JobViewModel(newJob));
            }
            else
            {
                existingJob.State = newJob.State;
                existingJob.Progression = newJob.Progression;

                // Si tu as ces propriétés dans JobViewModel
                // existingJob.NbFilesLeftToDo = newJob.NbFilesLeftToDo;
                // existingJob.TotalFilesToCopy = newJob.TotalFilesToCopy;

                // Avertir que tout a changé pour mettre à jour l’affichage
                // Si JobViewModel expose une méthode RaiseAllPropertiesChanged(), tu peux l’appeler ici.
            }
        }
    }
}
