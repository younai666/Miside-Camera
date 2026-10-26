# Validation scope

## v0.3.0

The maintainer confirmed that original MiSide support passed real-device testing on a Samsung Galaxy S23 before this release. This confirmation applies to the existing MiSide adapter and Android camera/control/viewfinder chain; it is reused for this packaging release rather than repeating a long three-device test. Application/plugin versions become 0.3.0, the Android signing identity and package ID stay unchanged, and this release adds no new control or streaming behavior.

Directed checks cover both host-specific Release adapter/capture builds, the self-contained Desktop bundle, signed/shrunk Android APK, Core/Desktop protocol and discovery checks, package contents and a short Desktop startup. Game interop and proprietary content remain outside the repository and release.

Prior short gameplay checks reached a real MiSide v0.93L scene, changed Manual XYZ and combined Pitch/Yaw/Roll, retained native mouse/W movement with the phone offset, and exercised optical 2x zoom, recenter, disconnect/native-view restoration and connection reacquisition. Android displayed the live 1920×1080 game input at about 28 captured fps on that setup. Desktop media stop/restart retained Pose connectivity and resumed the Viewfinder; Desktop shutdown closed the owned game/media processes.

A normal 2.13-second recording exported to the phone gallery and decoded as the centered 1080×1920 game crop without Android UI. Media3 may encode the track in landscape with standard MP4 rotation metadata; its displayed orientation is portrait. One earlier take exported after leaving the app hit Media3's 25-second no-output timeout. Keep the app foreground until saving finishes; background/long-recording reliability is not claimed.

NeuroMita Unity IL2CPP v2026.10.22 retains the previously accepted camera composition, source-switch reset, native input, USB/ARCore and video behavior. Both hosts use the same Pose math and lifecycle source but are compiled against their own Unity bindings. No universal device, game-version, encoder or measured motion-to-photon latency guarantee is made. NVIDIA/NVENC remains required. Further scenes, physical mixed-DPI monitor moves and long unattended streaming/recording are outside this minimal release pass.

Actual game/video pixels and Android screenshots were inspected in the earlier targeted pass. The Windows screenshot helper timed out, so no new Desktop screenshot acceptance is claimed. Private game screenshots, recordings, generated bindings, research outputs and signing material are not public assets.
