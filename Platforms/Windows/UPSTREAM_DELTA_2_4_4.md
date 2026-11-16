# Upstream Snug 2.4.4 delta — Windows port

Baseline: origin/main 16fe980 (official Snug 2.4.4), merged into Windows branch at d736e88.
This file separates SOURCE SYNC from WINDOWS BEHAVIOR PARITY. No claim of iPhone UU acceptance yet.

## Confirmed Windows outcomes
- Upstream latest sources merged; Windows-only platform files preserved.
- Release build: zero errors, zero warnings.
- Core unit tests: 128 passed, 0 failed.
- Installed Windows package remains 0.2.18; 0.2.19 setup exists but has not been verified installed.

## Newly introduced upstream behaviors to port and test

1. Controller/session detection (upstream 626c837)
   - Only trust controllers in the local controlled-device page; same-account sessions from another Mac should not trigger a local phone screen.
   - Windows equivalents: UuSessionDetector.cs, UuConnectionTracker.cs. Windows behavior NOT YET VERIFIED.

2. Display mode restoration and teardown (upstream 12dcba4, 42e1c02, e67323a)
   - Preserve other displays' modes when adding/removing the phone screen; restore modes by matching attributes, not only mode IDs; defer permanent changes until the remote session actually disconnects.
   - Windows equivalents: PhoneDisplayModeSwitcher.cs, DisplayEnumerator.cs, VddDriverClient.cs. Platform APIs differ; direct Swift implementation cannot be copied.
   - Acceptance: primary and all non-phone monitors retain geometry/scaling/refresh before and after phone screen startup, shutdown and reconnect.

3. Close-window interception and left-behind recovery (upstream cf6a502, 4861eec, 154f5f3)
   - Touch close button is now intercepted, window identity and frontmost position rechecked immediately; closed/hidden apps have retained restore bookkeeping.
   - Windows equivalents: TouchTranslator.cs, MouseGestureHook.cs, WindowManager.cs, RestoreLedger.cs, ZoomBook.cs.
   - Current Windows TouchTranslator only generates ReplayPress, ReplayCurrent, RightClick and Scroll; CloseWindow not yet implemented.
   - Acceptance: close from remote client does not strand an application on the virtual display, reuse stale handles or lose per-app zoom; normal clicks remain unaffected.

4. Gesture changes (upstream TouchTranslator.swift and TouchTap.swift)
   - Close-button-specific routing adds to existing tap/drag/hold/scroll mechanics.
   - Current Windows baseline already has a different native hook implementation; preserve the working 128 core cases before extending.

## Immediate acceptance gates
- Apply and verify installer 0.2.19 on Windows (admin UAC required), compare installed DLL SHA with candidate.
- Real iPhone 13 Pro + UU: phone bottom chevron, app catalog return, tap, scroll, long-press, keyboard, webpage fit, Edge/Chrome, and app restoration.
- Only after these pass, migrate the remaining 2.4.4 behavior gaps with targeted Windows tests and propose an upstream PR.

## Do not confuse
Git merge of macOS Swift sources does not automatically implement their behavior on Windows.
'Chrome isolated window fits on Windows virtual display' does not prove mobile UU compatibility.
'Setup EXE exists' does not mean the installed application was upgraded.