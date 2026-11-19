# Keyboard viewport repeat-open — 2026-11-19

Baseline: installed 0.2.41, accepted latency fix. Do not change its mouse replay path.

## Reproduction
User reports that clicking an input field then showing the UU iOS input-method keyboard correctly pans the remote image to reveal the field. Hiding the keyboard and reopening it directly, without clicking the remote input again, fails to pan. A new click on the input then opening the keyboard works again. Switching to computer keyboard and back also restored pan in the earlier paired screenshots. The symptom is inconsistent with a fixed display resolution mismatch.

## Native field control
Independent Windows Forms test window: Snug Keyboard Test, two standard native Edit controls and alignment marks, no private conversation or recorded text. Script and images are local under diagnostics\keyboard-viewport.
Host observation on DISPLAY18:
- 02:17:14Z: window [1920,0,3000,1779], monitor [1920,0,3000,1834], DPI 144.
- 02:19:57Z: middle control focused, visible native caret [1959,406], HWND 134872.
- 02:19:58Z: bottom control focused, visible native caret [1959,1415], HWND 134864.
- Until focus left the window at 02:20:19Z, no changes to the window or monitor rectangle, caret or focus were logged. These are change-only samples, not a synchronized recording of the iOS keyboard state. Do not claim the entire sequence was timed against the keyboard hide/show.

## Interpretation and next discriminating test
Leading hypothesis: UU remembers a recent touch/cursor/focus position to pan on keyboard show; keyboard dismissal clears or fails to reuse that anchor. Clicking the same input may refresh the anchor without any change in actual Win32 focus. Need compare the same standard input on the normal Windows screen with Snug phone screen not in use. No documented API for in-session viewport control in UU public integration guide. Do not force resize, refocus/click, synthesize key input, or poll private chat content to compensate.
If normal UU screen reproduces, send UU a minimal bug report and ask whether keyboard re-show uses stale or cleared touch anchor; if only Snug screen reproduces, inspect portrait capture geometry/coordinate mapping against UU native virtual screen before modifying Snug. No fix claimed.

Acceptance: five consecutive show/hide cycles on the same focused input, with no additional remote click or keyboard mode switch; reveal composer/native Edit on every show, restore full viewport on hide. Preserve 0.2.41 latency and window restoration.
## Native UU screen comparison — user confirmed
User explicitly confirms that the same keyboard re-open problem occurs on the normal computer screen in UU remote, without using the Snug phone display. This is sufficient to classify the defect as reproducible independently of Snug Phone. The exact UU internal anchoring/reset implementation remains unverified. Refer the repeat-open behavior to UU support; do not add speculative Snug-side resize, focus, or input-injection workarounds. Retain 0.2.41 click latency fix. Private screenshots and diagnostic data stay local.
