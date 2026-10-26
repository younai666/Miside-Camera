using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Miside.Camera.Local;

// A current-user-only management channel. It never carries Pose or video frames.
internal sealed class LocalControlServer : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly Task worker;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public LocalControlServer(string name, Func<object> snapshot, Action<string> command, Action<string> log)
    {
        worker = Task.Run(async () =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(stop.Token);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                    timeout.CancelAfter(2000);
                    var bytes = new byte[4096]; int count = 0;
                    while (true)
                    {
                        if (count == bytes.Length) throw new FormatException("Management request exceeds 4096 bytes");
                        int read = await pipe.ReadAsync(bytes.AsMemory(count, 1), timeout.Token);
                        if (read == 0) throw new IOException("Management client disconnected");
                        if (bytes[count] == 10) break;
                        count++;
                    }
                    string responseJson;
                    try {
                        using var doc = JsonDocument.Parse(bytes.AsMemory(0, count));
                        if (doc.RootElement.GetProperty("version").GetInt32() != 1) throw new FormatException("Unsupported management version");
                        string operation = doc.RootElement.GetProperty("operation").GetString() ?? "";
                        if (operation != "status") command(operation);
                        responseJson = JsonSerializer.Serialize(new { version = 1, state = snapshot() }, Json);
                    }
                    catch (Exception error) when (error is JsonException or FormatException or KeyNotFoundException or InvalidOperationException) {
                        log("Desktop management: " + error.Message);
                        responseJson = JsonSerializer.Serialize(new { version = 1, error = error.Message }, Json);
                    }
                    byte[] response = Encoding.UTF8.GetBytes(responseJson + "\n");
                    await pipe.WriteAsync(response, timeout.Token);
                }
                catch (OperationCanceledException) { }
                catch (Exception error) when (error is IOException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
                { if (!stop.IsCancellationRequested) log("Desktop management: " + error.Message); }
            }
        });
    }

    public void Dispose() { stop.Cancel(); worker.Wait(2500); stop.Dispose(); }
}
