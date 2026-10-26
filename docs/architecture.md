# Architecture

```text
Android Manual / ARCore  -> TCP Pose -> game adapter -> native Camera + relative phone offset
game framebuffer -> FFmpeg/NVENC -> MediaMTX -> Android Media3 / MediaCodec -> Viewfinder
                H.264 samples -> private cache -> Media3 portrait crop/export -> gallery MP4
Desktop -> current-user named pipes -> adapter/video lifecycle and status
```

- `android/`: Compose controls, independent ARCore session, Pose transport, Media3 playback, MP4 recording and MediaStore publication.
- `desktop/`: WPF workspace management, USB/LAN discovery, persistent settings, localization and logs.
- `pc/Miside.Camera.Core`: bounded Pose parsing, authenticated latest-frame receiver and adapter contract; no Unity/media dependency.
- `pc/Shared/Unity`: existing render-only Pose composition, release, receiver and runtime lifecycle, compiled against each host's Unity interop.
- `pc/Miside.Camera.NeuroMita` and `pc/Miside.Camera.MiSide`: host-specific gameplay camera discovery/loading state; game/Unity references are compile-only.
- `pc/Miside.Camera.Streaming`: final-frame capture and external encoder process; no Pose ownership.

## Camera lifetime

Read the current native world pose each render, then compose `position = nativePosition + nativeRotation * phoneOffset` and `rotation = nativeRotation * phoneRotation`. Apply only for rendering and restore the native local pose/FOV afterward. No external player-input session is registered, so normal mouse, WASD and interactions remain available. Scene transitions release and rediscover the camera. A source change clears the old offset; re-entering ARCore uses its first valid tracked frame as the new origin. Recenter holds the current relative composition and establishes a new input origin; zoom uses the optical FOV relationship.

## Video and recording

The tested stream is native 1920×1080 H.264 through NVENC, local UDP publication and RTSP/TCP delivery. The existing BT.601 matrix / sRGB transfer / limited-range contract is retained. MediaMTX relays without transcoding. Android keeps its player/Surface through recomposition, fills the portrait viewfinder by cropping, retains the last frame during short recovery, and catches up without a growing playback queue.

NeuroMita retains asynchronous GPU readback. MiSide's stripped Unity 2021 bindings omit `AsyncGPUReadbackRequest`; its build reads the final backbuffer with `Texture2D.ReadPixels` at end-of-frame and copies rows into the same latest-frame queue. No additional scaling, encoder, transport or Android path is introduced. Synchronous readback costs GPU time; do not assume identical capture performance across hosts.


The Media3 1.8.0 RTP rollover correction is a measured reliability fix, not a test overlay. Its source transform and regression check are retained; review it when changing Media3 versions.

## Portable release and portrait export

Desktop resolves `tools/ffmpeg/ffmpeg.exe` and `tools/mediamtx/mediamtx.exe` from its own directory, never from the working directory or persisted developer tool paths. Host discovery is bounded to running games, adjacent folders and Steam libraries; a valid saved game selection wins. Bundles contain explicit owned-DLL allowlists in `adapters/miside` and `adapters/neuromita`. The loader selects its adapter from the running executable, and Desktop deploys only that host's binaries. A single active workspace retains the existing ports, plugin GUIDs, authentication and pipe protocol. The generated host FFmpeg setting is relative to the game directory.

MiSide discovery follows `PlayerMove.mainCamera` (or `GameController.cameraPlayer`), rather than acquiring menu/reflection cameras. Loading releases the camera; the active scene and camera instance are rediscovered by the shared lifecycle. The adapter does not change player input, mouse flags or gameplay controllers.

Recording buffers encoded game samples into an app-private cache, then Media3 Transformer decodes/crops/encodes the same centered 9:16 zoom-fill geometry used by the Viewfinder. Only the completed 1080×1920 portrait export is published to MediaStore. The temporary files are removed on success or error. The player Surface and Pose socket are untouched.
