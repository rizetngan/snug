# Upstream parity audit: macOS 2.5.0 vs Windows 0.2.29

## Baseline
- Fresh fetch origin main succeeded on 2026-11-17. Upstream VERSION=2.5.0, 7ef8b8d588be53aacecdc6a0ab9ca10dacd2311c.
- Windows pre-audit HEAD 0b62a7dae89eb7de4b7717d91113d19bc2798ac2; merge base 16fe9804a9dcf9c532b4306332c86dee2f1d1032 (2.4.4). Ahead 24 / behind 16 before this documentation change. No upstream merge performed.
- Actual portable runtime PID 1752: artifacts/verify-0.2.29/Snug.Windows.dll. Installed Program Files version remains 0.2.26; HKCU Run points at installed launcher. 0.2.29 setup exists (55,651,623 bytes).
- Last verified regression: 196/196, Release 0 errors/warnings. Live DISPLAY27 present: 720x1223 logical. No functional code or runtime changes in this audit.

## New upstream feature since 2.4.4
The 16 commits are overwhelmingly the optional screenshot PR #15 and its review, UI/permission fixes, docs and release. No change to existing TouchTranslator.swift, ControllerReader.swift, or PhoneDesk.swift in this range.
- AppConfig.swift, SettingsWindow.swift: screenshot disabled by default; independent permission/status. Windows SnugSettings.cs/MainWindow.xaml have no screenshot setting.
- PhoneScreenWindows.swift: left camera in bottom bar, center return behavior retained. Windows HomeBarWindow.xaml has only return button.
- Screenshots.swift: serial invalidation, capture current placed window, latest request wins, full PNG clipboard, 5-second preview, 3-second notice, crop/cancel/finish, disconnect teardown. Windows has no corresponding state machine.
- ScreenshotService.swift: ScreenCaptureKit desktop-independent window, native pixel resolution, no cursor, PNG/clipboard. Windows needs a native window-capture adapter (evaluate Windows Graphics Capture), not a capture of compressed UU image or whole desktop. Exact capture behavior must be verified, not assumed.
- CropGeometry.swift / CropView.swift: normalized rectangle persisted across layouts, eight edge/corner handles, offset-based resize, minimum dimensions, full screenshot on cancel. Windows no crop UI. Gesture translator suspended while overlay consumes crop events. Screenshot data only in memory/clipboard; no screenshot history. Windows permission model differs; do not copy macOS-only permission prompts.
- Setting binding fix 83eb3d3 and Swift/compiler fixes apply to macOS code; no reason to rewrite unrelated Windows UI.

## Existing functional comparison
| Area | Windows 0.2.29 | Remaining |
|---|---|---|
| App library, all apps, recent window, pinyin search | AppCatalogModels.cs, PhoneSurfaceWindow.xaml implemented | Cross-virtual-desktop and broad packaged app/fullscreen matrix not demonstrated |
| Model picker, custom aspect, dynamic fit | shared model data and ScreenFit; real Chrome/VS Code placement | VDD 20pt mode steps vs upstream 10pt; retained driver-tested compromise; tablet landscape is upstream limitation, not missing parity |
| Phone menu drag | PhoneDragScroll.cs; actual local WPF/phone menu injection tested | UU actual iPhone after 0.2.28 not explicitly accepted; mouse drag adapter lacks explicit fling |
| Browser content and tab input | MouseGestureHook.cs, WheelDeltas.cs; real horizontal/vertical wheel dispatch, previous tab switch tests | iPhone UU horizontal/diagonal gestures, actual momentum and per-browser matrix pending |
| Back bar, return, close, quit, save dialog | implemented, isolated Windows tests | broader app-specific and actual phone validation |
| Window restoration | RestoreLedger/LeftBehindBook; real fixture and VS Code geometry verified | maximized/minimized, cross-device/virtual desktops and broad app validation |
| Zoom | ZoomBook range -4..8; VS Code cross-process real test passes | Cursor/Zed scope and non-default VS Code zoom settings not verified; Windows-specific app support differs |
| Other monitors | OtherDisplayGuard; real main monitor unchanged across VDD lifecycle | multi-physical-screen and actual mode drift repair not verified |
| Incoming controller identity | UuControllerReader and tracker ported | actual UU 4.46.0.4716 UIA/MSAA no rows, device status JSON empty, controlled log files locked with zero bytes; iPhone connected sample did not distinguish. Unknown fallback persists; cannot prove mobile vs PC or accurate disconnect from these signals |
| Active streamed display identity | upstream CaptureSessionState.swift + PhoneScreenWindows.swift watching gate | Windows does not resolve actual streamed display; no equivalent pause gate |
| Installation | installer/owned VDD and manual UAC upgrade tested through 0.2.26 | install 0.2.29 or successor and verify reboot; do not say candidate is installed |
| Automatic updater | omitted intentionally | explicitly excluded by user; not a development blocker |

## Priorities
P0: reliable local UU inbound controller and streamed-display evidence; if unavailable, explicitly state Unknown and retain user-confirmed fallback; avoid account-wide guessing. Fix confirmed phone regressions only.
P1: upstream screenshot feature, preserving default-off, modular state + native capture + crop + clipboard. Test actual iPhone input and screenshot dimensions. Then install the verified successor using standard UAC (no auto-update).
P2: finish Cursor/Zed and multimonitor/virtual-desktop acceptance. Preserve known VDD compromise until tested improvement.
No equivalence claim to 2.5.0, no functional implementation or merge in this audit.
## 0.2.29 formal installation and 0.2.30 upstream screenshot feature
- UAC upgrade to 0.2.29 returned installer exit code 0; C:\Program Files\Snug DLL 0.2.29+0b62a7dae89eb7de4b7717d91113d19bc2798ac2, installed launcher started PID 24876. The previous 0.2.26 version is no longer the installed baseline. Upgrade log: F:\snug for Windows\diagnostics\UPGRADE_INSTALLED_0.2.29.log.
- Upstream 2.5.0 PR #15 (Screenshots.swift, CropGeometry.swift, ScreenshotService.swift, PhoneScreenWindows.swift, ScreenshotViews.swift, CropView.swift) now has a Windows counterpart. Core screenshot state machine, normalized geometry, Windows capture/crop/clipboard service, default-off settings toggle, camera left of return bar, thumbnail/notice and full-phone crop UI. Scope kept within placed window; no file history, no desktop-wide fallback; crop temporarily stops the app gesture translator. Dismiss and serial guard stale completion on return, switching app, closing, expiry, and disconnect. User's auto-update exclusion retained.
- Platform implementation deliberately uses native PrintWindow(PW_RENDERFULLCONTENT), not macOS ScreenCaptureKit, with scoped physical DPI, HWND/PID validation and one bounded worker. Chrome rendered 860x640 original pixels; sampled RGB red=(216,39,64), green=(36,188,107); 400x300 crop matched source pixels. Both visible and covered standalone Chrome passed. This is not a guarantee for all GPU/protected windows (some PrintWindow implementations can return blank content); broader app-matrix and WGC alternative remain validation work.
- Real independent WPF crop UI 540x917: camera default-off/toggle verified; mouse drag of top-left handle set normalized x=.25, y=.20048, right=1, bottom=1. Visual screenshot reviewed (black canvas, top Cancel/Done, dimmed outside, white eight handles). Diagnostic images and probe sources in F:\snug for Windows\diagnostics\screenshot-250. Actual user clipboard was deliberately not overwritten in probes; copy integration is unit-tested through a fake service, native clipboard and actual iPhone interaction remain acceptance checks.
- 212/212 core regression tests; final Release no errors/warnings before version bump. Existing actual UU controller/streamed-display evidence remains unresolved; no change to detector or automatic update.
## 0.2.30 package, live candidate and upstream merge
- Setup: F:\snug for Windows\artifacts\Snug-Setup-0.2.30-x64.exe; SHA256 9B76020440760142C57E636EC3A272F5F14FEC28294D98336FAE94D0B710C132. Portable runtime PID 22924 version 0.2.30+0013d0a; settings UI 0.2.30 and screenshot toggle Off; VDD DISPLAY29 720x1223 logical. Installed stable version remains 0.2.29. No native clipboard write or actual iPhone screenshot acceptance yet.
- Merged reviewed origin/main 7ef8b8d (2.5.0) into local Windows branch; verified current Windows source and shared phone-model data unchanged by merge. Existing package was built from pre-merge Windows implementation commit 0013d0a, thus binary Windows source is identical. No push to upstream.