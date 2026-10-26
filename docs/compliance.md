# Repository and release boundary

Publish only the project source, supplied branding resources and necessary dependency notices. Do not publish game binaries/assets, generated interop, loader DLLs, APK/EXE research targets, decompilation, private host code, settings, device data, recordings, credentials or signing material.

The Desktop ZIP contains the owned application, four owned adapter DLLs and the self-contained .NET/WPF runtime with its notices. It includes the independent FFmpeg and MediaMTX tools with their licenses; FFmpeg corresponding source archives/build instructions and MediaMTX dependency notices are retained. It does not contain BepInEx, the game or NVIDIA driver binaries. The Android asset is a signed, non-debuggable Release APK; test APKs are not release assets. Android carries the Media3 license/notice in its assets and the source tree retains wrapper notices.

NeuroMita/NeuroMita-VR and INK were architecture references, not copied implementations or assets. Third-party license/copyright strings remain accurate even though the project branding is Miside. User-supplied artwork retains its original rights; the code MIT license does not independently license that artwork.

Keep the signing key private for updates. Review the staged tree and asset allowlist before pushing; public history must not contain private research checkpoints. Preserve those only in a local archive. See [third-party notices](../THIRD_PARTY_NOTICES.md).
