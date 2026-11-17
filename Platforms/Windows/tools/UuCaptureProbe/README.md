# UU capture lifecycle diagnostic (Windows)

This standalone diagnostic is not part of Snug's runtime or installer. It uses Microsoft.Diagnostics.Tracing.TraceEvent 4.0.0 and two Windows ETW manifest providers:
- Microsoft-Windows-Graphics-Capture-Server: event IDs 1 (CAPTURE_STARTED), 2 (CAPTURE_STOPPED); target process ID from the manifest payload.
- Microsoft-Windows-DxgKrnl: event IDs 1073 (OutputDuplicationCreate), 1078 (OutputDuplicationDestroy); require matching server event header PID. Context pointers only map to temporary ordinals. VidPnSourceId is adapter-relative, not yet a validated screen identity.

Build from this directory with the repository .NET SDK: dotnet build -c Release. Run the compiled DLL in a normal administrator-approved terminal: dotnet UuCaptureProbe.dll 120 <new-output.jsonl>. It rejects ordinary non-admin execution and any existing output path. Duration is bounded to 5–180 seconds. It uses event-ID allowlists, no ETL recording, no capture frames or input logs, no UU process modification. Only exact known GameViewerServer generation events are written; unmatched events are counted without details. Stop() ends the trace session at timeout even with zero events.

Controlled experiment: first establish explicitly disconnected baseline; begin the diagnostic; note timestamps of mobile connection, switching from physical display to Snug phone display and back, then disconnection; compare emitted event sequences with timestamps and observe quiet idle interval. Repeat with desktop controller before claiming device distinction. A zero-event interval alone does not mean disconnected. Missing starts, stops, incorrect process attribution, event loss or server restart block acceptance. A start event is capture evidence, not direct remote session or mobile device proof. No production policy changes until validation.

The existing .slog files contained binary-like data; do not decode protected logs, read credentials, or use undocumented UU APIs. Existing read-only log compatibility behavior remains separate.
Second-pass diagnostics add IDs 192/193 (separate diagnostic context-index namespace), IDs 1074/1075 (aggregate count of operations on previously attributed capture contexts), adapter-handle ordinals, and pointer-correlated destroy candidates. Header attribution is classified as Server, Kernel, or OtherProcessCandidate; the latter is not confirmed ownership. No raw pointers, frames, input or foreign process identity are emitted. Counters of all observed event IDs help distinguish missing events from an attribution gap. Adapter ordinal + source ID is a probe-local fingerprint, not a proven physical monitor name. Capture activity is not proof of a live remote session in an idle or cached-capture scenario.
### Validated 192/193 lifecycle replay

The second real iPhone experiment produced three direct server diagnostic connect/destroy pairs. For this exact controlled experiment, with a user-confirmed empty initial inventory, run:

    dotnet UuCaptureProbe.dll --replay-disconnected <completed-second-report.jsonl>

It checks zero missing/failed/unattributed events, one stable server generation, paired contexts, two switch rebuilds, final teardown, and the existing connection tracker's expiry even with sticky log hints. Offline replay does not modify the UI or require elevated privileges. The state machine treats late attachment or an unhealthy trace as unknown; it does not treat silence as disconnect. The current installed application does not consume this event source yet.