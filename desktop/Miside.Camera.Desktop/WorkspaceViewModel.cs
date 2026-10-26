using static Miside.Camera.Desktop.Localization;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace Miside.Camera.Desktop;

public sealed class ActionCommand(Action action, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => action();
}

public sealed class WorkspaceViewModel : Observable
{
    public DesktopSettings Settings { get; }
    public WorkspaceManager Manager { get; }
    public ObservableCollection<LogLine> Logs { get; } = new();
    private readonly string settingsFile, logFile;
    private readonly object logLock = new();
    private bool busy, running;
    private string lastRuntimeError = "";
    private Exception? commandError;
    private string page = "workspace", status = T("NotStarted"), error = "", phone = T("NotConnected"), camera = T("WaitingGame"),
        video = T("NotStarted"), resolution = "—", fps = "—", latency = "—", quaternion = "—", scene = "—", fov = "—", source = "—", sequence = "—";
    public bool Busy { get => busy; private set { Set(ref busy, value); Changed(nameof(CanEdit)); Changed(nameof(CanChooseLanguage)); CommandManager.InvalidateRequerySuggested(); } }
    public bool Running { get => running; private set { Set(ref running, value); Changed(nameof(StartLabel)); Changed(nameof(CanEdit)); } }
    public bool CanEdit => !Busy && !Running;
    public bool CanChooseLanguage => !Busy;
    public IReadOnlyList<ConnectionModeOption> ConnectionModes { get; } = new[] {
        new ConnectionModeOption("local", "LocalEmulator"), new ConnectionModeOption("usb", "UsbMode"), new ConnectionModeOption("lan", "LanConnection")
    };
    private string usbAddresses = "";
    private DateTime nextUsbRefresh;
    public string UsbConnectionInfo => usbAddresses.Length > 0 ? usbAddresses : T("UsbWaiting");
    public ICommand RefreshUsbCommand { get; }
    private void RefreshUsb()
    {
        usbAddresses = UsbNetwork.Addresses(); nextUsbRefresh = DateTime.UtcNow.AddSeconds(5);
        Changed(nameof(UsbConnectionInfo));
    }
    public IReadOnlyList<LanguageOption> Languages { get; } = new[] {
        new LanguageOption("zh-CN", T("Chinese")), new LanguageOption("en", T("English")), new LanguageOption("ru", T("Russian"))
    };
    public string SelectedLanguage {
        get => Settings.Language;
        set {
            if (value is not ("zh-CN" or "en" or "ru") || value == Settings.Language) return;
            try {
                // Persist the language without committing unfinished path/code edits in the settings form.
                var persisted = DesktopSettings.Load(settingsFile);
                persisted.Language = value; persisted.Save(settingsFile);
                Settings.Language = value; Localization.Current.Use(value);
            } catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) {
                commandError = error; Error = error.Message;
            }
            Changed(nameof(SelectedLanguage));
        }
    }
    public string Page { get => page; set { if (!Set(ref page, value)) return; foreach (string name in new[] { nameof(IsWorkspace), nameof(IsSettings), nameof(IsLogs), nameof(IsAndroid), nameof(IsCamera), nameof(IsVideo), nameof(IsPose), nameof(ShowConnection), nameof(ShowCamera), nameof(ShowVideo), nameof(ShowInspector), nameof(PageTitle) }) Changed(name); } }
    public bool IsWorkspace => Page == "workspace";
    public bool IsSettings => Page == "settings";
    public bool IsLogs => Page == "logs";
    public bool IsAndroid => Page == "android";
    public bool IsCamera => Page == "camera";
    public bool IsVideo => Page == "video";
    public bool IsPose => Page == "pose";
    public bool ShowConnection => IsWorkspace || IsAndroid;
    public bool ShowCamera => IsWorkspace || IsCamera;
    public bool ShowVideo => IsWorkspace || IsVideo;
    public bool ShowInspector => !IsSettings && !IsLogs && !IsPose;
    public string PageTitle => Page switch { "android" => T("AndroidConnection"), "camera" => T("CameraAdapter"), "video" => T("VideoPage"), "pose" => T("PoseCamera"), "settings" => T("Settings"), "logs" => T("Logs"), _ => T("Workspace") };
    public string Peer { get; private set; } = "—";
    public string InputX { get; private set; } = "—";
    public string InputY { get; private set; } = "—";
    public string InputZ { get; private set; } = "—";
    public string WorldX { get; private set; } = "—";
    public string WorldY { get; private set; } = "—";
    public string WorldZ { get; private set; } = "—";
    public string Pitch { get; private set; } = "—";
    public string Yaw { get; private set; } = "—";
    public string Roll { get; private set; } = "—";
    public string Tracking { get; private set; } = "—";
    public string Discovery { get; private set; } = T("Disabled");
    public string ClientVideo { get; private set; } = "—";
    public string ClientResolution { get; private set; } = "—";
    public string Buffered { get; private set; } = "—";
    public string EncoderPid { get; private set; } = "—";
    public string RelayPid { get; private set; } = "—";
    public string GamePid { get; private set; } = "—";
    public string WorldQuaternion { get; private set; } = "—";
    public string StreamError { get; private set; } = "—";
    public string Status { get => status; private set => Set(ref status, value); }
    public string Error { get => error; private set { Set(ref error, value); Changed(nameof(HasError)); } }
    public bool HasError => Error.Length > 0;
    public string Phone { get => phone; private set => Set(ref phone, value); }
    public string Camera { get => camera; private set => Set(ref camera, value); }
    public string Video { get => video; private set => Set(ref video, value); }
    public string Resolution { get => resolution; private set => Set(ref resolution, value); }
    public string Fps { get => fps; private set => Set(ref fps, value); }
    public string Latency { get => latency; private set => Set(ref latency, value); }
    public string Quaternion { get => quaternion; private set => Set(ref quaternion, value); }
    public string Scene { get => scene; private set => Set(ref scene, value); }
    public string Fov { get => fov; private set => Set(ref fov, value); }
    public string Source { get => source; private set => Set(ref source, value); }
    public string Sequence { get => sequence; private set => Set(ref sequence, value); }
    public string AdapterState { get; private set; } = T("NotStarted");
    public string CameraAction { get; private set; } = T("PauseAdapter");
    public string VideoAction { get; private set; } = T("StopStream");
    public string StartLabel => Running ? T("StopWorkspace") : T("StartWorkspace");
    public ICommand StartCommand { get; }
    public ICommand CameraCommand { get; }
    public ICommand VideoCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand WorkspaceCommand { get; }
    public ICommand SettingsCommand { get; }
    public ICommand LogsCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand AndroidCommand { get; }
    public ICommand AdapterPageCommand { get; }
    public ICommand VideoPageCommand { get; }
    public ICommand PosePageCommand { get; }
    private bool cameraEnabled = true, videoEnabled = true;

    public WorkspaceViewModel(DesktopSettings settings, string settingsFile)
    {
        Settings = settings; this.settingsFile = settingsFile;
        Localization.Current.Use(settings.Language); Settings.Language = Localization.Current.Language;
        Directory.CreateDirectory(DesktopSettings.DataDirectory);
        logFile = Path.Combine(DesktopSettings.DataDirectory, "desktop.log");
        if (File.Exists(logFile) && new FileInfo(logFile).Length > 2_000_000) File.Move(logFile, logFile + ".previous", true);
        Manager = new(settings, Log);
        Manager.Updated += snapshot => Application.Current.Dispatcher.InvokeAsync(() => Update(snapshot));
        Localization.Current.PropertyChanged += (_, _) => {
            Changed(nameof(PageTitle)); Changed(nameof(StartLabel));
            Changed(nameof(UsbConnectionInfo));
            foreach (var mode in ConnectionModes) mode.Refresh();
            if (commandError != null) Error = commandError.Message;
            foreach (var line in Logs) line.Refresh();
            Manager.Refresh();
        };
        StartCommand = new ActionCommand(() => _ = RunAsync(async () => {
            if (Running) await Manager.StopAsync(); else { Settings.Save(settingsFile); await Manager.StartAsync(); }
        }), () => !Busy);
        CameraCommand = new ActionCommand(() => _ = RunAsync(() => Manager.SetCameraAsync(!cameraEnabled)), () => Running && !Busy);
        VideoCommand = new ActionCommand(() => _ = RunAsync(() => Manager.SetVideoAsync(!videoEnabled)), () => Running && !Busy);
        RestartCommand = new ActionCommand(() => _ = RunAsync(Manager.RestartMediaAsync), () => Running && !Busy);
        SaveCommand = new ActionCommand(() => _ = RunAsync(() => { Settings.Save(settingsFile); Settings.Validate(); Log(M("SettingsSaved")); return Task.CompletedTask; }), () => CanEdit);
        WorkspaceCommand = new ActionCommand(() => Page = "workspace");
        SettingsCommand = new ActionCommand(() => Page = "settings");
        LogsCommand = new ActionCommand(() => Page = "logs");
        AndroidCommand = new ActionCommand(() => Page = "android");
        AdapterPageCommand = new ActionCommand(() => Page = "camera");
        VideoPageCommand = new ActionCommand(() => Page = "video");
        PosePageCommand = new ActionCommand(() => Page = "pose");
        OpenLogsCommand = new ActionCommand(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(DesktopSettings.DataDirectory) { UseShellExecute = true }));
        RefreshUsbCommand = new ActionCommand(RefreshUsb);
        Settings.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(Settings.UsbEnabled) && Settings.UsbEnabled) RefreshUsb(); };
    }

    public async Task RunAsync(Func<Task> action)
    {
        if (Busy) return;
        Busy = true; commandError = null; Error = "";
        try { await action(); }
        catch (Exception e) { commandError = e; Error = e.Message; Log(MessageFor(e)); }
        finally { Busy = false; }
    }

    public void Log(LocalizedText message)
    {
        var line = new LogLine(DateTime.Now, message);
        lock (logLock) File.AppendAllText(logFile, line.Text + Environment.NewLine);
        Application.Current.Dispatcher.InvokeAsync(() => {
            Logs.Add(line); while (Logs.Count > 300) Logs.RemoveAt(0);
        });
    }

    private void Update(WorkspaceSnapshot snapshot)
    {
        if (Settings.UsbEnabled && DateTime.UtcNow >= nextUsbRefresh) RefreshUsb();
        Status = snapshot.Phase; Running = snapshot.Running;
        cameraEnabled = snapshot.CameraRequested; videoEnabled = snapshot.VideoRequested;
        CameraAction = cameraEnabled ? T("PauseAdapter") : T("EnableAdapter"); Changed(nameof(CameraAction));
        VideoAction = videoEnabled ? T("StopStream") : T("StartStream"); Changed(nameof(VideoAction));
        if (snapshot.Error != lastRuntimeError) {
            if (snapshot.Error.Length > 0 || Error == lastRuntimeError) Error = snapshot.Error;
            lastRuntimeError = snapshot.Error;
        }
        var h = snapshot.Host;
        AdapterState = !snapshot.Running ? T("NotStarted") : h == null ? T("WaitingPlugin") : !cameraEnabled ? T("Paused") : T("Connected");
        Changed(nameof(AdapterState));
        bool connected = B(h, "connected");
        Phone = connected ? S(h, "client", "name", "Android") : T("NotConnected");
        Source = connected ? S(h, "pose", "source", "—") switch { "manual" => T("Manual"), "arcore" => "ARCore", var value => value } : "—";
        Sequence = connected ? N(h, "pose", "sequence").ToString("0") : "—";
        Camera = !snapshot.Running ? T("WaitingGame") : h == null ? T("WaitingPlugin") : !cameraEnabled ? T("Paused") : S(h, "camera", "phase", "") switch {
            "active" => T("CameraActive"), "tracking-paused" => T("TrackingPaused"), "waiting-scene" => T("SceneChanging"), "released" => T("Released"), _ => T("WaitingPose")
        };
        Scene = S(h, "camera", "scene", "—");
        double field = N(h, "camera", "fieldOfView"); Fov = field > 0 ? field.ToString("0.0") + "°" : "—";
        Quaternion = connected ? string.Join("   ", new[] { "qx", "qy", "qz", "qw" }.Select(k => N(h, "pose", k).ToString("0.000", CultureInfo.InvariantCulture))) : "—";
        Latency = connected ? N(h, "client", "roundTripMs").ToString("0") + " ms" : "—";
        int width = (int)N(snapshot.Video, "width"), height = (int)N(snapshot.Video, "height");
        Resolution = width > 0 ? $"{width} × {height}" : "—";
        Fps = snapshot.Video != null && videoEnabled ? N(snapshot.Video, "fps").ToString("0.0") : "—";
        string clientPhase = S(h, "client", "videoPhase", "");
        Video = !snapshot.Running ? T("NotStarted") : !videoEnabled ? T("Stopped") : snapshot.Video == null ? T("WaitingPlugin") :
            clientPhase == "playing" ? T("AndroidPlaying") : S(snapshot.Video, "phase", "") == "publishing" ? T("Publishing") : T("Recovering");
        Peer = connected ? S(h, "peer", "—") : "—";
        Tracking = connected ? B(h, "pose", "tracking") ? T("TrackingValid") : T("TrackingHold") : "—";
        InputX = connected ? Format(N(h, "pose", "x")) : "—"; InputY = connected ? Format(N(h, "pose", "y")) : "—"; InputZ = connected ? Format(N(h, "pose", "z")) : "—";
        bool active = S(h, "camera", "phase", "") is "active" or "tracking-paused";
        WorldX = active ? Format(N(h, "camera", "x")) : "—"; WorldY = active ? Format(N(h, "camera", "y")) : "—"; WorldZ = active ? Format(N(h, "camera", "z")) : "—";
        Pitch = active ? N(h, "camera", "pitch").ToString("0.0°") : "—"; Yaw = active ? N(h, "camera", "yaw").ToString("0.0°") : "—"; Roll = active ? N(h, "camera", "roll").ToString("0.0°") : "—";
        WorldQuaternion = active ? string.Join("   ", new[] { "qx", "qy", "qz", "qw" }.Select(k => N(h, "camera", k).ToString("0.000", CultureInfo.InvariantCulture))) : "—";
        Discovery = !Running ? T("Disabled") : Settings.UsbEnabled ? T("UsbMode") : Settings.LanEnabled ? T("LanDiscovery") : T("LocalEmulator");
        ClientVideo = clientPhase switch { "playing" => T("Playing"), "buffering" => T("Buffering"), "reconnecting" => T("Reconnecting"), "connecting" => T("Connecting"), _ => "—" };
        ClientResolution = connected && N(h, "client", "width") > 0 ? $"{N(h, "client", "width"):0} × {N(h, "client", "height"):0}" : "—";
        Buffered = connected ? N(h, "client", "bufferedMs").ToString("0") + " ms" : "—";
        EncoderPid = Pid((int)N(snapshot.Video, "encoderPid")); RelayPid = Pid(snapshot.RelayPid); GamePid = Pid(snapshot.GamePid);
        StreamError = S(snapshot.Video, "error", "—"); if (StreamError.Length == 0) StreamError = "—";
        foreach (string property in new[] { nameof(Peer), nameof(Tracking), nameof(InputX), nameof(InputY), nameof(InputZ), nameof(WorldX), nameof(WorldY), nameof(WorldZ), nameof(Pitch), nameof(Yaw), nameof(Roll), nameof(WorldQuaternion), nameof(Discovery), nameof(ClientVideo), nameof(ClientResolution), nameof(Buffered), nameof(EncoderPid), nameof(RelayPid), nameof(GamePid), nameof(StreamError) }) Changed(property);
        CommandManager.InvalidateRequerySuggested();
    }

    private static string Pid(int pid) => pid > 0 ? pid.ToString() : "—";
    private static string Format(double number) => number.ToString("0.000", CultureInfo.InvariantCulture);
    private static JsonElement? Value(JsonElement? node, params string[] path)
    { foreach (string part in path) { if (node?.ValueKind != JsonValueKind.Object || !node.Value.TryGetProperty(part, out var next)) return null; node = next; } return node; }
    private static string S(JsonElement? node, string key, string fallback) => Value(node, key) is { ValueKind: JsonValueKind.String } v ? v.GetString() ?? fallback : fallback;
    private static string S(JsonElement? node, string parent, string key, string fallback) => S(Value(node, parent), key, fallback);
    private static double N(JsonElement? node, params string[] path) => Value(node, path) is { ValueKind: JsonValueKind.Number } v ? v.GetDouble() : 0;
    private static bool B(JsonElement? node, params string[] path) => Value(node, path) is { ValueKind: JsonValueKind.True };
}
