using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Miside.Camera.Core;

public sealed record ClientTelemetry(string Name = "Android", long RoundTripMs = 0, string VideoPhase = "idle", int Width = 0, int Height = 0, long Frames = 0, long BufferedMs = 0);

public sealed record ReceivedPose(PoseFrame Frame, long Connection, long ReceivedAt, string Peer = "", ClientTelemetry? Client = null)
{
    public bool IsFresh => (Stopwatch.GetTimestamp() - ReceivedAt) / (double)Stopwatch.Frequency < 1.5;
}

/// <summary>One loopback client, bounded NDJSON frames, latest pose only. No Unity calls.</summary>
public sealed class PoseReceiver : IDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource stop = new();
    private readonly Action<string> log;
    private readonly Func<CameraStatus> status;
    private readonly Task worker;
    private readonly string accessToken;
    private ReceivedPose? latest;
    public ReceivedPose? Latest => Volatile.Read(ref latest);
    public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public PoseReceiver(int port, Func<CameraStatus> status, Action<string> log, string listenAddress = "127.0.0.1", string accessToken = "")
    {
        this.status = status;
        this.log = log;
        this.accessToken = accessToken;
        var address = IPAddress.Parse(listenAddress);
        if (!IPAddress.IsLoopback(address) && string.IsNullOrEmpty(accessToken)) throw new ArgumentException("LAN control requires an access code");
        listener = new TcpListener(address, port);
        listener.Start();
        worker = Task.Run(Run);
        log($"Pose receiver listening on {listenAddress}:{Port}");
    }

    private async Task Run()
    {
        long connection = 0;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                using var client = await listener.AcceptTcpClientAsync(stop.Token);
                using var close = stop.Token.Register(client.Close);
                long current = ++connection;
                client.NoDelay = true;
                client.ReceiveTimeout = 3000;
                client.SendTimeout = 1500;
                long sequence = 0, timestamp = 0;
                log($"Pose client {current} connected");
                try
                {
                    using var stream = client.GetStream();
                    using var input = new BufferedStream(stream, 4096);
                    var bytes = new byte[4096];
                    while (!stop.IsCancellationRequested)
                    {
                        int length = 0;
                        while (true)
                        {
                            int next = input.ReadByte();
                            if (next == -1)
                            {
                                if (length != 0) throw new FormatException("Truncated JSON frame.");
                                goto EndSession;
                            }
                            if (next == 10) break;
                            if (length == bytes.Length) throw new FormatException("Frame exceeds 4096 bytes.");
                            bytes[length++] = (byte)next;
                        }
                        string message = Utf8.GetString(bytes, 0, length);
                        using (var packet = JsonDocument.Parse(message, new JsonDocumentOptions { MaxDepth = 8 }))
                        {
                        if (sequence == 0 && accessToken.Length > 0 &&
                            (!packet.RootElement.TryGetProperty("accessToken", out var token) || token.GetString() != accessToken))
                        {
                            stream.Write(Utf8.GetBytes("{\"version\":1,\"error\":\"access-code\"}\n"));
                            throw new FormatException("Incorrect Desktop access code");
                        }
                        var pose = PoseFrame.Parse(packet.RootElement, sequence, timestamp);
                        ClientTelemetry? telemetry = null;
                        if (packet.RootElement.TryGetProperty("client", out var clientData))
                        {
                            telemetry = JsonSerializer.Deserialize<ClientTelemetry>(clientData.GetRawText(), JsonOptions);
                            if (telemetry == null || telemetry.Name == null || telemetry.VideoPhase == null || telemetry.Name.Length > 80 || telemetry.VideoPhase.Length > 32 ||
                                telemetry.RoundTripMs < 0 || telemetry.Width is < 0 or > 8192 || telemetry.Height is < 0 or > 8192 ||
                                telemetry.Frames < 0 || telemetry.BufferedMs < 0) throw new FormatException("Invalid client telemetry");
                        }
                        sequence = pose.Sequence;
                        timestamp = pose.TimestampNs;
                        Volatile.Write(ref latest, new ReceivedPose(pose, current, Stopwatch.GetTimestamp(), client.Client.RemoteEndPoint?.ToString() ?? "", telemetry));
                        byte[] ack = Utf8.GetBytes(JsonSerializer.Serialize(new { version = 1, ack = sequence, host = status() }, JsonOptions) + "\n");
                        stream.Write(ack);
                        }
                    }
                }
                catch (Exception e) when (e is IOException or SocketException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException or DecoderFallbackException)
                {
                    if (!stop.IsCancellationRequested) log($"Pose client {current} closed: {e.Message}");
                }
                EndSession:
                Volatile.Write(ref latest, null);
                log($"Pose client {current} disconnected after {sequence} frames");
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (SocketException) when (stop.IsCancellationRequested) { }
        catch (Exception e) { log($"Pose receiver stopped: {e}"); }
    }

    public void Dispose()
    {
        stop.Cancel();
        listener.Stop();
        Volatile.Write(ref latest, null);
        if (!worker.Wait(TimeSpan.FromSeconds(2))) log("Pose receiver shutdown is still pending");
        stop.Dispose();
    }
}
