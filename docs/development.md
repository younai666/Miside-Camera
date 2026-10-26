# Development

Use the repository root for Git. Game copies, interop, recordings, credentials and machine-specific settings must stay outside the repository. Do not modify the original host distribution or unrelated Android projects.

## Toolchain

- Android: JDK 17, SDK 36 / build-tools 36.0.0, Gradle wrapper 8.13, AGP 8.13.2, Kotlin 2.2.21.
- Desktop: .NET 10 SDK on Windows, WPF.
- Game adapter: .NET 6 SDK, the matching local Unity IL2CPP interop, BepInEx 6.0.0-be.788. Only this host-specific build needs the game.

```powershell
cd android
.\gradlew.bat assembleDebug assembleDebugAndroidTest lintDebug
cd ..
dotnet run --project desktop/Miside.Camera.Desktop.Checks -c Release
dotnet run --project pc/Miside.Camera.Core.Checks -c Release
```

Start an existing Android AVD with a visible GUI; do not use headless mode. For an emulator-only connection choose Local emulator in Desktop and `10.0.2.2` on Android. ADB is an installation/debugging tool, not the application transport.

## Release builds

```powershell
.\tools\build_desktop.ps1 -HostDirectory C:\Games\NeuroMita -MiSideHostDirectory C:\Games\MiSide -DesktopDotnet dotnet -MediaDirectory C:\Build\Media
```

Both private host directories must contain their matching `BepInEx/interop/Assembly-CSharp.dll`. The script compiles against each Unity version and stages four owned DLLs per host in `adapters/neuromita` and `adapters/miside`, publishes the self-contained Desktop and copies the supplied media bundle. `MediaDirectory` must contain `ffmpeg/` and `mediamtx/` with their executables, licenses and FFmpeg corresponding source archives. Use the manual **Build bundled media runtime** workflow for FFmpeg and **Collect MediaMTX dependency notices** for the dependency license inventory. FFmpeg n9.0.2 is built with only the LGPL raw-RGBA/NVENC/RTSP components, retaining SIMD; no GPL/nonfree libraries are enabled. NVENC is part of FFmpeg and still requires an installed NVIDIA driver. Game/interop/loader DLLs are compile-only and must never be copied into a release.

For a signed Android release, set `MISIDE_SIGNING_STORE_FILE`, `MISIDE_SIGNING_STORE_PASSWORD`, `MISIDE_SIGNING_KEY_ALIAS` and `MISIDE_SIGNING_KEY_PASSWORD` in the local build environment, then run `gradlew assembleRelease`. Without these variables the release build is unsigned, suitable for CI checks only. Keep the signing key and passwords outside Git and back them up privately for future updates.

Release builds shrink Android code/resources. Signing material, debug/test APKs, PDBs, game files, recordings, paths and settings do not belong in the public packages. Desktop copies the exact .NET runtime pack licenses into its bundle.

Run only affected checks and a short smoke test after a change. Recording/UI tests must not compete with another UiAutomation screenshot service. Live recording checks are opt-in through the `recordingHost`/`accessCode` instrumentation arguments; their temporary media is removed by the test. No unattended long three-endpoint soak is part of normal CI.

Portrait export uses Media3 Transformer 1.8.0. The opt-in `PortraitRecordingExportTest` needs `cache/portrait-test-input.mp4`: a synthetic 1920×1080 video with red sides and center quadrants white/pink/pink/black. Push it with ADB to `/data/local/tmp`, then copy into the debug app cache using `run-as` before running only that test. It verifies decoded 1080×1920 geometry and the four visible quadrants without a game/network session. The release smoke check uses the actual stream and gallery output.
