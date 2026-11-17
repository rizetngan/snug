@echo off
setlocal
set "SNUG_ROOT=%~dp0"
cd /d "%SNUG_ROOT%"
call "%SNUG_ROOT%tools\Resolve-Dotnet.cmd"
if errorlevel 1 exit /b %errorlevel%
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
