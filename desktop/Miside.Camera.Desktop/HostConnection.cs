using static Miside.Camera.Desktop.Localization;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Miside.Camera.Desktop;

internal static class HostConnection
{
    public const string CameraPipe = "Miside.Camera.Host.v1", VideoPipe = "Miside.Camera.Video.v1";
    public static async Task<JsonElement> RequestAsync(string name, string operation, CancellationToken token = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(2000);
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(350, timeout.Token);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, true);
        await writer.WriteLineAsync(JsonSerializer.Serialize(new { version = 1, operation }));
        string line = await reader.ReadLineAsync(timeout.Token) ?? throw E("HostClosed");
        if (line.Length > 32768) throw E("HostStateTooLarge");
        try {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (root.GetProperty("version").GetInt32() != 1) throw E("HostIncompatible");
            if (root.TryGetProperty("error", out var error)) throw new IOException(error.GetString() ?? T("HostRejected"));
            var state = root.GetProperty("state");
            if (state.ValueKind != JsonValueKind.Object) throw E("HostInvalidState");
            return state.Clone();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) {
            throw new LocalizedException(M("HostInvalidState"), e);
        }
    }

    public static async Task<JsonElement?> TryStatusAsync(string name, CancellationToken token)
    {
        try { return await RequestAsync(name, "status", token); }
        catch (Exception e) when (e is IOException or TimeoutException or OperationCanceledException) { return null; }
    }
}
