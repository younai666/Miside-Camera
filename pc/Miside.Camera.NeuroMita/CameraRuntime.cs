using BepInEx.Unity.IL2CPP;
using Il2CppInterop.Runtime.Attributes;
using Miside.Camera.Core;
using Miside.Camera.Local;
using UnityEngine;
using UnityEngine.Rendering;
using UnityCamera = UnityEngine.Camera;

namespace Miside.Camera.Hosts.NeuroMita;

public static class CameraRuntime
{
    public static IDisposable Start(BasePlugin owner)
    {
        var behaviour = owner.AddComponent<CameraBehaviour>();
        behaviour.Initialize(owner);
        return new Lifetime(behaviour);
    }
    private sealed class Lifetime : IDisposable
    {
        private CameraBehaviour? behaviour;
        public Lifetime(CameraBehaviour behaviour) { this.behaviour = behaviour; }
        public void Dispose() { if (behaviour != null) { behaviour.Stop(); UnityEngine.Object.Destroy(behaviour); behaviour = null; } }
    }
}

public sealed class CameraBehaviour : MonoBehaviour
{
    public CameraBehaviour(IntPtr pointer) : base(pointer) { }
    private NeuroMitaCameraAdapter? adapter;
    private PoseReceiver? receiver;
    private UnityCamera.CameraCallback? builtin;
    private UnityCamera.CameraCallback? builtinEnd;
    private Il2CppSystem.Action<ScriptableRenderContext, UnityCamera>? srp;
    private Il2CppSystem.Action<ScriptableRenderContext, UnityCamera>? srpEnd;
    private bool background;
    private Action<string>? log;
    private LocalControlServer? desktop;
    private volatile bool controlEnabled = true;
    private volatile bool quitRequested;

    [HideFromIl2Cpp]
    public void Initialize(BasePlugin owner)
    {
        log = message => owner.Log.LogInfo(message);
        background = Application.runInBackground;
        Application.runInBackground = true;
        adapter = new NeuroMitaCameraAdapter(log);
        receiver = new PoseReceiver(owner.Config.Bind("Control", "Port", 39570, "Pose control port").Value, () => adapter.Status, log,
            owner.Config.Bind("Control", "ListenAddress", "127.0.0.1", "Loopback by default; LAN requires AccessToken").Value,
            owner.Config.Bind("Control", "AccessToken", "", "Access code configured by Desktop for LAN").Value);
        desktop = new LocalControlServer("Miside.Camera.Host.v1", Snapshot, operation => {
            if (operation == "enable") controlEnabled = true;
            else if (operation == "disable") controlEnabled = false;
            else if (operation == "quit") quitRequested = true;
            else throw new FormatException("Unknown Camera operation");
        }, log);
        builtin = (UnityCamera.CameraCallback)(Action<UnityCamera>)(camera => adapter?.BeforeRender(camera));
        UnityCamera.onPreCull = Il2CppSystem.Delegate.Combine(UnityCamera.onPreCull, builtin)?.Cast<UnityCamera.CameraCallback>();
        builtinEnd = (UnityCamera.CameraCallback)(Action<UnityCamera>)(camera => adapter?.AfterRender(camera));
        UnityCamera.onPostRender = Il2CppSystem.Delegate.Combine(UnityCamera.onPostRender, builtinEnd)?.Cast<UnityCamera.CameraCallback>();
        srp = (Il2CppSystem.Action<ScriptableRenderContext, UnityCamera>)(Action<ScriptableRenderContext, UnityCamera>)((_, camera) => adapter?.BeforeRender(camera));
        RenderPipelineManager.add_beginCameraRendering(srp);
        srpEnd = (Il2CppSystem.Action<ScriptableRenderContext, UnityCamera>)(Action<ScriptableRenderContext, UnityCamera>)((_, camera) => adapter?.AfterRender(camera));
        RenderPipelineManager.add_endCameraRendering(srpEnd);
        log("Miside Camera runtime ready; Manual/ARCore share the same adapter");
    }

    public void LateUpdate()
    {
        if (receiver == null || adapter == null) return;
        if (quitRequested) { adapter.Release("Desktop shutdown"); Application.Quit(); return; }
        var pose = receiver.Latest;
        try
        {
            if (!controlEnabled || pose == null || !pose.IsFresh)
            {
                if (adapter.Status.Phase != "released") adapter.Release(!controlEnabled ? "Desktop paused Camera" : pose == null ? "client disconnected" : "pose timed out");
            }
            else adapter.Apply(pose.Frame, pose.Connection);
        }
        catch (Exception e)
        {
            log?.Invoke($"Camera adapter failure: {e}");
            adapter.Release("adapter failure");
        }
    }

    [HideFromIl2Cpp]
    private object Snapshot()
    {
        var pose = receiver?.Latest;
        return new { enabled = controlEnabled, camera = adapter?.Status, connected = pose?.IsFresh == true,
            connection = pose?.Connection ?? 0, peer = pose?.Peer ?? "", client = pose?.Client,
            pose = pose == null ? null : new { pose.Frame.Sequence, pose.Frame.Source, pose.Frame.Tracking,
                x = pose.Frame.Position.X, y = pose.Frame.Position.Y, z = pose.Frame.Position.Z,
                qx = pose.Frame.Rotation.X, qy = pose.Frame.Rotation.Y, qz = pose.Frame.Rotation.Z, qw = pose.Frame.Rotation.W, pose.Frame.Zoom } };
    }

    public void OnApplicationQuit() => Stop();
    public void OnDestroy() => Stop();

    [HideFromIl2Cpp]
    public void Stop()
    {
        if (receiver == null) return;
        desktop?.Dispose(); desktop = null;
        receiver.Dispose(); receiver = null;
        if (builtin != null) UnityCamera.onPreCull = Il2CppSystem.Delegate.Remove(UnityCamera.onPreCull, builtin)?.Cast<UnityCamera.CameraCallback>();
        if (builtinEnd != null) UnityCamera.onPostRender = Il2CppSystem.Delegate.Remove(UnityCamera.onPostRender, builtinEnd)?.Cast<UnityCamera.CameraCallback>();
        if (srp != null) RenderPipelineManager.remove_beginCameraRendering(srp);
        if (srpEnd != null) RenderPipelineManager.remove_endCameraRendering(srpEnd);
        adapter?.Dispose(); adapter = null;
        Application.runInBackground = background;
        log?.Invoke("Miside Camera runtime stopped and restored");
    }
}
