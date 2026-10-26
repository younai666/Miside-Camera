# Miside-Camera

让 Android 手机成为 **原版 MiSide 与 NeuroMita** 的手持 6DoF 摄影机。移动、旋转手机即可运镜，同时实时取景、录制竖屏游戏画面。

[下载 v0.3.0](https://github.com/younai666/Miside-Camera/releases/tag/v0.3.0) · [English](README.md) · [Русский](README.ru.md)

**Miside Camera Desktop** 管理游戏插件、手机连接和串流；**Miside Camera Android** 提供摄影机控制与取景器。两端均支持中文、English、Русский。

## 现有功能

- **ARCore 6DoF 实体运镜**：手机平移、旋转控制游戏 Camera；保留 Manual 手动调试与回退。
- **PC 原生控制同时生效**：手机相对位姿叠加在当前原生 Camera 上，鼠标、WASD 和交互继续可用。
- **USB 网络共享 / LAN**：自动发现 Desktop，用首页六位连接码配对；正常通信不需要 ADB 或 USB 调试。
- **实时 Viewfinder**：接收 1920×1080 游戏串流，填充竖屏取景框，支持重连恢复。
- **Recenter 与 Zoom**：建立新的手机基准，通过 Camera FOV 调整变焦。
- **9:16 录像**：导出取景框内中央游戏画面为 1080×1920 MP4，保存到相册；不含 Android 按钮、边框、网格或计时，暂不录音。

## 四步开始

1. **安装两端。** 从 [Release](https://github.com/younai666/Miside-Camera/releases/tag/v0.3.0) 下载 `Miside-Camera-Desktop-v0.3.0.zip` 和 `Miside-Camera-Android-v0.3.0.apk`。PC 完整解压 ZIP，手机安装 APK。游戏尚未准备插件加载器时，先完成下方的一次性准备。
2. **打开 Desktop。** 自动查找运行中的游戏、相邻安装目录和 Steam 游戏库，自动使用随包提供的 FFmpeg/NVENC、MediaMTX 和 .NET。在设置中选择 **USB 网络共享** 或 **LAN**，保存并启动工作区。保留完整解压目录，无需配置媒体工具路径。
3. **连接手机。** USB：连接数据线，在 Android 系统设置中开启 **USB 网络共享**，仅选择「传输文件」不等于开启网络共享。LAN：让两端处于可互通的局域网。打开 Android App，点击连接 PC，选择发现的 Desktop，输入 Desktop 首页显示的六位连接码。
4. **进入游戏使用。** 在可操作的游戏场景中选择 **模式 → ARCore**，允许相机访问，轻缓移动手机直至开始跟踪。PC 原生控制仍可使用。**更多 → 回正** 建立新基准；**变焦** 调整构图；**录像** 开始/停止录制。等待保存完成后再离开 App，视频位于相册的 `Movies/Miside Camera`。

有效的已保存游戏选择会保留。只有自动发现失败或选中了另一个宿主时，才需要在 **设置** 手动选择 `MiSideFull.exe` 或 `NeuroMita.exe`，保存后重启工作区。普通使用无需开发 SDK 或命令行。

## 必要环境与一次性游戏准备

| 设备 | 要求 |
| --- | --- |
| PC | Windows 10/11 x64；支持 H.264 NVENC 的 NVIDIA 显卡与兼容驱动 |
| 手机 | Android 8+；实体 6DoF 需要支持 ARCore 的设备和 [Google Play Services for AR](https://play.google.com/store/apps/details?id=com.google.ar.core) |
| 原版 MiSide | 已测试 v0.93L，`MiSideFull.exe`，Unity 2021.3.35f1 IL2CPP |
| NeuroMita | 已测试 Unity IL2CPP v2026.10.22，`NeuroMita.exe` |

游戏需要 **BepInEx 6 Unity IL2CPP Windows x64**，已测试版本为 **6.0.0-be.788**。已准备好的游戏可以跳过；否则关闭游戏，将匹配的 [官方加载器](https://builds.bepinex.dev/projects/bepinex_be) 解压到游戏 EXE 所在目录，启动一次并等待进入主菜单，再关闭游戏。之后由 Desktop 自动部署对应的 Camera Adapter。ZIP 不包含游戏或加载器，Desktop 不会自动安装加载器。

FFmpeg/NVENC、MediaMTX 和 Desktop 所需 .NET **均随包提供**；显卡驱动及手机 AR 服务需另行安装。本版本不支持 AMD/Intel 编码器或软件编码。一次只运行一个游戏工作区。

## 连接遇到问题

- 找不到 Desktop：确认 USB 网络共享已开启，或 LAN 两端可互通；可手动填写 PC 的 IPv4 地址与同一连接码。
- 游戏未就绪：检查一次性加载器准备；关闭旧游戏会话，再通过 Desktop 启动。
- 没有画面：完整保留 `tools/`、`licenses/`，检查 NVIDIA 驱动和 Desktop 串流状态。
- 更改网络模式、连接码或软件位置后：保存并重启工作区。检查 VPN、路由器隔离和应用防火墙规则，不要关闭整个防火墙。
- 跟踪暂停：在明亮、有纹理的环境中轻缓移动，或切回 Manual；切回后会清除旧手机偏移。录像导出完成前请保持 App 在前台。

开发用模拟器选择本地模拟器模式，地址 `10.0.2.2`，无需连接码。发现使用 UDP/TCP `39569`，Pose 使用 TCP `39570`，视频使用 TCP `8554`；请在可信 USB/LAN 网络内使用，不要映射到公网。

## 项目信息

原版 MiSide 支持已在 **Samsung Galaxy S23 真机**上确认通过。短实测覆盖镜头叠加、回正/变焦、断开恢复、串流重启与竖屏录像；边界见 [验证范围](docs/validation.md)。不承诺所有设备、游戏版本或固定端到端延迟。

[构建](docs/development.md) · [架构](docs/architecture.md) · [协议](docs/pose-protocol.md) · [第三方声明](THIRD_PARTY_NOTICES.md)

项目原创代码采用 [MIT](LICENSE)。游戏、角色及第三方图稿权利仍归各自权利人；不分发游戏文件/资产、生成绑定、私有测试内容或签名密钥。本项目独立开发，不代表 MiSide、NeuroMita 或 INK 官方。
