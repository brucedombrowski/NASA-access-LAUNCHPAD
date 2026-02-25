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

REM --- Set up launch log (temp file for GitHub issue body) ---
set "LOG=%TEMP%\launchpad_launch.log"
type nul > "%LOG%"

echo.

REM --- Auto git pull if this is a git repo ---
if exist "%SCRIPT_DIR%.git" (
    echo  Checking for updates from GitHub...
    echo Checking for updates from GitHub... >> "%LOG%"
    git -C "%SCRIPT_DIR%." pull --ff-only > "%TEMP%\launchpad_git.tmp" 2>&1 || git -C "%SCRIPT_DIR%." pull --rebase >> "%TEMP%\launchpad_git.tmp" 2>&1
    type "%TEMP%\launchpad_git.tmp"
    type "%TEMP%\launchpad_git.tmp" >> "%LOG%"
    del "%TEMP%\launchpad_git.tmp" >nul 2>&1
    echo.
    echo. >> "%LOG%"
)

REM --- Find the exe ---
set "EXE="
if exist "%SCRIPT_DIR%LaunchpadAuth.exe" set "EXE=%SCRIPT_DIR%LaunchpadAuth.exe"
if not defined EXE if exist "%SCRIPT_DIR%LaunchpadAuth\bin\Release\net8.0-windows\win-x64\publish\LaunchpadAuth.exe" set "EXE=%SCRIPT_DIR%LaunchpadAuth\bin\Release\net8.0-windows\win-x64\publish\LaunchpadAuth.exe"
if not defined EXE if exist "%SCRIPT_DIR%LaunchpadAuth\bin\Debug\net8.0-windows\win-x64\publish\LaunchpadAuth.exe" set "EXE=%SCRIPT_DIR%LaunchpadAuth\bin\Debug\net8.0-windows\win-x64\publish\LaunchpadAuth.exe"

if not defined EXE goto :notfound

echo  Launching: %EXE%
echo Launching: %EXE% >> "%LOG%"
echo.

REM --- Create GitHub issue to track this session ---
set "ISSUE_URL="
for /f "delims=" %%I in ('gh issue create --repo brucedombrowski/NASA-access-LAUNCPAD --title "Launch: %DATE% %TIME%" --body-file "%LOG%" --label "launch-log" 2^>nul') do set "ISSUE_URL=%%I"
del "%LOG%" >nul 2>&1

if defined ISSUE_URL (
    echo  Launch logged: !ISSUE_URL!
    echo.
)

REM --- Run app and wait for it to close ---
echo  Waiting for app to close...
"%EXE%"

echo.
echo  ============================================================
echo   Session ended at %DATE% %TIME%
echo  ============================================================
echo.

if not defined ISSUE_URL goto :end

REM --- Add close timestamp as comment ---
gh issue comment !ISSUE_URL! --body "Session closed at %DATE% %TIME%" >nul 2>&1

set /p "FEEDBACK_CHOICE=  Add feedback to launch log? [Y/N]: "
if /i "!FEEDBACK_CHOICE!"=="Y" (
    echo.
    set /p "FEEDBACK=  Enter feedback: "
    if defined FEEDBACK (
        (echo !FEEDBACK!) > "%TEMP%\launchpad_feedback.tmp"
        gh issue comment !ISSUE_URL! --body-file "%TEMP%\launchpad_feedback.tmp" >nul 2>&1
        gh issue edit !ISSUE_URL! --add-label "feedback" >nul 2>&1
        del "%TEMP%\launchpad_feedback.tmp" >nul 2>&1
        echo  Feedback added.
    )
)

echo.
set /p "CLOSE_CHOICE=  Close this launch log issue? [Y/N]: "
if /i "!CLOSE_CHOICE!"=="Y" (
    gh issue close !ISSUE_URL! >nul 2>&1
    echo  Issue closed.
) else (
    echo  Issue left open: !ISSUE_URL!
)

:end
echo.
pause
exit /b 0

:notfound
echo  LaunchpadAuth.exe not found.
echo.
echo  Build first:
echo    dotnet publish LaunchpadAuth\LaunchpadAuth.csproj -c Release -r win-x64
echo.
echo  Or copy LaunchpadAuth.exe next to this launch.bat.
pause
