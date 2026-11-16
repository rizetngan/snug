# Snug Windows — 逐文件上游差异审计（2026-11-16）

Reference repository: https://github.com/zephyr4123/snug
Source revision checked: `c9f46d94ed3f6d46b1132f07f3df9e2bdb969fb5`
Windows branch: `feat/windows-platform-support`
Baseline installed before this iteration: 0.2.15
New source changes: intended for 0.2.16, not considered installed until installer/runtime verification.

## 核心判定

原版的浏览器兼容来自“窗口先铺到当前档位 → 回读窗口实际最小大小 → 升到能装下的虚拟屏档位 → 窗口重新铺满”，**不是浏览器渲染裁切**。原版对浏览器不发送字号放大快捷键。底部的 HomeBarView 是放入窗口之后出现的返回 App 资源库控制，点击或上滑都能触发；原版应用窗口上没有独立的 AppBackBar 顶部覆盖条。

## 逐项对照

| # | 原版源码 / 行为 | Windows 对应模块 | 2026-11-16 状态 | 处理 |
| --- | --- | --- | --- | --- |
| 1 | PhoneScreenWindows.swift: 同一块手机屏上只有全屏资源库面板 + 窗口模式底部 HomeBarView | PhoneSurfaceWindow.xaml, HomeBarWindow.xaml, AppBackBarWindow.xaml | **已修源码、待真机**；移除应用模式额外顶栏，底栏保留 | 用底栏返回资源库，不在浏览器上叠顶栏 |
| 2 | HomeBarView: 中央 chevron.compact.up；点击或向上滑动（12 点）返回列表；随 ScreenFit.scale 缩放 | HomeBarWindow.xaml/.cs | **已修源码、待真机**；此前是深色底 + 横线、没有上滑事件 | 改为浅色背景、向上 chevron，支持鼠标、触摸、滚轮动作 |
| 3 | ScreenFit.swift: 540→1080 点，每 10 点一档（55 档） | ScreenFit.cs + VddResolutionProfile.cs | **有驱动边界**；本机 55 档无法枚举 VDD；2/5/12/28 档枚举并实际切换成功 | Windows 使用每 20 点一档，共 28 档，保持原始宽高比 |
| 4 | PhoneDesk.fill(): 先铺窗口，读最小尺寸，width(toFit)，换档再铺；直到装得下 | AppController.FitMovedWindowAsync + ChangePhoneModeAsync | **已修源码、待真实浏览器**；此前单档且换档失败返回 true，让裁切状态冒充成功 | 当前按 28 档向上匹配；无法移动/超大时明确报告并还原 |
| 5 | ZoomBook.swift: 浏览器不在 zoomableApps，不能对网站使用快捷键重写缩放 | ZoomBook.cs | **原则已对齐，浏览器真机未验收** | 保留 Chrome/Edge 等浏览器原缩放，不发送 Ctrl +/- |
| 6 | WindowService.swift: 系统权限 API 移动/回读窗口，尊重最小尺寸；全屏/最小化恢复 | WindowManager.cs, RestoreLedger.cs | **部分实现**：Win32 SetWindowPos、账本恢复；跨 DPI、全屏、多进程浏览器仍需用真实应用验收 | 按窗口独立测试，不碰用户已有窗口 |
| 7 | PhoneAppCard.swift: 圆形图标，窗口在手机屏上的标记、底部还原/字号 | PhoneSurfaceWindow.xaml + PlacedVisibilityConverter | **源代码已有（0.2.15）**，桌面 UI 回归通过 | 实测放置窗口后验证标记和还原 |
| 8 | PhoneHomeView.swift: 运行中、全部、搜索、页眉退出确认、模型选择 | PhoneSurfaceWindow.xaml, PhoneModelPicker.xaml | **主要界面已有**，仍有平台字体/符号细节差异 | 不重新设计，逐项核对尺寸与交互 |
| 9 | PhoneDesk.swift: 队列串行，打开/新建窗口等待，切换窗口前先还原旧窗口 | AppController.cs, PhoneActionLane.cs | **部分实现**，核心任务队列已有但窗口边界场景待验收 | 使用浏览器、记事本、Electron 分别验收 |
| 10 | NewWindowMenu.swift / WindowService: 通过 AX 菜单语义选择真实「新窗口」，处理各应用不同菜单 | NewWindowMenu.cs, WindowManager.NewWindow | **部分实现**：Win32 HMENU 与已知快捷键，Chromium/Electron 部分菜单可能不暴露 | 使用 UI Automation 菜单补偿而不是仅猜快捷键 |
| 11 | TouchTranslator.swift / TouchTap.swift: 点按、拖动、惯性、长按右键，并拦截放在手机屏上的窗口关闭按钮 | TouchTranslator.cs, MouseGestureHook.cs | **部分实现**：鼠标注入与惯性存在；closeWindow 拦截缺失；UU 输入类型待验证 | 优先补关闭按钮的“还原后关闭”与真实手势 |
| 12 | PhoneDesk.close/quit: 对话框移上手机屏供保存或取消，退出不阻塞事件队列 | DialogClassifier.cs + WindowManager.FindAskingWindow | **部分实现**：只可靠识别当前 PID 的部分 Win32 对话框 | 跨进程 Browser/Electron 弹窗与复杂保存流待验证 |
| 13 | CaptureLogWatcher.swift + AppState.phoneInUse: 知道 UU 正在推哪块显示器，不在看 Snug 时暂停列表/关搜索 | UuSessionDetector.cs | **明显缺失**：只能通过锁定 UU slog 判断连接，尚无法确认目标 display ID | 补真实 UU 采集屏判断和状态反馈 |
| 14 | PhoneDisplayService.reshape: 不换虚拟屏身份即可更新模式/手机比例，UU 多数时候不需重选屏 | VddDriverClient / PhoneDisplayModeSwitcher | **部分实现**：同一套配置内能在线换模式；换手机比例仍延后下次会话 | 保持监视器 ID 不变，明确告知 UU 必须手工重新选屏的场景 |
| 15 | PhoneScreenWindows.swift: 面板平时不抢键盘焦点，只在搜索/机型页暂时获取 | PhoneSurfaceWindow.xaml.cs, HomeBarWindow.cs | **部分实现**，还需真实 UU 键盘焦点验收 | 手机端键盘输入不应转到 Snug |
| 16 | 独立手机屏切换由用户在 UU 的显示菜单完成（README, docs/how-it-works.md） | UuSessionDetector 和 Snug Phone VDD | **上游限制**：Snug 本身不能强制 UU 自动选第二屏 | 保留真实边界，不修改 GameViewer 内部配置 |

## 驱动兼容性 A/B 测试（已在用户 Windows 10 实机执行）

测试模式分别为 2、5、12、28、55 档。2、5、12、28 档均能经 Windows EnumDisplaySettings 枚举，ChangeDisplaySettingsEx 真实切到最宽档并切回基础档，返回码 0；55 档触发“未枚举出 Snug Phone monitor”的情况。每轮使用备份的 phone-profile.json/vdd_settings.xml，在 finally 中还原原来**单档**配置，避免遗留。日志和备份：`F:\snug for Windows\diagnostics\VDD_MODE_PROBE_20261116_*`。28 档短时 Win32 验证通过，仍需 30 秒持续稳定与真实 UU 会话验证，不能据此声称全设备兼容。

## 本轮源码变更与质量门禁

- HomeBarView 风格改为浅色背景 + chevron，补点击/上滑/滚轮返回；应用窗口移除多余顶部栏
- VDD 模式从固定一档恢复为每 20 点一档；上游 widthToFit 仍以实际最小窗口为依据，所需宽度向上对齐驱动模式
- 窗口移动失败或超出模式上限时拒绝伪成功，保留用户原窗口
- 128/128 Core 测试已过；Release 0 errors, 0 warnings；PhoneUiSmoke home/all/search/confirmation/model/return 通过
- **尚未验证**：新版在 Windows 实际安装并承受 UU 会话、真实 Chrome/Edge 最小宽度切换与浏览器网页缩放、HomeBar 真机上滑、跨进程保存对话框、UU 采集屏编号

## 实际验收门槛

1. Win10 安装 28 档版后，Snug Phone 30 秒以上持续在线，无主屏干扰、无需重启。
2. Chrome 和 Edge 各开一个独立临时窗口：验证 540 点宽基准、遇最小窗口宽度后扩到 560/580/...、窗口左/右/底部均完整；关闭时还原原尺寸。
3. 手机端显示已放置网页后，底部灰色上箭头可点、可上滑返回 App 资源库；不出现原先多余的顶部控制条。
4. UA/输入验收：列表点击、网页滚动、长按右键、键盘输入、关闭按钮安全还原。
5. UU 仅显示 Snug Phone：主屏壁纸/Windows 任务栏不混入，且换屏/切机型后摄取坐标一致。
6. 完成全部门禁后才能将“与原版同等体验”标记为已验收，之后才考虑给原作者提交 PR。

## 0.2.16 真实 Chrome 窗口调度发现的回归与 0.2.17 修正

用独立用户资料 Chrome（PID 24956，临时本地页面）验证 0.2.16：应用成功进入虚拟屏并触发多档换屏，但在物理 1640×2784 的新屏上，浏览器实际只占 1440×2274，底部 HomeBar 也仍占旧的宽 1440、高 76，导致右边及下方出现 Windows 壁纸与任务栏。保存的实际截图：F:\snug for Windows\diagnostics\SNUG_0.2.16_BROWSER_PLACED.png。说明“模式可切换”并不等于“窗口实际铺满”，不能将 0.2.16 标为浏览器完整适配。

复核 AppController.ChangePhoneModeAsync：旧代码只等 EnumDisplaySettings 回到新物理模式，而 WinForms Screen.AllScreens 的 DPI 虚拟化尺寸尚未从 720×1223 刷新，因此把旧窗口边界重新用于 MoveTo 和 HomeBar.PlaceOn。这与原版 PhoneDesk 屏幕参数变化后复查/重铺逻辑不符。0.2.17 源码修复：模式变更时额外等候桌面布局宽/高改变才采纳 found 显示器边界；窗口移动和 HomeBar 同用 ScreenFit.HomeBarHeight(display.Bounds.Width) 留足底栏高度，去掉对逻辑屏幕宽度错误的二次乘 2。

0.2.17 待验：原本的隔离 Chrome 重新放置、真实物理屏尺寸下的窗口实际覆盖率、底栏贴底位置、点击箭头后的恢复与还原。除非整套通过，否则不标记为浏览器兼容完成。
## 0.2.19 — Chrome / VDD / HomeBar real device check (2026-11-16)

- Upstream source baseline: zephyr4123/snug commit c9f46d94ed3f6d46b1132f07f3df9e2bdb969fb5. Windows branch: feat/windows-platform-support.
- Release build: 0 errors, 0 warnings. Core 128/128 PASS; PhoneUiSmoke home/all/search/confirmation/model/return PASS.
- Isolated Chrome test (not the user's standard Chrome profile), physical browser rect 1920,0,1640,2698. Physical Snug Phone rect 1920,0,1640,2784. Reserved HomeBar 86 px; no clipping. Local test page F:\snug for Windows\diagnostics\browser-fit-0.2.16\fit-test.html, temporary Chrome PID 24956.
- Version 0.2.18 exposed Shell_SecondaryTrayWnd raising itself above HomeBar during mode changes. Version 0.2.19 adds HomeBarWindow.EnsureAboveTaskbar(): checks whether the secondary taskbar is above and geometrically overlaps the HomeBar, then uses SetWindowPos(HWND_TOPMOST, SWP_NOMOVE|SWP_NOSIZE|SWP_NOACTIVATE). AppController calls it from the 500 ms tick without claiming focus.
- Actual 0.2.19 candidate WPF runtime: Z-order HomeBar rank 20, secondary taskbar rank 21. Evidence screenshot F:\snug for Windows\diagnostics\SNUG_0.2.19_CANDIDATE_PHONE_CHROME.png shows centered HomeBar up-chevron above taskbar.
- UIAutomation HomeBar button invoked successfully: returned to App catalog with isolated Chrome still running. Then the isolated Chrome window's inline restore button was invoked. Native Win32 restored Chrome rect = 180,100,667,520 on the host main screen. Virtual Snug Phone returned to base 1080x1834 physical mode (WinForms logical 720x1223).
- Built installer F:\snug for Windows\artifacts\Snug-Setup-0.2.19-x64.exe, 55,622,776 bytes, SHA-256 2B5C8BF9C12FEB36E7BDE38C436DA2DA0B6D30DA70ABAEE6529DD34680016B76.
- Install status DISTINCT from candidate test: Windows uninstall registry still says 0.2.18 installed; tested 0.2.19 via pinned .tools dotnet and Release verify-build DLL. An elevation request from a nonadmin remote session produced no new setup log. Do not claim the installer applied until it reports completion and the installed runtime DLL hash is checked.
- Remaining gaps: real iPhone UU finger gestures/keyboard, Edge/Electron cross-process windows, long-run secondary taskbar stability. No upstream PR until full behavior parity acceptance.
### 0.2.19 HomeBar 置顶恢复补测

Using the 0.2.19 candidate runtime, opened an isolated Chrome window on the real phone VDD, confirmed HomeBar visible. A diagnostic Win32 call intentionally promoted Shell_SecondaryTrayWnd above HomeBar (GetWindow Z scan returned JUST_PROMOTED_TRAY_ABOVE=True). Without further intervention, the 500ms AppController watchdog restored the bar within three seconds (AFTER_THREE_SECONDS_BAR_ABOVE=True, WATCHDOG_REPAIR_PASS=True). The diagnostic cleanup required no manual raise. After the test, UIAutomation invoked the only bottom HomeBar button and then the isolated Chrome window's Restore action: both passed, Chrome remained alive and was returned to the host display.

These checks validate the new WPF implementation on this Windows host, not touch input behavior inside the iPhone UU client. Installed registry version still needs elevation-backed upgrade from 0.2.18 to 0.2.19.