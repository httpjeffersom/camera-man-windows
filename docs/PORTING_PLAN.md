# CameraMan.Windows â€” Porting Plan

Analysis of [camera-man-macos](https://github.com/lucianodiisouza/camera-man-macos) (reviewed 2026-10-22, repository
state at `main`) and the plan for the native Windows 11 port.

## 1. Source review

| Item | Finding |
|---|---|
| License | MIT, Copyright (c) 2025 Camera-Man Contributors. Reuse of code, assets and behaviour is permitted with the copyright notice. See `LICENSE` and `THIRD_PARTY_NOTICES.md`. |
| Language / UI | Swift 5.9+, SwiftUI + AppKit, AVFoundation, Core Image, Carbon hot keys. |
| Minimum OS | macOS 14 Sonoma. |
| App model | Menu-bar utility with a floating camera window and a separate settings window. App Sandbox + camera entitlement. No network access. |
| Privacy | Never records or transmits video. Camera frames live in memory only. |
| Localization | English and Brazilian Portuguese (String Catalogs). |

### Files that define the behaviour

- `CameraMan/AppState.swift` â€” every setting, ranges, defaults, persistence, window-preset position memory.
- `CameraMan/AppDelegate.swift` â€” floating window styling, status-bar menu, global shortcuts, size presets, quit/restore.
- `CameraMan/CameraManApp.swift` â€” main camera window scene.
- `CameraMan/Services/CameraService.swift` â€” discovery, device switching, start/pause/resume, permission handling.
- `CameraMan/Services/GlobalHotKeys.swift` â€” system-wide Carbon hot keys.
- `CameraMan/Services/KeyboardShortcutMonitor.swift` â€” camera-window keys.
- `CameraMan/Views/CameraView.swift` â€” shape mask, soft edge, border, shadow, breathing, status states.
- `CameraMan/Views/Components/CameraPreviewRepresentable.swift` â€” GPU preview layer and Core Image color controls.
- `CameraMan/Models/ShapeType.swift`, `OrganicBlob.swift` â€” outline geometry and organic presets.
- `CameraMan/Views/Settings/*` â€” nine-page settings window with sidebar search and live previews.

## 2. Confirmed feature inventory and Windows mapping

Status vocabulary: **Implemented** = functional code exists; **Adapted** = equivalent Windows behaviour with a
documented platform difference; **Not applicable** = the concept does not exist on Windows; **Pending** = not built.

### 2.1 Camera and floating window

| Original feature | Original implementation | Windows implementation | Status |
|---|---|---|---|
| Enumerate video devices | `AVCaptureDevice.DiscoverySession` (built-in + external) | `Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(DeviceClass.VideoCapture)`; details via `MediaFrameSource` | Implemented |
| Select camera | `AVCaptureSession` input switch | `MediaCapture.InitializeAsync` with `VideoDeviceId`, frame source selection | Implemented |
| Real-time preview | `AVCaptureVideoPreviewLayer` (GPU) | `MediaFrameReader` + `SoftwareBitmap` (BGRA8) â†’ `WriteableBitmap` â†’ WPF composition | Adapted (one CPU copy per frame) |
| Camera "best mode" | `.high` session preset | Chooses 16:9, 30 fps, â‰¤1920Ã—1080, preferring 1280Ã—720; falls back to next best source format | Implemented |
| Missing camera state | `no camera found` overlay | `Video slash` icon + message + "Refresh" + privacy settings link | Implemented |
| Permission denied | macOS TCC + `System Settings` deep link | Catch `UnauthorizedAccessException` from `MediaCapture`; link to `ms-settings:privacy-webcam` | Implemented |
| Pause when hidden | `session.stopRunning()` | Stop frame reader and dispose `MediaCapture`; camera indicator light turns off | Implemented |
| Pause when screen locks / sleeps | `NSWorkspace` notifications | `SystemEvents.SessionSwitch` (lock/unlock) and `PowerModeChanged` (suspend/resume) | Implemented |
| Device disconnected | discovery refresh on menu open | Capture error â†’ status â†’ re-enumerate; refresh from tray/settings | Implemented |
| Frameless, transparent window | `NSWindow` with hidden title bar, clear background | WPF `WindowStyle=None`, `AllowsTransparency=true`, transparent background, custom hit testing | Implemented |
| Drag by background | `isMovableByWindowBackground` | WPF `DragMove()` from the camera surface; buttons remain clickable | Implemented |
| Resize | macOS window frame | Win32 `WM_NCHITTEST` resize borders + free resize; size remembered per preset | Adapted |
| Always on top | `window.level = .floating` | `Topmost` / `SetWindowPos(HWND_TOPMOST)` with a user setting (default on) | Implemented |
| Does not steal focus when shown | `orderFrontRegardless` | `ShowActivated=false` and `SWP_NOACTIVATE` when shown from tray/hotkey | Implemented |
| Follows all desktops / full-screen apps | `.canJoinAllSpaces`, `.fullScreenAuxiliary` | Topmost + tool window; **does not** overlay exclusive-fullscreen DirectX apps (Windows limitation) | Adapted |
| Hide/show with camera off | `toggleCamera` orders window out and pauses | Tray/hotkey hides and disposes capture | Implemented |
| Close hides instead of quitting | `windowShouldClose` | `Closing` cancels and hides; quit only from tray menu / Ctrl+Q in menu | Implemented |

### 2.2 Shapes, framing and look

| Original feature | Original implementation | Windows implementation | Status |
|---|---|---|---|
| Circle | Centred circle mask | WPF ellipse geometry clip/mask | Implemented |
| Organic outline | Radius harmonics `r(Î¸)=1+Î£aÂ·cos(kÎ¸+Ï†)`, 240 samples | Same formula, 5 presets, shuffle, optional breathing at 15 fps | Implemented |
| Square | Rounded square | Rounded rectangle with clamped radius | Implemented |
| Vertical 9:16 | Aspect-fit rounded rectangle | Same aspect-fit math, tested | Implemented |
| Horizontal 16:9 | Aspect-fit rounded rectangle | Same aspect-fit math, tested | Implemented |
| Corner radius | 0â€“150 pt | 0â€“150 DIP, clamped to half the short side | Implemented |
| Soft edge | Inset mask + Gaussian blur | Inset geometry rendered to a blurred mask bitmap, used as WPF opacity mask | Implemented |
| Aspect-fill preview | `.resizeAspectFill` | Manual cover-fit drawing; never distorts | Implemented |
| Framing offsets | âˆ’50â€¦50 % of window | Same, arrows adjust 1 % | Implemented |
| Zoom | 1.0â€“2.0Ã— | Same, `+`/`âˆ’` adjust 0.1 | Implemented |
| Mirror / flip | `scaleEffect(-1)` | Negative-scale transform around centre | Implemented |
| Brightness / contrast / saturation | `CIColorControls` on the GPU | CPU lookup table + fixed-point saturation pass, only when non-neutral; **documented difference** (no GPU shader dependency) | Adapted |
| Border | Stroke on clip shape, solid or linear gradient | WPF `Path` stroke, solid or diagonal `LinearGradientBrush` | Implemented |
| Shadow | Blurred outline stroke behind preview | Blurred outlined `Path` behind preview (`BlurEffect`) | Implemented |
| Soft edge hides border | Hard line would fight the feathered edge | Same rule | Implemented |
| macOS Portrait / Studio Light / backgrounds | Provided by macOS; app only explains how to enable | **Not available through a stable public Windows API.** Documented; no simulated replacement. | Not applicable |
| Custom masks beyond the five shapes | Not present in original (old custom shapes removed in 1.1.0) | Not present | â€” |

### 2.3 Window sizes and placement

| Original feature | Original implementation | Windows implementation | Status |
|---|---|---|---|
| Five sizes | xs 200, sm 300, md 400, lg 560, full = visible frame | Same DIP sizes; full = monitor work area | Implemented |
| Snap to four corners | `screenEdge` per preset | Same against the monitor work area | Implemented |
| Each size remembers position | `originForPreset` in `UserDefaults` | Per-preset origin in JSON; monitor device name + physical pixels for multi-monitor/DPI accuracy | Implemented |
| Free move | system window drag | `WM_EXITSIZEMOVE` saves physical rect and monitor | Implemented |
| Free resize | system resize | Resize borders; stores custom width/height for the active preset | Adapted |
| Space toggles two favourite sizes | `spaceSlot1/2` | Same, in camera window and tray menu | Implemented |
| Multi-monitor | `NSScreen` | `MonitorFromWindow`/`GetMonitorInfo` + per-monitor DPI (`GetDpiForMonitor`) | Implemented |

### 2.4 Application experience

| Original feature | Original implementation | Windows implementation | Status |
|---|---|---|---|
| Global show/hide | âŒƒâŒ¥âŒ˜C via Carbon | `RegisterHotKey` Ctrl+Alt+C; on/off setting; conflict reported in Settings | Adapted (Windows reserves Win-key chords) |
| Global switch size | âŒƒâŒ¥âŒ˜S | Ctrl+Alt+S | Adapted |
| Camera-window keys | arrows, +, âˆ’, R, /, V, O, Backspace, Space | Identical via WPF `PreviewKeyDown` | Implemented |
| Menu-bar icon + menu | `NSStatusItem` + `NSMenu` | `NotifyIcon` + native `ContextMenuStrip`; dynamic camera/size/edge/slot menus; double-click toggles | Adapted |
| Settings window | Nine pages, sidebar search, System Settings style | WPF window, same nine pages, searchable sidebar, grouped cards, native controls | Implemented |
| Live shape previews | SwiftUI shape previews | WPF geometry previews generated by the same shape math | Implemented |
| Persist settings | `UserDefaults` | `%APPDATA%\CameraMan\settings.json`, atomic writes, debounced | Implemented |
| Restore defaults + confirmation | `NSAlert` | WPF confirmation dialog, then reset + reapply | Implemented |
| Open at login | `SMAppService.mainApp` | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` value `CameraMan`, only when the user enables it | Adapted |
| Hide Dock icon | activation policy accessory | **Not applicable**: Windows has no Dock. The camera is a tool window (never in the taskbar); the tray icon always exists. | Not applicable |
| Open Settings shortcut | âŒ˜, | Ctrl+, in the camera window menu/tray | Adapted |
| Quit | âŒ˜Q | Tray â†’ Quit CameraMan; Ctrl+Q in menu | Implemented |
| English + pt-BR | String Catalogs | `.resx` resources, language selector (System/English/PortuguÃªs), restart notice | Implemented |
| Single instance | macOS app model | Named mutex + restore of the running window | Implemented |
| Toasts for device switch | translucent capsule | Rounded toast overlay, fades after 2 s | Implemented |
| Reduce Motion | `accessibilityReduceMotion` | Honors Windows `ClientAreaAnimation` system setting; disables breathing | Implemented |
| Accessible controls | SwiftUI semantics | WPF automation properties, keyboard focus and text-labelled sliders. Custom light/dark palettes do **not** follow Windows High Contrast mode (documented limitation). | Adapted |
| No network / no recording | Sandbox | No network or media-writing code; documented in README/privacy | Implemented |

## 3. Architecture

```
CameraMan.Windows.slnx
â”œâ”€ src/CameraMan.Windows
â”‚  â”œâ”€ App.xaml(.cs)                 composition root, lifecycle, single instance
â”‚  â”œâ”€ Models/                       settings, shapes, presets, pure math
â”‚  â”œâ”€ Camera/                       WinRT MediaCapture wrapper + frame buffers
â”‚  â”œâ”€ Services/                     coordinator, settings store, hot keys, tray, startup, placement
â”‚  â”œâ”€ ViewModels/                   AppState (observable settings + commands), settings search
â”‚  â”œâ”€ Views/                        CameraWindow, SettingsWindow, nine pages
â”‚  â”œâ”€ Views/Controls/               camera surface, settings kit, shape preview, slider
â”‚  â”œâ”€ Interop/                      Win32/PInvoke declarations, DWM, window hit testing
â”‚  â””â”€ Resources/                    icon, resx strings, theme dictionaries
â”œâ”€ tests/CameraMan.Windows.Tests    xUnit tests for non-hardware logic
â””â”€ docs/                            this plan, APIs, build, feature status
```

- **MVVM, pragmatically applied.** `AppState` is the single observable settings/state object. Pages bind to it.
  Window-specific behaviour (placement, tray, hot keys) stays in services; no empty abstractions.
- **One capture owner.** `CameraService` owns MediaCapture/MediaFrameReader lifecycles. `CameraCoordinator` serializes
  start/stop/switch requests, maps failures to UI status, and reacts to window visibility and session events. No
  duplicate sessions are possible; a `SemaphoreSlim` guards transitions.
- **One copy per frame.** MediaCapture converts to BGRA8; the frame reader copies into a reusable ring buffer. The UI
  pulls the latest frame in `CompositionTarget.Rendering`, so a slow UI never queues frames and no per-frame
  allocation occurs after the buffers are sized.
- **Shape rendering.** Geometry is shared by the live window and the settings previews. A custom
  `CameraShapeSurface` draws the image with a clip or a pre-rendered soft mask. Border and shadow are sibling paths so
  they are not affected by the mask. Breathing regenerates only the mask, at 15 fps, when enabled and visible.
- **Persistence.** `AppSettings` (System.Text.Json records) â†” `AppState`. Writes are atomic (temp file + move) and
  debounced; flushing happens on exit. Tests cover round-tripping and corrupt-file fallback.

## 4. Technology decisions

| Choice | Reason |
|---|---|
| .NET 10 (SDK 10.0.401 found in environment) | Current LTS available; Windows Desktop runtime present. |
| WPF | Required by the brief, mature accessibility/DPI support, hardware-composited visuals, no WinUI deployment overhead. |
| `Windows.Media.Capture` + `MediaFrameReader` (WinRT) | Official Windows capture stack, device enumeration, format negotiation and BGRA conversion handled by Media Foundation; far less fragile than hand-written MF COM interop or DirectShow. Works in unpackaged desktop apps. |
| WPF `WriteableBitmap` | Direct, zero-copy-friendly upload of the BGRA buffer; enough for a 720p/30 preview while keeping complexity low. |
| Win32 (`RegisterHotKey`, `SetWindowPos`, `WM_NCHITTEST`, monitors, DWM) | Official APIs for global shortcuts, topmost behaviour, free resize and per-monitor DPI. |
| `NotifyIcon` (WinForms component) | Official, maintained tray API; avoids a third-party dependency. |
| System.Text.Json | No external dependency; source-friendly. |
| xUnit | Requested; tests run without a camera. |
| No Direct2D/Vortice dependency | The only GPU-heavy operations (clip, border, blur, composition) are native WPF. Color correction is a CPU LUT; adding a D3D interop layer would be a large risk for a modest gain. Documented. |

## 5. Deliberate differences and limitations

1. **Global shortcut chords.** Windows reserves most `Win`-key combinations; Ctrl+Alt+C / Ctrl+Alt+S are the closest
   reliable equivalents. Conflicts with another app are detected and surfaced.
2. **Over exclusive-fullscreen apps.** A topmost Win32 window is hidden by exclusive DirectX fullscreen. Borderless
   fullscreen (the common case) works. macOS can float over fullscreen spaces.
3. **Video effects.** Portrait, Studio Light and background replacement are macOS system effects with no stable public
   Windows equivalent usable inside a third-party preview. Not simulated.
4. **Color correction** runs on the CPU with a precomputed LUT to avoid a shader toolchain dependency. It is only
   executed when values differ from neutral.
5. **Tray-only mode.** Windows has no Dock; the equivalent concept does not apply. The camera window uses
   `WS_EX_TOOLWINDOW` and never appears in the taskbar.
6. **Settings visual language.** System Settings-like grouping, icons and search are reproduced in WPF; the window
   keeps a native title bar for accessibility and reliable window management.
7. **App Sandbox / notarization.** Windows uses standard user-level installation; no sandbox equivalent is claimed.
   No drivers, codecs or proprietary components are bundled.

## 6. Verification checklist

- [x] Solution builds with zero errors and zero warnings (`dotnet build CameraMan.Windows.slnx -c Release`).
- [x] 101 unit tests for settings persistence, geometry, organic blob, color correction, frame buffer, placement math
      and settings page layout.
- [x] Camera enumeration/start/stop and error mapping implemented; manual hardware test observed live 1920Ã—1080 30 fps
      frames and a working device picker.
- [x] Frameless/topmost/draggable/resizable camera window; settings suppresses topmost and closing restores it.
- [x] Shape masks: circle, organic, square, 9:16, 16:9; soft edge; border; shadow.
- [x] Framing: offsets, zoom, mirror, flip.
- [x] Settings persist and restore; corrupt files fall back to defaults.
- [x] Settings pages share a left-aligned content column; the left edge and width stay fixed when a scrollbar appears
      (verified at 760, 900 and 1200 px widths, plus layout tests).
- [x] Global hot keys register/unregister; `Ctrl+Alt+C` and `Ctrl+Alt+S` were exercised end-to-end.
- [x] Tray icon present; menu mirrors all commands (menu itself not automated in the last pass).
- [x] Open-at-login is explicit and reversible.
- [x] Self-contained win-x64 publish verified (launched from `publish\win-x64`, live preview and settings confirmed).
- [ ] Automated hardware integration test (manual procedure in `docs/BUILDING.md`).
- [ ] Signed installer (out of scope; publish output is provided).

Feature-by-feature status and the exact manual observations are recorded in `docs/FEATURE_STATUS.md`. Build and
publish commands are in `docs/BUILDING.md`; the API inventory is in `docs/WINDOWS_APIS.md`.
