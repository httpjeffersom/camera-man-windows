# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

This repository is the Windows port of [CameraMan for macOS](https://github.com/lucianodiisouza/camera-man-macos).
Version numbers are independent from the macOS app.

## [Unreleased]

- (Add new changes here before releasing)

## [1.0.0] - 2026-10-30

First Windows release: a native C#/WPF port of CameraMan, published as a fork of the macOS project.

### Added

- Live camera preview in a frameless, transparent, always-on-top window, with cover-fit framing, zoom (1–2×), panning
  offsets and horizontal/vertical mirroring.
- Five shapes: circle, organic blob (five presets, shuffle, breathing), rounded square, vertical 9:16 and horizontal
  16:9, with per-shape corner radius.
- Soft edge (distance-field feather), solid or gradient border, blurred shadow.
- Brightness, contrast and saturation applied to the frames (CPU lookup table).
- Five window sizes, corner snapping, per-size and per-monitor position memory, mixed-DPI aware placement.
- Nine-page settings window with sidebar, search, live previews, English and Brazilian Portuguese.
- Notification-area icon with show/hide, size, corner, slot, camera, always-on-top, restore defaults and quit.
- Global `Ctrl+Alt+C` / `Ctrl+Alt+S` shortcuts, `Ctrl+,` for settings, `Ctrl+Q` to quit and in-window framing keys.
- Pause on hide, session lock and sleep; single-instance activation.
- Settings persisted as JSON with atomic replacement, validation and corrupt-file recovery; optional start at login.
- Local error log with rotation; per-user, `asInvoker` execution.

### Fixed before publishing

- The camera could jump back above the Settings window while a border or shadow slider was being dragged.
- A hand-edited settings file with duplicate placement keys could stop the app from starting.
- A malformed organic blob (null wave, extreme amplitude or harmonic) could break startup or the outline.
- The error log grew without a limit.

### Known limitations

See the "Status: what still needs work" section of the [README](README.md) and
[docs/FEATURE_STATUS.md](docs/FEATURE_STATUS.md).
