# Snug 2.4.4 vs Windows 0.2.21 — independent source parity re-audit

Reviewed on 2026-11-16, Windows host DESKTOP-32L33EE.
Official baseline: zephyr4123/snug `origin/main`, commit `16fe9804a9dcf9c532b4306332c86dee2f1d1032`, VERSION=2.4.4.
Port: branch `feat/windows-platform-support`, latest baseline at beginning of audit `8788d43`, Windows assembly/installer version 0.2.21.
Source history: `git rev-list --left-right --count origin/main...HEAD` = 0 behind / 9 port commits ahead at start of audit. All port code under Platforms/Windows; syncing Swift sources does **not** port new behavior automatically.

## Critical differences (fix/verify before claiming parity)

| Priority | Module | Original macOS 2.4.4 | Current Windows 0.2.21 | Status/evidence |
|---|---|---|---|---|
| P0 | UU controller recognition | ControllerReader + ControllerTable reads controller devices from the **local** '本机被控' page; combines accessibility, connection/capture evidence; avoids same-account remote controller on a different Mac being misinterpreted | UuSessionDetector only probes GameViewerServer's controlled connection/stream logfile lock. It always emits `ControllerKind.Unknown`; UuConnectionTracker.WantsPhoneScreen accepts Unknown after 2 seconds | **Not equivalent**. Windows may add phone screen for a desktop controller, or misclassify multi-device sessions. Sources: Sources/Snug/Sensing/ControllerReader.swift, Sources/SnugCore/Session/ControllerTable.swift, Platforms/Windows/src/Snug.Windows/Services/UuSessionDetector.cs, Platforms/Windows/src/Snug.Windows.Core/UuConnectionTracker.cs |
| P0 | Closed/replaced window recovery | LeftBehindBook persists records when a placed app window disappears outside Snug; matches later returning windows using app identity/size/zoom; monitors reopening; up to 14 days and 20 records | One RestoreLedger record keyed by current ProcessId/handle. PutBackCoreAsync and RecoverInterruptedMove clear it immediately if handle is gone. No LeftBehindBook counterpart | **Missing functional capability**. Sources: Sources/SnugCore/PhoneScreen/LeftBehind.swift, PhoneDesk.swift, Platforms/Windows/src/Snug.Windows.Core/RestoreLedger.cs, Platforms/Windows/src/Snug.Windows/Services/AppController.cs |
| P1 | Native close-button touch semantics | TouchTranslator emits special closeWindow command; rechecks frontmost window at tap time; PhoneDesk can restore before actual close | Windows MouseGestureHook converts ordinary taps plus browser-header passthrough, but lacks a native-close-specific command and identity recheck | **Incomplete**. Sources: Sources/SnugCore/Touch/TouchTranslator.swift, Sources/SnugCore/PhoneScreen/PhoneDesk.swift, Platforms/Windows/src/Snug.Windows.Core/TouchTranslator.cs, Platforms/Windows/src/Snug.Windows/Services/MouseGestureHook.cs |
| P1 | Forced quit on timeout | Original waits for quit/confirmation and exposes a **force quit** action if stalled (with further observation) | Windows QuitAppCoreAsync waits 10 s and explicitly reports it will **not** force-terminate; no equivalent action in PhoneSurfaceWindow | **Missing**. Sources: Sources/SnugCore/PhoneScreen/PhoneDesk.swift (`forceQuit`, `quitStalled`), Sources/Snug/PhoneScreen/PhoneHomeView.swift, Platforms/Windows/src/Snug.Windows/Services/AppController.cs (`QuitAppCoreAsync`) |
| P1 | Multi-display state preservation | Tracks all other displays' width/height/pixel mode/refresh; restores modes by properties and coordinates permanent-vs-login-only changes across removal, disconnect and sleep | PhoneDisplayModeSwitcher changes only identified Snug monitor; no other-display mode snapshot/rollback equivalent | **Requires Windows-specific real multi-display validation**. Source: Sources/SnugCore/PhoneScreen/ScreenMode.swift + PhoneDesk.swift vs Platforms/Windows/src/Snug.Windows/Services/PhoneDisplayModeSwitcher.cs, DisplayEnumerator.cs |
| P1 | Automatic update | Signed & checked in-app updater with update feed, failure recovery, update UI | No Updater/UpdateClient equivalent under Platforms/Windows; manual installer only | **Missing**. Sources: Sources/Snug/Update/*, Sources/SnugCore/Update/* vs Platforms/Windows |
| P1 | Packaging/installation maturity | Released macOS signed/notarized package, in-app update, standard install flow | Privileged Inno Setup installer + third-party signed VDD. Registry still reported installed 0.2.18 during audit while separate 0.2.21 portable candidate was running. Upgrading needs admin UAC and installed-DLL verification | **Test candidate not equivalent to installed release**. Sources: Platforms/Windows/installer/Snug.iss, Services/VddDriverClient.cs |
| P1 | End-to-end remote and touch validation | macOS implementation has Swift unit + integration/state tests covering recovery, controller table, modes and touch; original README describes native phone experience | Windows Core unit tests 136/136 and local mouse-click Chrome A->B->A passed. Actual iPhone 13 Pro UU touch/keyboard, page scrolling, popup and extended session are **not yet accepted** | **Acceptance gap**, not proof of failure |
| P2 | Dynamic virtual-screen mode granularity | ScreenFit 10-point width grid at 2x | Windows VDDResolutionProfile advertises every other width (20 points) because full 55-mode driver table failed enumeration on tested Windows host; some sizes differ slightly | **Intentional platform adaptation**, potential fit differences. Sources: Sources/SnugCore/PhoneScreen/ScreenFit.swift, Platforms/Windows/src/Snug.Windows.Core/VddResolutionProfile.cs |
| P2 | Retry behavior | PhoneDesk includes retry delay sequence 5/15/30 s and mode/topology settle, interrupted session/remote teardown handling | AppController has polling/restart/orphan cleanup but not a one-for-one PhoneDesk retry and topology state machine | **Incomplete evidence; run fault-injection acceptance**. Sources: PhoneDesk.swift, AppController.cs |
| P2 | Visible Windows version label | Original derives release version from update/runtime metadata | AppController.VersionText still hardcodes '0.2.19', although project package specifies '0.2.21' | **Confirmed cosmetic bug**. Sources: AppController.cs `VersionText`, Snug.Windows.csproj `<Version>` |

## Capabilities already ported or substantially aligned
- Phone model data: shared `Assets/phone-models.json`, with search and custom size inputs on Windows.
- iPhone-like App catalog sections (running/all/search); app-window rows, create, close/quit confirmation, and settings entry points implemented via WPF.
- Dedicated virtual portrait display, resize/fit against app minimum widths, restoration ledger and per-app zoom bookkeeping exist.
- Bottom HomeBar chevron has click, mouse-swipe, wheel and touch handlers; real UU finger gesture still needs validation.
- Chrome test: with isolated temporary profile on Windows VDD, normal injected mouse click on tab B failed in 0.2.19, but passed B then A in 0.2.21 after native browser-header input bypass. This is **not** a broad Edge/Electron/iPhone acceptance.
- Windows has driver ownership marker and signed driver source checks; do not remove this security protection in the name of parity.

## Direct visual comparison (2026-11-16)
Evidence:
- Original screenshot previously saved at `F:\snug for Windows\diagnostics\UPSTREAM_ORIGINAL_PHONE_LIBRARY.png` (historic original reference; not necessarily 2.4.4).
- Current active Windows 0.2.21 phone VDD capture at `F:\snug for Windows\diagnostics\UPSTREAM_REAUDIT_WIN_0.2.21.png` (720x1223 DPI-virtualized).
- Latest original layout source: `Sources/Snug/PhoneScreen/PhoneAppCard.swift`; Windows layout: `Platforms/Windows/src/Snug.Windows/PhoneSurfaceWindow.xaml`.

P1 confirmed **UI composition regression**: Windows currently shows the '还原' action layered over the placed window row instead of in a distinct row below the window. In `PhoneSurfaceWindow.xaml`, the window row Grid has fixed Height=56 at ~line 194, and the extra placed-controls StackPanel begins at ~line 266 but is another child of the *same Grid* (closing tag ~line 305), without a separate grid row. This causes a visual overlap and may obscure the '在手机屏上' status badge. The original `PhoneAppCard.swift` uses a vertical VStack with PlacedControls below window rows. Refactor the Windows tree (separate row) and verify with real phone screenshot, not just core tests.

Other visual notes: both screenshots have a similar App title, search, Running/All control and app cards; do not count historic screenshot differences as changes to official 2.4.4. The black bottom toolbar visible in the official reference is UU Remote's toolbar, not a Snug control.
## Recommended implementation order
1. P0: Device classification / UU session correctness (mobile vs desktop, multi-controller and disconnect/reconnect), with unit and live UU tests.
2. P0: Left-behind restoration and native-window close interception, including crash, closed/hidden/replaced window, zoom and stale HWND.
3. P1: App quit timeout -> opt-in force quit action, matching upstream.
4. P1: Main/secondary screen mode preservation and orphan display cleanup across session teardown.
5. P1: Full Chrome/Edge/Electron keyboard + finger test matrix on real iPhone 13 Pro in UU; test click/swipe/long press/selection, save dialogs and focus.
6. P2: Native-screen visual alignment & installation/update experiences, supported VDD grid validation, version-label fix.
7. Only then package/sign/release and propose an upstream PR. Do not claim upstream feature parity solely because main Swift files were merged.

## Important boundaries
- Apple's CGVirtualDisplay and macOS Accessibility are not Windows APIs. Behavior should be ported with Windows-specific Win32/VDD equivalents; Swift cannot be mechanically copied.
- This audit is grounded in checked-in source and **previous test evidence**, not a newly executed real-device mobile screenshot comparison.
- macOS original 2.4.4 does not claim finished iPad/tablet workflow; do not count that planned feature as a Windows-only regression.
## 2026-11-16 execution update — Windows 0.2.22
- **Resolved** visual card overlap. `PhoneSurfaceWindow.xaml` now places the placed-window action StackPanel *after* the 56-point window-row Grid, as a sibling in the row's vertical StackPanel, following upstream `PhoneAppCard.swift`.
- Evidence screenshot: `F:\snug for Windows\diagnostics\SNUG_0.2.22_PHONE_CARD_FIXED.png`; header row, placed window status and independent Restore button are clearly separated.
- New `tests/PhoneUiSmoke.ps1` checks the XAML visual hierarchy and verifies restore button has no positive-area overlap with any Close-window button; checks both passed using actual UIAutomation in the 0.2.22 preview.
- The full phone UI smoke passed: Home, All, Search, Confirmation, Model Picker and Return; six preview screenshots were captured to `Platforms/Windows/artifacts/phone-parity-*.png`.
- `AppController.VersionText` now derives from the running assembly; settings-preview UIAutomation reported **0.2.22**.
- Core tests 136/136, Release build 0 errors and 0 warnings.
- Pending still: real iPhone + UU finger verification, session controller classification, left-behind record reattachment and driver-mode recovery. The running 0.2.21 candidate was not replaced during this isolated preview verification.