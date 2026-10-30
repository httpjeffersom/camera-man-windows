using System.Windows;
using System.Windows.Controls;
using CameraMan.Windows.ViewModels;

namespace CameraMan.Windows.Views.Settings;

public partial class ImageSettingsPage : UserControl
{
    public ImageSettingsPage()
    {
        InitializeComponent();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state)
        {
            state.ResetImage();
        }
    }
}
