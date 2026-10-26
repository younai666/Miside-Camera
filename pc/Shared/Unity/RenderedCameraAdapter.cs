using Miside.Camera.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityCamera = UnityEngine.Camera;
using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;

namespace Miside.Camera.Hosts.Unity;

/// <summary>Applies the photography pose only while rendering; gameplay keeps its native camera and input.</summary>
public sealed class RenderedCameraAdapter : ICameraAdapter
{
    private readonly Action<string> log;
    private CameraStatus status = new("waiting-pose");
    public CameraStatus Status => Volatile.Read(ref status);
    private UnityCamera? camera;
    private bool renderOverride;
    private int sceneHandle;
    private long connection, recenter = -1;
    private string source = "";
    private float discoveryAt;
    private Vector3 gameplayLocalPosition, offsetAnchorPosition, offsetPosition;
    private Quaternion gameplayLocalRotation, offsetAnchorRotation = Quaternion.identity, offsetRotation = Quaternion.identity;
    private float gameplayFov;
    private float originalFov, targetFov;
    private NVector3 inputPosition;
    private NQuaternion inputRotation = NQuaternion.Identity;
    private PoseFrame? applied;
    private bool tracking;

    public RenderedCameraAdapter(Action<string> log) { this.log = log; }

    public void Apply(PoseFrame pose, long client)
    {
        bool sourceChanged = source.Length != 0 && source != pose.Source;
        if (sourceChanged) Release("pose source changed");
        // Release during loading so the next anchor belongs to the destination scene.
        if (HostCameraProvider.Transitioning)
        {
            if (camera != null) Release("scene transition");
            Volatile.Write(ref status, new CameraStatus("waiting-scene"));
            return;
        }
        int scene = SceneManager.GetActiveScene().handle;
        if (connection != client || (camera != null && sceneHandle != scene)) Release("connection/scene changed");
        connection = client;
        if (camera == null || !Usable(camera) || Time.unscaledTime >= discoveryAt)
        {
            discoveryAt = Time.unscaledTime + 0.25f;
            UnityCamera? candidate = HostCameraProvider.Find();
            if (candidate != camera)
            {
                Release("camera changed");
                connection = client;
                camera = candidate;
                if (camera != null)
                {
                    originalFov = camera.fieldOfView;
                    offsetAnchorPosition = offsetPosition = Vector3.zero;
                    offsetAnchorRotation = offsetRotation = Quaternion.identity;
                    targetFov = originalFov;
                    sceneHandle = scene;
                    SetReference(pose, relativeManual: sourceChanged);
                    log($"Camera discovered: {camera.name}, id={camera.GetInstanceID()}, scene={camera.gameObject.scene.name}");
                }
            }
        }
        if (camera == null) { Publish("waiting-camera", pose); return; }
        if (!pose.Tracking && (applied == null || source != pose.Source))
        {
            tracking = false;
            Publish("tracking-paused", applied ?? pose);
            return;
        }
        if (applied == null && pose.Source == "arcore") SetReference(pose);
        if (recenter != pose.Recenter)
        {
            // Keep the photography view and establish a new input origin.
            offsetAnchorPosition = offsetPosition;
            offsetAnchorRotation = offsetRotation;
            SetReference(pose, relativeManual: true);
            log($"Recenter {recenter} applied");
        }
        if (pose.Tracking)
        {
            NQuaternion inverse = NQuaternion.Inverse(inputRotation);
            NVector3 offset = NVector3.Transform(pose.Position - inputPosition, inverse);
            NQuaternion rotation = inverse * pose.Rotation;
            offsetPosition = offsetAnchorPosition + offsetAnchorRotation * new Vector3(offset.X, offset.Y, -offset.Z);
            offsetRotation = offsetAnchorRotation * new Quaternion(-rotation.X, -rotation.Y, rotation.Z, rotation.W);
        }
        tracking = pose.Tracking;
        targetFov = 2 * Mathf.Atan(Mathf.Tan(originalFov * Mathf.Deg2Rad / 2) / (float)pose.Zoom) * Mathf.Rad2Deg;
        applied = pose;
    }

    private void SetReference(PoseFrame pose, bool relativeManual = false)
    {
        source = pose.Source;
        recenter = pose.Recenter;
        inputPosition = pose.Source == "manual" && !relativeManual ? NVector3.Zero : pose.Position;
        inputRotation = pose.Source == "manual" && !relativeManual ? NQuaternion.Identity : pose.Rotation;
    }

    public void BeforeRender(UnityCamera rendered)
    {
        if (camera == null || rendered != camera || applied == null || renderOverride) return;
        // Never register an external input provider or leave this pose in the gameplay loop.
        // WASD, mouse look and interaction rays continue to use the native player camera.
        gameplayLocalPosition = camera.transform.localPosition;
        gameplayLocalRotation = camera.transform.localRotation;
        gameplayFov = camera.fieldOfView;
        renderOverride = true;
        // Compose against this frame's native world pose, before applying any phone offset.
        // Restoring after rendering prevents the phone offset feeding back into next frame's base.
        Vector3 nativePosition = camera.transform.position;
        Quaternion nativeRotation = camera.transform.rotation;
        camera.transform.SetPositionAndRotation(nativePosition + nativeRotation * offsetPosition, nativeRotation * offsetRotation);
        camera.fieldOfView = targetFov;
        Publish(tracking ? "active" : "tracking-paused", applied);
    }

    public void AfterRender(UnityCamera rendered)
    {
        if (rendered == camera) RestoreGameplayCamera();
    }

    private void RestoreGameplayCamera()
    {
        if (!renderOverride) return;
        if (camera != null)
        {
            camera.transform.localPosition = gameplayLocalPosition;
            camera.transform.localRotation = gameplayLocalRotation;
            camera.fieldOfView = gameplayFov;
        }
        renderOverride = false;
    }

    private void Publish(string phase, PoseFrame pose, string detail = "")
    {
        var p = camera != null ? camera.transform.position : Vector3.zero;
        var q = camera != null ? camera.transform.rotation : Quaternion.identity;
        Volatile.Write(ref status, new CameraStatus(phase, phase is "active" or "tracking-paused" ? pose.Sequence : 0,
            camera != null ? camera.name : "", SceneManager.GetActiveScene().name, camera != null ? camera.fieldOfView : 0,
            p.x, p.y, p.z, detail, CameraId: camera != null ? camera.GetInstanceID() : 0,
            Qx: q.x, Qy: q.y, Qz: q.z, Qw: q.w, Recenter: recenter,
            Pitch: camera != null ? camera.transform.rotation.eulerAngles.x : 0,
            Yaw: camera != null ? camera.transform.rotation.eulerAngles.y : 0,
            Roll: camera != null ? camera.transform.rotation.eulerAngles.z : 0));
    }

    public void Release(string reason)
    {
        RestoreGameplayCamera();
        if (applied != null) log($"Photography camera released: {reason}");
        camera = null; applied = null; source = ""; recenter = -1;
        offsetAnchorPosition = offsetPosition = Vector3.zero;
        offsetAnchorRotation = offsetRotation = Quaternion.identity;
        inputPosition = NVector3.Zero;
        inputRotation = NQuaternion.Identity;
        tracking = false;
        Volatile.Write(ref status, new CameraStatus("released", Detail: reason));
    }

    internal static bool Usable(UnityCamera? c) => c != null && c.isActiveAndEnabled && c.targetTexture == null && c.cullingMask != 0;
    public void Dispose() => Release("plugin stopped");
}

