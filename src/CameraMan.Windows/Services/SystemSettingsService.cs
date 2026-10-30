using System.Diagnostics;

namespace CameraMan.Windows.Services;

/// <summary>Opens the Windows pages and links the app points users at. Failures are silent (the app stays usable).</summary>
public static class SystemSettingsService
{
    public const string CameraPrivacyUri = "ms-settings:privacy-webcam";

    /// <summary>This port. Kept in one place so the About page and the docs cannot drift apart.</summary>
    public const string ProjectUrl = "https://github.com/httpjeffersom/camera-man-windows";

    /// <summary>The MIT-licensed macOS original this repository is a fork of.</summary>
    public const string MacProjectUrl = "https://github.com/lucianodiisouza/camera-man-macos";

    public static void OpenCameraPrivacySettings() => Open(CameraPrivacyUri);

    public static void OpenProjectPage() => Open(ProjectUrl);

    public static void OpenMacProjectPage() => Open(MacProjectUrl);

    public static void Open(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // No handler for the ms-settings URI; the user can still open Settings manually.
        }
    }
}
