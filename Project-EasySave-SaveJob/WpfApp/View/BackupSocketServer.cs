using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using Projet.Infrastructure; // For StatusEntry

public class BackupSocketServer
{
    private readonly string _statusFilePath;
    private TcpListener _listener;

    public BackupSocketServer(string statusFilePath, int port = 5555)
    {
        _statusFilePath = statusFilePath;
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    public void Start()
    {
        _listener.Start();
        Task.Run(async () =>
        {
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        });
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            byte[] buffer = new byte[1024];
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            if (request == "GET_JOBS")
            {
                var jobs = ReadStatusEntries(_statusFilePath);
                string json = JsonSerializer.Serialize(jobs);
                byte[] response = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(response, 0, response.Length);
            }
            else if (request.StartsWith("PAUSE:") || request.StartsWith("RESUME:") || request.StartsWith("STOP:"))
            {
                var parts = request.Split(':');
                if (parts.Length == 2)
                {
                    string command = parts[0];
                    string jobName = parts[1];

                    var jobs = ReadStatusEntries(_statusFilePath);
                    var job = jobs.Find(j => j.Name == jobName);

                    if (job != null)
                    {
                        switch (command)
                        {
                            case "PAUSE":
                                job.State = "Paused";
                                break;
                            case "RESUME":
                                job.State = "Running";
                                break;
                            case "STOP":
                                job.State = "Stopped";
                                job.Progression = 0;
                                job.NbFilesLeftToDo = job.TotalFilesToCopy;
                                break;
                        }

                        // Réécriture du fichier JSON
                        WriteStatusEntries(_statusFilePath, jobs);
                    }
                }
            }
            // sinon : requête non reconnue (ignorer)
        }
    }

    private static void WriteStatusEntries(string filePath, List<StatusEntry> jobs)
    {
        string json = JsonSerializer.Serialize(jobs, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }



    private static List<StatusEntry> ReadStatusEntries(string statusFilePath)
    {
        if (!File.Exists(statusFilePath))
            return new List<StatusEntry>();

        string json = File.ReadAllText(statusFilePath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<StatusEntry>();

        return JsonSerializer.Deserialize<List<StatusEntry>>(json) ?? new List<StatusEntry>();
    }
}
