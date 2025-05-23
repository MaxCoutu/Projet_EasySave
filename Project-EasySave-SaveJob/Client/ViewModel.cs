using System.Collections.ObjectModel;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Projet.Infrastructure; 

public class MonitorViewModel
{
    public ObservableCollection<StatusEntry> Jobs { get; } = new ObservableCollection<StatusEntry>();

    public async Task LoadJobsAsync()
    {
        using (var client = new TcpClient())
        {
            await client.ConnectAsync("127.0.0.1", 5555);
            using (var stream = client.GetStream())
            {
                byte[] request = Encoding.UTF8.GetBytes("GET_JOBS");
                await stream.WriteAsync(request, 0, request.Length);

                byte[] buffer = new byte[8192];
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                string json = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                var jobs = JsonSerializer.Deserialize<List<StatusEntry>>(json);
                Jobs.Clear();
                foreach (var job in jobs)
                    Jobs.Add(job);
            }
        }
    }
}
