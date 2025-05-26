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
            string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (request.Trim() == "GET_JOBS")
            {
                var jobs = ReadStatusEntries(_statusFilePath);
                string json = JsonSerializer.Serialize(jobs);
                byte[] response = Encoding.UTF8.GetBytes(json);
                await stream.WriteAsync(response, 0, response.Length);
            }
        }
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
