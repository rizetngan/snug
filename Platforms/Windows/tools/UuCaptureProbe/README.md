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
### Bounded live integration pilot

The real Snug UuSessionDetector can consume fresh capture snapshots through a
one-way local named pipe, enabled only by the process-scoped
SNUG_UU_CAPTURE_PILOT GUID environment variable. Normal startup still uses the
existing production compatibility mode. Build the probe and Snug candidate
from the same source revision before testing.

- Helper: dotnet UuCaptureProbe.dll --pilot <new-guid> <local-user-SID> <5..300 seconds>
- An explicit --pilot-disconnected mode is for a supervised experiment whose
  operator has confirmed that all incoming UU connections are disconnected
  before enabling tracing. Do not use it to guess the baseline.
- Listener subscribes only to directly server-attributed DxgKrnl 192/193.
  It uses a single server generation and closes after the time limit or client
  exit, including an eventless trace. No ETL recordings, service install,
  auto-launch, or persistent system permissions changes.
- The unprivileged UI uses a 64-byte versioned snapshot with unique feed ID,
  monotonic sequence, three-second freshness, and matching process generation.
  The helper exposes an outbound-only local pipe, with read permission for
  the explicitly identified local user and remote/network access denied.
  Neither side accepts commands to perform privileged actions.
- Std-user process handles are denied for GameViewerServer on this machine.
  UuServerIdentity uses Win32_Process creation date via the documented WMI
  provider when needed. Both sides normalize creation times to microseconds.
- Unknown startup state, lost events, failed process identity, unreadable WMI,
  malformed messages or expired heartbeat preserve the existing compatibility
  fallback. A fully observed inactive inventory overrides sticky log handles.
  Do not infer controller type or physical monitor from capture events.
- A timed pilot is not an unattended release: when it ends, the old log
  compatibility policy returns and can recreate the phone display. For live
  display acceptance, verify the final normal exit/restore and resume the
  installed version or explicitly disable phone screen before expiry.
- tests/UuCaptureBridgeProbe verifies the actual UuSessionDetector using a
  synthetic local pipe in a separate process (no AppController or display
  changes). Its --transport <guid> mode checks real cross-elevation heartbeat
  transport against the currently running server generation.