# NASA-access-LAUNCHPAD

Minimal WebView2 app that opens a NASA LAUNCHPAD-enabled site and
handles CAC/PIV smart card authentication.

- Test site: https://id.nasa.gov/
- Redirect: https://auth.launchpad.nasa.gov/

## Setup

1. Copy `config.json.example` to `config.json` and set your target URL:

```json
{
  "url": "https://id.nasa.gov/",
  "title": "NASA access LAUNCHPAD"
}
```

2. Build and publish (requires .NET 8 SDK):

```
dotnet publish LaunchpadAuth/LaunchpadAuth.csproj -c Release -r win-x64
```

3. Copy `config.json` next to the published exe and run.

## What it does

- Opens a maximized WebView2 window to your configured URL
- LAUNCHPAD redirects to CAC/PIV certificate selection
- Auto-focuses the Windows Security PIN dialog so you don't miss it
- Status bar shows auth state (waiting / authenticated / failed)

## Requirements

- Windows 10/11
- WebView2 Runtime (ships with Edge, or install from
  https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
- CAC/PIV smart card reader
