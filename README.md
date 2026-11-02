# Network Profiles

A Windows desktop tool that stores network settings as reusable profiles and
applies them to any connected adapter. A profile is not tied to an adapter:
you pick the adapter when you apply it.

A profile covers:

- **IP address**: DHCP, or one or more static IPv4 addresses, each with a subnet mask (dotted or prefix length), plus an optional gateway.
- **DNS**: DHCP, or a list of servers in priority order.
- **Proxy**: no proxy, a manual server with bypass list, or a PAC script URL.

Each group can also be set to *Don't change*, so a profile can, for example,
switch only the proxy.

## Features

- **Undo:** before applying, the app remembers the adapter's current settings
  (and the proxy, if the profile changes it). After applying it asks whether to
  keep the new settings and restores the old ones after 15 seconds without an
  answer. If any step fails, the old settings are restored right away.
- **Tray quick switch:** closing the window keeps the app in the notification
  area. Right-click the icon to apply a profile to the adapter used last; Exit
  quits.
- **Import and export:** share profiles as a JSON file. Imports never overwrite
  existing profiles; clashing names get a number.
- **Capture:** "Save current as profile" turns an adapter's current settings
  into a new profile.
- **Auto-switch:** rules like "while `game.exe` runs, use Gaming". When the
  program starts, its profile is applied to the adapter chosen in the rule;
  when it exits, the previous settings come back. If that adapter (say a USB
  network card) is unplugged, the rule waits and applies once it is plugged
  in, and a restore for an unplugged adapter waits until it is back. Only one rule is active at a time
  (first in the list wins), and applying a profile by hand while a rule is
  active means nothing is undone when the program exits. The app watches
  programs, so it has to be running; it can start in the tray at sign-in.

## How it is put together

The app runs as the signed-in user. Changing adapter addresses needs
administrator rights, so a small Windows service (`NetworkProfiles.Service`,
running as LocalSystem) does that part: the app sends it the settings over a
local named pipe, and the service builds and runs the netsh commands itself.
The pipe only accepts interactive (signed-in) users, administrators and
SYSTEM, and rejects network logons. The proxy is per user, so the app sets it
itself. Without the service, the app can still apply profiles when started as
administrator.

## Requirements

- Windows 10 or 11
- .NET 10 SDK to build
- To change adapter settings, either the service is installed or the app is
  started as administrator.

## Build and run

```
dotnet build
dotnet run --project src/NetworkProfiles.App
dotnet test
```

To build one self-contained `NetworkProfiles.exe` that runs without .NET
installed (written to `publish/`):

```
dotnet publish src/NetworkProfiles.App -p:PublishProfile=SingleFile
```

The exe is not code-signed, so Windows SmartScreen may warn on first start.

### Installer

On Windows, `.\build-release.ps1` runs the tests, publishes the app and the
service as single files into `publish/`, and builds
`installer/bin/Release/NetworkProfilesSetup.msi` (WiX Toolset 6, restored
automatically by `dotnet build`). The MSI installs both into
`Program Files\Network Profiles`, registers and starts the service, and adds a
Start menu shortcut. Pass `-Version 1.2.3` to stamp the executables and the
MSI (otherwise `ProductVersion` in `installer/NetworkProfiles.Installer.wixproj`
is used); raise it for each release so upgrades replace the old install. WiX
only runs on Windows.

### Automated builds

`.github/workflows/build.yml` runs `build-release.ps1` on a Windows runner for
every push to `main`, every pull request, and on demand (*Actions > Build > Run
workflow*). The two exe files and the MSI are attached to each run as a
downloadable artifact.

To publish a release, create it on GitHub (*Releases > Draft a new release*),
choose a new tag like `v1.2.3` on `main`, and publish it. The workflow then
builds version 1.2.3 and attaches `NetworkProfiles.exe`,
`NetworkProfiles.Service.exe` and `NetworkProfilesSetup.msi` to the release,
usually within about five minutes. The tag must look like `v1.2.3`, or the build
fails. Pushing a tag on its own does not build or release anything.

Without the MSI, `scripts/install-service.ps1` (run as administrator next to
the two exe files) copies them to Program Files and registers the service;
`scripts/uninstall-service.ps1` removes it again.

The solution also builds on Linux and macOS (`EnableWindowsTargeting`), which
is useful for CI; the app itself only runs on Windows.

## Layout

| Project | Purpose |
| --- | --- |
| `src/NetworkProfiles.Core` | Profile model, validation, netsh command builder and runner, JSON storage, service protocol, auto-switch rules. No Windows-only APIs. |
| `src/NetworkProfiles.App` | WPF app (Fluent theme, MVVM with CommunityToolkit.Mvvm), tray icon, adapter discovery, proxy API, service client. |
| `src/NetworkProfiles.Service` | Windows service that applies adapter settings for the app. |
| `installer` | WiX MSI installer for the app and the service. |
| `tests/NetworkProfiles.Core.Tests` | xUnit tests for the Core library. |

## How settings are applied

- IP address, gateway and DNS go through `netsh interface ipv4`, addressing the
  adapter by interface index and passing arguments as a list, so adapter names
  are never pasted into a command line.
- The proxy is set with WinINet's per-connection options
  (`InternetSetOption`), the same settings as *Settings > Network & internet >
  Proxy*. It belongs to the current Windows user, not to an adapter. Choosing
  *No proxy* or *Manual* also turns off automatic proxy detection.
- Profiles are saved automatically to `%APPDATA%\NetworkProfiles\profiles.json`;
  the last used adapter and profile go to `settings.json` next to it. Profile
  files from the first version (one address per profile) are upgraded on load.

## Current limits

- IPv4 only; IPv6 settings are left untouched.
- One gateway per profile.
