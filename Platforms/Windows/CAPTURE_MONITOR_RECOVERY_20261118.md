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