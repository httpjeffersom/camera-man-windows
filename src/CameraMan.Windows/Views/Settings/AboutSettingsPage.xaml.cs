using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using CameraMan.Windows.Resources;
using CameraMan.Windows.Services;

namespace CameraMan.Windows.Views.Settings;

public partial class AboutSettingsPage : UserControl
{
    public AboutSettingsPage()
    {
        InitializeComponent();
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var text = version is null ? "1.0.0" : $"{version.Major}.{version.Minor}.{version.Build}";
        VersionText.Text = Strings.Format("About_Version", text);
    }

    private void OnOpenSourceClick(object sender, RoutedEventArgs e) =>
        SystemSettingsService.OpenProjectPage();

    private void OnOpenMacSourceClick(object sender, RoutedEventArgs e) =>
        SystemSettingsService.OpenMacProjectPage();
}
