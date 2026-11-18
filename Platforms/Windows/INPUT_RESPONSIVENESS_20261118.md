# 0.2.40 candidate — input responsiveness and keyboard focus

User observed high tap feedback delay and worse phone-keyboard display adaptation than upstream.

## Inspected baseline
- Upstream Sources/SnugSystem/TouchTap.swift: dedicated userInteractive thread and run loop for event tap and inertia.
- Upstream Sources/Snug/PhoneScreen/PhoneScreenWindows.swift: nonactivating panels accept first mouse; keyboard activation only during search/model selection.
- Windows 0.2.39: WH_MOUSE_LL installed on WPF dispatcher; coast uses same dispatcher. OnTick also scans windows and displays; repeated collection notices queue redundant rebuilds. Native browser chrome lookup created process wrappers even for content taps. These are plausible latency contributors, not measured end-to-end phone latency.
- The examined upstream files do not establish a signal for actual iPhone keyboard height. Windows virtual-display captures cannot show the UU client keyboard overlay or viewport transform.

## Candidate changes
- Dedicated input dispatcher and thread (AboveNormal), immutable region snapshot, volatile placed-window reference; native close callback dispatched asynchronously to UI. Clear pending gestures and momentum on stop; reuse worker until dispose.
- Native Snug controls and browser chrome pass original mouse events. Skip process-name lookup outside header; retain half-second long-press and existing scroll/close policy.
- Coalesce catalog updates at Background priority.
- WM_MOUSEACTIVATE returns MA_NOACTIVATE for real phone library outside search/model overlay, preserving keyboard focus instead of taking it on ordinary clicks. Explicit search/model activation retained.
- No changes to UU capture/session lifecycle, driver, window sizing or restore policy. No guessed keyboard inset or automatic display shrinking.

## Verification
- Release package compiled, 283/283 core tests passed (7 added header bounds cases).
- Isolated Windows hook smoke: real hook start, stop and restart 20 times; worker distinct from caller, AboveNormal, executes queued action during deliberately blocked caller/UI (400ms). Reported queued_ms=0 for this test only, not phone touch latency. No region assigned, no mouse injection. The first smoke apphost hit known Windows 10 CET startup issue; running the DLL via existing dotnet host passed. Reproducible test source: tests/InputHookSmoke; run using the .NET host after packaging.
- Installer size 58,974,390 bytes, SHA256 DBBE2ED6345C85AA3B4009F19396813048336BE9B09AE00CE847253D11D42C2A.
- Built before commit, local candidate only. Installed 0.2.39 and public Release not changed.

## Remaining acceptance
1. Measure tap/repeated tap response in ChatGPT, Chrome and App Library; compare against UU original mode and 0.2.39 on the same network.
2. Verify swipe, momentum stop, horizontal scroll, long press, native close, return bar, search keyboard/focus, app switch and restoration on disconnect.
3. Capture actual iPhone UU client with keyboard visible and hidden; determine whether stream viewport shrinks, keyboard covers composer, or focus/scroll is wrong. Compare to upstream behavior; then implement evidence-based keyboard fitting if host-side signal exists. Do not resize/remove virtual display blindly on text focus.

Candidate not ready for public release.
## Keyboard viewport follow-up — 2026-11-18

User supplied paired iPhone screenshots: input-method keyboard opens with desktop top visible and composer/return bar occluded; switching to computer keyboard and back to input method pans the same-scale stream upward, exposing composer and return bar. Images include private work; do not publish them.
- Installed 0.2.39, active UU capture, DISPLAY49; user reproduced while Windows diagnostic sampled foreground/focus/caret and window rectangles. ChatGPT foreground observed in 112 samples with the same HWND and focus HWND, no native caret exposed. Window fitting at the beginning changed the app bounds; after fitting the reported app bounds were stable while GPT was foreground. Screen.AllScreens in the persistent probe cached its original geometry, so it is not evidence of unchanged display mode. Fresh enumeration after reproduction showed the same display ID at 820 x 1392 logical pixels (1640 x 2784 physical).
- A/B: disabled Snug touch gesture conversion through its settings, confirmed GesturesEnabled=False; user repeated direct keyboard opening and explicitly confirmed the issue persisted. Restored GesturesEnabled=True and confirmed the setting.
- Upstream source inspected: PhoneScreenWindows.swift covers nonactivating panels and search/model keyboard focus, not iPhone keyboard viewport insets or pan. UU public integration guide https://github.com/netease-uuyc/uuremote-integration-guide documents device binding and connect with full-screen/PiP, no documented control of in-session keyboard or viewport pan.
- Intermediate conclusion: gesture replay is not required to reproduce the problem. Client viewport refresh is a leading hypothesis, not a confirmed UU-only bug. A native UU screen without Snug involvement is the next discriminating comparison, ideally the same GPT application and a comparable portrait display. If both reproduce, report to UU; if only Snug reproduces, inspect display geometry and client coordination before touching display-fit logic.
- Do not introduce speculative resize, forced refocus, synthetic keystrokes, or permanent gesture disable. Continue latency-candidate regression independently. No 0.2.40 keyboard viewport fix has been implemented.