# Feature status

Status vocabulary:

- **Verified** â€” implemented and observed working in this environment (manual test or command output recorded below).
- **Implemented** â€” code path exists and compiles; not yet exercised end-to-end here.
- **Adapted** â€” works with a documented Windows-specific difference.
- **Not applicable** â€” the macOS concept has no Windows equivalent.
- **Not built** â€” deliberately out of scope.

Verification environment: Windows 11 x64, .NET SDK 10.0.401, build `Release`, 84 unit tests passing (2026-10-23).

## 1. Camera

| Feature | Status | Notes / evidence |
|---|---|---|
| Enumerate cameras | Verified | Camera page listed `GENERAL WEBCAM` and a "System default" entry. |
| Select camera | Verified | Selection is saved and passed to `MediaCapture`; "System default" leaves the device id empty so Windows chooses. |
| Live preview | Verified | Frames observed at 1920Ã—1080 Â· 30 fps; a face and then a lit room were visible in the floating window. A frame probe reported 1920Ã—1080, stride 7680, with the version counter advancing. |
| Format negotiation | Verified | Active format reported as `1920Ã—1080 Â· 30 fps`. |
| Permission denied handling | Implemented | `UnauthorizedAccessException` maps to a status overlay with a button to `ms-settings:privacy-webcam`. Not triggered on this machine (permission already granted). |
| No camera found | Implemented | Status overlay with Refresh, plus the camera privacy link. |
| Camera busy / disconnected | Implemented | Shared-then-exclusive retry, error mapping, re-enumeration from the UI. Unit-tested cycling logic only. |
| Pause when hidden | Verified | Hiding the camera stops the stream; the hotkey toggle was observed hiding/showing the window. |
| Pause on lock / sleep | Implemented | `SessionSwitch` and `PowerModeChanged` handlers exist. Not exercised in this session. |
| Next camera (`Backspace`) | Implemented | Cycling logic unit-tested; keyboard path not exercised. |
| No recording / no network | Verified by inspection | No encoder, file-writing of frames, socket or HTTP client in the codebase. |

## 2. Floating window

| Feature | Status | Notes / evidence |
|---|---|---|
| Frameless transparent window | Verified | Captured screenshots show only the shaped camera, no chrome or background rectangle. |
| Drag to move | Verified (indirect) | `DragMove` path exercised during automation clicks; no exception. |
| Free resize | Implemented | `WM_NCHITTEST` resize border; not resized manually in this session. |
| Always on top | Verified | Window observed above other applications; `WS_EX_TOPMOST` reported true. |
| Settings suppresses topmost | Verified | While Settings was visible the camera reported `topmost=False` and was drawn behind it; after closing Settings it returned to `topmost=True`. |
| Does not steal focus when shown | Implemented | `ShowActivated=false` + `SWP_NOACTIVATE`. Tray/hotkey show path not instrumented for focus. |
| Close hides, tray/hotkey brings back | Verified | `Ctrl+Alt+C` hid and restored the window; close-to-hide path is the same `HideRequested` handler. |
| Five sizes | Verified | `Ctrl+Alt+S` changed the window from 300Ã—300 to 400Ã—400. |
| Corner snapping | Verified | Window was placed at the bottom-right work area; settings file holds the per-preset origin. |
| Per-size position memory | Implemented | JSON contains `placements` with physical origins, monitor name and custom sizes; restore path not re-tested across launches here. |
| Multi-monitor / mixed DPI | Implemented | Monitor and per-monitor DPI calls are present; this machine used one monitor. |
| Space toggles two sizes | Implemented | Same code path as the size hotkey, different target preset. |
| Over exclusive-fullscreen DirectX | Adapted (limitation) | Topmost Win32 windows cannot cover exclusive fullscreen. Borderless fullscreen works. |
| Follows macOS Spaces | Not applicable | Windows virtual desktops do not expose an equivalent per-window API; the window lives on its desktop. |

## 3. Shapes and look

| Feature | Status | Notes / evidence |
|---|---|---|
| Circle / organic / square / 9:16 / 16:9 | Verified | Organic and circle rendering observed on screen and in Settings previews. Cards restyled after the macOS reference: 110 px tiles, radius 16, shape filling the frame, solid white person silhouette (vector `person.fill`-style head + dome shoulders, anchored to the bottom), detached `#EF4444` selection ring (floats 1 px outside, 3 px thick), 1 px white 8% edge (25% on hover) and bold label when selected. Geometry math covered by tests. |
| Organic blob presets + shuffle | Verified | `OrganicBlob` math unit-tested; chips restyled to match the macOS reference (flat purple silhouettes on 6%-white 56 px tiles, `#EF4444` selection ring, labels below, shuffle glyph last); selection and shuffle exercised on screen. |
| Breathing | Verified | Enabled in the running profile; the timer path rebuilds the mask. Reduce Motion disables it. |
| Corner radius (per shape) | Verified | Corner radius is per shape: Square/Vertical/Horizontal each store their own value (`squareCornerRadius`/`verticalCornerRadius`/`horizontalCornerRadius` in `settings.json`; older files migrate from the shared `shapeCornerRadius`). The "Raio dos cantos" row only appears for the square and the two rectangles (hidden entirely for Circle/Organic). The slider edits the active shape's value, the preview cards render each shape's own radius and the floating window renders the active shape's radius (Horizontal 16:9 observed with 60 pt rounding; Circle with no row). Clamped 0-150, math covered by tests. |
| Soft edge | Verified | Feather mask in `CameraChrome.BuildSoftMask` + `Rendering/SoftMaskField`: the shape is rasterised at half device resolution with an 8 px margin, the distance to the outline is measured for every pixel with a two-pass chamfer transform (seeded from the empty pixels around the shape) and mapped linearly to alpha â€” 0 exactly on the contour, rising with the pixel distance to full opacity at SoftEdge px inward. Because the fade is driven by the true distance to the contour, its width stays constant along every shape: straight sides, rounded corners, circle curvature and the organic blob waves all keep the same band (the corner of a square gets the correct quarter-disc falloff). The margin matters: when the outline runs along the window edge (circle tangencies, rectangle sides, blob crests) the outside pixels still exist and seed the field, so the fade never collapses into a hard edge there. The mask is pushed with `DrawingContext.PushOpacityMask` using an absolute viewport pinned to the element's layout box â€” `UIElement.OpacityMask` sizes its brush by the *content* box, and the cover-scaled frame overflows the element, which used to stretch the mask off the shape's sides. Covered by `SoftMaskFieldTests` (linear half-plane ramp, constant half-alpha depth in eight directions around a disc, fully transparent outside, monotonic inward growth). Verified on screen with the real camera on all five shapes at SoftEdge 30. Platform difference vs macOS: macOS pads its window by `softEdge` and blurs outward (fade extends outside the shape); the Windows port fades inward only, so the fade always stays inside the window (tighter visible silhouette). Debug masks/white-frame instrumentation removed from the final build. |
| Cover-fit preview | Verified | No distortion observed in the 16:9 stream inside a circular window. |
| Zoom / pan / mirror / flip | Implemented | Math and keyboard handlers present; not individually exercised in this session. |
| Brightness / contrast / saturation | Verified (path) | Current profile uses non-neutral brightness; frame path applied it without artifacts. Unit-tested LUT. |
| Border (solid/gradient) | Verified | Stroke centred on the contour, exactly like the macOS `shape.stroke` (half inside, half outside the camera), with the padding reserving room for the outer half. Verified on screen: 20 px solid square (10 px over the camera + 10 px in the padding), 8 px circle measured at the tangency points, 6 px tealâ†’pink gradient on the 9:16 pill (top-leading to bottom-trailing, same direction as macOS), and 6 px solid on the organic blob **with breathing on** (full ring, outline follows the animation; measured 4 681 blue pixels spanning the whole blob, and the bounding box shifts between frames). Three defects found and fixed: (1) the stroke lives outside the `Path`'s layout slot, so WPF's automatic layout clip cut the outer half wherever the contour touched the element bounds â€” the outlines now sit in `Canvas` layers (no desired size, no clipping) with a slot inflated by the overflow and offset so the contour stays on the chrome bounds; (2) `SizeChanged` only rebuilt the surface, so resizing the window (or toggling the border, which changes the padding) left the border/shadow on the previous geometry â€” both outlines are rebuilt now; (3) the organic outline was built with every segment flagged `isStroked: false`, so it filled the camera but drew no border at all (only a stray sliver from the closing segment) â€” the segments are strokable now and `ShapeGeometryTests` widens every shape outline to prove it (the test fails on the old flag). Windows refinement over macOS: the padding is capped so it can never eat the whole camera (`BorderPaddingToThicknessConverter` keeps at least 48 px of shape), because a 120 px window with a 20 px border and a 24 px shadow used to collapse to a stray fragment; verified at that extreme (small camera with a thick ring instead of nothing). |
| Shadow | Verified | Same construction as macOS: a sibling stroke of `borderWidth + 2Â·radius` behind the camera, blurred by `BlurEffect` (Gaussian), colour with alpha honoured (`#80000000` renders 50 % black over the wallpaper). The same layout-clip defect squared off the halo at the tangencies and is fixed with the inflated slot (extent = stroke/2 + radius); the organic `isStroked` defect also removed its halo and is fixed by the same change. Verified on screen with radius 12 on circle, square, 9:16 pill and organic blob (halo follows every outline, four sides, no clipping at the window edge), and with the border off (`stroke = 2Â·radius`). `BorderPadding = borderWidth + 2Â·shadowRadius` matches macOS and leaves room for the halo. Same macOS quirk as the original: with a soft edge the blurred shadow shows through the feathered band. Colour picking keeps the stored alpha (`ColorCode.WithChannelsOf`, covered by tests), so a hue change does not turn the 50 % default into a solid shadow; there is no separate opacity control (see Â§7). |
| Soft edge hides border | Verified | `showBorder` is false whenever `SoftEdge > 0` (macOS `softEdge == 0` rule); the shadow stays. Confirmed on screen: organic + SoftEdge 30 shows no border and a full halo. The shape page shows the same footer note as macOS ("A borda fica oculta enquanto a borda suave estiver ativa"). |
| Portrait / Studio Light / backgrounds | Not applicable | macOS system effects; no stable public Windows equivalent. Explained in the UI, not simulated. |

## 4. Settings

| Feature | Status | Notes / evidence |
|---|---|---|
| Nine pages | Verified | Camera, Shape, Framing, Image, Border, Window, General, Shortcuts and About all rendered. |
| Sidebar navigation | Verified | UI Automation invoked the sidebar buttons; pages switched. |
| Search (page + row titles) | Implemented | Matching logic present; opens the page but does not scroll to the matching row (known limitation). |
| Live previews in pages | Verified | Camera page and About page rendered; shape previews share the window geometry factory. |
| Persist to `%APPDATA%\CameraMan\settings.json` | Verified | File exists and contains the current shape, border colour, size and placements. |
| Atomic writes + debounce | Verified by tests | `SettingsStoreTests` cover round-trip, corruption fallback and atomic replacement. |
| Corrupt file recovery | Verified by test | Bad JSON is preserved as `settings.json.corrupt-<timestamp>`. |
| Restore defaults | Implemented | Confirmation dialog then reset; not exercised. |
| Language English/pt-BR + System | Verified | Combo listed System/English/PortuguÃªs (Brasil); UI currently in pt-BR. Change requires restart. |
| Accessible names | Verified | Fixed during this pass: combo boxes and list items exposed readable names, sidebar buttons and the search box are named for screen readers. |
| Native title bar + dark mode | Verified | Settings title bar rendered with the Windows dark theme. |
| Sidebar layout | Verified | Rows share the same left edge and uniform pitch (36 px rows, 2 px spacing, 16 px group gap); glyphs lifted 1 px for optical centering inside the 24 px tiles; the selected row keeps the accent colour on hover; search results align left under their page. |
| Page content alignment | Verified | All pages share the same content column (max 640 px), aligned to the left; the left edge and width stay fixed when a page's scrollbar appears. Layout covered by `SettingsPageFrameTests`. |

## 5. System integration

| Feature | Status | Notes / evidence |
|---|---|---|
| Tray icon | Verified | Icon present in the notification overflow with tooltip "CameraMan - cÃ¢mera flutuante". |
| Tray menu commands | Implemented | Menu is built from the same actions as the hotkeys/settings. Automation of the shell overflow flyout failed, so the menu was not clicked in this session. |
| Global `Ctrl+Alt+C` | Verified | Toggled camera visibility hidden â†’ visible. Registration held (external attempt returned 1409). |
| Global `Ctrl+Alt+S` | Verified | Changed the window from 300Ã—300 to 400Ã—400. |
| Conflict reporting | Implemented | Both chords are released and an error string is surfaced on the Shortcuts page. |
| Camera-window keys | Implemented | Handlers exist for arrows, +/-, R, /, V, O, Backspace, Space. Only the global chords were exercised. |
| Open at login | Implemented | Per-user Run key, explicit opt-in, reversible. Not toggled in this session. |
| Single instance | Implemented | Named mutex + activation event. Not exercised (one instance was used). |
| Theme follows Windows | Verified | Dark palette active, matching the OS. |
| Reduce Motion | Implemented | Mirrors `SystemParameters.ClientAreaAnimation` at startup and when Windows changes it, disabling breathing. Not toggled during this pass. |
| Toast for device switch | Implemented | Overlay exists; not exercised. |

## 6. Build, test and distribution

| Item | Status | Evidence |
|---|---|---|
| Release build | Verified | `dotnet build CameraMan.Windows.slnx -c Release`: 0 warnings, 0 errors. |
| Unit tests | Verified | 101 passed, 0 failed, 0 skipped (`dotnet test CameraMan.Windows.slnx -c Release`). |
| Self-contained win-x64 publish | Verified | See `docs/BUILDING.md`; publish output launched and previewed the camera. |
| Installer / code signing | Not built | Out of scope. The publish folder is portable; SmartScreen may warn for an unsigned executable. |
| Auto-update | Not built | Not in the macOS original and not requested. |

## 7. Known limitations and differences

1. **Exclusive fullscreen.** A topmost window cannot be drawn over an exclusive DirectX fullscreen app.
2. **Video effects.** Portrait, Studio Light and background replacement are not available; no fake replacement is
   offered.
3. **CPU colour correction.** Three controls are applied with a lookup table on the capture thread instead of a GPU
   shader. Documented in `docs/WINDOWS_APIS.md`.
4. **Language change needs a restart.** The UI is already built when the setting changes; a toast explains it.
5. **Settings search.** Finds pages and rows by title, opens the page, but does not scroll to the row.
6. **Tray overflow.** Windows 11 may hide the icon under the chevron; the app cannot force a pinned position.
7. **Full size uses the work area.** On Windows the taskbar is excluded; macOS uses the screen's visible frame.
8. **Single monitor tested.** Multi-monitor and per-monitor DPI code is present but was not exercised on this machine.
9. **High Contrast mode.** The app follows the Windows light/dark theme but uses its own palette; it does not adopt
   Windows High Contrast colours. Screen-reader names and keyboard navigation are provided.
10. **Unsigned binary.** A portable publish is provided; no installer, signature or notarization.
11. **Settings scrollbar.** Pages use the default WPF scrollbar; it is light-coloured and 17 px wide in dark mode.
12. **Border/shadow padding on tiny windows.** The macOS padding (`borderWidth + 2·shadowRadius`) is kept, but it is
    capped on Windows so a small window keeps at least 48 px of camera; macOS lets the padded content collapse
    instead. At that extreme the halo is cut by the window edge.
13. **Shadow opacity.** There is no opacity control in the UI. Picking a colour keeps the alpha already stored for
    the setting (the WinForms colour dialog has no alpha channel, unlike the macOS colour panel), so the 50 % black
    default shadow stays soft.

## 8. Pre-publish review

Security, architecture and bug review done before publishing the fork (2026-10-30). Fixed in this pass:

| Area | Finding | Fix |
|---|---|---|
| Z-order | Moving or resizing the camera re-applied the *saved* topmost preference, so dragging a border/shadow slider while Settings was open pushed the camera back above it. | `WindowPlacementService.ApplyCurrentPreset` uses `EffectiveAlwaysOnTop` for both `SetWindowPos` and `Window.Topmost`. |
| Settings load | A hand-edited file with duplicate placement keys (`md` and `MD`) threw in `ToDictionary` and the app exited. | `AppSettings.Normalize` rebuilds the map with canonical keys (case-insensitive match); `AppState` merges defensively with first-key-wins. |
| Organic blob | A `null` wave crashed the clone at startup; extreme amplitude/harmonic/phase values made the outline degenerate or slow. | `Normalize` drops nulls, caps the wave count at 8 and clamps `k` 1–8, amplitude ±0.25 and phase ±2π. |
| Settings load | Only I/O and JSON exceptions were caught, so other malformed shapes could still stop startup. | The loader falls back to defaults for any non-fatal exception and preserves the bad file. |
| Diagnostics | `error.log` grew without a limit. | Rotated to `error.log.1` past 256 KB. |
| Manifest | The executable level relied on the .NET default. | Explicit `requestedExecutionLevel level="asInvoker"`. |
| Tray docs | `docs/BUILDING.md` said double-click toggles the camera; the code opens Settings. | Documentation corrected to match the shipped behaviour. |

Checked and found sound: no HTTP/socket/telemetry code; frames never leave memory; P/Invoke limited to
`user32`/`shcore`/`dwmapi` with fixed signatures and marshalled structs; only `HKCU` is written (Run key, opt-in);
settings are validated and clamped on load; the single-instance mutex/event live in the per-session `Local\` namespace.

Still open (not fixed here): multi-monitor/mixed-DPI and lock/sleep paths need a real second monitor and a hands-on
pass; settings search does not scroll to the matching row; there is no installer or signature.

## 9. Not implemented, on purpose

- Recording, streaming, virtual-camera output (also absent from the original).
- Custom masks beyond the five shapes (removed from the original in 1.1.0).
- A Dock icon, macOS Spaces behaviour, or macOS system video effects.
- Any cloud, analytics or auto-update component.
