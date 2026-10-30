# Building and testing CameraMan for Windows

## Prerequisites

| Requirement | Notes |
|---|---|
| Windows 11 x64 | The project targets `net10.0-windows10.0.19041.0`. Windows 10 2004+ uses the same APIs but is not the tested target. |
| .NET SDK 10 | Developed with SDK `10.0.401`. `dotnet --info` must list a Windows Desktop SDK. |
| Webcam | Required only for the manual hardware test, not for building or unit tests. |

No Visual Studio workload, Windows App SDK, DirectX SDK, Node.js or third-party runtime is required. Restore pulls the
Windows SDK projection package (`Microsoft.Windows.SDK.NET.Ref`) automatically.

## Build

```powershell
dotnet build CameraMan.Windows.slnx -c Release
```

Expected: `0 Aviso(s) / 0 Erro(s)` (zero warnings, zero errors). The app is emitted to:

```
src\CameraMan.Windows\bin\Release\net10.0-windows10.0.19041.0\CameraMan.Windows.exe
```

Debug build works the same way with `-c Debug`.

## Run from source

```powershell
dotnet run --project src\CameraMan.Windows -c Release
```

The app is a single-instance tray application. Launching it again while it runs only brings the existing camera window
forward.

## Tests

```powershell
dotnet test CameraMan.Windows.slnx -c Release
```

The suite covers pure, hardware-independent logic only: shape geometry, organic blobs, placement math, colour codes,
colour transform, frame buffer, camera cycling, settings serialization/corruption/atomic writes and range clamping.
It does not open a camera.

Expected tail: `Com falha: 0, Aprovado: 101, Ignorado: 0, Total: 101`.

> `dotnet test` writes xUnit failures to stderr. PowerShell may surface them as `NativeCommandError`; read the final
> pass/fail summary rather than the stream name.

## Publish

Self-contained, portable folder (recommended; includes the .NET runtime):

```powershell
dotnet publish src\CameraMan.Windows\CameraMan.Windows.csproj `
  -c Release -r win-x64 --self-contained true `
  -o publish\win-x64
```

Run `publish\win-x64\CameraMan.Windows.exe`. The folder can be copied to another x64 Windows 11 machine; no installer
or runtime is needed. WPF does not support trimming or NativeAOT, so do not add `PublishTrimmed`/`PublishAot`.

Framework-dependent build (smaller, requires the .NET 10 Desktop Runtime):

```powershell
dotnet publish src\CameraMan.Windows\CameraMan.Windows.csproj -c Release -o publish\framework-dependent
```

## Manual hardware test

Run through this list on a machine with a camera. Check off the items you actually observed; the status document
records which of them were verified in this repository.

1. Launch the app. The camera window appears at the last saved position/size; the preview is live and the camera
   indicator light turns on.
2. Open Settings (tray menu, or `Ctrl+,` while the camera window has focus).
   - The Camera page shows the device, "Connected" status and the negotiated format.
   - The device dropdown lists "System default" first, then each camera.
   - Switching to another camera restarts the preview.
3. Visit all nine pages: Camera, Shape, Framing, Image, Border, Window, General, Shortcuts, About. Change at least one
   control per page and confirm the floating window updates live.
4. While Settings is visible, the camera window drops out of topmost and stays behind it. Close Settings; the camera
   returns to topmost.
5. Try each shape, corner radius, soft edge, border (solid and gradient), shadow, zoom/pan, mirror/flip, brightness,
   contrast and saturation.
6. Switch through the five sizes and all four corners. Move a window, switch presets and return: the position is
   remembered per preset and per monitor.
7. Global shortcuts, from another application: `Ctrl+Alt+C` hides the camera (light off) and shows it again;
   `Ctrl+Alt+S` changes size.
8. Tray icon: right-click opens the menu (camera, sizes, edges, slots, settings, restore defaults, quit); double-click
   opens Settings.
9. Close the camera window: it hides and capture stops. Reopen it from the tray or `Ctrl+Alt+C`.
10. Lock Windows or sleep the machine, then resume: capture pauses and resumes with the window.
11. Change the language, restart the app, confirm the UI language changed.
12. Quit from the tray. Reopen: shape, colours, size and camera selection are restored.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| The preview is black | Usually the scene, not the app: a covered lens, privacy shutter or a dark room. Point the camera at a light source. The app pads the window with black when there is no frame. |
| "Camera access is blocked" | Windows privacy settings. Use the button on the Camera page (opens `ms-settings:privacy-webcam`) or Settings â†’ Privacy & security â†’ Camera. |
| Camera busy | Another application has exclusive control. The app first requests shared read-only access and retries once with exclusive control; close the other app if both fail. |
| Nothing happens on `Ctrl+Alt+C` | Another program owns the chord. The Shortcuts page shows a registration error; disable and re-enable the global shortcuts after freeing the chord. |
| Tray icon seems missing | Windows 11 hides new icons in the overflow flyout (the `^` next to the clock). Drag it to the taskbar if you want it visible permanently. |
| Settings were reset | A corrupt `settings.json` is preserved as `settings.json.corrupt-<timestamp>` and defaults are used. Inspect the backup before replacing the file. |
| Global hotkeys or startup do nothing after moving the app | Startup registration is refreshed only while the app runs with the option enabled; toggle it off/on once. Hot keys are per session. |
| App does not start twice | By design: a named mutex and event restore the running instance. |

Unexpected exceptions are appended to `%APPDATA%\CameraMan\error.log`. The log is never uploaded.

## Notes for contributors

- Keep the Windows port independent from the macOS source; do not copy Swift sources into this repository.
- `CameraService` is the only owner of `MediaCapture`. Do not create a second capture session.
- Frames are pulled on `CompositionTarget.Rendering`; publish only complete frames through `FrameBuffer`.
- Read `docs/WINDOWS_APIS.md` before replacing a Win32/WinRT call with another API.
- Never mark a feature as verified in `docs/FEATURE_STATUS.md` without a command result or an observed manual test.
