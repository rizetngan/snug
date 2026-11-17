@echo off
rem Caller owns setlocal; resolve SDK without machine-specific paths.
if defined SNUG_DOTNET_ROOT (
  set "DOTNET_ROOT=%SNUG_DOTNET_ROOT%"
) else if exist "%SNUG_ROOT%.tools\dotnet\dotnet.exe" (
  set "DOTNET_ROOT=%SNUG_ROOT%.tools\dotnet"
) else if not defined DOTNET_ROOT (
  set "DOTNET_ROOT=%ProgramFiles%\dotnet"
)
if not exist "%DOTNET_ROOT%\dotnet.exe" (
  echo .NET 10 SDK not found. Install the SDK in global.json or set SNUG_DOTNET_ROOT.
  exit /b 1
)
set "DOTNET_MULTILEVEL_LOOKUP=0"
exit /b 0