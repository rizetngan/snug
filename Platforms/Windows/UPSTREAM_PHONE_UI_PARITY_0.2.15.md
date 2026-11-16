# Snug for Windows 0.2.15 — Phone UI source parity

Upstream source baseline: zephyr4123/snug main at c9f46d94ed3f6d46b1132f07f3df9e2bdb969fb5.

Inspected original SwiftUI modules:
- Sources/Snug/PhoneScreen/PhoneHomeView.swift: home, current app, sticky tabs, banner confirmation, session navigation.
- Sources/Snug/PhoneScreen/PhoneAppCard.swift: AppCard, WindowRow, onPhone tag, PlacedControls.
- Sources/Snug/PhoneScreen/PhoneWidgets.swift: RoundButton, ConfirmButtons, AppRowLabel.
- Sources/Snug/PhoneScreen/PhoneStyle.swift: 540-point design, system-grouped background, card, text hierarchy.

Windows implementation:
- PhoneSurfaceWindow.xaml uses 540-point canvas scaled uniformly to the native virtual monitor (never resize phone contents independently).
- Rounded icon buttons with 56-point hit targets replace generic + and X text.
- App quit and window close confirmations appear inline on the selected row instead of opening a full-screen modal. At most one confirmation exists at a time.
- Header exit phone-screen confirmation is inline, preserving the catalog and ability to cancel.
- The currently placed window displays blue "在手机屏上" tag and restore control. Zoomable apps display zoom buttons. The actions delegate to existing AppController.PutBackAsync and AdjustCurrentAppZoom.
- Search, model selection, running/all tabs and app cards use the original module structure.
- Readable text Foreground applied explicitly to WPF TextBlocks inside Button content to prevent Windows theme inheritance from washing out text.

Remaining differences: SwiftUI SF Symbols are redrawn as small XAML vector icons; these are functionally equivalent but not exact Apple symbols. Windows UIA and Win32 window APIs differ from AppKit. Runtime search/icon catalog and gesture controller behavior are not yet exact upstream parity. Automatic UU selection of the virtual display remains unsupported by upstream and by this Windows port; user must select Snug Phone in UU. Real iPhone touch and full window placement are pending separate end-to-end acceptance.

Release gate: Release build, core tests, UI smoke screenshots (home/all/search/inline quit/phone model/dismiss). Live Windows driver monitor presence and UU display choice must also be checked after installing.
