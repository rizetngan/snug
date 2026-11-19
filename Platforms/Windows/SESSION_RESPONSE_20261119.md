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
