using System.Threading;
using System.Windows;

namespace Miside.Camera.Desktop;

public partial class App : Application
{
    private Mutex? singleInstance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        singleInstance = new Mutex(true, "Local\\Miside.Camera.Desktop", out bool first);
        if (!first) { Shutdown(); return; }
        string file = DesktopSettings.DefaultFile;
        int at = Array.IndexOf(e.Args, "--settings");
        if (at >= 0 && at + 1 < e.Args.Length) file = Path.GetFullPath(e.Args[at + 1]);
        DesktopSettings settings;
        try { settings = DesktopSettings.Load(file); }
        catch (System.Text.Json.JsonException) { settings = new(); }
        Localization.Current.Use(settings.Language);
        var model = new WorkspaceViewModel(settings, file);
        var window = new MainWindow(model); MainWindow = window; window.Show();
    }
    protected override void OnExit(ExitEventArgs e) { singleInstance?.Dispose(); base.OnExit(e); }
}
