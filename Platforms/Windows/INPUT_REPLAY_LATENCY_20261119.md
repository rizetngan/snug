# Windows 0.2.41 input latency candidate — 2026-11-19

## Phone measurement: installed 0.2.40
User observed UI selection highlighting quickly but delayed file opening in GPT, even after input-thread isolation. With the user's iPhone connected to Snug Phone, a scoped, read-only mouse-button observer captured six taps; it waited on CallNextHookEx to measure the downstream hook chain. Only event timing and replay marker were recorded, with no coordinates or user content.

| Tap | Original down to up (ms) | Downstream release duration (ms) |
| --- | ---: | ---: |
| 1 | 81.042 | 1008.386 |
| 2 | 66.016 | 1009.434 |
| 3 | 47.329 | 1014.427 |
| 4 | 77.242 | 1015.086 |
| 5 | 75.355 | 1003.664 |
| 6 | 78.384 | 1002.214 |

All six originals had down return=1, up return=0 after ~1000ms, followed by a marked replay down and up. This is consistent with the Windows low-level hook timeout and reentrancy during injected mouse input. The observer itself adds a hook, so this is a measured chain delay under observation, not an exact uninstrumented end-to-end phone result.

Microsoft references:
- https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput

## Changes
- Remove synchronous SetCursorPos and mouse_event calls from WH_MOUSE_LL callback; enqueue replay actions on a dedicated, ordered MouseReplayLane instead.
- Submit atomic left/right click sequences using SendInput with physical virtual-desktop coordinates; preserve the existing replay marker.
- Queue all selection move/release events with the initial synthetic press, preserving order. Region change and stop invalidate the generation; release held synthetic buttons.
- No change to virtual display modes, device detection, keyboard viewport fitting, or gesture thresholds.

## Real Windows smoke
Tests/InputHookSmoke now includes --replay: a separate isolated example window and a temporary scoped hook/observer chain. It measures original release chain time, replay release arrival and the example window receiving the release. This is not a phone or streaming benchmark.

Second candidate run (milliseconds):

| Tap | Chain | Replay | Window |
| --- | ---: | ---: | ---: |
| 1 | 2.67 | 33.48 | 48.32 |
| 2 | 0.22 | 1.24 | 2.71 |
| 3 | 0.25 | 2.86 | 5.15 |
| 4 | 0.32 | 4.46 | 8.29 |
| 5 | 0.07 | 1.09 | 2.28 |
| 6 | 0.08 | 1.13 | 2.24 |

Mean chain 0.602ms; six exact releases, original release return=1 each time. Previous candidate run also passed (chain 0.10–2.07ms). Double click, right click, selection release, scroll, cancel held selection PASS. Hook lifecycle 20 restart cycles and independent input worker PASS. Package suite: 283/283 core tests.

## Pending
- Install and repeat the same GPT taps on the phone under the observer; validate no ~1000ms timeout and real interaction improvement.
- Verify phone scroll/long press/close/return and window restoration. Keyboard first-open viewport pan remains an independent issue; see INPUT_RESPONSIVENESS_20261118.md.
- Public release and main branch unchanged until phone acceptance.
## Candidate artifact
Installer: 58,980,920 bytes; SHA256 1694A90FE31F0AB165E48C856C63246AE921066AEC7498DE677878E089C27295. Built before the source commit. Local only.

## Installed candidate acceptance
- Installed exact local 0.2.41 package in user session; setup exit 0, no reboot. Verified installed assembly version and input gestures enabled.
- User explicitly reported on 2026-11-19: "延迟问题解决了" (latency issue resolved). Mark subjective mobile tap latency ACCEPTED. The 041 observer expired before the user's session, so no new measured phone-side latency figure is available. Do not present the isolated smoke measurements as iPhone results.
- Post-test check: primary display only, original placement ledger absent; current session reports no placed window/virtual screen.
- Keep a3e0487 (and this acceptance record) as the input-fix baseline for keyboard viewport investigation. Do not regress serial replay or reintroduce injection within WH_MOUSE_LL.
- Outstanding: keyboard first-open viewport panning and the broader mobile gesture regression matrix. No public release performed as part of this acceptance record.
