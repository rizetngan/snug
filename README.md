<div align="center">

<img src="Assets/AppIcon/AppIcon-1024.png" width="100" alt="Snug 图标">

# Snug for Windows

**让 Windows 电脑在手机上拥有原生级的操作体验**

为 UU 远程打造 · 独立竖屏 · 原生触控 · App 资源库

![Windows x64](https://img.shields.io/badge/Windows-x64-blue) ![Preview](https://img.shields.io/badge/version-0.2.38%20preview-orange) [![MIT](https://img.shields.io/badge/License-MIT-green)](LICENSE)

简体中文 · [English](README.en.md)

[下载](#下载安装) · [使用](#使用方法) · [开发](#开发)

</div>

Snug for Windows 是 [Snug](https://github.com/zephyr4123/snug) 的独立社区 Windows 移植项目，由 [rizetngan](https://github.com/rizetngan) 维护。基于 Snug 2.5.0，使用 C# / .NET 10 / WPF。macOS 用户请前往上游原项目。

## 下载安装

**0.2.38 社区预览版** · Windows x64（Windows 10 22H2 已实测）

[GitHub Releases](https://github.com/rizetngan/snug-for-windows/releases) · [从源码构建](Platforms/Windows/README.md#构建与打包)

安装包文件名：`Snug-Setup-0.2.38-x64.exe`。当前已完成实机验收，独立 Release 待发布。安装包内置 .NET 运行时，首次安装需要管理员授权以安装签名虚拟显示驱动并注册 UU 捕获监视任务。安装器本身尚未签名。

## 核心功能

- 专属手机竖屏，应用窗口适配与还原
- App 资源库，查找、打开、切换及管理应用窗口
- 单指滚动、双轴滚动、长按交互与菜单惯性
- 可选窗口截图与裁剪，复制到 Windows 剪贴板
- 同一有效 UU 会话中退出手机屏后可以重新进入

## 使用方法

1. 在电脑与手机安装 [UU 远程](https://uuyc.163.com/)，在电脑安装并运行 Snug。
2. 在设置中选择手机型号并开启手机屏。
3. 手机通过 UU 连接电脑，在屏幕列表选择 Snug Phone，使用屏幕触控模式。
4. 在 App 资源库打开窗口，轻点底部返回条切回资源库。
5. 退出手机屏后，可从主屏右下角「进入手机屏」重新创建，再通过 UU 切换显示器。结束远程后，Snug 恢复受管理窗口并释放虚拟屏。

[详细安装与使用文档](Platforms/Windows/README.md)

## 验证与限制

2026-11-18：268 项核心测试通过，Release 构建通过，iPhone + UU 远程实机验收通过，包括退出、重新进入及断开后的恢复。

控制端设备类型与实际推流显示器身份仍未知；部分截图、字号兼容、多屏恢复及完整手势矩阵仍待扩大验证。

[验收记录](Platforms/Windows/ACCEPTANCE_20261118.md) · [更新记录](Platforms/Windows/CHANGELOG.md) · [已知限制](Platforms/Windows/README.md#已验证与限制)

## 开发

Windows 当前维护入口为默认分支 `main`，平台代码位于 [Platforms/Windows](Platforms/Windows/)。

```powershell
git clone https://github.com/rizetngan/snug-for-windows.git
cd snug-for-windows/Platforms/Windows
.\build-snug.cmd
.\run-snug.cmd
```

[构建、测试与打包](Platforms/Windows/README.md) · [移植进度](Platforms/Windows/PORTING_STATUS.md)

## 致谢与许可

感谢 [Snug / zephyr4123](https://github.com/zephyr4123/snug)、[UUyc-companion / AoyamaShou](https://github.com/aoyamashou/UUyc-companion)、原项目社区贡献者、[Virtual Display Driver](https://github.com/VirtualDrivers/Virtual-Display-Driver) 与 [NefCon](https://github.com/nefarius/nefcon)。

保留原项目历史、macOS 源码与版权声明。Windows 版本独立开发、发布和维护，不属于原项目官方 Windows 版本或 UU 远程官方软件。

[MIT License](LICENSE) · [第三方许可](Platforms/Windows/installer/THIRD_PARTY_NOTICES.md)
