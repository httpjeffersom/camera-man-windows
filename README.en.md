# CameraMan for Windows

**[Português](README.md) | English**

A floating camera for Windows 11 — put yourself on screen in recordings, demos, classes and calls.

This repository is a **fork** of [CameraMan for macOS](https://github.com/lucianodiisouza/camera-man-macos) and contains
the native Windows port: the same product, the same behaviour and the same MIT license, re-implemented in C# with
WPF and the Windows camera stack. The macOS app remains the upstream project and is not modified here.

- Windows port: <https://github.com/httpjeffersom/camera-man-windows>
- macOS original: <https://github.com/lucianodiisouza/camera-man-macos>
- Download: [CameraMan-Windows-1.0.0-win-x64.zip](https://github.com/httpjeffersom/camera-man-windows/releases/download/v1.0.0/CameraMan-Windows-1.0.0-win-x64.zip)

> **This is an early build of the port.** The app works, but there may still be interface problems, wrong or missing
> icons and other rough edges; they will be fixed little by little. See
> [Status: what still needs work](#status-what-still-needs-work).

## Features

- **Made for recording**: global shortcuts work from any app (`Ctrl+Alt+C` show/hide, `Ctrl+Alt+S` switch size); the
  floating camera stays above other windows; capture stops completely while the camera is hidden or the session is
  locked.
- **Shapes**: circle, organic (five presets, shuffle, optional breathing motion), square, vertical 9:16 and horizontal
  16:9, with soft edge and per-shape corner radius.
- **Framing**: move the image inside the shape, zoom, mirror or flip.
- **Look**: brightness, contrast and saturation on the camera frames; solid or gradient border; soft shadow.
- **Window**: five sizes, snap to any corner, each size remembers its own place and monitor, `Space` switches between
  your two favourite sizes.
- **Settings window** in the style of Windows Settings, with nine pages and search.
- **Notification-area icon**, optional start at login, English and Brazilian Portuguese.
- **Private**: no network access, no account, no analytics — video never leaves the machine.

## Windows port notes

| macOS | Windows port |
|---|---|
| Swift, SwiftUI, AVFoundation | C#, WPF, WinRT `MediaCapture` |
| ⌃⌥⌘C / ⌃⌥⌘S | `Ctrl+Alt+C` / `Ctrl+Alt+S` (Windows reserves most Win-key chords) |
| Core Image on the GPU | CPU lookup table + fixed-point saturation |
| Soft edge blurred outward, window padded | Soft edge feathered inward from the outline (no transparent padding) |
| Menu bar icon, optional Dock icon | Notification-area icon, no taskbar button |
| System Settings style | Windows 11 Settings style, native title bar |
| `UserDefaults` | `%APPDATA%\CameraMan\settings.json` |
| App Sandbox + notarization | Plain per-user executable, no installer yet |

The full analysis is in [docs/PORTING_PLAN.md](docs/PORTING_PLAN.md), the API-by-API mapping in
[docs/WINDOWS_APIS.md](docs/WINDOWS_APIS.md) and the verification matrix in [docs/FEATURE_STATUS.md](docs/FEATURE_STATUS.md).

## Requirements

- Windows 11 (x64). Windows 10 2004+ should work but is not the tested target.
- A webcam. The app uses the standard Windows camera stack and asks the system for access on first use.
- To build: .NET SDK 10.0.401 or newer. See [docs/BUILDING.md](docs/BUILDING.md).
- To run a published copy: nothing else. Self-contained builds include the .NET runtime.

## Quick start

Download the zip from the [release page](https://github.com/httpjeffersom/camera-man-windows/releases/tag/v1.0.0),
unzip it anywhere and run `CameraMan.Windows.exe`. There is no installer and no administrator rights are needed; because
the executable is not code-signed, SmartScreen may warn on the first run ("More info" → "Run anyway").

From source:

```powershell
dotnet build CameraMan.Windows.slnx -c Release
dotnet run --project src/CameraMan.Windows -c Release
```

Or run the published, self-contained build:

```powershell
.\publish\win-x64\CameraMan.Windows.exe
```

On first launch the camera window appears at the top-left corner of the primary monitor, 400 px wide. The tray icon is
the way back to the app after you close the camera window.

## Keyboard

| Shortcut | Action |
|---|---|
| `Ctrl+Alt+C` | Show/hide the camera (global; can be disabled in Settings) |
| `Ctrl+Alt+S` | Switch to the next size preset (global) |
| `Ctrl+,` | Open settings (camera window focused, or tray menu) |
| `Ctrl+Q` | Quit (camera window or tray menu) |
| Arrows | Move the framing |
| `+` / `-` / `R` | Zoom in, zoom out, reset |
| `/` / `V` | Mirror horizontally / vertically |
| `O` | Next shape |
| `Backspace` | Next camera |
| `Space` | Toggle the two favourite sizes |
| `Ctrl+F` | Search settings |
| `Esc` | Close settings (when the search box is empty) |

## Where things are stored

- Settings: `%APPDATA%\CameraMan\settings.json`
- Error log (only written on unexpected failures, rotated at 256 KB): `%APPDATA%\CameraMan\error.log`
- Start at login: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `CameraMan`, written only when you enable
  the option.

Camera frames exist only in memory. They are never written to disk or sent over the network, and the project contains
no networking or media-encoding code.

## Status: what still needs work

The port builds cleanly and the automated suite passes, but it is **not finished**. Known gaps and open items:

- **Interface and icons.** Some screens, spacing and icons still differ from the macOS app or from Windows 11 itself,
  and some labels may read oddly. These will be corrected gradually.
- **Manual verification is incomplete.** Live preview, formats, shapes, borders, shadows, soft edge, tray, global
  shortcuts and settings persistence were exercised on real hardware. Multi-monitor with mixed DPI, lock/sleep
  resume, per-preset position restore across launches, camera switching by `Backspace`, zoom/pan/mirror by keyboard and
  "restore defaults" still need a hands-on pass. The matrix in [docs/FEATURE_STATUS.md](docs/FEATURE_STATUS.md) marks
  each item as *Verified*, *Implemented* or *Adapted*.
- **Search finds settings but does not scroll to them.** Clicking a result opens the page; the matching row is not
  highlighted.
- **No installer or code signing.** The published folder is portable and unsigned, so SmartScreen warns on first run.
- **Topmost has limits.** A normal topmost window cannot cover exclusive-fullscreen DirectX apps; borderless
  fullscreen works.
- **macOS video effects are absent.** Portrait, Studio Light and background replacement are system features with no
  stable public Windows equivalent; the app explains them but does not simulate them.
- **Soft edge fades inward only**, so the visible silhouette is slightly tighter than on macOS (which blurs outward
  into transparent padding).
- **Colour correction runs on the CPU**, which costs more than Core Image but keeps the app dependency-free.
- **Language changes need a restart.**
- **No separate shadow opacity control**; the shadow colour keeps its alpha (the default is 50 % black).
- **Notifications and updates**: there is no update check and no toast outside the app's own overlay.

## Privacy and security

CameraMan has no networking code at all: no HTTP client, no sockets, no telemetry, no accounts. The camera stream is
read frame by frame into a reusable buffer and drawn; it is never encoded or saved. The only files touched are the
settings/log in `%APPDATA%` and the per-user Run key when you enable start at login. The app runs as the current user
(`asInvoker`) and never requests administrator rights. See [PRIVACY.md](PRIVACY.md) and [SECURITY.md](SECURITY.md).

## Build from source

Requirements: Windows 11 x64, .NET SDK 10, a camera for the manual test.

```powershell
dotnet build CameraMan.Windows.slnx -c Release
dotnet test CameraMan.Windows.slnx -c Release
dotnet publish src\CameraMan.Windows\CameraMan.Windows.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
```

No Visual Studio workload, Windows App SDK, DirectX SDK or Node.js is required. See
[docs/BUILDING.md](docs/BUILDING.md) for prerequisites, the manual test checklist and troubleshooting.

## Localization

English and Brazilian Portuguese. The language picker is in Settings → General; the choice applies on the next
launch. "System" follows the installed Windows UI language.

## Contributing

Contributions are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md) for the development setup and
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) for community standards. Security issues: [SECURITY.md](SECURITY.md).

## Changelog

See [CHANGELOG.md](CHANGELOG.md) for version history.

## License

MIT. The original macOS project is Copyright (c) 2025 Camera-Man Contributors; this port adds
Copyright (c) 2026 CameraMan.Windows contributors. See [LICENSE](LICENSE) and
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
