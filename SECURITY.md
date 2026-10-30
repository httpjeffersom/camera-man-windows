# Security Policy

## Supported Versions

We release updates for the latest major version. Security fixes may be backported to previous versions when practical.

| Version | Supported          |
| ------- | ------------------ |
| 1.x     | :white_check_mark: |

## Reporting a Vulnerability

If you discover a security issue in CameraMan for Windows, please report it responsibly:

1. **Do not** open a public GitHub issue for security vulnerabilities.
2. Open a private report through GitHub's **Security → Report a vulnerability** form on
   <https://github.com/httpjeffersom/camera-man-windows>, or contact the maintainer listed in the repository.
   Include:
   - A clear description of the vulnerability
   - Steps to reproduce
   - Impact and possible mitigations (if you have ideas)
3. We will acknowledge your report and work on a fix. We may ask for more details.
4. After a fix is released, we can credit you in the release notes (unless you prefer to stay anonymous).

## Scope

CameraMan for Windows is a local desktop app that uses the camera, stores settings in `%APPDATA%\CameraMan` and
optionally registers itself in the per-user Run key. It has no networking code, no accounts and no telemetry.

In scope:

- unauthorized camera access or capture that survives hiding the window;
- camera frames or settings leaving the machine;
- code execution, privilege escalation or persistence the user did not ask for;
- memory-safety issues in the frame-copy/colour-correction paths.

Out of scope:

- issues that require physical access or an already compromised machine;
- issues in Windows, the .NET runtime, WPF or the camera driver (report those to Microsoft or the vendor);
- the absence of code signing or an installer (tracked as a known limitation, not a vulnerability);
- a local attacker who can already edit files in your user profile.

## Design notes

- The process runs as `asInvoker`; it never requests administrator rights.
- Camera access goes through WinRT `MediaCapture` with the user's Windows camera permission.
- Frames are copied into a reusable buffer and drawn; nothing is encoded, logged or written to disk.
- Settings are plain JSON in `%APPDATA%`; the app validates and clamps every value on load and falls back to defaults
  when the file cannot be read, keeping the bad file as `settings.json.corrupt-<timestamp>`.
- The only registry write is `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\CameraMan`, and only while the
  "open at login" option is enabled.
