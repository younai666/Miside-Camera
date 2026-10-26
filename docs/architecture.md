# Architecture

```text
Android Manual / ARCore  -> TCP Pose -> game adapter -> native Camera + relative phone offset
game framebuffer -> FFmpeg/NVENC -> MediaMTX -> Android Media3 / MediaCodec -> Viewfinder
                                            compressed H.264 samples -> MediaMuxer -> gallery MP4
Desktop -> current-user named pipes -> adapter/video lifecycle and status
```

- `android/`: Compose controls, independent ARCore session, Pose transport, Media3 playback, MP4 recording and MediaStore publication.
- `desktop/`: WPF workspace management, USB/LAN discovery, persistent settings, localization and logs.
- `pc/Miside.Camera.Core`: bounded Pose parsing, authenticated latest-frame receiver and adapter contract; no Unity/media dependency.
- `pc/Miside.Camera.NeuroMita`: the current host adapter; game/Unity references are compile-only.
- `pc/Miside.Camera.Streaming`: final-frame capture and external encoder process; no Pose ownership.

## Camera lifetime

Read the current native world pose each render, then compose `position = nativePosition + nativeRotation * phoneOffset` and `rotation = nativeRotation * phoneRotation`. Apply only for rendering and restore the native local pose/FOV afterward. No external player-input session is registered, so normal mouse, WASD and interactions remain available. Scene transitions release and rediscover the camera. A source change clears the old offset; re-entering ARCore uses its first valid tracked frame as the new origin. Recenter holds the current relative composition and establishes a new input origin; zoom uses the optical FOV relationship.

## Video and recording

The tested stream is native 1920×1080 H.264 through NVENC, local UDP publication and RTSP/TCP delivery. The existing BT.601 matrix / sRGB transfer / limited-range contract is retained. MediaMTX relays without transcoding. Android keeps its player/Surface through recomposition, fills the portrait viewfinder by cropping, retains the last frame during short recovery, and catches up without a growing playback queue.

Recording copies compressed samples before decode into a bounded queue; a separate thread muxes MP4 from a keyframe. The original H.264 SPS/VUI color information is preserved; an SDP fallback must not override it. No Surface, Compose layer, Android UI, portrait crop or re-encoding enters the recording. Failed/empty files are removed; finished files are published via MediaStore. Playback teardown, disconnect or format change finalizes the current segment. Recording does not restart across reconnect automatically.

The Media3 1.8.0 RTP rollover correction is a measured reliability fix, not a test overlay. Its source transform and regression check are retained; review it when changing Media3 versions.
