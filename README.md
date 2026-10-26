# Miside-Camera

Use an Android phone as a handheld 6DoF game camera and live viewfinder.

[Download v0.2.0](https://github.com/younai666/Miside-Camera/releases/tag/v0.2.0) · [中文](README.zh-CN.md) · [Русский](README.ru.md)

**Miside Camera Desktop** manages the game adapter, pairing, video services and recovery. **Miside Camera Android** provides ARCore or Manual control, zoom, recenter and original-stream recording. Connect over USB tethering or LAN; normal operation needs no ADB or USB debugging.

> Host compatibility: this release includes the adapter tested with **NeuroMita Unity IL2CPP v2026.10.22**. The project name does not imply support for the standalone MiSide game. The game, loader and game assets are not included.

## Install

1. Download `Miside-Camera-Desktop-v0.2.0.zip`, extract it, and open `Miside Camera Desktop.exe` on Windows 10/11 x64. No separate .NET installation is needed.
2. Install `Miside-Camera-Android-v0.2.0.apk` on Android 8 or newer. ARCore tracking needs a supported device and [Google Play Services for AR](https://play.google.com/store/apps/details?id=com.google.ar.core); Manual mode remains available.
3. Prepare the supported game with [BepInEx Unity IL2CPP](https://builds.bepinex.dev/projects/bepinex_be) build `6.0.0-be.788`. Run the game once until `BepInEx/interop` has been generated, then close it.
4. Download [FFmpeg with NVENC](https://www.gyan.dev/ffmpeg/builds/) and [MediaMTX](https://github.com/bluenviron/mediamtx/releases). In Desktop **Settings**, select the game executable, `ffmpeg.exe` and `mediamtx.exe`. The tested video path uses an NVIDIA GPU with NVENC. These tools are separate downloads, not bundled.

## Connect

**USB:** connect a data-capable USB cable and enable **USB tethering** in Android system settings. In Desktop, select **USB tethering**, save, and start the workspace. On Android, tap **Connect PC**, select the discovered Desktop (or enter its USB IPv4 address), and enter the six-digit code shown on the Desktop home page.

**LAN:** put the phone and PC on the same reachable local network, select **LAN** in Desktop, then use the same pairing steps. For an Android emulator, choose **Local emulator** and connect to `10.0.2.2` without a code.

Enter a game scene. Select **ARCore** to move and rotate the camera with the phone; use **Manual** for debugging or fallback. PC mouse/WASD remain active. **More → Recenter** establishes a new phone reference; **Zoom** changes the camera FOV. Switching ARCore to Manual releases the previous phone offset.

Tap **Record** to start or stop. MP4 files appear in the phone gallery under `Movies/Miside Camera`. Recordings contain the original **1920×1080 H.264 game video**, with no Android borders, grid, buttons or timer, and no re-encoding. The current stream has no audio track. Disconnecting ends and saves the current recording.

## If connection fails

- USB: confirm Windows sees the tethering network adapter. Refresh the address in Desktop Settings.
- LAN: check that VPNs/router isolation are not separating the devices. Allow the relevant applications through the firewall; do not disable the entire firewall.
- After changing connection mode or code, close the game and restart the workspace.
- Discovery uses UDP/TCP `39569`, Pose uses TCP `39570`, video uses TCP `8554`. Use trusted USB/LAN connections; do not expose these ports to the internet.

## Development and scope

[Build instructions](docs/development.md) · [Architecture](docs/architecture.md) · [Protocol](docs/pose-protocol.md) · [Validation scope](docs/validation.md)

The release was checked with a visible Android emulator; earlier S23 USB/ARCore checks are described in the validation scope. No universal device, host-version or latency guarantee is claimed. No AI, following, photo capture or standalone MiSide adapter is included.

Original project code is [MIT licensed](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md) for dependency and artwork rights. NeuroMita, MiSide and INK assets are not part of this repository or release. This independent project does not imply endorsement by those projects.
