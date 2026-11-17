# Snug for Windows

Windows 平台移植，基于 [Snug](https://github.com/zephyr4123/snug) 2.5.0 (MIT)，保留原项目及其 macOS 构建。Windows 0.2.32 是独立验证版本，尚未获得上游发布。

## 使用

1. 安装 Windows x64 版本的 UU 远程，以及 Snug 安装程序。首次安装需要正常的管理员授权，用于部署官方签名虚拟显示驱动。
2. 手机通过 UU 连接电脑，在屏幕列表切换到 Snug Phone，使用屏幕触控模式。
3. 在资源库查找、打开 App 或选择已有窗口；底部返回条回到资源库。窗口铺屏、滚动、长按及字号操作由 Snug 处理。
4. 可在设置打开截图（默认关闭）。进入 App 后点底部相机，复制当前窗口图片；点预览可以裁剪后再次复制。
5. 当前 Windows UU 会话信息不可读取时，设置显示“UU 兼容模式 · 设备未知”。兼容模式仍允许手机屏，不代表确认了入站连接、设备类型或实际推流屏幕。结束远程后若手机屏未自动释放，可在设置关闭手机屏或正常退出 Snug，以恢复窗口。

## 构建与打包

依赖：Windows x64（Windows 10 22H2 已实测），.NET SDK 10.0.404（global.json 允许同 feature band 的较新补丁），NuGet 还原所需网络；打包还需 Windows Desktop Runtime 10.0.15 与 Inno Setup 7。首次安装需要管理员权限，编译和纯逻辑测试不需要。

从仓库根目录进入 Platforms/Windows：

    cd Platforms\Windows
    build-snug.cmd
    run-snug.cmd

脚本按顺序选择 SNUG_DOTNET_ROOT、项目 .tools/dotnet、DOTNET_ROOT、标准 Program Files/dotnet。非标准 SDK 路径可通过 **当前终端** 环境变量指定。编译器桥使用相同 SDK resolver 和 global.json；不会修改系统 CET 策略或 SDK 二进制。

PowerShell 示例：

    $env:SNUG_DOTNET_ROOT = 'C:\Program Files\dotnet'
    $env:SNUG_ISCC = 'C:\Program Files (x86)\Inno Setup 7\ISCC.exe'
    .\build-snug.cmd
    .\package-snug.cmd

输出：artifacts/installer/Snug-Setup-0.2.32-x64.exe。安装包内置指定 .NET runtime，无需目标电脑另外安装 SDK。驱动分发许可、版本及校验见 installer/THIRD_PARTY_NOTICES.md；安装器本身尚未签名。支持 CET 的系统可用 -p:SnugUseLegacyWindowsCETWorkaround=false 使用 SDK 原生编译路径。

## 架构

- Snug.Windows.Core：独立的窗口/恢复账本、手机尺寸与适配、触控翻译、菜单惯性、UU 会话状态及截图裁剪几何，不依赖 WPF。
- Snug.Windows：WPF 设置及手机界面，Win32 窗口/两轴输入、虚拟显示器管理、UU 只读观察、原像素窗口截图。
- installer：固定版本签名 VDD 25.7.23 和 NefCon 1.21.1；仅操作 Snug 管理的设备节点，驱动包保留。第三方 MIT 许可随安装包分发。

## 已验证与限制

- 224 项核心测试通过；真实 Windows 测试涵盖窗口搬运/恢复、VS Code 跨进程字号恢复、Chrome 标签及双轴滚动、资源库拖动、强制退出、独立窗口截图和裁剪。
- 菜单惯性经过真实 WPF 定时器测试；完整实机手势及真实剪贴板写入尚待验收。实际手机端仍需要一轮完整检查。
- Windows 使用固定的 VDD 支持模式，动态宽度步长为 20 点；多物理屏恢复、部分应用字号、跨虚拟桌面、GPU/受保护窗口的截图兼容仍待测试。
- Windows UU 版本的辅助功能、官方 CLI 和日志尚不能稳定暴露入站设备类型、推流屏幕和真实断开。日志仅作为显式兼容模式，不做设备身份或推流状态的确认。
- 自动更新不在本次移植范围。本分支不是官方 Windows 发布，也不是与 macOS 完全等效的宣称。

## 测试

    dotnet test Snug.Windows.sln -c Release

使用非默认 SDK 时先设置 SNUG_DOTNET_ROOT；测试/构建脚本可直接使用相同路径。PhoneUiSmoke.ps1 支持独立 UI 预览（不等同于真实手机验收）；CapturePhoneVdd.ps1 通过 PMv2 获取真实物理像素；MenuDragProbe 支持 --programmatic 进行不抢占鼠标的惯性定时器验证。探测 UU 的工具 Probe-UuLocalController.ps1 仅用于本地只读诊断。

MIT 许可和上游版权保留，macOS 原有源码不需要改动。