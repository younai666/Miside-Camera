using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Miside.Camera.Core;
using Miside.Camera.Desktop;
using Miside.Camera.Local;

static void Require(bool value, string message) { if (!value) throw new Exception(message); }

var messages = new ConcurrentQueue<string>();
string pipeName = "Miside.Camera.Checks." + Guid.NewGuid().ToString("N");
bool enabled = false;
using (var server = new LocalControlServer(pipeName, () => new { enabled }, operation => {
    if (operation == "enable") enabled = true;
    else throw new FormatException("Unknown management operation");
}, messages.Enqueue)) {
    var state = await HostConnection.RequestAsync(pipeName, "enable");
    Require(state.GetProperty("enabled").GetBoolean(), "A successful command must return real state");
    bool rejected = false;
    try { await HostConnection.RequestAsync(pipeName, "not-a-command"); }
    catch (IOException error) { rejected = error.Message == "Unknown management operation"; }
    Require(rejected, "Rejected commands must return a visible error, never success");
    state = await HostConnection.RequestAsync(pipeName, "status");
    Require(state.GetProperty("enabled").GetBoolean(), "A rejected command must not terminate management");
    using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
    await client.ConnectAsync(1000);
    byte[] request = Encoding.UTF8.GetBytes("{\"version\":1,\"operation\":\"status\"}\n");
    foreach (byte value in request) await client.WriteAsync(new[] { value });
    using var reader = new StreamReader(client);
    using var timeout = new CancellationTokenSource(2000);
    using var response = JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);
    Require(response.RootElement.GetProperty("state").GetProperty("enabled").GetBoolean(), "Fragmented requests must reassemble");
}
Console.WriteLine("PASS: management state, rejection and fragmented requests");

string malformedPipe = pipeName + ".malformed";
using (var malformed = new NamedPipeServerStream(malformedPipe, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous)) {
    var serving = Task.Run(async () => {
        await malformed.WaitForConnectionAsync();
        using var reader = new StreamReader(malformed, leaveOpen: true);
        await reader.ReadLineAsync();
        await malformed.WriteAsync(Encoding.UTF8.GetBytes("{\"version\":1,\"state\":\"invalid\"}\n"));
    });
    bool rejected = false;
    try { await HostConnection.RequestAsync(malformedPipe, "status"); }
    catch (IOException) { rejected = true; }
    await serving;
    Require(rejected, "Malformed plugin state must fail at the IPC boundary");
}
Console.WriteLine("PASS: malformed plugin state rejected");

string configuration = Path.Combine(Path.GetTempPath(), "Miside.Camera.Checks." + Guid.NewGuid().ToString("N") + ".cfg");
try {
    Require(WorkspaceManager.ReadIni(configuration, "Control", "AccessToken") is null, "Absent configuration must not claim a running code");
    WorkspaceManager.SetIni(configuration, "Other", "AccessToken", "untouched");
    WorkspaceManager.SetIni(configuration, "Control", "AccessToken", "123456");
    Require(WorkspaceManager.ReadIni(configuration, "Control", "AccessToken") == "123456", "Read the effective host code from its control section");
    WorkspaceManager.SetIni(configuration, "Control", "AccessToken", "654321");
    Require(WorkspaceManager.ReadIni(configuration, "Control", "AccessToken") == "654321", "Detect a changed access code before reusing the game");
    Require(WorkspaceManager.ReadIni(configuration, "Other", "AccessToken") == "untouched", "Changing a code must preserve other sections");
} finally { File.Delete(configuration); }
Console.WriteLine("PASS: effective host access code and configuration updates");

string installs = Path.Combine(Path.GetTempPath(), "Miside.Camera.Installs." + Guid.NewGuid().ToString("N"));
try {
    string miside = Path.Combine(installs, "MiSide");
    string host = Path.Combine(installs, "NeuroMita", "Build", "NeuroMita-Unity");
    Directory.CreateDirectory(miside); Directory.CreateDirectory(Path.Combine(host, "BepInEx", "interop"));
    File.WriteAllText(Path.Combine(miside, "MiSideFull.exe"), "disposable detection fixture, never executable");
    File.WriteAllText(Path.Combine(host, "NeuroMita.exe"), "disposable detection fixture, never executable");
    File.WriteAllText(Path.Combine(host, "BepInEx", "interop", "Assembly-CSharp.dll"), "fixture");
    Require(HostLocator.Find(new[] { installs }) == Path.Combine(host, "NeuroMita.exe"), "Prefer a prepared host over an unprepared install");
    Require(HostLocator.Find(new[] { miside }) == Path.Combine(miside, "MiSideFull.exe"), "Detect the supported standalone MiSide host");
    Require(HostLocator.IsSupported(Path.Combine(miside, "MiSideFull.exe")), "MiSideFull must support the existing workspace lifecycle");
    Require(!HostLocator.IsSupported(Path.Combine(miside, "MiSide.exe")), "Do not claim unverified executable variants");
    Directory.CreateDirectory(Path.Combine(miside, "BepInEx", "interop"));
    File.WriteAllText(Path.Combine(miside, "BepInEx", "interop", "Assembly-CSharp.dll"), "fixture");
    Require(HostLocator.Find(new[] { miside, host }) == Path.Combine(miside, "MiSideFull.exe"), "A prepared MiSide host must be selectable without a NeuroMita override");
    Require(HostLocator.AdapterHost(Path.Combine(miside, "MiSideFull.exe")) == "miside"
        && HostLocator.AdapterFiles(Path.Combine(miside, "MiSideFull.exe")).Contains("Miside.Camera.MiSide.dll"), "MiSide must receive its own Unity bindings");
    Require(HostLocator.AdapterHost(Path.Combine(host, "NeuroMita.exe")) == "neuromita"
        && !HostLocator.AdapterFiles(Path.Combine(host, "NeuroMita.exe")).Contains("Miside.Camera.MiSide.dll"), "NeuroMita deployment must retain its own adapter");
    Require(HostLocator.Find(new[] { Path.Combine(installs, "absent") }) is null, "Missing installs must permit manual selection");
    string file = Path.Combine(installs, "settings.json");
    var migrated = JsonSerializer.Deserialize<DesktopSettings>("{\"FfmpegPath\":\"Z:/old-dev/ffmpeg.exe\",\"MediaMtxPath\":\"Z:/old-dev/mediamtx.exe\"}")!;
    migrated.GamePath = Path.Combine(host, "NeuroMita.exe"); migrated.Save(file);
    var loaded = DesktopSettings.Load(file); loaded.DetectHost();
    Require(loaded.GamePath == migrated.GamePath, "A valid saved manual selection must survive auto-detection");
    Require(!Path.IsPathFullyQualified(loaded.FfmpegPath) && !File.ReadAllText(file).Contains("old-dev"), "Legacy machine-specific tool paths must not survive migration");
    Require(loaded.FfmpegExecutable.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase), "Bundled tools must resolve against the application, never the working directory");
    string encoder = Path.Combine(installs, "release", "tools", "ffmpeg", "ffmpeg.exe");
    string relative = WorkspaceManager.RelativeEncoderPath(loaded.GamePath, encoder);
    Require(!relative.Contains('\\') && !Path.IsPathFullyQualified(relative), "BepInEx relative values must avoid backslash string escapes");
    Require(Path.GetFullPath(relative, Path.GetDirectoryName(loaded.GamePath)!) == encoder, "Relative host configuration must resolve to this bundle's encoder");
} finally { Directory.Delete(installs, true); }
Console.WriteLine("PASS: host discovery, manual persistence and portable tool path migration");

using (var receiver = new PoseReceiver(0, () => new CameraStatus("active"), messages.Enqueue, accessToken: "123456")) {
    string Packet(string code, long seq) => JsonSerializer.Serialize(new {
        version = 2, sequence = seq, timestampNs = seq,
        source = "manual", position = new { x = .25, y = -.1, z = .2 }, rotation = new { x = 0, y = 0, z = 0, w = 1 },
        zoom = 2, recenter = 1, tracking = true, accessToken = code,
        client = new { name = "disposable-check", roundTripMs = 1, videoPhase = "playing", width = 1920, height = 1080, frames = seq, bufferedMs = 50 }
    }) + "\n";
    using (var wrong = new TcpClient()) {
        await wrong.ConnectAsync("127.0.0.1", receiver.Port);
        await wrong.GetStream().WriteAsync(Encoding.UTF8.GetBytes(Packet("000000", 1)));
        using var reader = new StreamReader(wrong.GetStream());
        using var response = JsonDocument.Parse((await reader.ReadLineAsync())!);
        Require(response.RootElement.GetProperty("error").GetString() == "access-code", "Invalid codes must be rejected");
    }
    using (var client = new TcpClient { NoDelay = true }) {
        await client.ConnectAsync("127.0.0.1", receiver.Port);
        using var reader = new StreamReader(client.GetStream());
        for (int i = 1; i <= 120; i++) {
            var packet = Encoding.UTF8.GetBytes(Packet("123456", i));
            await client.GetStream().WriteAsync(packet.AsMemory(0, 7));
            await client.GetStream().WriteAsync(packet.AsMemory(7));
            using var timeout = new CancellationTokenSource(2000);
            using var response = JsonDocument.Parse((await reader.ReadLineAsync(timeout.Token))!);
            Require(response.RootElement.GetProperty("ack").GetInt64() == i, "Buffered Pose transport must preserve every sequence");
        }
        Require(receiver.Latest?.Frame.Position.X == .25f && receiver.Latest.Client?.Width == 1920, "Pose and video telemetry must reach the receiver");
    }
}
Console.WriteLine("PASS: Pose authentication, split frames and 120 sequential acknowledgements");
// Validate complete satellite resources and retained messages, not just startup text.
var resources = new System.Resources.ResourceManager("Miside.Camera.Desktop.Resources.Strings", typeof(Localization).Assembly);
var keys = resources.GetResourceSet(System.Globalization.CultureInfo.InvariantCulture, true, false)!
    .Cast<System.Collections.DictionaryEntry>().Select(entry => (string)entry.Key).ToHashSet();
var savedError = Localization.E("PortOccupied", 8554);
var savedLog = new LogLine(DateTime.Now, Localization.M("MediaRecoveryFailed", savedError.Text));
string previous = "";
foreach (string language in new[] { "zh-CN", "en", "ru" }) {
    var culture = language == "en" ? System.Globalization.CultureInfo.InvariantCulture : System.Globalization.CultureInfo.GetCultureInfo(language);
    var entries = resources.GetResourceSet(culture, true, false)!.Cast<System.Collections.DictionaryEntry>().ToArray();
    Require(keys.SetEquals(entries.Select(entry => (string)entry.Key)), "Every language must contain the same resource keys");
    foreach (var entry in entries) {
        string text = (string)entry.Value!;
        Require(!string.IsNullOrWhiteSpace(text), "Empty translation: " + entry.Key);
        _ = string.Format(culture, text, 1.25, "file.dll");
    }
    Localization.Current.Use(language);
    Require(savedError.Message.Contains("8554") && savedError.Message != previous, "Retained errors must switch language and retain arguments");
    Require(savedLog.Text.Contains(savedError.Message), "Existing log entries must switch language");
    previous = savedError.Message;
    string file = Path.Combine(Path.GetTempPath(), "Miside.Camera.Language." + Guid.NewGuid().ToString("N"), "settings.json");
    try {
        new DesktopSettings { Language = language }.Save(file);
        Require(DesktopSettings.Load(file).Language == language, "Language must survive settings reload");
    } finally { File.Delete(file); Directory.Delete(Path.GetDirectoryName(file)!); }
}
Localization.Current.Use("zh-CN");
Console.WriteLine("PASS: three complete resource sets, live errors/logs, persisted language");
var networkSettings = JsonSerializer.Deserialize<DesktopSettings>("{\"LanEnabled\":true}")!;
Require(networkSettings.ConnectionMode == "lan", "Existing LAN preferences must retain their mode");
networkSettings.ConnectionMode = "usb";
Require(networkSettings.UsbEnabled && !networkSettings.LanEnabled && networkSettings.NetworkEnabled, "USB must enable the existing authenticated network path");
var reloadedNetwork = JsonSerializer.Deserialize<DesktopSettings>(JsonSerializer.Serialize(networkSettings))!;
Require(reloadedNetwork.ConnectionMode == "usb", "USB mode must survive settings reload");
networkSettings.ConnectionMode = "local";
Require(!networkSettings.NetworkEnabled, "Local mode must retain loopback isolation");
Require(UsbNetwork.IsUsbDevice(@"USB\VID_18D1&PID_4EE3\device") && !UsbNetwork.IsUsbDevice(@"PCI\device"), "USB identification must use PnP ancestry, not a localized adapter name");
using (var discovery = new DesktopDiscovery("usb", () => true, _ => { }, 0)) {
    using var udp = new UdpClient();
    await udp.SendAsync(Encoding.UTF8.GetBytes("MISIDE_CAMERA_DISCOVER_V1"), new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, discovery.Port));
    using var deadline = new CancellationTokenSource(1500);
    var reply = await udp.ReceiveAsync(deadline.Token);
    using var metadata = JsonDocument.Parse(reply.Buffer);
    Require(metadata.RootElement.GetProperty("transport").GetString() == "usb" && metadata.RootElement.GetProperty("requiresCode").GetBoolean(), "USB discovery must advertise the authenticated existing channels");
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(System.Net.IPAddress.Loopback, discovery.Port, deadline.Token);
    await tcp.GetStream().WriteAsync(Encoding.UTF8.GetBytes("MISIDE_CAMERA_DISCOVER_V1\n"), deadline.Token);
    using var reader = new StreamReader(tcp.GetStream());
    using var tcpReply = JsonDocument.Parse((await reader.ReadLineAsync(deadline.Token))!);
    Require(tcpReply.RootElement.GetProperty("videoPort").GetInt32() == 8554 && tcpReply.RootElement.GetProperty("ready").GetBoolean(), "TCP discovery must preserve emulator and manual-address fallback");
}
Console.WriteLine("PASS: USB/LAN settings and real UDP/TCP discovery (disposable port)");
Console.WriteLine("All checks use disposable pipes and an ephemeral loopback port; no running workspace is contacted.");
