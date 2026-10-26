using static Miside.Camera.Desktop.Localization;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Miside.Camera.Desktop;

public class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; Changed(name); return true; }
}

public sealed class DesktopSettings : Observable
{
    private string gamePath = "", ffmpegPath = "", mediaMtxPath = "", accessCode = "";
    private bool lanEnabled, usbEnabled, autoStart = true, closeGameOnStop = true;
    private string language = "en";
    public string Language { get => language; set => Set(ref language, value); }
    public string GamePath { get => gamePath; set => Set(ref gamePath, value); }
    public string FfmpegPath { get => ffmpegPath; set => Set(ref ffmpegPath, value); }
    public string MediaMtxPath { get => mediaMtxPath; set => Set(ref mediaMtxPath, value); }
    public string AccessCode { get => accessCode; set => Set(ref accessCode, value); }
    public bool LanEnabled {
        get => lanEnabled;
        set { if (!Set(ref lanEnabled, value)) return; if (value) UsbEnabled = false; NetworkChanged(); }
    }
    public bool UsbEnabled {
        get => usbEnabled;
        set { if (!Set(ref usbEnabled, value)) return; if (value) LanEnabled = false; NetworkChanged(); }
    }
    [JsonIgnore] public bool NetworkEnabled => LanEnabled || UsbEnabled;
    [JsonIgnore] public string ConnectionMode {
        get => UsbEnabled ? "usb" : LanEnabled ? "lan" : "local";
        set { if (value is not ("usb" or "lan" or "local")) return; UsbEnabled = value == "usb"; LanEnabled = value == "lan"; NetworkChanged(); }
    }
    private void NetworkChanged() { Changed(nameof(NetworkEnabled)); Changed(nameof(ConnectionMode)); }
    public bool AutoStart { get => autoStart; set => Set(ref autoStart, value); }
    public bool CloseGameOnStop { get => closeGameOnStop; set => Set(ref closeGameOnStop, value); }
    public static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Miside Camera Desktop");
    public static string DefaultFile => Path.Combine(DataDirectory, "settings.json");
    public static DesktopSettings Load(string path)
        => File.Exists(path) ? JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(path)) ?? new() : new();
    public void Save(string path)
    {
        if (AccessCode.Length == 0) AccessCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
    public void Validate()
    {
        if (!File.Exists(GamePath) || !string.Equals(Path.GetFileName(GamePath), "NeuroMita.exe", StringComparison.OrdinalIgnoreCase))
            throw E("SelectGame");
        string host = Path.GetDirectoryName(Path.GetFullPath(GamePath))!;
        if (!File.Exists(Path.Combine(host, "BepInEx", "interop", "Assembly-CSharp.dll")))
            throw E("PrepareInterop");
        if (!File.Exists(FfmpegPath) || !Path.IsPathFullyQualified(FfmpegPath)) throw E("SelectFfmpeg");
        if (!File.Exists(MediaMtxPath) || !Path.IsPathFullyQualified(MediaMtxPath)) throw E("SelectMediaMtx");
        if (NetworkEnabled && (AccessCode.Length != 6 || !AccessCode.All(char.IsAsciiDigit))) throw E("InvalidAccessCode");
    }
}
