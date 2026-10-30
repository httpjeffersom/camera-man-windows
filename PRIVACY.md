# CameraMan Privacy Policy

_Last updated: October 30, 2026_

CameraMan for Windows shows your camera in a floating window on your PC. That is all it does with your camera.

## What CameraMan collects

Nothing. CameraMan:

- does not record, save or upload video or images;
- does not send any data over the network (the app has no networking code);
- has no account, analytics, advertising or tracking;
- does not share anything with third parties.

The camera image is processed on your PC, in memory, only to draw the preview, and is discarded frame by frame.

## Data stored on your PC

Your settings (chosen camera, shape, size, colors and so on) are saved in `%APPDATA%\CameraMan\settings.json`, so they
are there the next time you open the app. They never leave your PC. Deleting the file (or the app folder) removes
them. If the app hits an unexpected error it appends a local diagnostic to `%APPDATA%\CameraMan\error.log`; that file
is never uploaded and is rotated once it passes 256 KB.

If you enable "open at login", the app writes its own path to
`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` so Windows can start it. Turning the option off removes only that
value.

## Camera permission

Windows asks for your permission the first time CameraMan uses the camera. You can turn it off at any time in
Settings → Privacy & security → Camera.

## Contact

Questions about this policy: open an issue at <https://github.com/httpjeffersom/camera-man-windows/issues>.
