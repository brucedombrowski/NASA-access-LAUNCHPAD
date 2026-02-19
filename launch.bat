@echo off
REM ============================================================
REM  NASA LAUNCHPAD Auth — one-click launcher
REM ============================================================
REM
REM  Double-click to open a WebView2 window to your NASA
REM  LAUNCHPAD-enabled site with CAC/PIV authentication.
REM
REM  Config: copy config.json.example to config.json and set
REM          your target URL before first run.
REM
REM ============================================================

setlocal enabledelayedexpansion

REM --- Maximize terminal window and enable UTF-8 ---
title NASA LAUNCHPAD Auth
chcp 65001 >nul 2>&1
powershell -NoProfile -Command "Add-Type -Name Win -Namespace Native -MemberDefinition '[DllImport(\"user32.dll\")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow); [DllImport(\"user32.dll\")] public static extern IntPtr GetForegroundWindow();'; [Native.Win]::ShowWindow([Native.Win]::GetForegroundWindow(), 3)" >nul 2>&1

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

echo.

REM --- Auto git pull if this is a git repo ---
if exist "%SCRIPT_DIR%.git" (
    echo  Checking for updates from GitHub...
    git -C "%SCRIPT_DIR%." pull --ff-only 2>&1 || git -C "%SCRIPT_DIR%." pull --rebase 2>&1
    echo.
)

REM --- Find the exe ---
set "EXE="
if exist "%SCRIPT_DIR%LaunchpadAuth.exe" set "EXE=%SCRIPT_DIR%LaunchpadAuth.exe"
if not defined EXE if exist "%SCRIPT_DIR%LaunchpadAuth\bin\Release\net8.0-windows\win-x64\publish\LaunchpadAuth.exe" set "EXE=%SCRIPT_DIR%LaunchpadAuth\bin\Release\net8.0-windows\win-x64\publish\LaunchpadAuth.exe"
if not defined EXE if exist "%SCRIPT_DIR%LaunchpadAuth\bin\Debug\net8.0-windows\win-x64\publish\LaunchpadAuth.exe" set "EXE=%SCRIPT_DIR%LaunchpadAuth\bin\Debug\net8.0-windows\win-x64\publish\LaunchpadAuth.exe"

if defined EXE (
    echo  Launching: %EXE%
    echo.
    start "" "%EXE%"
    exit /b 0
)

echo  LaunchpadAuth.exe not found.
echo.
echo  Build first:
echo    dotnet publish LaunchpadAuth\LaunchpadAuth.csproj -c Release -r win-x64
echo.
echo  Or copy LaunchpadAuth.exe next to this launch.bat.
pause
