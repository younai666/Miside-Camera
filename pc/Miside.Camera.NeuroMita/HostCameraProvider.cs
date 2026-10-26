using NeuroMita.Modding;
using NeuroMita.Player;
using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace Miside.Camera.Hosts.Unity;

internal static class HostCameraProvider
{
    internal static bool Transitioning => SceneService.IsTransitionInProgress;

    internal static UnityCamera? Find()
    {
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerActor>();
        var camera = player != null && RenderedCameraAdapter.Usable(player.MainCamera) ? player.MainCamera : UnityCamera.main;
        return RenderedCameraAdapter.Usable(camera) ? camera : UnityCamera.allCameras.FirstOrDefault(RenderedCameraAdapter.Usable);
    }
}
