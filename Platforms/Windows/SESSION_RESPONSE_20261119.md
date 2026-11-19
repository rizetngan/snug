# Windows 0.2.43 — connection and disconnect responsiveness

Baseline: accepted local 0.2.42 (619ff2c); public release remains 0.2.41.

## Baseline measurements
Two actual 0.2.42 episodes: Connected-to-ready 6306/5989 ms; start-to-ready 4289/3954 ms; Interrupted-to-Ended 15122/15088 ms; Interrupted-to-display-stop 16956/17050 ms. Host timestamps exclude unknown time before the first signal or user action. Explicit restore verified sameDisplay/sameBounds.

## Candidate
- ReconnectWindow 15s -> 10s, including departed phone/controller grace. Existing 500ms polling, 1.5s debounce, 5s quiet guard, 2s unknown-controller grace, 3s ETW capture rebuild grace unchanged.
- Skip cold PING before requesting the existing signed-driver start task only if Snug-owned display is absent. Existing monitor still requires PING. Task already validates ownership and executes NefCon with --no-duplicates. Post-start PING and monitor/geometry checks unchanged. Audit VddStartTiming tracks request/ready.
- No changes to input replay, driver payload or 0.2.42 persistent-display/restore UI.
- Exact new boundary tests; original acceptance test updated to new intended 10s timing. Initial run had one failure due to old 15s expectation; corrected run 285/285 passing, app build zero errors/warnings and package successful.
- Installer 58986888 bytes, SHA256 F2E9098F1C3BB6B7A2E5D4AE6506F63E6FCDAC108349D975361129F7985D288E. Actual candidate timings and reconnect safety require mobile acceptance; no public release.


## 0.2.43 installed acceptance
User confirmed stable. One actual episode: Connected 03:20:33.124Z, ready 03:20:37.946Z (4822ms); start-to-ready 2778ms; cold driver request-to-ready 1110ms. Interrupted 03:20:49.457Z, Ended 03:20:59.534Z (10077ms), display-stop 03:21:01.423Z (11966ms). No unexpected Interrupted/Ended before the final disconnect. This sample does not establish the minimum safe reconnect window.

## 0.2.44 candidate
- Full-session ReconnectWindow 10s -> 6s. DepartedControllerGrace stays 10s while another controller remains. No other timing or driver change.
- 287/287 tests pass, including exact boundary, repeated interrupted unknown-controller sessions, and late reconnect. Build/package succeeded.
- Package 58983831 bytes, SHA256 52D4A0685A71DAF1C9B6CE8D3D54CA65836A1E63C94904732D8745A881F43619. Real-phone validation pending. 0.2.43 package remains the rollback baseline.

## 0.2.44 measured acceptance
One complete recorded episode: Connected 04:51:46.634Z, ready 04:51:51.953Z (5319ms); start-to-ready 3302ms; driver 1511ms; post-driver 1774ms. Interrupted 04:52:41.616Z, Ended 04:52:47.660Z (6044ms), display-stop 04:52:49.561Z (7946ms). No unexpected teardown. Capture inventory was incomplete, transitioning Active to Unknown, not Inactive. A separate Resumed event was not observed.

## 0.2.45 design
Keep 6s fallback; 4s accelerated expiry only on continuously verified inactive complete inventory and no presence, after the normal debounce and quiet window. Current UU server predates monitor start; its incomplete inventory cannot authorize acceleration. No claim of an explicit UU disconnect event. Reuse proven startup driver readiness; preserve display checks and add topology-ready/mode-start timestamps. Actual results pending.

- 0.2.45 automated result: 294/294 core tests passed; application and capture monitor builds 0 warnings/errors. Setup compiled (58974041 bytes, SHA256 A75538BA4F52F1AAA8AC7BAB05BB5A7DFF155EAA38DC6D58C7C14D76CC575168). 0.2.44 remains installed. Candidate not released. No new real-phone timing asserted.
