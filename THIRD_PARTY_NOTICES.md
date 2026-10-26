# Third-party notices

The root MIT license covers original project code only. Dependencies, branding artwork, games and research references retain their own licenses and rights. The following inventory distinguishes shipped material from compile-only dependencies and research.

## 入库材料

**Gradle Wrapper 8.13**：Gradle contributors，Apache-2.0。新 wrapper 由本机缓存的 Gradle 8.13 生成，没有复制 Yomikomi 文件。保留生成脚本原始版权/SPDX 头；随附 [Gradle LICENSE](licenses/gradle-LICENSE.txt) 与 [NOTICE](licenses/gradle-NOTICE.txt)，取自用于生成 wrapper 的同一 Gradle 8.13 缓存分发，内容保留原样。仅分发 wrapper，未分发整个 Gradle。

JAR SHA-256：`81a82aaea5abcc8ff68b3dfcb58b3c3c429378efd98e7433460610fecd7ae45f`，已与 [Gradle 官方 wrapper 校验值](https://services.gradle.org/distributions/gradle-8.13-wrapper.jar.sha256) 对照。ZIP 摘要也已与[官方分发校验值](https://services.gradle.org/distributions/gradle-8.13-bin.zip.sha256)对照，并写入 wrapper 配置。

## 构建依赖

| 组件 | 版本 / 许可记录 | 使用方式 |
| --- | --- | --- |
| Android Gradle Plugin | 8.13.2，Apache-2.0；[AOSP](https://android.googlesource.com/platform/tools/base/) | 构建工具，不把工具二进制入库 |
| Kotlin plugin / stdlib | 2.2.21，Apache-2.0；[JetBrains](https://github.com/JetBrains/kotlin/blob/master/license/LICENSE.txt) | Kotlin compiler and APK standard library |
| AndroidX Activity / AppCompat / Compose / Lifecycle | Activity 1.11.0、AppCompat 1.7.1、Compose BOM 2025.10.01、Lifecycle 2.9.4，Apache-2.0；[AndroidX](https://android.googlesource.com/platform/frameworks/support/) | Compose UI、应用语言、窗口/生命周期与设备测试；不复制第三方 UI 资产 |
| kotlinx.coroutines | 解析到 1.8.1，Apache-2.0；[Kotlin 协程](https://github.com/Kotlin/kotlinx.coroutines) | 协程与网络线程调度，标准依赖解析 |
| AndroidX Media3 | 1.8.0，Apache-2.0；[上游许可](https://github.com/androidx/media/blob/1.8.0/LICENSE) | ExoPlayer、RTSP、PlayerView 与平台 MediaCodec；不包含 INK/Moonlight 实现 |
| Google ARCore SDK | 1.57.0；[SDK LICENSE](https://github.com/google-ar/arcore-android-sdk/blob/1.57.0/LICENSE) 区分 Apache-2.0 源码与受 [ARCore Additional Terms](https://developers.google.com/ar/develop/terms) 约束的二进制 | Maven AAR 为二进制，不能统一标成 Apache-2.0；使用设备的 Google Play Services for AR。Mode 面板提供所需的 Google 服务说明和隐私政策链接；不使用 Cloud Anchors/Geospatial |
| BepInEx Unity IL2CPP / Core / Unity.Common | 6.0.0-be.788，实际 NuGet metadata 标记 LGPL-2.1-only，Copyright © 2022 BepInEx Team；[上游](https://github.com/BepInEx/BepInEx) | compile-only、PrivateAssets=all；未分发 loader/runtime |
| Il2CppInterop | 1.5.3，NuGet 标记 LGPL-3.0-only；[上游](https://github.com/BepInEx/Il2CppInterop) | BepInEx 传递构建依赖，未入库/打包 |
| .NET / Windows Desktop Runtime | Desktop 使用 .NET 10 / WPF；MIT，Copyright (c) .NET Foundation and Contributors | 自包含 Desktop 构建包含对应运行时；`tools/build_desktop.ps1` 从实际发布依赖对应的 NuGet runtime pack 原样复制 LICENSE 及可用的 THIRD-PARTY-NOTICES 到包内 `licenses/`，不包含宿主运行时 |

PC transitive versions are locked in `pc/Miside.Camera.Plugin/packages.lock.json`. The Release APK contains its Android runtime dependencies; the Windows ZIP contains the self-contained .NET/WPF runtime and original adapter DLLs. The build copies notices from the exact .NET runtime packs. Game interop, the game/loader, FFmpeg and MediaMTX are excluded. `PrivateAssets` alone does not determine redistribution obligations. Any future bundle needs a fresh inventory for the actual files distributed.

第二阶段 NeuroMita Adapter 通过 `HostDirectory` 引用本地宿主生成的 Unity/游戏 interop，只调用宿主 API；这些引用为 `Private=false`，不入库、不复制到发行包。实验副本中的 loader、生成绑定、私有验证探针、分析工具及含内部游戏内容的验收截图均在正式仓库外。本项目未复制 NeuroMita、NeuroMita-VR 或 INK 的业务实现和资产。

Android 对 Media3 1.8.0 的 `RtpPacketReorderingQueue.calculateSequenceNumberShift` 施加一处构建期修正：序号空间由 65535 改为 65536，避免回绕时丢弃有效包。不复制或替换其余播放器实现；上游 AAR 保持原样，AGP instrumentation 只修改 APK 内的该常量。构建器要求恰好命中一次，否则失败并要求复核版本。原版权 `Copyright 2021 The Android Open Source Project`、[Media3 LICENSE](licenses/media3-LICENSE.txt) 与[修改说明](licenses/media3-NOTICE.txt)保留，并打入 APK 的 `assets/licenses/`。此修改不由上游背书。

## 项目品牌图标

Desktop 与 Android 图标由项目维护者提供的双图 PNG 沿中线裁开生成，仅做等比尺寸适配并保留透明通道，未从 INK 或游戏中提取。原图权利归对应权利人；源码 MIT 许可不自动授予品牌图稿的独立使用权。源图拆分、平台尺寸与再生成步骤见 [品牌图标](assets/branding/README.md)。

## 单独安装的媒体工具

MediaMTX 1.21.2（MIT，Copyright (c) 2019 aler9；[许可](https://github.com/bluenviron/mediamtx/blob/v1.21.2/LICENSE)）与 FFmpeg 9.0.2 essentials Windows 构建（此构建启用 GPL，GPL-3.0-or-later；[构建来源](https://www.gyan.dev/ffmpeg/builds/)、[FFmpeg legal](https://ffmpeg.org/legal.html)）仅作为本地独立进程使用。插件通过标准输入/RTSP 与其通信，未链接或复制工具代码；仓库只包含原创调用代码与 MediaMTX 配置，不包含二进制。NVENC 还依赖设备驱动和适用的 NVIDIA 条款。当前仅验证本机 NVIDIA 路径，不暗示其他 GPU 可用。若将来捆绑工具发行，需按实际构建重新履行许可、版权、源码提供及相关分发要求；本项目 MIT 不覆盖这些工具。

## 仅研究，未复用

- [NeuroMita](https://github.com/VinerX/NeuroMita) / [NeuroMita-VR](https://github.com/NeuroMita/NeuroMita-VR)，研究 commit `cc62a577a5506db0665a1d9f9910e0a4eb71d69c`。自定义许可证，要求保留 `Copyright (c) 2025–2026 VinerX and NeuroMita contributors` 及 canonical 项目有效直链；不授予 MiSide 内容权利。没有复制业务代码或资产。
- Yomikomi：只读检查已有工具链和模拟器环境，未复制源码、资源、签名或配置。其 Apache-2.0 许可不使本机私有配置可公开。
- [INK](https://inkcamera.top/)：仅研究用户提供样本及产品说明；不包含其 APK、EXE、提取内容、反编译实现、布局、图标或资产。
- [Moonlight Android](https://github.com/moonlight-stream/moonlight-android)、[Sunshine](https://github.com/LizardByte/Sunshine)：仅核对研究背景和上游 GPL-3.0 许可，未作为依赖引入。
- Development guidance: [UI/UX Pro Max](https://github.com/nextlevelbuilder/ui-ux-pro-max-skill), [Emil skills](https://github.com/emilkowalski/skills) (MIT), and [Android Skills](https://github.com/android/skills) (Apache-2.0). These tools are installed separately and are not packaged application dependencies.
- NeuroMita / MiSide 游戏、角色及内部测试材料属于各自权利人，未包含在本仓库或任何发行物中。
