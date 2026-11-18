# Windows changelog

## 0.2.38 — multi-signal UU session detection

- Add visible GameViewerServer overlay detection.
- Add PID-scoped established peer TCP detection; ignore loopback and persistent HTTP/HTTPS ports 80/443.
- Keep UIA controller-table evidence, SYSTEM ETW capture inventory and bounded streamer-log growth as independent signals.
- Preserve controller identity as Unknown when the UIA table cannot identify a device; peer TCP indicates a remote session, not a phone.
- Complete inactive ETW inventory suppresses stale log fallback but does not erase independent peer/controller evidence.
- Validation: 268 core tests pass; full Release solution build passes with 0 warnings and 0 errors.
- Live P/Invoke check on the current UU server decoded one established TCP peer on port 443; no non-web peer was present in that sample.

## 0.2.37 — activity-based UU log fallback

- Replace lifetime file-lock detection with PID-scoped streamer-log growth tracking.
- Require two append-growth observations within 3 seconds; expire activity 2 seconds after the last growth.
- Reset on log rotation, truncation, server ambiguity, unreadable log state and clock regression.
- Keep complete SYSTEM ETW capture inventory authoritative; log growth remains a low-confidence compatibility hint.
- Preserve the existing phone-screen re-entry flow and all upstream 2.5.0 features.
- Validation: 259 core tests pass; full Release solution build passes with 0 warnings and 0 errors.

## 0.2.36

- Add an explicit same-session phone-screen re-entry control after a user dismisses the phone screen.