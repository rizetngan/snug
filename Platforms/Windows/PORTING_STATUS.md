# Snug Windows — current project status

Current candidate: 0.2.37
Upstream baseline: Snug 2.5.0
Development branch: feat/windows-platform-support (44 local commits ahead of upstream origin/main)
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
- Activity-based UU log fallback: two streamer-log growths within 3 seconds, expiring 2 seconds after the last growth. A locked file alone is not evidence of a live session.

## Verified

- Core regression suite: 259 passed, 0 failed.
- Full Release solution build: 0 warnings, 0 errors.
- Real iPhone: phone-screen exit and re-entry passed twice.
- Real UU disconnect: test Chrome window restored to its original physical-display bounds, virtual phone display removed, restore ledger cleared, and SYSTEM monitor remained running.
- Chrome app placement/restore and two-axis scroll regression checks passed.
- Installed stable version before this candidate: 0.2.36. Candidate source and installer metadata now target 0.2.37.

## Known limitations

1. UU's current Windows build does not expose reliable inbound controller identity through the available UIA/CLI surfaces.
2. Capture events expose adapter-relative VidPnSourceId; on the test machine both physical and Snug display paths report source 0. Adapter LUID correlation is not yet reliable, so streamed-display identity remains Unknown.
3. Native clipboard write after an actual iPhone screenshot/crop has not been accepted end-to-end.
4. Complete iPhone gesture matrix, mixed multi-physical-monitor restoration, cross-virtual-desktop behavior, and some GPU/protected-window screenshot cases still need broader acceptance.
5. Some application-specific zoom and custom dialog behavior may differ from macOS.

## Release and contribution

- Build: Platforms/Windows/build-snug.cmd
- Package: Platforms/Windows/package-snug.cmd
- Installer: Platforms/Windows/artifacts/installer/Snug-Setup-0.2.37-x64.exe
- Installer itself is unsigned; it bundles the official signed VDD and pinned .NET runtime.
- Built artifact size: 58,979,964 bytes. SHA-256: 3CA39AD044BC4641CEF55FBC84954984933854B5922C68B86F3654B92697553E.
- Upgrade preflight found a live phone-screen placement ledger and correctly aborted. No Snug process was closed, no ledger was cleared, and no installation was attempted; install after the current phone-screen episode ends normally.
- Upstream PR #17 remains open, but the maintainer has said Windows support should be maintained independently. The connected GitHub integration currently has read-only permission on rizetngan/snug; no push or new repository was attempted.

## Next work

1. Install 0.2.37 after the live placement ledger clears; confirm idle behavior with a static locked log and active behavior during real streaming.
2. Complete actual iPhone screenshot/crop/clipboard acceptance.
3. Finish the real-device gesture matrix and mixed-display restore checks.
4. Decide the standalone GitHub repository name/ownership before publishing the independent Windows release.