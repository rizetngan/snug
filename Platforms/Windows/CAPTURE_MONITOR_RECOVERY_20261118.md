# 0.2.39 candidate — repeated display recreation

Observed on November 18, 2026 during a connected iPhone + UU README media capture:
- Display IDs changed repeatedly; the installed 0.2.38 app moved the demo Chrome window and restored it before a stable phone frame could be captured.
- Snug driver-install.log records successful StopDisplay/StartDisplay pairs, for example 21:28:06/21:28:11 and 21:30:15/21:30:20 local time. This is actual device removal/recreation, not merely a resolution change.
- The SYSTEM monitor exited at 14:01 and 14:02 local time with COMException 0x800705AA. The existing scheduled task reported result 1. A manual run at 21:35 reproduced the same error.
- Previous installer logs show repeated task stop/install operations. The old monitor used a fresh GUID trace name on every launch. TraceEvent documents that abruptly terminated processes can leave orphaned ETW sessions. Resource exhaustion is consistent with leaked sessions, but the exact count/ownership requires an elevated ETW inventory or successful startup cleanup log.
- Candidate changes: stable persistent trace name, exclusive service-instance handle, startup reclamation limited to exact legacy persistent Snug monitor names, and provider-start logging. No change to controller evidence, reconnect timing, window restoration or VDD fitting.
- Automated tests: 276 passed, including eight new trace-name boundary cases. Installer build succeeded. Candidate tested binary has not yet been installed or exercised with an iPhone.
- Installer SHA-256: 8A751E2ABDBB72D3C4618E0777119246C644EBF29D548ED324C60ACF99A898A9; size 58,974,536 bytes. Built from candidate working tree before commit, not a public release artifact.

Acceptance gates: capture monitor starts and stays running; no unexpected StopDisplay/StartDisplay during an active session; Chrome fits and returns; explicit phone-screen dismissal/re-entry; disconnect restores windows and releases display. Preserve 0.2.38 release until gates pass.
## Installed candidate acceptance — 2026-11-18

The exact installer hash above was installed successfully on Windows 10. The installed version reported 0.2.39 (built from the 9c24aea-based candidate working tree before commit 9917e3e).
- 21:53:47 local time: monitor reclaimed six orphaned legacy ETW sessions; provider enabled and capture feed connected successfully.
- First post-upgrade connection did not yield a reliable capture inventory because UU's server predated the restarted monitor. With the phone disconnected, the UU Windows service was restarted at 22:04:53; device binding and configuration were preserved.
- 22:06:54: real iPhone + UU capture Active, complete inventory true. DISPLAY44 created once; no unexpected driver stop/start until the explicit dismissal at 22:12:24. Chrome dynamically fitted to 1640 x 2784 physical pixels, with the bottom return bar, and returned to the App Library.
- Explicit dismissal restored Chrome to its original bounds (10,10,945,1020), cleared the restore ledger and released the virtual display. Re-entry created DISPLAY45, and Chrome fitted again.
- 22:13:47-48: direct UU disconnect; capture Rebuilding then Inactive, complete inventory true. 22:14:05: window restored; 22:14:07: display stop confirmed. Main display only, restore ledger absent, original Chrome bounds verified.
- The test used actual iPhone connectivity with PC-side automation; not a full touch-gesture matrix or proof of controller-type/display-identity recognition.
- Upgrade guidance: if Snug cannot identify an active session immediately after an upgrade with an already-running UU server, disconnect UU and restart the UU service or Windows once. This avoids claiming an invented prior capture inventory.

Result: PASS for this regression on the tested machine; community preview remains appropriate.