using System.Windows;
using System.Windows.Controls;
using CameraMan.Windows.ViewModels;

namespace CameraMan.Windows.Views.Settings;

public partial class FramingSettingsPage : UserControl
{
    public FramingSettingsPage()
    {
        InitializeComponent();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is AppState state)
        {
            state.ResetFraming();
        }
    }
}
