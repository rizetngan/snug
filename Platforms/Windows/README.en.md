# Snug · Windows platform support

[简体中文](README.md) · [English](README.en.md) · [Project home](../../README.en.md)

Snug enhances display and touch interaction for UU Remote. This community contribution adds Windows support to [Snug](https://github.com/zephyr4123/snug) 2.5.0, keeping the original App Library, phone workspace, window restoration, gestures and screenshot workflow as well as macOS implementation and copyright history. Windows 0.2.33 is a preview pending upstream review, not an official upstream release.

## Installation and usage

1. Install UU Remote and the Snug Windows x64 installer on the controlled computer. First installation requests administrator approval to deploy the signed virtual display driver.
2. Connect from your phone in UU Remote, select Snug Phone in the display list and use screen-touch mode.
3. Open an app or window in the App Library; use the bottom bar to return. Snug manages window fitting, scrolling, long press and per-app text enlargement.
4. Enable Screenshot in desktop Settings (off by default). Tap the camera when a window is placed; tap the thumbnail to crop and copy the result to the host clipboard.
5. When UU inbound session information is unavailable, Snug displays compatibility mode / unknown device. This preserves the phone screen but does not confirm the connection, controller type or streamed monitor. If the display remains after disconnecting, turn off Phone Screen or quit Snug normally to restore the windows.

## Build and package

Requirements: Windows x64 (Windows 10 22H2 tested), .NET SDK 10.0.404 (latest patch in its feature band allowed), NuGet connectivity. Packaging additionally requires Windows Desktop Runtime 10.0.15 and Inno Setup 7. Build and unit tests do not need administrator rights.

    cd Platforms\Windows
    build-snug.cmd
    run-snug.cmd

SDK resolution: SNUG_DOTNET_ROOT, .tools/dotnet, DOTNET_ROOT, standard Program Files/dotnet. For custom tools set the environment variables in the current terminal:

    $env:SNUG_DOTNET_ROOT = 'C:\Program Files\dotnet'
    $env:SNUG_ISCC = 'C:\Program Files (x86)\Inno Setup 7\ISCC.exe'
    .\build-snug.cmd
    .\package-snug.cmd

The output is artifacts/installer/Snug-Setup-0.2.33-x64.exe. The installer bundles the pinned runtime and signed VDD; the installer itself is not signed. The app-scoped legacy CET compiler bridge preserves the SDK binaries and system security settings. On supported systems, -p:SnugUseLegacyWindowsCETWorkaround=false uses the native SDK compiler.

## Architecture

- Snug.Windows.Core: platform-independent window and restoration state, screen fit, gesture and momentum logic, UU session evidence and screenshot geometry.
- Snug.Windows: WPF interface and Windows system adapters.
- installer: signed VDD 25.7.23 and NefCon 1.21.1. Only Snug-managed device resources are removed on uninstall.

## Validation and limitations

224 core tests pass. Local Windows tests cover window placement/restoration, VS Code restart zoom recovery, Chrome tab input and two-axis scrolling, menu drag, force quit, native window capture and crop. Actual installed camera setting and return/restore were verified.
Menu momentum passed WPF timer tests; full iPhone gesture and native clipboard acceptance are pending. VDD mode steps differ from macOS. Mixed physical-display configurations, some apps' zoom behavior, cross-virtual-desktop handling and protected/GPU window capture need wider testing.
UU Windows does not currently expose a reliable inbound controller or streamed-display identity; locked logs are compatibility hints only. Manual upgrade only.

## Credits and license

Original Snug author: zephyr4123. Project predecessor: UUyc-companion by AoyamaShou. Original copyright, history and community acknowledgments are preserved. Windows support additionally thanks Virtual Display Driver and NefCon. See the [MIT license](../../LICENSE) and [third-party notices](installer/THIRD_PARTY_NOTICES.md). Snug is not an official UU Remote product.