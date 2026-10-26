using static Miside.Camera.Desktop.Localization;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace Miside.Camera.Desktop;

public sealed record WorkspaceSnapshot(string Phase, bool Running, bool CameraRequested, bool VideoRequested,
    int GamePid, int RelayPid, JsonElement? Host, JsonElement? Video, string Error);

public sealed class WorkspaceManager : IAsyncDisposable
{
    private readonly DesktopSettings settings;
    private readonly Action<LocalizedText> log;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource stop = new();
    private readonly Task poll;
    private Process? game, relay;
    private DesktopDiscovery? discovery;
    private bool running, cameraRequested = true, videoRequested = true;
    private string phase = "NotStarted";
    private Exception? error;
    private JsonElement? host, video;
    private DateTime nextRelayRestart;
    private string RelayConfig => Path.Combine(DesktopSettings.DataDirectory, "mediamtx.yml");
    public event Action<WorkspaceSnapshot>? Updated;
    public bool Running => running;

    public WorkspaceManager(DesktopSettings settings, Action<LocalizedText> log)
    {
        this.settings = settings; this.log = log;
        poll = Task.Run(PollAsync);
    }

    public async Task StartAsync()
    {
        await gate.WaitAsync();
        try {
            if (running) return;
            settings.Validate();
            error = null; phase = "Starting"; Emit();
            Directory.CreateDirectory(DesktopSettings.DataDirectory);
            game = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(settings.GamePath)).FirstOrDefault(p => {
                try { return string.Equals(p.MainModule?.FileName, Path.GetFullPath(settings.GamePath), StringComparison.OrdinalIgnoreCase); }
                catch (System.ComponentModel.Win32Exception) { return false; }
            });
            if (game != null) {
                host = await HostConnection.TryStatusAsync(HostConnection.CameraPipe, stop.Token);
                if (host == null) throw E("PrepareDesktopHost");
                // An already running plugin retains its startup socket configuration.
                var expected = settings.NetworkEnabled ? IPAddress.Any : IPAddress.Loopback;
                if (!System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
                    .Any(endpoint => endpoint.Port == 39570 && endpoint.Address.Equals(expected))) throw E("RestartForNetwork");
                if (settings.NetworkEnabled && ReadIni(Path.Combine(Path.GetDirectoryName(settings.GamePath)!, "BepInEx", "config", "dev.miside.camera.cfg"),
                    "Control", "AccessToken") != settings.AccessCode) throw E("RestartForNetwork");
                string configuredEncoder = ReadIni(Path.Combine(Path.GetDirectoryName(settings.GamePath)!, "BepInEx", "config", "dev.miside.camera.streaming.cfg"), "Video", "FFmpegPath") ?? "";
                if (!configuredEncoder.Equals(RelativeEncoderPath(settings.GamePath, settings.FfmpegExecutable), StringComparison.OrdinalIgnoreCase))
                    throw E("RestartForNetwork");
            } else {
                CheckTcpPort(39570);
                DeployAdapters(); ConfigureHost();
            }
            WriteRelayConfig();
            await StartRelayAsync();
            discovery = new DesktopDiscovery(settings.ConnectionMode, () => running && host != null, log);
            if (game == null) {
                var start = new ProcessStartInfo(settings.GamePath) { WorkingDirectory = Path.GetDirectoryName(settings.GamePath)!, UseShellExecute = false };
                foreach (string arg in new[] { "-screen-fullscreen", "0", "-screen-width", "1920", "-screen-height", "1080" }) start.ArgumentList.Add(arg);
                game = Process.Start(start) ?? throw E("CannotStartGame");
                log(M("GameStarted", game.Id));
            }
            cameraRequested = videoRequested = true; running = true;
            phase = "WaitingHostPlugin"; Emit();
            await WaitForAsync(HostConnection.CameraPipe, _ => true, 45000);
            await WaitForAsync(HostConnection.VideoPipe, _ => true, 10000);
            await HostConnection.RequestAsync(HostConnection.CameraPipe, "enable");
            await HostConnection.RequestAsync(HostConnection.VideoPipe, "enable");
            host = await HostConnection.TryStatusAsync(HostConnection.CameraPipe, stop.Token);
            video = await HostConnection.TryStatusAsync(HostConnection.VideoPipe, stop.Token);
            phase = "WaitingAndroid";
            log(M("WorkspaceReady"));
        } catch (Exception e) {
            error = e; phase = "StartFailed";
            discovery?.Dispose(); discovery = null;
            await StopRelayAsync();
            // Preserve an adopted/running host for the user, but allow startup to be retried.
            running = false; host = video = null; game?.Dispose(); game = null;
            log(MessageFor(e)); throw;
        } finally { Emit(); gate.Release(); }
    }

    public async Task SetCameraAsync(bool enabled)
    {
        await gate.WaitAsync();
        try {
            RequireRunning();
            await HostConnection.RequestAsync(HostConnection.CameraPipe, enabled ? "enable" : "disable");
            if (!enabled) await WaitForAsync(HostConnection.CameraPipe, s => s.GetProperty("camera").GetProperty("phase").GetString() == "released", 2500);
            cameraRequested = enabled; log(M(enabled ? "AdapterEnabled" : "AdapterPaused"));
        } finally { Emit(); gate.Release(); }
    }

    public async Task SetVideoAsync(bool enabled)
    {
        await gate.WaitAsync();
        try {
            RequireRunning();
            if (enabled) { await StartRelayAsync(); await HostConnection.RequestAsync(HostConnection.VideoPipe, "enable"); }
            else { await DisableVideoAsync(); await StopRelayAsync(); }
            videoRequested = enabled; error = null;
            log(M(enabled ? "StreamEnabled" : "StreamStopped"));
        } finally { Emit(); gate.Release(); }
    }

    public async Task RestartMediaAsync()
    {
        await gate.WaitAsync();
        try {
            RequireRunning();
            await DisableVideoAsync(); await StopRelayAsync(); await StartRelayAsync();
            await HostConnection.RequestAsync(HostConnection.VideoPipe, "enable");
            videoRequested = true; error = null; log(M("MediaRestarted"));
        } finally { Emit(); gate.Release(); }
    }

    public async Task StopAsync()
    {
        await gate.WaitAsync();
        try {
            if (!running && relay == null) return;
            phase = "Closing"; Emit();
            try {
            if (game is { HasExited: false }) {
                await DisableVideoAsync();
                await HostConnection.RequestAsync(HostConnection.CameraPipe, "disable");
                // Wait for the main/render thread to release ownership, not just an IPC acknowledgement.
                await WaitForAsync(HostConnection.CameraPipe, s => s.GetProperty("camera").GetProperty("phase").GetString() == "released", 2500);
                if (settings.CloseGameOnStop) {
                    await HostConnection.RequestAsync(HostConnection.CameraPipe, "quit");
                    using var timeout = new CancellationTokenSource(15000);
                    try { await game.WaitForExitAsync(timeout.Token); }
                    catch (OperationCanceledException) { throw E("GameExitPending"); }
                }
            }
            } catch (Exception e) when ((e is IOException or TimeoutException or OperationCanceledException) && game is { HasExited: true }) {
                // The user can close the game while an IPC shutdown request is in flight.
                log(M("CleanupAfterExit"));
            }
            await StopRelayAsync(); discovery?.Dispose(); discovery = null;
            running = false; cameraRequested = videoRequested = false; phase = "NotStarted"; error = null;
            host = video = null; game?.Dispose(); game = null;
            log(M("WorkspaceStopped"));
        } catch (Exception e) { error = e; log(MessageFor(e)); throw; }
        finally { Emit(); gate.Release(); }
    }

    private async Task DisableVideoAsync()
    {
        await HostConnection.RequestAsync(HostConnection.VideoPipe, "disable");
        await WaitForAsync(HostConnection.VideoPipe, s => !s.GetProperty("enabled").GetBoolean() && s.GetProperty("encoderPid").GetInt32() == 0, 3500);
    }

    private static async Task WaitForAsync(string pipe, Func<JsonElement, bool> condition, int milliseconds)
    {
        using var timeout = new CancellationTokenSource(milliseconds);
        try {
            while (true) {
                var value = await HostConnection.TryStatusAsync(pipe, timeout.Token);
                if (value.HasValue && condition(value.Value)) return;
                await Task.Delay(100, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested) {
            throw E("WaitHostTimeout", milliseconds / 1000.0);
        }
    }

    private void RequireRunning() { if (!running) throw E("StartWorkspaceFirst"); }
    private static void CheckTcpPort(int port)
    {
        var check = new TcpListener(IPAddress.Any, port) { ExclusiveAddressUse = true };
        try { check.Start(); }
        catch (SocketException) { throw E("PortOccupied", port); }
        finally { check.Stop(); }
    }

    private async Task StartRelayAsync()
    {
        if (relay is { HasExited: false }) return;
        relay?.Dispose();
        relay = null;
        CheckTcpPort(8554);
        CheckTcpPort(9997);
        var start = new ProcessStartInfo(settings.MediaMtxExecutable) { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = DesktopSettings.DataDirectory };
        start.ArgumentList.Add(RelayConfig);
        relay = Process.Start(start) ?? throw E("RelayStartFailed");
        relay.OutputDataReceived += (_, e) => { if (e.Data != null) log(M("MediaLog", e.Data)); };
        relay.ErrorDataReceived += (_, e) => { if (e.Data != null) log(M("MediaLog", e.Data)); };
        relay.BeginOutputReadLine(); relay.BeginErrorReadLine();
        using var timeout = new CancellationTokenSource(5000);
        while (!timeout.IsCancellationRequested) {
            if (relay.HasExited) throw E("RelayExited");
            try { using var socket = new TcpClient(); await socket.ConnectAsync(IPAddress.Loopback, 8554, timeout.Token); log(M("RelayReady", relay.Id)); return; }
            catch (SocketException) { await Task.Delay(100, timeout.Token); }
        }
        throw E("RelayNotReady");
    }

    private async Task StopRelayAsync()
    {
        if (relay == null) return;
        if (!relay.HasExited) { relay.Kill(); await relay.WaitForExitAsync(); }
        relay.Dispose(); relay = null;
    }

    private async Task PollAsync()
    {
        try {
            while (!stop.IsCancellationRequested) {
                await Task.Delay(500, stop.Token);
                if (!await gate.WaitAsync(0, stop.Token)) continue;
                try {
                    if (running) {
                        if (game == null || game.HasExited) {
                            log(M("StopServicesAfterExit"));
                            await StopRelayAsync(); discovery?.Dispose(); discovery = null;
                            game?.Dispose(); game = null; running = false; host = video = null; phase = "GameExited";
                        } else {
                            host = await HostConnection.TryStatusAsync(HostConnection.CameraPipe, stop.Token);
                            video = await HostConnection.TryStatusAsync(HostConnection.VideoPipe, stop.Token);
                            phase = host == null ? "WaitingHostPlugin" : video == null ? "WaitingVideoPlugin" :
                                !host.Value.GetProperty("connected").GetBoolean() ? "WaitingAndroid" :
                                videoRequested && (relay == null || relay.HasExited || video.Value.GetProperty("phase").GetString() != "publishing") ? "StreamRecovering" :
                                cameraRequested && host.Value.GetProperty("camera").GetProperty("phase").GetString() != "active" ? "WaitingCamera" : "Running";
                            if (videoRequested && (relay == null || relay.HasExited) && DateTime.UtcNow >= nextRelayRestart) {
                                nextRelayRestart = DateTime.UtcNow.AddSeconds(5);
                                try { await StartRelayAsync(); error = null; log(M("MediaRecovered")); }
                                catch (Exception e) { error = e; log(M("MediaRecoveryFailed", MessageFor(e))); }
                            }
                        }
                    }
                    Emit();
                } finally { gate.Release(); }
            }
        } catch (OperationCanceledException) { }
    }

    public void Refresh() => Emit();

    private void Emit() => Updated?.Invoke(new(T(phase), running, cameraRequested, videoRequested,
        game is { HasExited: false } ? game.Id : 0, relay is { HasExited: false } ? relay.Id : 0, host, video, error?.Message ?? ""));

    private void DeployAdapters()
    {
        string root = Path.GetDirectoryName(Path.GetFullPath(settings.GamePath))!;
        foreach (string name in new[] { "Miside.Camera.Plugin.dll", "Miside.Camera.Core.dll", "Miside.Camera.NeuroMita.dll", "Miside.Camera.Streaming.dll" }) {
            string source = Path.Combine(AppContext.BaseDirectory, "adapters", name);
            if (!File.Exists(source)) throw E("MissingAdapter", name);
            string directory = Path.Combine(root, "BepInEx", "plugins", name.Contains("Streaming") ? "MisideVideo" : "MisideCamera");
            Directory.CreateDirectory(directory);
            string destination = Path.Combine(directory, name);
            if (File.Exists(destination) && File.ReadAllBytes(source).AsSpan().SequenceEqual(File.ReadAllBytes(destination))) continue;
            if (File.Exists(destination)) {
                string backup = Path.Combine(DesktopSettings.DataDirectory, "adapter-backups", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(backup); File.Copy(destination, Path.Combine(backup, name), true);
            }
            File.Copy(source, destination, true);
        }
        log(M("AdaptersDeployed"));
    }

    private void ConfigureHost()
    {
        string config = Path.Combine(Path.GetDirectoryName(settings.GamePath)!, "BepInEx", "config");
        Directory.CreateDirectory(config);
        SetIni(Path.Combine(config, "dev.miside.camera.cfg"), "Control", "ListenAddress", settings.NetworkEnabled ? "0.0.0.0" : "127.0.0.1");
        SetIni(Path.Combine(config, "dev.miside.camera.cfg"), "Control", "AccessToken", settings.NetworkEnabled ? settings.AccessCode : "");
        SetIni(Path.Combine(config, "dev.miside.camera.cfg"), "Control", "Port", "39570");
        SetIni(Path.Combine(config, "dev.miside.camera.streaming.cfg"), "Video", "Enabled", "true");
        SetIni(Path.Combine(config, "dev.miside.camera.streaming.cfg"), "Video", "FFmpegPath", RelativeEncoderPath(settings.GamePath, settings.FfmpegExecutable));
        SetIni(Path.Combine(config, "BepInEx.cfg"), "Logging.Console", "Enabled", "false");
    }

    // BepInEx unescapes relative string values: backslashes such as \t or \r corrupt a path.
    internal static string RelativeEncoderPath(string gamePath, string encoder)
        => Path.GetRelativePath(Path.GetDirectoryName(gamePath)!, encoder).Replace('\\', '/');

    internal static void SetIni(string path, string section, string key, string value)
    {
        var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
        int start = lines.FindIndex(l => l.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
        if (start < 0) { lines.Add(""); lines.Add("[" + section + "]"); lines.Add(key + " = " + value); }
        else {
            int end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith('[')); if (end < 0) end = lines.Count;
            int at = lines.FindIndex(start + 1, end - start - 1, l => l.Split('=', 2)[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase));
            if (at >= 0) lines[at] = key + " = " + value; else lines.Insert(end, key + " = " + value);
        }
        File.WriteAllLines(path, lines);
    }

    internal static string? ReadIni(string path, string section, string key)
    {
        if (!File.Exists(path)) return null;
        bool active = false;
        foreach (string line in File.ReadLines(path)) {
            string text = line.Trim();
            if (text.StartsWith('[')) { active = text.Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase); continue; }
            if (!active || text.StartsWith('#')) continue;
            string[] pair = text.Split('=', 2);
            if (pair.Length == 2 && pair[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return pair[1].Trim();
        }
        return null;
    }

    private void WriteRelayConfig()
    {
        string address = settings.NetworkEnabled ? "0.0.0.0" : "127.0.0.1";
        string auth = settings.NetworkEnabled ? $"""
            authInternalUsers:
              - user: any
                ips: [127.0.0.1]
                permissions:
                  - action: publish
                    path: miside
                  - action: api
              - user: camera
                pass: '{settings.AccessCode}'
                permissions:
                  - action: read
                    path: miside
            """ : "";
        File.WriteAllText(RelayConfig, $"""
            logLevel: info
            udpReadBufferSize: 4194304
            rtsp: yes
            rtspAddress: {address}:8554
            rtspTransports: [udp, tcp]
            rtpAddress: 127.0.0.1:8000
            rtcpAddress: 127.0.0.1:8001
            rtmp: no
            hls: no
            webrtc: no
            srt: no
            moq: no
            api: yes
            apiAddress: 127.0.0.1:9997
            {auth}
            paths:
              miside:
                source: publisher
            """);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync(); stop.Cancel(); await poll;
        discovery?.Dispose(); stop.Dispose(); gate.Dispose();
    }
}
