# Miside-Camera

Turn your Android phone into a handheld 6DoF camera for **MiSide and NeuroMita**. Move and rotate the phone to frame the game, watch the live viewfinder, and record portrait game footage.

[Download v0.3.0](https://github.com/younai666/Miside-Camera/releases/tag/v0.3.0) · [中文](README.zh-CN.md) · [Русский](README.ru.md)

**Miside Camera Desktop** manages the game adapter, pairing and video services. **Miside Camera Android** is your camera controller and viewfinder. Both apps support English, Chinese and Russian.

## Features

- **ARCore 6DoF:** phone translation and rotation control the game camera; Manual remains a fallback.
- **Native PC controls stay active:** mouse, WASD and interactions work alongside the phone's relative camera offset.
- **USB tethering or LAN:** automatic Desktop discovery and a six-digit pairing code; no ADB or USB debugging needed for normal communication.
- **Live Viewfinder:** 1920×1080 game input, portrait framing and reconnect recovery.
- **Recenter and Zoom:** establish a new phone reference and adjust the camera FOV.
- **9:16 recording:** save the visible central game crop as a 1080×1920 MP4 to the phone gallery, without Android controls, borders, grid or timer. Video-only for now.

## Start in four steps

1. **Install both apps.** Download `Miside-Camera-Desktop-v0.3.0.zip` and `Miside-Camera-Android-v0.3.0.apk` from the [release](https://github.com/younai666/Miside-Camera/releases/tag/v0.3.0). Fully extract the ZIP on your PC and install the APK on your phone. If the game has no plug-in loader yet, complete the one-time setup below.
2. **Open Desktop.** It detects supported running games, nearby installations and Steam libraries, and automatically uses the bundled FFmpeg/NVENC, MediaMTX and .NET runtime. Choose **USB tethering** or **LAN** in Settings, save, and start the workspace. Keep the extracted folder together; no media-tool paths need configuration.
3. **Connect the phone.** For USB, connect a data cable and enable **USB tethering** in Android system settings; file-transfer mode alone does not create the network. For LAN, put both devices on the same reachable network. In Android, tap **Connect PC**, choose the discovered Desktop and enter the six-digit code shown on Desktop's home page.
4. **Enter a game scene and shoot.** Select **Mode → ARCore**, allow camera access and move gently until tracking starts. PC controls remain usable. **More → Recenter** sets a new phone reference, **Zoom** adjusts framing, and **Record** starts/stops recording. Wait for saving to finish before leaving the app; videos appear in the gallery under `Movies/Miside Camera`.

A valid saved game selection is retained. Only if discovery fails or selects another installed host, choose `MiSideFull.exe` or `NeuroMita.exe` in **Settings**, save and restart the workspace. Normal use needs no development SDK or command line.

## Requirements and one-time game setup

| Component | Requirement |
| --- | --- |
| PC | Windows 10/11 x64; NVIDIA GPU with H.264 NVENC support and a compatible installed driver |
| Phone | Android 8+; ARCore-compatible device and [Google Play Services for AR](https://play.google.com/store/apps/details?id=com.google.ar.core) for physical 6DoF tracking |
| MiSide | Tested: v0.93L, `MiSideFull.exe`, Unity 2021.3.35f1 IL2CPP |
| NeuroMita | Tested: Unity IL2CPP v2026.10.22, `NeuroMita.exe` |

The game needs **BepInEx 6 Unity IL2CPP for Windows x64**, tested with build **6.0.0-be.788**. Skip this if already prepared. Otherwise close the game, extract the matching [official loader](https://builds.bepinex.dev/projects/bepinex_be) beside the game executable, launch once until the main menu, then close it. Desktop deploys its matching Camera Adapter on the next workspace start. The game and loader are not included; Desktop does not install the loader automatically.

FFmpeg/NVENC, MediaMTX and the Desktop .NET runtime **are included**. NVIDIA drivers and Google Play Services for AR are separate requirements. AMD/Intel encoders and software encoding are not supported in this release. Use one game workspace at a time.

## Troubleshooting

- **Desktop not found:** check USB tethering or LAN reachability. Enter the PC's IPv4 address and the same code manually if discovery fails.
- **Game not ready:** check the loader setup above; close the older game session and launch through Desktop.
- **No video:** retain `tools/` and `licenses/`, check the NVIDIA driver and Desktop stream status.
- **Connection changed:** after changing USB/LAN mode, code or application location, save and restart the workspace. Check VPN/router isolation and application firewall rules; do not disable the entire firewall.
- **Tracking paused:** move gently in a well-lit, textured environment, or switch to Manual. Switching back clears the previous phone offset. Keep the app foreground until recording export finishes.

For emulator development, select **Local emulator** and use `10.0.2.2`, without a code. Discovery uses UDP/TCP `39569`, Pose TCP `39570`, and video TCP `8554`; use trusted USB/LAN networks, without public internet port forwarding.

## Project

Original MiSide support was confirmed on a **Samsung Galaxy S23** before this release. Short gameplay checks also covered camera composition, recenter/zoom, disconnect restoration, video restart and portrait recording. See [validation scope](docs/validation.md); performance depends on the game, hardware and connection, with no fixed end-to-end latency promised.

[Build](docs/development.md) · [Architecture](docs/architecture.md) · [Protocol](docs/pose-protocol.md) · [Third-party notices](THIRD_PARTY_NOTICES.md)

Original project code is [MIT licensed](LICENSE). Games, characters and third-party artwork retain their own rights. Game binaries/assets, generated game bindings, private test content and signing keys are not distributed. This independent project is not endorsed by MiSide, NeuroMita or INK.
