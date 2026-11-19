# 0.2.42 — Keep the phone display within one UU episode

- Baseline: public v0.2.41 (bdaa9e0), accepted input replay latency fix.
- Remove episode-dismiss state, exit confirmation overlay, primary monitor re-entry button, and obsolete re-entry probe. The existing App Library Restore Window action is the sole explicit window-return action.
- Preserve UuConnectionTracker, ETW helper, VDD driver and mouse replay code. The existing tracker + user setting decide display lifetime; normal UU monitor switching never calls a display teardown. Real disconnect, disable and normal quit retain existing teardown behavior.
- Explicit window restore is serialized on PhoneActionLane, calls the existing PutBackCoreAsync, refreshes the same catalog without requesting a display mode change, and emits RestoreManagedWindow audit records with display identity/bounds. Existing restore ledger behavior remains.
- Core 282/282 tests pass (obsolete re-entry tests replaced by display-lifetime tests); Release app build 0 warnings/errors. PhoneUiSmoke passed including old exit controls absent and preview home/all/search/model/confirmation. Candidate screenshot inspected; only phone model button remains in live header, preview close control only appears in preview.
- Installer packaged successfully. No new public release; actual iPhone UU monitor switching, explicit restore and disconnect require acceptance.
- Installer: 58986059 bytes; SHA-256 32DD61B892CD2653F7BFE179F13CC862EA0D5189C6EED10588E13B3A93473F7C.
