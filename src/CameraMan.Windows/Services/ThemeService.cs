using System.Windows;
using Microsoft.Win32;

namespace CameraMan.Windows.Services;

/// <summary>
/// CameraMan follows the Windows app theme, like the macOS original follows the system appearance. There is no
/// in-app theme switch, so this is read once at startup.
/// </summary>
public static class ThemeService
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: false);
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return false;
        }
    }

    /// <summary>Adds the palette and the shared control styles to the application resources.</summary>
    public static void Apply(Application app, bool dark)
    {
        var palette = new ResourceDictionary
        {
            Source = new Uri(
                dark
                    ? "pack://application:,,,/CameraMan.Windows;component/Resources/Themes/Palette.Dark.xaml"
                    : "pack://application:,,,/CameraMan.Windows;component/Resources/Themes/Palette.Light.xaml",
                UriKind.Absolute),
        };
        var controls = new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/CameraMan.Windows;component/Resources/Themes/Controls.xaml",
                UriKind.Absolute),
        };

        app.Resources.MergedDictionaries.Clear();
        app.Resources.MergedDictionaries.Add(palette);
        app.Resources.MergedDictionaries.Add(controls);
    }
}
