# Snug Windows Platform — current upstream source parity

Source: https://github.com/zephyr4123/snug
Reviewed source commit: c9f46d94ed3f6d46b1132f07f3df9e2bdb969fb5

| Original upstream file | Windows port | Verified state |
| --- | --- | --- |
| Sources/SnugCore/PhoneScreen/ScreenFit.swift | Snug.Windows.Core/ScreenFit.cs and PhoneCanvasLayout.cs | 540-point design and UU inset calculation ported |
| Sources/SnugSystem/PhoneDisplayService.swift | VddDriverClient and PhoneDisplayModeSwitcher | Stable one-mode Windows VDD; upstream live multi-mode reshape not yet supported |
| Sources/Snug/PhoneScreen/PhoneScreenWindows.swift | PhoneSurfaceWindow and HomeBarWindow | Dedicated phone panel, model selection, search, new exit confirmation |
| Sources/SnugCore/PhoneScreen/PhoneDesk.swift | AppController, PhoneSessionPresentation, PhoneActionLane | Session dismissal, window restore and serialized operations partially ported |
| Sources/SnugCore/Session/RemoteSession.swift | UuConnectionTracker, UuSessionDetector | Session tracking present; UU capture target evidence not yet parsed on Windows |
| Sources/SnugSystem/TouchTap.swift | MouseGestureHook | Windows event translation exists; real-iPhone gesture parity still unverified |

## Observed upstream constraints

- README.md and docs/how-it-works.md say the user must manually select the Snug Phone display in the UU screen list.
- Original upstream commits 74020c7 and 0ec5e1e record that UU may begin on the main display during a new remote session. Original Snug does not force its phone screen to become the host primary monitor.
- Upstream maintains monitor identity when changing phone models via CGVirtualDisplay reshape. The present Windows VDD cannot refresh the mode table in-place without a device reset, so the port defers model changes until a new remote episode.
- On Windows 10, the validated physical 1080x1834 mode appears through Screen.AllScreens as logical 720x1223. Validate raw monitor modes using EnumDisplaySettings, separate from the WPF canvas size.
- The prior 55-mode Windows VDD configuration produced no active SNG00F0 monitor; single-mode recovered it on actual hardware. Preserve that compatibility fix.

## Version 0.2.14 acceptance

- New session-scoped phone-screen dismissal button/confirmation corresponds to latest PhoneDesk.dismissScreen.
- Avoid VDD recreation on live model changes.
- Native raw-mode validation in place of physical-vs-logical rectangle comparisons.
- Core 121/121 pass, Release 0 errors and 0 warnings; preview UI flow pass.
- Unresolved: actual iPhone UU exclusively capturing display 2, real touch/gesture parity, automatic on-line shape changes, all upstream window behaviors. Do not mark these complete based on WPF preview alone.

## 0.2.15 — UI fidelity aligned to the upstream SwiftUI source

Compared source against the upstream GitHub README live phone screenshot and:
- Sources/Snug/PhoneScreen/PhoneHomeView.swift
- Sources/Snug/PhoneScreen/PhoneAppCard.swift
- Sources/Snug/PhoneScreen/PhoneWidgets.swift
- Sources/Snug/PhoneScreen/PhoneScreenWindows.swift
- Sources/SnugCore/PhoneScreen/ScreenFit.swift

Adapted running app card to the upstream 48pt icons, 56pt round action targets,
native-like macwindow badge/close glyphs, 18/22pt labels and white grouped cards.
Moved app quit and window close confirmation from a blocking global modal into the
selected card/row, with mutually exclusive inline state and explicit cancellation.
Phone-display dismissal now confirms in the header, as upstream PhoneHomeView does,
instead of masking the entire screen. Debug preview exercises dismissal/cancellation
without modifying the actual display. Text color and contrast verified by screenshots.
WPF UI automation smoke covers home, all apps, search, inline confirmation,
inline dismiss, model selection and return. Core 121/121 PASS.

Phone geometry remains ScreenFit-derived for iPhone 13 Pro:
1170x2532px, 390pt logical width -> ratio 1.6974 -> 540x917pt -> 1080x1834px.
The 91pt UU inset on each side is intentional upstream behavior, not WPF padding.
Actual UU remote-injected touches and on-device window placement still require
independent real-device testing; UI fidelity is not the same as full PhoneDesk parity.
