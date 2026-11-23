using System.Net.Sockets;
using System.Text;

namespace LibraryManagementSystem.Network;

public class LibraryNetworkClient
{
    private readonly string _host;
    private readonly int _port;

    public LibraryNetworkClient(string host = "127.0.0.1", int port = 8888)
    {
        _host = host;
        _port = port;
    }

    public async Task<string> SendCommandAsync(string command, int timeoutMs = 5000)
    {
        using var client = new TcpClient();
        using var cts = new CancellationTokenSource(timeoutMs);

        try
        {
            await client.ConnectAsync(_host, _port, cts.Token);
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            // Read welcome greeting
            var greeting = await reader.ReadLineAsync(cts.Token);

            // Send command
            await writer.WriteLineAsync(command);

            // Read response
            var response = await reader.ReadLineAsync(cts.Token);
            return response ?? "No response received from server.";
        }
        catch (OperationCanceledException)
        {
            return $"Connection timed out after {timeoutMs / 1000} seconds. Ensure the Library Network Server is running.";
        }
        catch (SocketException ex)
        {
            return $"Connection error ({ex.SocketErrorCode}): Could not connect to {_host}:{_port}. Is the server started?";
        }
        catch (Exception ex)
        {
            return $"Network error: {ex.Message}";
        }
    }
}
