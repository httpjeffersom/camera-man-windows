# Third-party notices

CameraMan.Windows is a native Windows port of the MIT-licensed macOS application **CameraMan**. It reuses the
original project's behaviour, feature design and application icon. The Windows implementation is an independent
codebase; no Swift source is compiled here.

## CameraMan (macOS original)

- Project: https://github.com/lucianodiisouza/camera-man-macos
- License: MIT
- Copyright: Copyright (c) 2025 Camera-Man Contributors
- Reused in this repository:
  - product concept, settings model, shape/organic-blob presets, framing and window behaviour (re-implemented in C#);
  - `src/CameraMan.Windows/Resources/app.png` and `app.ico`, derived from
    `CameraMan/Assets.xcassets/AppIcon.appiconset/icon_256x256.png`;
  - the MIT license text in `LICENSE`.

The full MIT text is reproduced in `LICENSE`, including the port copyright line.

## .NET and Windows platform components

The app targets .NET 10 and uses WPF, Windows Forms (`NotifyIcon` only) and the CsWinRT Windows SDK projection.

- .NET runtime and libraries — MIT, Copyright (c) .NET Foundation and Contributors.
  https://github.com/dotnet/runtime/blob/main/LICENSE.TXT
- C#/WinRT (`WinRT.Runtime`, `Microsoft.Windows.SDK.NET.dll`) — MIT, Copyright (c) Microsoft Corporation.
  https://github.com/microsoft/CsWinRT/blob/master/LICENSE
- Windows SDK metadata and WinRT APIs — provided by Microsoft under the Windows SDK terms; no SDK binaries are
  redistributed beyond the .NET projection assemblies that the .NET SDK itself publishes.
- Segoe Fluent Icons / Segoe MDL2 Assets — system fonts supplied with Windows. The app references the installed font by
  name and does not redistribute font files.

A self-contained publish bundles Microsoft's .NET runtime files. Those files remain under their own licenses; the
`LICENSE` and this notice are copied next to the executable by the project file.

## Build and test dependencies

These packages are used only at build/test time and are not part of the application UI:

| Package | Version | License |
|---|---|---|
| `Microsoft.NET.Test.Sdk` | 17.14.1 | MIT |
| `xunit` | 2.9.3 | Apache-2.0 |
| `xunit.runner.visualstudio` | 3.1.4 | Apache-2.0 |
| `coverlet.collector` | 6.0.4 | MIT |

Their license texts are available from the respective NuGet packages and project repositories.

## Assets not included

- No macOS system effects, Core Image filters, sample videos, screenshots or fonts from the original project are
  bundled, other than the application icon noted above.
- No third-party camera, codec, DirectShow or Media Foundation binary is distributed.
