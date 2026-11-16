# Snug for Windows

将 [zephyr4123/snug](https://github.com/zephyr4123/snug) 的手机工作区体验移植到 Windows，配合 iPhone 上的 UU 远程使用。

## 当前实现

- WPF 桌面端，深色 Snug 视觉风格，托盘常驻与单实例运行
- 只读检测 UU 远程会话：观察 GameViewerServer 的 controlled 会话日志句柄
- 连接时创建独立竖屏显示器「Snug Phone」，断开时移除设备节点
- 窗口列表、窗口搬运、原位置/最大化/最小化状态恢复
- 底部返回条、左上角 App 返回入口、触控手势与字号放大
- 终端和编辑器字号按应用独立记忆，手机屏顶部可直接调节 A− / A+
- 中断恢复记录，避免窗口被移动后因异常退出而丢失原位置
- 不轮询 uuyc-cli，不修改 UU 原有配置

## 显示驱动

首次安装会自动安装官方签名的 Virtual Display Driver（VDD 25.7.23），并使用 NefCon 1.21.1 管理驱动包与虚拟设备节点。

- 安装阶段只预装驱动包，不常驻创建空白显示器
- UU 会话开始时，通过两个固定的 SYSTEM 计划任务创建 Snug Phone 设备节点
- UU 会话结束时移除设备节点，驱动包保留，避免每次连接重复安装
- 计划任务只允许启动/移除固定的 Root\MttVDD 设备，不接受任意命令参数
- 卸载时只清理 Snug ownership marker 标记为 Snug 管理的驱动；不会接管或删除外部安装的 VDD
- 显示规格：1080 × 1900，60 Hz，EDID 名称 Snug Phone

驱动安装日志：%ProgramData%\Snug\Logs\driver-install.log

第三方许可证与驱动来源见 installer/THIRD_PARTY_NOTICES.md。

## 安装

运行 package-snug.cmd。

生成安装程序：

artifacts/installer/Snug-Setup-0.2.7-x64.exe

安装程序内置 .NET 10 Windows Desktop Runtime，不要求目标电脑预装 .NET。安装器以管理员权限运行，并在安装阶段自动部署官方签名 VDD；安装完成后启动 Snug。

支持 Windows 10 1903 及以上 x64 系统。

## 开发

运行 build-snug.cmd 编译与测试，运行 run-snug.cmd 启动开发版本。

也可以分别执行 dotnet restore、dotnet test -c Release、dotnet build -c Release。

## Windows 10 CET 兼容处理

当前开发机是 Windows 10 22H2 Build 19045。为避免修改系统级安全策略或篡改 Microsoft 签名的 SDK 文件：

- 保留 .NET 10 SDK、Roslyn 与 .NET 10 目标框架
- 使用 C# bridge，通过 dotnet host 启动 SDK 编译器
- Snug 主程序以 DLL 方式运行，不生成 CET-marked apphost
- 安装包使用独立的 Win32 GUI launcher 隐藏启动本地 .NET host，避免出现控制台窗口

Windows 更新到完整支持 CET 的版本后，可用 -p:SnugUseLegacyWindowsCETWorkaround=false 关闭兼容层。

## 原版与移植边界

原版 Snug 的 MIT 版权声明保留在根目录 LICENSE。Windows 版本重新实现显示器、窗口、输入钩子和 UU 感知，不调用 macOS 私有 API。

## 当前验证

- Release 编译：通过，0 warning / 0 error
- 核心测试：17/17 通过，包含 UU 会话持续计时、每 App 字号记忆/设置恢复、应用名回退
- 安装包：0.2.7 已安装验证；SHA-256 与下载目录副本一致
- 打包运行时：独立 .NET 10 runtime；安装包内置运行时
- VDD 与 NefCon：官方包 SHA-256 与数字签名已核验
- 0.2.2：驱动绑定、1080 × 1900 / 60 Hz 手机屏、普通用户启停、设备移除实机通过
- 0.2.3：修复 UU 持续会话反复重置自动创建等待时间，自动创建实机通过
- 0.2.5：窗口列表全宽布局与按钮模板修正，原生分辨率截图验收通过
- 0.2.6：左上角 App 返回入口、每 App 字号记忆、手机屏内 A− / A+ 控件；记事本搬运、返回列表、退出恢复原位验收通过
- 0.2.7 已安装：补齐应用名缺失时的字号识别，支持 Antigravity；检测无响应窗口，不强制搬运或恢复并保留还原记录；Cursor A+/A− 与切换应用后字号记忆实机通过
- 真实 1080 × 1900 手机屏已验证：窗口列表、窗口搬运、左上角返回、底部返回条
- 待完成：Windows 自动更新发布源/差量更新；真实 iPhone UU 端到端操作验收

## UU 远程使用

1. 电脑安装 Snug for Windows，并保持 UU 远程客户端运行；Snug 在后台托盘常驻。
2. iPhone 只需安装 UU 远程，不需要安装 Snug；使用同一 UU 账号连接电脑。
3. 在 iPhone 的 UU 远程屏幕列表中选择「Snug Phone」。
4. 点选窗口后窗口铺满手机屏；左上角「‹ App」或底部指示条返回窗口列表。
5. 选择另一个窗口时，前一个窗口自动归位；结束 UU 远程后，窗口位置和字号恢复，Snug Phone 自动移除。

Windows 版与原版功能映射：

- 已实现：独立手机屏、窗口列表/铺满、底部返回、左上角 App 返回、UU 会话自动创建/关闭、单指滚动/惯性、长按拖选/右键、按 App 记忆字号、托盘常驻、开机启动、异常恢复。
- 待完成：Windows 版自动更新发布通道；真实 iPhone + UU 远程端到端验收。

原版 Snug 的核心交互以「连接 UU → 切换 Snug Phone → 选择窗口 → 左上角或底部返回窗口列表 → 结束远程自动恢复」为主线。
### DPI-correct live phone-screen screenshot

Use `tests/CapturePhoneVdd.ps1` to capture the **real Windows VDD in physical pixels**.
The screenshot process must become Per-Monitor-DPI-Aware V2 before reading secondary-monitor bounds.
Otherwise a 1080x1834 monitor reported as 720x1223 at 150% scaling looks incorrectly cropped.
For example:

```powershell
& .\tests\CapturePhoneVdd.ps1 -OutputPath 'F:\snug for Windows\diagnostics\SnugPhone.png'
```

Running installed-version checks and in-app visual regression checks are separate from normal portable-preview tests.