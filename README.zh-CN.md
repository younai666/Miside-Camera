# Miside-Camera

[English](README.md) · [Русский](README.ru.md) · [下载 v0.2.0](https://github.com/younai666/Miside-Camera/releases/tag/v0.2.0)

让 Android 手机成为游戏的手持摄影机和实时取景器。桌面端为 **Miside Camera Desktop**，手机端为 **Miside Camera Android**，支持 USB 网络共享或局域网；日常通信不需要 ADB、USB 调试或互联网。

**当前适配的是 NeuroMita Unity IL2CPP v2026.10.22，并非 MiSide 本体。** 发布包不包含游戏、游戏资产或加载器。

## 安装和连接

1. 下载 Desktop ZIP，解压后打开 `Miside Camera Desktop.exe`；在 Android 8 及以上手机安装 APK。Windows 需要 10/11 x64；目前视频编码路径需要支持 NVENC 的 NVIDIA 显卡。
2. 为受支持游戏安装 [BepInEx Unity IL2CPP 6.0.0-be.788](https://builds.bepinex.dev/projects/bepinex_be)，运行一次，等待生成 `BepInEx/interop` 后关闭游戏。
3. 单独下载 [FFmpeg](https://www.gyan.dev/ffmpeg/builds/) 和 [MediaMTX](https://github.com/bluenviron/mediamtx/releases)。Desktop「设置」中选择游戏程序、`ffmpeg.exe`、`mediamtx.exe`。
4. **USB：** 数据线连接手机，系统设置开启「USB 网络共享」；Desktop 选 USB 网络共享，保存并启动工作区。**局域网：** 手机和 PC 接入可互相访问的网络，Desktop 选局域网。
5. Android 点「连接 PC」，选发现的 Desktop，或填写 Desktop 显示的 IPv4 地址，再输入首页六位连接码。模拟器选本机模拟器模式，使用 `10.0.2.2`，不填连接码。

进入游戏场景后，ARCore 模式用真实手机位移/旋转叠加控制镜头；Manual 永久保留作调试/回退，PC 鼠标及 WASD 继续有效。ARCore 需要受支持设备和 Google Play Services for AR。回正在「更多」中，变焦控制 FOV；切到 Manual 会释放上一段手机偏移。

点击「录像」开始/停止，文件保存到相册的 `Movies/Miside Camera`。保存的是 **1920×1080 游戏原始码流**，不含 Android UI，不二次编码；当前没有音轨。断开时会结束并保存当前录像。

连不上时先检查 USB 共享网卡、IPv4 地址、连接码，以及 VPN/路由隔离和应用防火墙规则。更换连接方式或连接码后，需要先关游戏、再重启工作区。不要关闭整个防火墙或把端口暴露到公网。

[详细构建](docs/development.md) · [验证范围](docs/validation.md) · [第三方声明](THIRD_PARTY_NOTICES.md)

项目原创代码采用 MIT；不包含 NeuroMita、MiSide、INK 的游戏代码或资产，也不代表这些项目的官方产品。
