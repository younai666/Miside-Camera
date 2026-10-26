using BepInEx;
using BepInEx.Unity.IL2CPP;

namespace Miside.Camera.Plugin;

[BepInPlugin("dev.miside.camera", "Miside Camera", "0.2.1")]
public sealed class Plugin : BasePlugin
{
    private IDisposable? runtime;
    public override void Load()
    {
        string directory = Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!;
        string adapter = Path.Combine(directory, "Miside.Camera.NeuroMita.dll");
        if (!File.Exists(adapter))
        {
            Log.LogError("Missing game adapter. Build it against the supported host's generated interop; do not distribute host DLLs.");
            return;
        }
        var assembly = System.Reflection.Assembly.LoadFrom(adapter);
        var start = assembly.GetType("Miside.Camera.Hosts.NeuroMita.CameraRuntime", throwOnError: true)!
            .GetMethod("Start", new[] { typeof(BasePlugin) })!;
        runtime = (IDisposable)start.Invoke(null, new object[] { this })!;
    }

    public override bool Unload() { runtime?.Dispose(); runtime = null; return true; }
}
