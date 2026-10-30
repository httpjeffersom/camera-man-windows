using Microsoft.Win32;

namespace CameraMan.Windows.Services;

/// <summary>
/// "Open at login" through the per-user Run key. Nothing is written until the user turns the option on, and turning
/// it off removes only CameraMan's own value.
/// </summary>
public sealed class StartupService
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "CameraMan";

    public static string BuildCommand(string executablePath) => $"\"{executablePath}\"";

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Enables or disables startup. Returns null on success, otherwise a user-readable error.</summary>
    public string? SetEnabled(bool enabled, string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return "The Windows startup registry key could not be opened.";
            }

            if (enabled)
            {
                key.SetValue(ValueName, BuildCommand(executablePath), RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return ex.Message;
        }
    }

    /// <summary>Keeps the stored command pointing at the current executable after an update or a move.</summary>
    public void RefreshIfEnabled(string executablePath)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (key?.GetValue(ValueName) is not string current)
            {
                return;
            }

            var expected = BuildCommand(executablePath);
            if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
            {
                SetEnabled(true, executablePath);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            // Startup is a convenience; failing to refresh it must not stop the app.
        }
    }
}
