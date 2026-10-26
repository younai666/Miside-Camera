using static Miside.Camera.Desktop.Localization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Miside.Camera.Desktop;

// One bounded LAN discovery request/reply; no Pose or media transport here.
internal sealed class DesktopDiscovery : IDisposable
{
    private readonly UdpClient socket;
    private readonly TcpListener tcp;
    private readonly CancellationTokenSource stop = new();
    private readonly Task worker;
    private readonly Task tcpWorker;
    internal int Port => ((IPEndPoint)socket.Client.LocalEndPoint!).Port;
    public DesktopDiscovery(string mode, Func<bool> ready, Action<LocalizedText> log, int port = 39569)
    {
        bool external = mode != "local";
        var address = external ? IPAddress.Any : IPAddress.Loopback;
        socket = new UdpClient(new IPEndPoint(address, port));
        tcp = new TcpListener(address, Port);
        try { tcp.Start(); }
        catch { socket.Dispose(); throw; }
        byte[] Reply() => JsonSerializer.SerializeToUtf8Bytes(new { service = "miside-camera", version = 1,
            name = Environment.MachineName, controlPort = 39570, videoPort = 8554, requiresCode = external, ready = ready(), transport = mode });
        worker = Task.Run(async () => {
            try {
                while (!stop.IsCancellationRequested) {
                    var request = await socket.ReceiveAsync(stop.Token);
                    if (request.Buffer.Length > 256 || Encoding.UTF8.GetString(request.Buffer) != "MISIDE_CAMERA_DISCOVER_V1") continue;
                    await socket.SendAsync(Reply(), request.RemoteEndPoint, stop.Token);
                }
            } catch (OperationCanceledException) { }
            catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
            catch (SocketException e) { if (!stop.IsCancellationRequested) log(M("UdpDiscoveryStopped", e.Message)); }
        });
        // Emulator NAT can drop UDP discovery while forwarding loopback TCP correctly.
        // Same service metadata and port; Pose and video remain on their own channels.
        tcpWorker = Task.Run(async () => {
            while (!stop.IsCancellationRequested) {
                try {
                    using var client = await tcp.AcceptTcpClientAsync(stop.Token);
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                    timeout.CancelAfter(500);
                    var stream = client.GetStream();
                    byte[] expected = Encoding.UTF8.GetBytes("MISIDE_CAMERA_DISCOVER_V1\n");
                    byte[] next = new byte[1];
                    int at = 0;
                    while (at < expected.Length) {
                        if (await stream.ReadAsync(next, timeout.Token) != 1 || next[0] != expected[at]) break;
                        at++;
                    }
                    if (at == expected.Length) {
                        await stream.WriteAsync(Reply(), timeout.Token);
                        await stream.WriteAsync(new byte[] { 10 }, timeout.Token);
                    }
                }
                catch (OperationCanceledException) { }
                catch (ObjectDisposedException) when (stop.IsCancellationRequested) { }
                catch (Exception e) when (e is IOException or SocketException) {
                    if (!stop.IsCancellationRequested) log(M("TcpDiscoveryError", e.Message));
                }
            }
        });
    }
    public void Dispose() { stop.Cancel(); socket.Dispose(); tcp.Stop(); Task.WaitAll(new[] { worker, tcpWorker }, 1000); stop.Dispose(); }
}
