# Contributing to CameraMan for Windows

Thank you for considering contributing to CameraMan. Below are some guidelines to make the process smooth.

## How to Contribute

- **Bug reports and feature requests:** Open a [GitHub Issue](https://github.com/httpjeffersom/camera-man-windows/issues).
  Include steps to reproduce for bugs, the Windows version and the camera model.
- **Code changes:** Open a Pull Request from a fork. Keep PRs focused (one feature or fix per PR) and reference any
  related issues.
- **macOS changes:** this repository is a fork of
  [camera-man-macos](https://github.com/lucianodiisouza/camera-man-macos) and only contains the Windows port. Send
  macOS fixes to the upstream project.

## Development Setup

1. Clone the repo.
2. Requirements: Windows 11 x64, .NET SDK 10, a webcam for the manual test.
3. Build and run:

```powershell
dotnet build CameraMan.Windows.slnx -c Release
dotnet run --project src\CameraMan.Windows -c Release
```

4. Run the tests (they never open a camera):

```powershell
dotnet test CameraMan.Windows.slnx -c Release
```

## Code and Style

- The project is **C# / WPF** targeting `net10.0-windows10.0.19041.0`. Keep changes consistent with the surrounding
  code and the existing folder structure (`Camera`, `Models`, `Rendering`, `Services`, `ViewModels`, `Views`).
- Do not add NuGet packages for things the .NET runtime already provides. The app is dependency-free on purpose.
- No networking, telemetry, frame recording or media encoding — this is a product guarantee, not a preference.
- Keep the UI strings in both `Strings.resx` (English) and `Strings.pt-BR.resx`.
- `CameraService` is the only owner of `MediaCapture`; frames are pulled on `CompositionTarget.Rendering`.
- Read [docs/WINDOWS_APIS.md](docs/WINDOWS_APIS.md) before replacing a Win32/WinRT call, and keep
  [docs/FEATURE_STATUS.md](docs/FEATURE_STATUS.md) honest: never mark something verified without a command result or an
  observed manual test.

## Pull Request Process

1. Create a branch from `main`.
2. Make your changes; make sure `dotnet build` reports zero warnings and `dotnet test` passes.
3. Update documentation if you change behavior or add features.
4. Open a PR with a clear title and description. Link related issues.
5. Maintainers will review and may request changes.

## Reporting Bugs

Include:

- Windows version and architecture
- Camera model and the format shown on the Camera page
- Steps to reproduce
- Expected vs actual behavior
- The relevant lines of `%APPDATA%\CameraMan\error.log`, if the app logged one

## License

By contributing, you agree that your contributions will be licensed under the same [MIT License](LICENSE) that covers
this project.
