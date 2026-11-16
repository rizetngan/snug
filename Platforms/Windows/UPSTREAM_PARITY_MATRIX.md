# Windows ↔ Upstream Snug 功能一致性门禁

基准：`zephyr4123/snug` commit `a4cb611`（原版 2.4.0）；Windows 分支 `feat/windows-platform-support`。

目标：Windows 作为原版新增平台，手机通过 UU 远程操作时，页面结构、反馈和业务流程与原版一致；系统实现保留 Windows 专有适配层。

## 模块映射

| 原版源码 | Windows 代码 | 状态 |
|---|---|---|
| `Assets/phone-models.json` | 直接引用仓库根目录同一文件 | 已复用 |
| `SnugCore/PhoneScreen/PhoneModels.swift` | `Core/PhoneModels.cs` | 已移植 |
| `SnugCore/PhoneScreen/ScreenFit.swift` | `Core/ScreenFit.cs` | 核心算法已移植 |
| `SnugCore/PhoneScreen/Apps.swift` | `Core/AppCatalogModels.cs` | 搜索、排序、分组已有 |
| `Snug/PhoneScreen/PhoneHomeView.swift` | `PhoneSurfaceWindow.xaml(.cs)` | 0.2.10 首页重构、吸顶切换、当前 App 分组 |
| `Snug/PhoneScreen/PhoneAppCard.swift` | `PhoneSurfaceWindow.xaml` | 卡片、窗口操作、手机内确认 |
| `Snug/PhoneScreen/PhoneSearchPage.swift` | `PhoneSurfaceWindow.xaml(.cs)` | 独立搜索、App 与窗口分组 |
| `Snug/PhoneScreen/PhoneModelPage.swift` | `PhoneModelPicker.xaml(.cs)` | 浅色原版结构、虚拟化列表、自定义比例 |
| `Snug/PhoneScreen/PhoneStyle.swift` | `PhoneSurfaceWindow.xaml`、`PhoneModelPicker.xaml` | 40/22/18/15 字阶、56 点触控区、浅色卡片 |
| `Snug/PhoneScreen/PhoneScreenWindows.swift` | `PhoneSurfaceWindow`、`HomeBarWindow`、`AppBackBarWindow` | WPF 实现；键盘焦点和全屏覆盖已初步移植 |
| `SnugCore/PhoneScreen/PhoneDesk.swift` | `Services/AppController.cs` | 尚未达到原版完整状态机行为 |
| `SnugCore/Touch/TouchTranslator.swift` | `Core/TouchTranslator.cs`、`Services/MouseGestureHook.cs` | 基础移植；真机手势质量未验收 |
| `SnugSystem/PhoneDisplayService.swift` | `VddDriverClient.cs`、`PhoneDisplayModeSwitcher.cs` | Windows VDD；无损在线变更比例未验收 |
| `Snug/Sensing/*` | `UuSessionDetector.cs` | 仍缺控制端类型精准识别 |

## 0.2.10 UI 门禁

- [x] 首页 iOS 分组背景、卡片、图标网格、吸顶分段。
- [x] 当前 App 优先区域、全部 App 字典序网格、独立搜索页。
- [x] App / 窗口操作反馈与手机内二次确认。
- [x] 手机上直接选择型号，与桌面设置共用配置。
- [x] 机型按上游数据搜索，使用虚拟化列表防止一次性创建 800 个控件。
- [x] 手机 UI 可见时停用应用手势翻译，切到放置窗口时重新启用。
- [x] 66 项核心测试；UI 预览自动化：主页面、全部、搜索、确认、机型、返回。
- [ ] iPhone + UU 真机、关闭/断开/重连的端到端验收。
- [ ] 视觉双端逐像素比对及各种手机比例（13 mini 到 Pro Max）的实际触控验收。
- [ ] 系统应用新窗口菜单、保存提示、强退、最小化/全屏等完整生命周期。
- [ ] 不更换 VDD 屏幕身份的在线比例变化与 UU 无断流验收。
- [ ] 原版 PhoneDesk 串行状态机及异常恢复的完整测试。

可复验的命令：

```powershell
& "Platforms\Windows\tests\PhoneUiSmoke.ps1" -Capture
dotnet test Platforms\Windows\Snug.Windows.sln -c Release
```

截图在 `Platforms/Windows/artifacts/phone-parity-*.png`；安装与驱动**没有**在本轮自动执行。

## 提交上游的边界

原 macOS 文件保持原样。对原版 UI 结构移植时保留上游版权与许可证；Windows 平台差异通过新接口和服务适配，而不是修改原 macOS 实现。正式提交前需要干净 Windows 安装、升级卸载、真实 UU 远程联动、触控与窗口恢复测试。

## 0.2.11 — PhoneDesk lifecycle and new-window parity (2026-11-16)

- Upstream NewWindowMenu matching rules are ported to Snug.Windows.Core/NewWindowMenu.cs, with exact and localized new-window titles preferred over document/new-file actions.
- WindowManager scans supported native Win32 menu items (HMENU) and invokes a verified enabled New Window command. Browser and VS Code/Cursor shortcuts are allowlisted; unknown apps no longer receive a blind Ctrl+N.
- Close/quit now observe the actual window/process result for up to 8/10 seconds. Owned native save/confirmation dialogs are detected with DialogClassifier, moved onto the phone monitor without resizing, and tracked by the existing restore ledger. The dialog's dismissal is watched for recovery.
- No process is force-killed when a close/quit request times out. Unsupported custom browser/native dialogs remain a real-device test gap.
- A FIFO PhoneActionLane serializes activation, new-window, close, quit, pull, restore and return-to-App operations; nested operations cannot deadlock the queue. Core regression tests now total 109 (including native menu selection, dialog classification and concurrency).
- Release build passed with 0 errors and 0 warnings. tests/PhoneUiSmoke.ps1 still passes all on-phone UI flows.
- Windows HMENU/Win32 behavior and real iPhone+UU save prompts have not yet been validated together. Driver installation, active display configuration and UU settings are unchanged.
