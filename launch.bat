@echo off
REM ============================================================
REM  NASA access LAUNCHPAD — one-click launcher
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
title NASA access LAUNCHPAD
chcp 65001 >nul 2>&1
powershell -NoProfile -Command "Add-Type -Name Win -Namespace Native -MemberDefinition '[DllImport(\"user32.dll\")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow); [DllImport(\"user32.dll\")] public static extern IntPtr GetForegroundWindow();'; [Native.Win]::ShowWindow([Native.Win]::GetForegroundWindow(), 3)" >nul 2>&1

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"
cls

REM --- Kill orphaned instances ---
taskkill /f /im LaunchpadAuth.exe >nul 2>&1

REM --- Set up launch log (temp file for GitHub issue body) ---
set "LOG=%TEMP%\launchpad_launch.log"
type nul > "%LOG%"

echo.

REM --- Auto git pull if this is a git repo ---
if exist "%SCRIPT_DIR%.git" (
    echo  Checking for updates from GitHub...
    echo Checking for updates from GitHub... >> "%LOG%"
    git -C "%SCRIPT_DIR%." pull --ff-only 2>&1 || git -C "%SCRIPT_DIR%." pull --rebase 2>&1
    echo.
    echo. >> "%LOG%"
)

REM --- Log commit hash ---
for /f "delims=" %%H in ('git rev-parse --short HEAD 2^>nul') do (
    echo  Commit: %%H
    echo Commit: %%H >> "%LOG%"
    echo.
)

REM --- First-run config setup ---
if exist "%SCRIPT_DIR%config.json" goto :configdone
if not exist "%SCRIPT_DIR%config.json.example" goto :configdone
echo  First run — creating config.json from template...
echo First run — creating config.json from template... >> "%LOG%"
copy "%SCRIPT_DIR%config.json.example" "%SCRIPT_DIR%config.json" >nul
echo  Config created with default URL: https://id.nasa.gov/
echo  Edit config.json to change the target URL.
echo.
:configdone

REM --- Find the exe ---
set "EXE="
if exist "%SCRIPT_DIR%LaunchpadAuth.exe" set "EXE=%SCRIPT_DIR%LaunchpadAuth.exe"
if not defined EXE if exist "%SCRIPT_DIR%LaunchpadAuth\bin\Release\net8.0-windows\win-x64\publish\LaunchpadAuth.exe" set "EXE=%SCRIPT_DIR%LaunchpadAuth\bin\Release\net8.0-windows\win-x64\publish\LaunchpadAuth.exe"
if not defined EXE if exist "%SCRIPT_DIR%LaunchpadAuth\bin\Debug\net8.0-windows\win-x64\publish\LaunchpadAuth.exe" set "EXE=%SCRIPT_DIR%LaunchpadAuth\bin\Debug\net8.0-windows\win-x64\publish\LaunchpadAuth.exe"

if not defined EXE goto :notfound

:launch

REM --- Create/update desktop shortcut (uses title from config.json) ---
set "SC_NAME=NASA access LAUNCHPAD"
if exist "%SCRIPT_DIR%config.json" for /f "delims=" %%T in ('powershell -NoProfile -Command "(Get-Content -Raw '%SCRIPT_DIR%config.json' | ConvertFrom-Json).title" 2^>nul') do if not "%%T"=="" set "SC_NAME=%%T"
set "SC_TARGET=%SCRIPT_DIR%launch.bat"
set "SC_WORKDIR=%SCRIPT_DIR%."
powershell -NoProfile -Command "$d=[Environment]::GetFolderPath('Desktop'); $lnk=Join-Path $d ($env:SC_NAME+'.lnk'); if(Test-Path $lnk){Remove-Item $lnk -Force}; $ws=New-Object -ComObject WScript.Shell; $sc=$ws.CreateShortcut($lnk); $sc.TargetPath=$env:SC_TARGET; $sc.WorkingDirectory=$env:SC_WORKDIR; $sc.Description=$env:SC_NAME; $sc.Save()" >nul 2>&1

echo  Launching: %EXE%
echo Launching: %EXE% >> "%LOG%"
echo.

REM --- Create issue to track this session ---
set "ISSUE_URL="
for /f "delims=" %%I in ('gh issue create --title "Launch: %DATE% %TIME%" --body-file "%LOG%" --label "launch-log" 2^>nul') do set "ISSUE_URL=%%I"
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

REM --- Add session summary as comment ---
set "AUTH_STATUS=Not authenticated"
if exist "%TEMP%\launchpad_auth_status.txt" (
    set /p AUTH_STATUS=<"%TEMP%\launchpad_auth_status.txt"
    del "%TEMP%\launchpad_auth_status.txt" >nul 2>&1
)
gh issue comment !ISSUE_URL! --body "!AUTH_STATUS!. Session closed at %DATE% %TIME%" >nul 2>&1

set /p "FEEDBACK_CHOICE=  Add feedback to launch log? [y/N]: "
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
set /p "CLOSE_CHOICE=  Keep this issue open? [y/N]: "
if /i "!CLOSE_CHOICE!"=="Y" (
    echo  Issue left open: !ISSUE_URL!
) else (
    gh issue close !ISSUE_URL! >nul 2>&1
    echo  Issue closed.
)

:end
echo.
pause
exit /b 0

:notfound
echo  LaunchpadAuth.exe not found.
echo.
where dotnet >nul 2>&1
if errorlevel 1 goto :nobuild
echo  .NET SDK found — building LaunchpadAuth...
echo .NET SDK found — building LaunchpadAuth... >> "%LOG%"
dotnet publish LaunchpadAuth\LaunchpadAuth.csproj -c Release -r win-x64
if exist "%SCRIPT_DIR%LaunchpadAuth\bin\Release\net8.0-windows\win-x64\publish\LaunchpadAuth.exe" (
    set "EXE=%SCRIPT_DIR%LaunchpadAuth\bin\Release\net8.0-windows\win-x64\publish\LaunchpadAuth.exe"
    echo.
    echo  Build successful!
    echo Build successful! >> "%LOG%"
    echo.
    goto :launch
)
:nobuild
echo  Build first:
echo    dotnet publish LaunchpadAuth\LaunchpadAuth.csproj -c Release -r win-x64
echo.
echo  Or copy LaunchpadAuth.exe next to this launch.bat.
pause
