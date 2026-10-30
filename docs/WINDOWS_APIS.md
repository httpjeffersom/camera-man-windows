# Windows APIs used by CameraMan.Windows

This document lists the platform surface the app depends on, what each call does here, and the constraints behind the
choice. Everything is an official Windows or .NET API. No drivers, codecs, shader toolchains or third-party native
libraries are bundled.

## 1. Camera capture

| API | Where | Purpose |
|---|---|---|
| `Windows.Devices.Enumeration.DeviceInformation.FindAllAsync(DeviceClass.VideoCapture)` | `CameraService.ListDevicesAsync` | Enumerates video capture devices. Each `Id` is passed back to `MediaCapture`; the list is refreshed from the tray/settings and after capture failures. |
| `Windows.Media.Capture.MediaCapture` + `MediaCaptureInitializationSettings` | `CameraService.InitializeCaptureAsync` | Opens the camera. Settings used: `StreamingCaptureMode.Video`, `MediaCaptureMemoryPreference.Cpu`, `MediaCaptureSharingMode.SharedReadOnly`, optional `VideoDeviceId`. An empty device id lets Windows choose the default camera. |
| `MediaCaptureSharingMode.ExclusiveControl` | same | Retry path when a driver cannot do shared access. The app prefers shared read-only so another app can keep the camera open. |
| `MediaCapture.FrameSources` / `MediaFrameSource` | `ChooseColorSource` | Selects the colour source, preferring `VideoRecord` over `VideoPreview`. |
| `MediaFrameSource.SupportedFormats` / `SetFormatAsync` | `ChooseFormat` | Picks a 16:9-ish format at 30 fps up to 1920×1080, preferring 1280×720. A refused format is ignored; the current one stays usable. |
| `MediaCapture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8)` | `CreateReaderAsync` | Asks Media Foundation to deliver straight BGRA8 frames. If the driver rejects the subtype, a plain reader is created and frames are converted per sample. |
| `MediaFrameReader` (`AcquisitionMode.Realtime`, `FrameArrived`, `TryAcquireLatestFrame`) | `CameraService` | Pull-based, newest-frame-only pipeline. `Realtime` drops stale frames instead of queueing them. |
| `Windows.Graphics.Imaging.SoftwareBitmap` + `LockBuffer(BitmapBufferAccessMode.Read)` + `BitmapBuffer.GetPlaneDescription` | `OnFrameArrived` | Gives access to the frame bytes, stride and plane offset. |
| `IMemoryBufferByteAccess` COM interface (`5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D`) | `CameraService.cs` | Raw pointer to the locked buffer. Declared with `[ComImport]`; obtained with CsWinRT's `reference.As<IMemoryBufferByteAccess>()`, because a direct CLR cast to the COM interface fails under the WinRT projection. The wrapper is released with `Marshal.ReleaseComObject` per frame; the buffer itself stays alive through the `IMemoryBufferReference`. |
| `SoftwareBitmap.Convert(..., BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight)` | fallback path | Used only when a driver ignores the requested BGRA8 subtype. The normal path does not convert, because camera frames are opaque and a conversion would add a full-frame copy. |

The capture thread copies/transforms each frame into `FrameBuffer`, a two-slot buffer; the UI thread copies the newest
published frame. A frame error is raised at most once until a frame succeeds again, so a broken driver cannot spam the
UI.

## 2. Preview and shapes (WPF)

| API | Purpose |
|---|---|
| `WriteableBitmap` (`PixelFormats.Bgra32`) | Receives each frame with `WritePixels`; no per-frame allocation once the bitmap exists. |
| `CompositionTarget.Rendering` | Display-rate pull of the newest frame. A slow UI never builds a queue. |
| `DrawingContext`, `StreamGeometry`/`PathGeometry`, `PushClip` | Draws the cover-scaled frame clipped to the current shape. |
| `OpacityMask` + `BlurEffect` + `RenderTargetBitmap` | Soft edge: the inset shape is rendered to a blurred mask bitmap and reused until the shape, size or breathing changes. |
| `LinearGradientBrush` / `SolidColorBrush` | Solid or diagonal-gradient borders. |
| `BlurEffect` on a sibling `Path` | Shadow, drawn behind the preview so the mask does not clip it. |
| `ScaleTransform` with negative scale | Horizontal/vertical mirroring around the framing centre. |
| `DispatcherTimer` (66 ms) | Organic breathing, only while visible, enabled and Reduce Motion is off. |
| `VisualTreeHelper.GetDpi`, `OnDpiChanged` | Re-renders masks at the monitor's DPI. |

Color correction (`ColorTransform`) is deliberately CPU-side: a 256-entry tone table plus integer saturation on the
capture thread. WPF has no supported custom-pixel-shader path without shipping a compiled shader; adding Direct3D
interop for a modest effect was rejected (see `docs/PORTING_PLAN.md`).

## 3. Window management and placement

| API | Purpose |
|---|---|
| `WindowStyle=None`, `AllowsTransparency=true`, transparent `Background` | Frameless camera window. |
| `GetWindowLongPtr`/`SetWindowLongPtr` (`GWL_EXSTYLE`, `WS_EX_TOOLWINDOW`) | Keeps the camera out of the taskbar and Alt+Tab. |
| `SetWindowPos` (`HWND_TOPMOST`/`HWND_NOTOPMOST`, `SWP_NOACTIVATE`) | Applies always-on-top, moves/resizes in physical pixels and shows the window without stealing focus. |
| `WM_NCHITTEST` + `HTLEFT`…`HTBOTTOMRIGHT` | Free resize from the frameless border. Returns `HTCLIENT` inside the shape so drag/resize still work. |
| `WM_EXITSIZEMOVE` | Persists the final physical rectangle and monitor per size preset. |
| `WM_DISPLAYCHANGE`, `WM_DEVICECHANGE` | Re-applies placement after monitor layout/device changes. |
| `MonitorFromWindow`, `MonitorFromPoint`, `GetMonitorInfo` (`MONITORINFOEX`), `EnumDisplayMonitors` | Monitor work areas and device names (`\\.\DISPLAY1`) for multi-monitor placement. |
| `GetDpiForWindow`, `GetDpiForMonitor(MDT_EFFECTIVE_DPI)` (`shcore.dll`) | Converts between DIPs and physical pixels per monitor. WPF's PerMonitorV2 mode handles live scale transitions; `CameraChrome.OnDpiChanged` rebuilds the masks. |
| `DwmSetWindowAttribute` | `DWMWA_USE_IMMERSIVE_DARK_MODE` (20) for the settings title bar and `DWMWA_WINDOW_CORNER_PREFERENCE` (33, `DWMWCP_ROUND`) for native rounded corners. |

Placement values are stored in physical pixels plus the monitor device name. That avoids the classic bug where a
window drifts or lands on the wrong monitor after a DPI or monitor-layout change. `Full` uses the monitor work area
(taskbar excluded), matching the macOS "visible frame" behaviour.

The settings window is a normal WPF window with a native title bar. While it is visible the camera window is
temporarily non-topmost (`EffectiveAlwaysOnTop`), so the window being edited can never be hidden behind the floating
camera. The saved `AlwaysOnTop` preference is untouched.

## 4. Global shortcuts

| API | Purpose |
|---|---|
| `RegisterHotKey(hwnd, id, MOD_CONTROL \| MOD_ALT \| MOD_NOREPEAT, VK_C / VK_S)` | System-wide show/hide (`Ctrl+Alt+C`) and size switch (`Ctrl+Alt+S`). |
| `WM_HOTKEY` | Handled on the placement service's message window. |
| `UnregisterHotKey` | Called when the feature is disabled, on conflict and on exit. |

Registration failures (for example `ERROR_HOTKEY_ALREADY_REGISTERED`, 1409) are collected, both chords are released and
the message is shown on the Shortcuts settings page. The app never leaves a half-registered pair.

## 5. Tray icon

| API | Purpose |
|---|---|
| `System.Windows.Forms.NotifyIcon` | Notification-area icon with tooltip. Used from WPF; this is the only reason `UseWindowsForms` is enabled. |
| `System.Windows.Forms.ContextMenuStrip` / `ToolStripMenuItem` | Native context menu: show/hide, size presets, screen edges, favourite-size slots, settings, restore defaults, quit. Rebuilt when relevant state changes. |
| `System.Drawing.Icon` from the app resource | Tray image. The WinForms high-DPI analyzer warning `WFO0003` is suppressed in the project because WPF owns the process DPI mode and the manifest declares PerMonitorV2. |

Windows 11 may place the icon in the overflow flyout; that is a shell policy, not an app setting.

## 6. Startup, theme and session events

| API | Purpose |
|---|---|
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, value `CameraMan` | "Open at login". Written only after the user enables it, removed when disabled, and refreshed when the executable moves. |
| `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme` | Follows the Windows light/dark app theme. There is intentionally no in-app theme switch. |
| `Microsoft.Win32.SystemEvents.SessionSwitch` | Pauses capture on lock/remote disconnect, resumes on unlock/remote connect. |
| `SystemEvents.PowerModeChanged` | Pauses on suspend, resumes on resume. |
| `SystemEvents.DisplaySettingsChanged` | Re-applies placement after resolution, DPI or monitor changes. |

## 7. Single instance and lifecycle

| API | Purpose |
|---|---|
| `Mutex` named `Local\CameraMan.Windows.Mutex` | Only one instance per session. |
| `EventWaitHandle` named `Local\...Activate` | A second launch signals the running instance, which shows the camera window and exits the new process. |

Shutdown stops the tray icon, unregisters hot keys, saves settings, then disposes the capture on a worker thread
(`Task.Run(...).Wait(5s)`) so no WinRT continuation needs the UI dispatcher while it waits. Camera teardown is
best-effort: a disconnected device must never prevent exit.

## 8. Persistence and accessibility

| API | Purpose |
|---|---|
| `System.Text.Json` with `JsonStringEnumConverter` | `settings.json`, camelCase, indented, versioned. Writes go to `settings.json.tmp` and are moved over the real file (atomic replace). |
| `INotifyPropertyChanged` / WPF data binding | `AppState` is the single source of truth for the settings UI and the camera window. |
| UI Automation properties (`AutomationProperties.Name`, control templates, focus order) | Screen-reader labels for the search box, sidebar buttons, combo boxes and list items, in addition to visible text. |
| `SystemParameters.ClientAreaAnimation` (via `SystemParameters`) | Reduce Motion: disables breathing when Windows animations are off. |

## 9. APIs deliberately not used

| Alternative | Why not |
|---|---|
| Raw Media Foundation COM / `IMFSourceReader` | Large, fragile COM surface. `MediaCapture` already performs device handling, format negotiation and BGRA conversion. |
| DirectShow | Legacy; codec and graph-registration issues on modern Windows. |
| `Windows.Media.Capture.CameraCaptureUI` | Opens a modal capture dialog and returns photos/video; it is not a live preview surface. |
| WinUI 3 / Windows App SDK | Adds deployment/runtime complexity without improving this small always-on-top window. |
| Custom pixel shader (`.fx`/`fxc`) | Not present in the installed SDK and unnecessary for three colour controls; the CPU LUT is predictable and testable. |
| `SetWindowsHookEx` keyboard hook | `RegisterHotKey` is the supported, lower-risk way to get two global chords. |
| `RegisterDeviceNotification` | The app re-enumerates on demand and after capture errors; device notifications would add another lifecycle to keep correct. |

## 10. Privacy properties of the API choices

- `MediaCapture` is configured for video only; no audio stream is opened.
- Frames are copied into a managed buffer and displayed; there is no encoder, file writer, socket or web request in the
  codebase.
- Settings contain only UI preferences and, optionally, the selected camera id. They contain no image data.
- The only outbound actions are user-initiated: opening the Windows camera privacy page and opening the source-code or
  license links from the About page.
