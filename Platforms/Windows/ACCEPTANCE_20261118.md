# Snug for Windows 0.2.38 — real-device acceptance

Date: 2026-11-18
Baseline: 0.2.38+18f0d184c9d3b8fb0b853c7dca17ec1d639e6321

## Result

**PASS**

Real iPhone + UU Remote end-to-end acceptance passed on the Windows build.

- Connect from iPhone through UU Remote -> Snug phone screen appears.
- Keep the UU session active beyond 60 seconds -> phone screen remains available; no false timeout withdrawal.
- Tap Exit Phone Screen -> phone screen is removed without ending the UU session.
- Re-enter the phone screen during the same UU session -> phone screen is recreated successfully.
- Disconnect UU -> managed windows restore and the virtual display is released.

## Automated validation

- 268 / 268 core tests passed.
- Release solution build passed with 0 warnings and 0 errors.
- Installed runtime reports 0.2.38+18f0d184c9d3b8fb0b853c7dca17ec1d639e6321.

## Installer

Snug-Setup-0.2.38-x64.exe

SHA-256:

5E803AD6E11676D609B5B4A9A1B8F5E391E536C254564408B115F86A7C132D96

This is the current Windows community-preview acceptance baseline; it is not an upstream official Windows release.