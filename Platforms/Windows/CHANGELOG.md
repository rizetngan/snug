# Windows changelog

## 0.2.38 — final UU session evidence and acceptance

- Align Windows UU session presence with the upstream model: visible overlay, controller-table evidence, active streamer/capture evidence and SYSTEM ETW capture inventory are authoritative signals.
- Treat remote peer TCP as diagnostic context only. Persistent 80/443 connections do not establish or extend a UU session.
- Preserve controller identity as Unknown when the local UIA table cannot identify a device; do not infer phone/device identity from network state.
- 268 core tests pass; Release solution build passes with 0 warnings and 0 errors.
- Real iPhone + UU Remote acceptance passed: phone-screen creation, >60-second stability, same-session phone-screen re-entry after Exit Phone Screen, and UU-disconnect window restoration / virtual-display cleanup.
- Installer baseline: Snug-Setup-0.2.38-x64.exe; SHA-256 5E803AD6E11676D609B5B4A9A1B8F5E391E536C254564408B115F86A7C132D96.
## 0.2.37 — activity-based UU log fallback

- Replace lifetime file-lock detection with PID-scoped streamer-log growth tracking.
- Require two append-growth observations within 3 seconds; expire activity 2 seconds after the last growth.
- Reset on log rotation, truncation, server ambiguity, unreadable log state and clock regression.
- Keep complete SYSTEM ETW capture inventory authoritative; log growth remains a low-confidence compatibility hint.
- Preserve the existing phone-screen re-entry flow and all upstream 2.5.0 features.
- Validation: 259 core tests pass; full Release solution build passes with 0 warnings and 0 errors.

## 0.2.36

- Add an explicit same-session phone-screen re-entry control after a user dismisses the phone screen.