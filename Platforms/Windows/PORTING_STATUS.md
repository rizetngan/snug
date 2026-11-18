# Snug for Windows — current project status

Current candidate: 0.2.38
Installed stable version: 0.2.36
Upstream baseline: Snug 2.5.0
Development branch: feat/windows-platform-support
Published source branch: https://github.com/rizetngan/snug-for-windows/tree/windows-platform-0.2.38
0.2.38 source commit: 3ad4af3
Release model: independently maintained Windows platform track; upstream has declined to merge or maintain the Windows port.

## Product scope

Windows support preserves the original Snug workflow: App Library, phone workspace, virtual display, window placement/restoration, per-app zoom, touch gestures, browser scrolling, screenshot/crop UI, and UU session lifecycle. The macOS source remains unchanged. Automatic updater remains intentionally excluded.

## Implemented

- WPF Windows application and signed Virtual Display Driver integration.
- Phone model catalog, custom screen fit, 28 advertised VDD modes, and DPI-aware phone canvas.
- App discovery/search, multi-window grouping, focus/pull, new-window, close/quit and restore ledger.
- Touch translation, close-tap protection, menu momentum, vertical and horizontal browser scrolling.
- Screenshot preview/crop workflow using native window capture.
- SYSTEM startup monitor for DxgKrnl capture lifecycle, with a one-way SID-scoped local pipe to the user app.
- Same-session phone-screen re-entry after explicit dismissal.
- UU session evidence fusion: visible GameViewerServer overlay, local controlled-device UIA table, established non-loopback GameViewerServer TCP peers on non-80/non-443 ports, PID-scoped streamer-log growth, and complete SYSTEM ETW capture inventory.
- File locks and persistent 443 relay connections are not treated as session evidence by themselves. TCP peer evidence does not identify the controller device.

## Verified

- Core regression suite: 268 passed, 0 failed.
- Full Release solution build: 0 warnings, 0 errors.
- Real iPhone: phone-screen exit and re-entry passed twice.
- Real UU disconnect: test Chrome window restored to its original physical-display bounds, virtual phone display removed, restore ledger cleared, and SYSTEM monitor remained running.
- Chrome app placement/restore and two-axis scroll regression checks passed.
- Live TCP table P/Invoke check decoded the current GameViewerServer connection correctly: one established peer on port 443, no non-web peer at the time sampled.
- Current UU streamer log remained static at 6,983 bytes during a 6-second sample. This alone does not prove disconnection.
- The SYSTEM capture monitor is running, but it started after the current GameViewerServer generation; its current capture inventory is therefore not a complete disconnected baseline.
- Upgrade was not attempted: Snug runtime PID 27044 is still running; phone-screen.json contains a live SystemSettings.exe placement (PID 14324), and the Snug Phone virtual monitor is active. No process was closed and no ledger or display state was changed.

## Known limitations

1. UU's current Windows build does not expose reliable inbound controller identity through the available UIA/CLI surfaces.
2. Capture events expose adapter-relative VidPnSourceId; on the test machine both physical and Snug display paths report source 0. Adapter LUID correlation is not yet reliable, so streamed-display identity remains Unknown.
3. The current UU server generation predates the active ETW monitor; a complete baseline will be available after a natural GameViewerServer restart while the monitor is already running.
4. Native clipboard write after an actual iPhone screenshot/crop has not been accepted end-to-end.
5. Complete iPhone gesture matrix, mixed multi-physical-monitor restoration, cross-virtual-desktop behavior, and some GPU/protected-window screenshot cases still need broader acceptance.

## Release and contribution

- Build: Platforms/Windows/build-snug.cmd
- Package: Platforms/Windows/package-snug.cmd
- Candidate installer: Platforms/Windows/artifacts/installer/Snug-Setup-0.2.38-x64.exe
- Installer itself is unsigned; it bundles the official signed VDD and pinned .NET runtime.
- Built artifact size: 58,986,254 bytes. SHA-256: 446A36B2A3D45DBED83A021AFE942ED1C3249A1A5F9F7A1DC58F69BE0C00473B.
- Installer copied to C:/Users/kj/Downloads/Snug-Setup-0.2.38-x64.exe; matching SHA-256 text file is beside it.
- Source branch windows-platform-0.2.38 is maintained in rizetngan/snug-for-windows. Upstream attribution and history remain intact.

## Next work

1. Build and package 0.2.38, then install only after the active phone-screen episode and placement ledger have cleared.
2. During a real iPhone UU session, confirm the peer TCP signal and ETW capture transition; after disconnect, confirm the signal clears and window/display cleanup completes.
3. Complete actual iPhone screenshot/crop/clipboard acceptance.
4. Finish the real-device gesture matrix and mixed-display restore checks.