# Windows changelog

## 0.2.41 — deferred mouse replay

- Fix a measured ~1-second low-level mouse-hook stall when replaying taps.
- Deliver replayed mouse input on a separate serial worker, with each click submitted as a single SendInput batch. Keep gesture classification on the existing input thread.
- Preserve double-click, long-press right-click, selection, scrolling, and release a synthetic held button on session changes.
- 283 core tests pass; native Windows replay, double-click, long-press, selection, scroll and cancellation smoke tests pass. The maintainer verified the phone tap-latency fix and disconnect restoration on a real iPhone. Repeated keyboard opening can still fail to pan in UU itself, including on a normal Windows screen; no Snug-side workaround is introduced.

## 0.2.40 — input latency candidate

- Move the global mouse hook and inertial scrolling to a dedicated input dispatcher, independent of UI work.
- Keep native Snug controls and browser chrome on the original event path; avoid process-name lookup for content taps.
- Coalesce redundant phone-library refreshes. Match upstream's nonactivating phone panel outside search and model selection.
- Candidate: actual iPhone tap latency, drag, native close, search focus, app switching, keyboard occlusion and disconnect regression still require verification. iOS/UU keyboard viewport fitting is not implemented by this patch.

## 0.2.39 — capture monitor recovery

- Fix persistent ETW monitor session ownership: use a stable service-specific trace name instead of allocating a fresh random name every launch.
- Reclaim orphaned GUID-named legacy Snug monitor sessions on startup; never stop temporary pilot/probe or unrelated ETW sessions.
- Prevent concurrent monitor service launches and record provider initialization in the monitor log.
- Passed startup and active iPhone + UU session regression (276 automated tests). Reclaimed six legacy traces; verified browser fitting, exit/re-entry and disconnect restore. Restart UU service or Windows once if the server predates the upgraded monitor and capture detection is unavailable. No changes to disconnect evidence or existing restoration policy.


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