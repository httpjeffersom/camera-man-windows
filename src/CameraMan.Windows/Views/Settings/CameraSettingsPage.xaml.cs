using System.Windows;
using System.Windows.Controls;
using CameraMan.Windows.Services;
using CameraMan.Windows.ViewModels;

namespace CameraMan.Windows.Views.Settings;

public partial class CameraSettingsPage : UserControl
{
    public CameraSettingsPage()
    {
        InitializeComponent();
    }

    private async void OnRefreshClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState || Application.Current is not App app)
        {
            return;
        }

        await app.Camera.RefreshDevicesAsync();
    }

    private void OnOpenPrivacyClick(object sender, RoutedEventArgs e) =>
        SystemSettingsService.OpenCameraPrivacySettings();
}
