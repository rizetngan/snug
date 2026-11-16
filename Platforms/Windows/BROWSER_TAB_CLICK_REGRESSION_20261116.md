# Browser tab click regression and fix (Windows 0.2.21)

## Symptom and actual reproduction
An iPhone 13 Pro + UU user reported that tapping Chrome browser tabs did not enter/switch them.
We reproduced the Windows-side input failure using an isolated Chrome profile and two local HTML tabs, SNUG_TAB_TEST_A and SNUG_TAB_TEST_B (not the user's regular Chrome profile).

0.2.19 candidate runtime (PID 13392):
- Snug catalog native injected click opened isolated Chrome, moved window onto the real Snug VDD.
- Browser window on VDD: x=1920,y=0,width=820,height=1349 (DPI-virtualized coordinates).
- UI Automation recognized both real TabItem hit areas at top y=0..41.
- Ordinary injected Windows mouse press/release (dwExtraInfo=0) at tab B failed to change selection; selected tab stayed A.
- The identical click bypassing Snug's injection filter via its own 0x534E5547 marker switched A -> B -> A. Therefore the target tab was not covered/clipped; the UU click translation path swallowed browser chrome input.

## Fix
- Added BrowserChromeHitTest to reserve the native browser tab/address/header area for unmodified mouse press/move/release while placed on Snug Phone.
- Bypass is fixed per press sequence through release; webpage content outside header still routes through Snug's touch translation.
- Supported browser process names include chrome, msedge, firefox, brave, vivaldi, opera and arc.
- The prior 0.2.20 tap-slop adjustment follows upstream TouchRegion.scale for higher-resolution phone modes.
- Files: BrowserChromeHitTest.cs, MouseGestureHook.cs, NativeMethods.cs, TouchTranslator.cs, AppController.cs.

## Confirmed after fix
0.2.21 candidate (PID 29276) tested against the same isolated Chrome fixture (PID 28904).
- Native click in Snug catalog -> Chrome moved to phone display: PASS.
- Ordinary injected Windows mouse click on tab B -> selected index 1: PASS.
- Ordinary injected Windows mouse click on tab A -> selected index 0: PASS.
- Test window restored to host display and closed: PASS.
- Unit tests 136/136 PASS. Release build 0 errors, 0 warnings.
- Git fix commit: 26a89a0 (predecessor tap-slop fix ac7b690).
- Installer: F:\snug for Windows\artifacts\Snug-Setup-0.2.21-x64.exe.
- Installer SHA256: 94EC352FBDB5C3635E2515182A9C2214143FE27E4BB3FC5D3DA99E79CBFBD8A7.

## Remaining
- iPhone 13 Pro UU actual finger retest is pending.
- A non-admin portable 0.2.21 candidate is running for live testing.
- The Windows installed registry still reports 0.2.18; installer needs normal administrator UAC approval before persistent deployment.
- Other browsers were covered by pure hit-test cases but were not end-to-end clicked on hardware in this test.