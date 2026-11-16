@echo off
setlocal
set "SNUG_ROOT=%~dp0"
set "DOTNET_ROOT=%SNUG_ROOT%.tools\dotnet"
if not exist "%DOTNET_ROOT%\dotnet.exe" (
  echo Local .NET 10 runtime was not found.
  exit /b 1
)
if not exist "%SNUG_ROOT%src\Snug.Windows\bin\Release\net10.0-windows\Snug.Windows.dll" (
  echo Snug has not been built. Run build-snug.cmd first.
  exit /b 1
)
"%DOTNET_ROOT%\dotnet.exe" "%SNUG_ROOT%src\Snug.Windows\bin\Release\net10.0-windows\Snug.Windows.dll" %*
endlocal
