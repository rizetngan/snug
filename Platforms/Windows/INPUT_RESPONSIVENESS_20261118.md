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