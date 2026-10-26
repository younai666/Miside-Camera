# Connection contracts

The rename changes product identifiers, not Pose fields, coordinate conventions or ports. Desktop and Android from this release must be used together.

| Channel | Address / identifier |
| --- | --- |
| Discovery | UDP/TCP 39569; request `MISIDE_CAMERA_DISCOVER_V1`, service `miside-camera`, metadata version 1 |
| Pose | TCP 39570, bounded newline-delimited UTF-8 JSON, maximum 4096 bytes per frame |
| Video | RTSP/TCP 8554, path `/miside`, user `camera` and the Desktop code in USB/LAN modes |
| Local management | Current-user named pipes `Miside.Camera.Host.v1` and `Miside.Camera.Video.v1` |

Local emulator mode binds to loopback; USB and LAN use authenticated IPv4 network listeners. USB uses a system RNDIS/NCM network interface, not ADB forwarding. Discovery reports `requiresCode`, `ready`, ports and `transport`. The first Pose frame authenticates using `accessToken`. Only one Pose client is accepted at a time; newer frames replace the previous sample, never form an unbounded queue.

```json
{"version":2,"sequence":1,"timestampNs":1,"source":"manual","position":{"x":0,"y":0,"z":0},"rotation":{"x":0,"y":0,"z":0,"w":1},"zoom":1,"recenter":0,"tracking":true,"accessToken":"123456"}
```

`123456` is an example, not a shipped credential. Positions are meters: right-handed X right, Y up, Z backward; quaternions are XYZW. Manual and ARCore share this packet. Sequence and monotonic timestamp must increase within a connection. Zoom is 1–3, quaternions must be normalized, and recenter generations are nonnegative. The Core still accepts the existing v1 manual Euler format; the retired standalone monitor is not shipped.

ACK receipt and rendered Camera state are distinct. Loss/expiry releases camera control. Client telemetry reports playback dimensions/state, frame count and control RTT; RTT is not end-to-end display latency. Control and RTSP are not encrypted and are intended for trusted USB/LAN connections only.
