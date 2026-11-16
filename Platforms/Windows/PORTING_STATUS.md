# Snug Windows Port — Current Status

## Baseline and isolation

- Upstream baseline: Snug 2.4.0, commit a4cb611.
- Local branch: feat/windows-platform-support.
- Windows work is isolated under Platforms/Windows; upstream macOS sources are unchanged.
- No push, pull request, or upstream merge has been performed.

## Implemented

### Windows runtime

- WPF desktop shell, Windows virtual display driver integration, display detection, window move/restore ledger, zoom controls, UU session detection, and gesture bridge are carried forward from the existing Windows prototype.
- The phone-screen app library is now a real Windows UI rather than a static list of window handles.
- Installed apps are enumerated from per-user and all-user Start Menu shortcuts plus shell:AppsFolder.
- App identity uses normalized executable paths or packaged-app AUMIDs; duplicate shortcuts are collapsed.
- Shell icons are loaded where available; missing icons fall back to the app's first letter.
- Running windows are grouped by process and ordered by z-order. Empty-title system windows are excluded.
- Search supports Chinese display names, English names, pinyin full spelling, initials, and running window titles.
- App launch, focus/pull, Ctrl+N new-window request, individual window close, and graceful app quit are wired.
- A placed window is restored before its own close/quit action. No force-kill is issued automatically.
- --catalog-preview starts an isolated phone-surface preview without starting or changing the virtual display driver.
- --settings-preview opens the real settings and phone-model picker UI without starting or changing the virtual display driver.

### Shared phone-screen core

- PhoneCatalog, PhoneChoice, PhoneScreen, and ScreenFit have been ported to Windows Core.
- The original Assets/phone-models.json remains the single model-data source (17 brands, 500+ model entries).
- The default iPhone 17 Pro Max profile reproduces the existing 540 × 950 logical / 1080 × 1900 pixel viewport.
- Chinese/English/pinyin search, app ordering, stable refresh, action selection, phone catalog validation, and screen-fit calculations have automated tests.
- VddResolutionProfile generates the 55 VDD modes for the standard iPhone 17 Pro Max profile. The driver script applies the selected profile before creating the VDD display; Windows now requests modes dynamically based on the App's actual minimum window size, with the base mode restored for the App list.

## Verification

- Release tests: 109 passed, 0 failed (iPhone profiles, NewWindowMenu, dialog detection, serial lifecycle actions).
- Release build: 0 warnings, 0 errors; output isolated from the running preview binaries.
- PowerShell driver script parses without syntax errors.
- UI automation opened the Windows phone-model picker and verified live search: visible model actions narrowed from 89 to 7 for "iPhone 17", and to 13 for "Pixel 9". Cancel was exercised without changing the saved model.
- Settings preview mode opens the real Windows settings UI without starting or modifying the virtual display driver.

## Windows phone-model integration

- The settings window now opens a searchable phone-model picker backed by the original 17-brand, 500+ model catalog.
- Custom width/height input is supported and normalized to portrait orientation.
- Model ID and screen geometry persist in Snug user settings and are restored at next launch.
- Selecting a model during an active phone session requests a safe session restart; an app window that cannot be restored is not forcibly moved or closed.
- The selected phone profile is written to `%ProgramData%\\Snug\\phone-profile.json`. The installer creates this file and grants modify access to authenticated users on that file only.
- The SYSTEM display tasks now invoke the installed Snug driver script, which validates the profile and updates only the VDD resolution list before creating the display. Existing settings are backed up only when the mode list actually changes.

### Dynamic Windows display path

- `PhoneDisplayModeSwitcher` changes only a detected non-primary Snug Phone monitor, using modes that Windows explicitly advertises for it. It runs a `CDS_TEST` preflight and does not persist a display-mode change to the Windows registry.
- `AppController` starts the phone screen in the chosen phone's baseline mode, measures the real App window after placement, and selects the narrowest wider VDD mode when its minimum size does not fit.
- The Home Bar, App switcher and touch region are repositioned using the newly enumerated display dimensions.
- Returning to the App list restores the baseline mode; reopening the same App restores its fitted mode and retains the original window-restore ledger.
- An unresponsive or unrestorable App stops the transition instead of losing its original placement record. Failed mode requests leave the last working mode untouched.
- These are source-level behaviors tested by compilation and pure ScreenFit unit tests, not yet a completed iPhone/UU live-session acceptance.

### Installation candidate

- 0.2.9 installer is built and copied to `F:\\snug for Windows\\artifacts\\Snug-Setup-0.2.9-x64.exe`; installation on a physical test PC is still pending.
- Final 0.2.9 installer: 55,603,269 bytes; SHA-256 `ED17BD03789DC813F32A566C6DE0EB5407AFC20C9F523C00935661C7BD46DBBE`. Source and copied artifact hashes match.
- Packaged `--catalog-preview` starts and exposes the shared on-phone picker. UI automation verified `iPhone13` returns four items, including mini and Pro Max, and returns to the catalog via `完成`. Screenshot: `artifacts/iphone13-phone-picker-0.2.9.png`. Only production binaries are packaged (no xUnit/testhost).
- Bundles .NET 10 Windows Desktop runtime and the official driver/NefCon payload with attribution.
- Packaging builds into a dedicated artifact directory, so a running UI preview no longer locks the development DLL.
- No PR has been submitted.

### Shared phone-model selection (0.2.9 candidate)

- PhoneModelPicker.xaml/.cs is one shared WPF control hosted by the desktop PhoneModelWindow and the PhoneSurfaceWindow virtual phone screen. The on-phone App catalog now has a "手机型号" button; the picker opens as an in-window overlay without launching a dialog on the PC's main monitor.
- Choice selection, cancel/complete, live model search and custom width/height are reused in both hosts. Choosing the current profile is a no-op.
- Windows app and test projects directly include the original repository root Assets/phone-models.json, rather than a copied Platforms/Windows/Assets file. The upstream source remains unchanged.
- Preview UI automation verified that the phone App screen opens the model picker; searching "iPhone13" returns four visible models (including mini and Pro Max); the "完成" control returns to the App list.
- Compatibility tests cover the 24 currently cataloged iPhone 13+ / iPhone Air model entries, with 66/66 total tests passing.
- Display changes from the phone screen still use Windows VDD session recreation where necessary; a seamless no-drop UU stream is NOT verified. The original macOS smooth mode-change behavior is not yet guaranteed on Windows.

## Remaining work

1. Real-hardware acceptance is pending: verify the driver's advertised modes, EDID, mode switches with different App minimum sizes, and the return-to-list/reveal loop through UU on an iPhone. Code and unit tests are complete, but the VDD/UI integration has not been field-tested.
2. Controlled installation and upgrade test: existing scheduled tasks may still use prior NefCon actions until the user installs 0.2.8 or runs the driver repair. No driver installation, display change or task registration was performed during this iteration.
3. Verify the new native HMENU command path and the allowlisted app-specific shortcuts against real Windows applications; support custom UIA menus where HMENU is unavailable.
4. Verify Win32 save-dialog transfer on iPhone; add detection for non-native browser/custom dialogs and a separately confirmed force-quit flow.
5. Recheck touch/mouse gestures, DPI changes, multi-monitor placement, and restore behavior against the 2.4.0 interaction flow.
6. 0.2.9 packaging completed. Verify clean-machine installation, upgrade/uninstall, live iPhone/UU screen recordings, and final PR review.

## Build and preview

Use the repository's pinned .NET SDK:

    dotnet restore Platforms/Windows/Snug.Windows.sln
    dotnet test Platforms/Windows/Snug.Windows.sln -c Release
    dotnet build Platforms/Windows/Snug.Windows.sln -c Release
    dotnet Platforms/Windows/src/Snug.Windows/bin/Release/net10.0-windows/Snug.Windows.dll --catalog-preview

## 0.2.10 upstream phone-UI parity candidate (2026-11-16)

- Rebuilt the phone UI from PhoneHomeView, PhoneAppCard, PhoneSearchPage and PhoneStyle: grouped light background, current-App section, four-column app grid, sticky segmented tabs, independent search, and in-phone confirmations.
- PhoneModelPage now uses a current-phone card, custom resolution validation, brand-grouped virtualized rows; both hosts use the original shared model catalog.
- The 540-point logical UI scales with the selected phone display; gesture translation is suspended while the app catalog is visible and resumes on a placed app.
- Keyboard focus is claimed for search/model input only and restored on leaving the page.
- UI acceptance script: tests/PhoneUiSmoke.ps1. Screenshots: artifacts/phone-parity-home.png, phone-parity-all-loaded.png, phone-parity-search.png, phone-parity-confirmation.png, phone-parity-model.png.
- 66 core tests and Release compilation verified. Packaging does not install a driver or run a real iPhone/UU session.
- Outstanding: full PhoneDesk state machine, native App menu new-window, save-dialog watch, precise UU controller detection, no-drop VDD resize, real-device gestures and installation acceptance.

See UPSTREAM_PARITY_MATRIX.md.

## 0.2.11 — PhoneDesk lifecycle and new-window parity (2026-11-16)

- Upstream NewWindowMenu matching rules are ported to Snug.Windows.Core/NewWindowMenu.cs, with exact and localized new-window titles preferred over document/new-file actions.
- WindowManager scans supported native Win32 menu items (HMENU) and invokes a verified enabled New Window command. Browser and VS Code/Cursor shortcuts are allowlisted; unknown apps no longer receive a blind Ctrl+N.
- Close/quit now observe the actual window/process result for up to 8/10 seconds. Owned native save/confirmation dialogs are detected with DialogClassifier, moved onto the phone monitor without resizing, and tracked by the existing restore ledger. The dialog's dismissal is watched for recovery.
- No process is force-killed when a close/quit request times out. Unsupported custom browser/native dialogs remain a real-device test gap.
- A FIFO PhoneActionLane serializes activation, new-window, close, quit, pull, restore and return-to-App operations; nested operations cannot deadlock the queue. Core regression tests now total 109 (including native menu selection, dialog classification and concurrency).
- Release build passed with 0 errors and 0 warnings. tests/PhoneUiSmoke.ps1 still passes all on-phone UI flows.
- Windows HMENU/Win32 behavior and real iPhone+UU save prompts have not yet been validated together. Driver installation, active display configuration and UU settings are unchanged.

## 0.2.12 — VDD monitor initialization regression verified (2026-11-16)

Live hardware finding on Windows 10 19045:
- Installed 0.2.11 wrote 55 VDD modes (1080x1900 through 2160x3800). The root-enumerated MttVDD adapter reported OK, but no SNG00F0 monitor was exposed to Windows.
- The stop-task isolation test kept the device present for 30 seconds but still produced only 1 active WMI monitor.
- With complete backups of phone-profile.json and vdd_settings.xml, a second test temporarily reduced the profile to one 1080x1900@60 mode and reloaded only Snug-owned Root\MttVDD. The SNG00F0 monitor appeared within 5 seconds and stayed present throughout a 25-second test and subsequent multi-minute check.
- StopPhoneDisplay was restored to its original enabled task state, and the original 55-mode files remain backed up under F:\snug for Windows\diagnostics\live-stop-isolation\backup_20261116_122114.
- VddResolutionProfile.Create now advertises only the per-phone base mode, preserving logical ScreenFit widths separately. Multi-step VDD modes are disabled pending controlled compatibility testing.
- Release build 0 warnings/0 errors; 109/109 tests passing; 0.2.12 installer built at F:\snug for Windows\artifacts\Snug-Setup-0.2.12-x64.exe.
- IMPORTANT: 0.2.11 is still the installed version until the pending administrator/UAC prompt for the 0.2.12 installer is approved. Do not claim an upgraded runtime before verifying installed version and the phone screen after installation.
- Older installers are retained for reversible archive only; upstream Snug source and windows-platform worktree must not be removed.

## 0.2.13 — iPhone 13 Pro phone-canvas layout regression (2026-11-16)

- Reproduced on user's iPhone 13 Pro with UU: app labels and buttons visually overcrowded on the 720x1223 virtual display; the selected phone model was already correct (Apple iPhone 13 Pro, 1170x2532).
- Root cause in PhoneSurfaceWindow.PlaceOn: design canvas width was calculated as physical virtual display width / 2, becoming 360 instead of upstream's 540 design points. UI font sizes and 48/56-point action controls were not scaled down, so labels were clipped.
- Added PhoneCanvasLayout.ForDisplay to pin the visual design width to ScreenFit.DesignWidth=540, derive height from monitor aspect ratio, and render through a Uniform Viewbox. Preview canvas uses the same phone-specific ratio.
- Running app header now uses constrained Grid columns; long window titles and all-app grid labels use ellipsis rather than overlapping adjacent action buttons.
- Added 8 layout geometry regression test cases; build 0 errors/0 warnings, 117/117 tests passed; phone UI smoke verified home, all, search, confirmation, model and return.
- Retain the 0.2.12 single-mode VDD compatibility workaround: 55-mode driver config previously prevented SNG00F0 from enumerating. Phone aspect ratio is computed from that working single native mode.
- User's screenshots include UU's own top/bottom inactive areas and touch overlays. Do not make Snug draw into UU toolbar space merely to remove black bands; upstream ScreenFit intentionally subtracts UU-safe-area insets.
- 0.2.13 is the iPhone layout hotfix; install and inspect the actual remote virtual monitor before claiming end-to-end iPhone acceptance.

### 0.2.13 post-install verification (2026-11-16)

- Verified live registry shows Snug for Windows 0.2.13, and the running runtime's product version is 0.2.13.
- Actual phone-surface WPF window on virtual display is 720x1223; model profile: Apple iPhone 13 Pro, 1170x2532.
- Screenshot F:\snug for Windows\diagnostics\live-stop-isolation\IPHONE13PRO_0.2.13_LIVE.png confirms card names and action buttons no longer overlap.
- Installed-app grid screenshot inspected, four columns without card layout overflow.
- Release build: 0 warnings, 0 errors. Core regression suite: 117/117 passing.
- First UI smoke rerun failed on load timeout. The automation wait now checks that loading controls are actually visible (rather than hidden WPF UIA elements) and allows up to 45 seconds. Second run PASSED: home, all, search, confirmation, model, return; measured all-app catalog loading in 8000ms.
- Phone-display device SNG00F0 remains present; the validated single-mode VDD configuration is retained and dynamic display width switching is intentionally restricted until multi-mode driver compatibility is resolved.
- The native iPhone touch/gesture and actual UU safe-area experience have not been directly re-accepted on device; host-side display and UI checks passed.
