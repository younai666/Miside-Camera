using UnityEngine;
using UnityCamera = UnityEngine.Camera;

namespace Miside.Camera.Hosts.Unity;

internal static class HostCameraProvider
{
    private static SceneLoading? loading;
    private static float nextLoadingDiscovery;
    internal static bool Transitioning
    {
        get {
            if (Time.unscaledTime >= nextLoadingDiscovery) {
                nextLoadingDiscovery = Time.unscaledTime + .25f;
                loading = UnityEngine.Object.FindObjectOfType<SceneLoading>();
            }
            return loading != null && loading.isActiveAndEnabled && !loading.loadReady;
        }
    }

    internal static UnityCamera? Find()
    {
        // Select the gameplay owner's camera, never a menu, reflection or UI camera.
        var player = UnityEngine.Object.FindObjectOfType<PlayerMove>();
        if (player == null) return null;
        var camera = player.mainCamera != null ? player.mainCamera.GetComponent<UnityCamera>() : null;
        if (RenderedCameraAdapter.Usable(camera)) return camera;
        var controller = UnityEngine.Object.FindObjectOfType<GameController>();
        return controller != null && RenderedCameraAdapter.Usable(controller.cameraPlayer) ? controller.cameraPlayer : null;
    }
}
