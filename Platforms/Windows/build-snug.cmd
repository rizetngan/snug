@echo off
setlocal
set "SNUG_ROOT=%~dp0"
set "DOTNET_ROOT=%SNUG_ROOT%.tools\dotnet"
"%DOTNET_ROOT%\dotnet.exe" restore "%SNUG_ROOT%Snug.Windows.sln"
if errorlevel 1 exit /b %errorlevel%
"%DOTNET_ROOT%\dotnet.exe" test "%SNUG_ROOT%Snug.Windows.sln" -c Release
if errorlevel 1 exit /b %errorlevel%
"%DOTNET_ROOT%\dotnet.exe" build "%SNUG_ROOT%Snug.Windows.sln" -c Release
exit /b %errorlevel%
