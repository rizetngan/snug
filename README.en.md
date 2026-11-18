<div align="center">

<img src="Assets/AppIcon/AppIcon-1024.png" width="100" alt="Snug icon">

# Snug for Windows

**A native-grade Windows experience, right on your phone**

Built for UU Remote · Dedicated portrait display · Native gestures · App Library

![Windows x64](https://img.shields.io/badge/Windows-x64-blue) ![Preview](https://img.shields.io/badge/version-0.2.38%20preview-orange) [![MIT](https://img.shields.io/badge/License-MIT-green)](LICENSE)

[简体中文](README.md) · English

[Download](#download) · [Usage](#usage) · [Development](#development)

</div>

Snug for Windows is an independent community port of [Snug](https://github.com/zephyr4123/snug), maintained by [rizetngan](https://github.com/rizetngan). Based on Snug 2.5.0, it uses C# / .NET 10 / WPF. For macOS, visit the upstream project.

## Download

**0.2.38 community preview** · Windows x64 (tested on Windows 10 22H2).

**[Download installer (Windows x64)](https://github.com/rizetngan/snug-for-windows/releases/download/v0.2.38/Snug-Setup-0.2.38-x64.exe)** · [Release notes](https://github.com/rizetngan/snug-for-windows/releases/tag/v0.2.38) · [SHA-256 checksums](https://github.com/rizetngan/snug-for-windows/releases/download/v0.2.38/SHA256SUMS.txt) · [Build from source](Platforms/Windows/README.en.md)

Installer: `Snug-Setup-0.2.38-x64.exe`. The community preview is publicly available. The published installer includes branding updates after the real-device acceptance build and has passed automated tests again. The installer includes the .NET runtime. Administrator authorization is required on first installation for the signed virtual display driver and UU capture monitoring task. The installer itself is unsigned.

## Features

- Dedicated portrait phone display with window fitting and restoration.
- App Library for finding, opening, switching and managing app windows.
- Single-finger and two-axis scrolling, long-press gestures and menu inertia.
- Optional window screenshots and cropping to the Windows clipboard.
- Exit and re-enter the phone display during the same active UU session.

## Usage

1. Install [UU Remote](https://uuyc.163.com/) on both devices; install and launch Snug on the Windows computer.
2. Select your phone model and enable the phone display in Settings.
3. Connect through UU Remote, select Snug Phone from the display list and use screen-touch mode.
4. Choose a window from the App Library; tap the bottom return bar to return to the library.
5. After exiting the phone display, use the re-entry control on the primary display to recreate it, then switch displays in UU. On disconnect, Snug restores managed windows and releases the virtual display.

[Full Windows guide](Platforms/Windows/README.en.md)

## Validation and limitations

November 18, 2026: 268 core tests, Release build, and real iPhone + UU Remote acceptance passed, including phone display exit and re-entry and restoration after disconnect.

The controlling device type and actual streamed display identity remain unknown. Some app capture and zoom behavior, multi-monitor restoration and the full gesture matrix require broader testing.

[Acceptance report](Platforms/Windows/ACCEPTANCE_20261118.md) · [Changelog](Platforms/Windows/CHANGELOG.md) · [Limitations](Platforms/Windows/README.en.md)

## Development

The default `main` branch is the Windows maintenance entry point. The platform implementation is under [Platforms/Windows](Platforms/Windows/).

```powershell
git clone https://github.com/rizetngan/snug-for-windows.git
cd snug-for-windows/Platforms/Windows
.\build-snug.cmd
.\run-snug.cmd
```

[Build, test and package](Platforms/Windows/README.en.md) · [Porting status](Platforms/Windows/PORTING_STATUS.md)

## Credits and license

Thanks to [Snug / zephyr4123](https://github.com/zephyr4123/snug), [UUyc-companion / AoyamaShou](https://github.com/aoyamashou/UUyc-companion), upstream contributors, [Virtual Display Driver](https://github.com/VirtualDrivers/Virtual-Display-Driver) and [NefCon](https://github.com/nefarius/nefcon).

Original history, macOS source and copyright notices are preserved. This independently maintained and released Windows port is not an official upstream Windows build or an official UU Remote product.

[MIT License](LICENSE) · [Third-party notices](Platforms/Windows/installer/THIRD_PARTY_NOTICES.md)
