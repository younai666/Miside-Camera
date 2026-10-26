# Validation scope

The pre-release pass uses static review, directed Release builds, disposable protocol/discovery checks and short emulator smoke tests. Existing acceptance evidence is reused; no long three-endpoint soak is implied.

For v0.2.0, both renamed Release builds passed, along with Core/Desktop protocol, authenticated discovery and localization checks. The signed, R8-shrunk APK was installed on the visible AndroidDev emulator and connected to the renamed Desktop/adapter. Its Viewfinder displayed the running game's video, and a short recording was published to `Movies/Miside Camera` at 1920×1080 with no pending entry. The actual Desktop window/title and running/playing states were checked through accessibility; the Windows screenshot service timed out, so no new Desktop screenshot acceptance is claimed.

- Earlier real-game checks covered six-axis Manual control, recenter, zoom, scene/camera rediscovery and disconnect restoration.
- S23 USB tethering and ARCore were exercised with the real game. The user confirmed restored player movement, native-view composition, and clearing the previous phone offset when switching back to Manual. These are observations on that setup, not universal host/device guarantees.
- Emulator recording checks verified MP4 decode at 1920×1080, gallery publication, finalization on disconnect, continuous playback while writing, and absence of Android UI in the decoded movie. Bitstream/container color metadata was compared with the source.
- The recording button has equal size to the other toolbar controls and keeps its color/size across disabled, pressed and recording states. Three Desktop languages were checked; a real mixed-DPI multi-monitor move was not performed.

The current host-specific adapter is built against NeuroMita Unity IL2CPP v2026.10.22. Standalone MiSide, other host builds, other GPU encoders, every Android device and a fixed motion-to-photon latency are not claimed. Real game screenshots, recordings, internal interop and historical probes stay private and are not public release assets.

## v0.2.1 targeted pass

The current recording contract supersedes the earlier full-frame transmuxing checks above: output is the centered 9:16 Viewfinder crop, exported at 1080×1920, without Android UI. A synthetic emulator export verifies decoded dimensions and four central color quadrants while rejecting the side panels. Desktop checks cover nested host discovery, MiSide detection without false adapter support, persisted manual paths, old developer-tool path removal and application-relative tool resolution. Release smoke checks cover the bundled runtime and final portrait gallery output; no repeat Camera/Pose acceptance or long soak is required for these changes.

The signed Release APK also saved a short real-game recording through the bundled FFmpeg/NVENC + MediaMTX path. The decoded frame was inspected at 1080×1920 and contained only the central game crop, with no Android controls/grid/timer. Media3 may store the encoded track in landscape with standard MP4 rotation metadata; its displayed orientation is portrait. Desktop was started with `C:\Windows` as working directory; both media processes ran from the bundle. Host configuration uses forward slashes to avoid BepInEx string unescaping of relative Windows paths.
