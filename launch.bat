@echo off
:: NASA LAUNCHPAD Auth — one-click launcher
:: Looks for LaunchpadAuth.exe next to this script or in the publish output.

setlocal

set "SCRIPT_DIR=%~dp0"

:: Check for published exe next to launch.bat
if exist "%SCRIPT_DIR%LaunchpadAuth.exe" (
    start "" "%SCRIPT_DIR%LaunchpadAuth.exe"
    goto :eof
)

:: Check publish output
if exist "%SCRIPT_DIR%LaunchpadAuth\bin\Release\net8.0-windows\win-x64\publish\LaunchpadAuth.exe" (
    start "" "%SCRIPT_DIR%LaunchpadAuth\bin\Release\net8.0-windows\win-x64\publish\LaunchpadAuth.exe"
    goto :eof
)

echo LaunchpadAuth.exe not found.
echo.
echo Build first:
echo   dotnet publish LaunchpadAuth\LaunchpadAuth.csproj -c Release -r win-x64
echo.
echo Or copy LaunchpadAuth.exe next to this launch.bat.
pause
