using System.Windows;
using System.Windows.Controls;
using CameraMan.Windows.Resources;
using CameraMan.Windows.ViewModels;

namespace CameraMan.Windows.Views.Settings;

public partial class GeneralSettingsPage : UserControl
{
    public GeneralSettingsPage()
    {
        InitializeComponent();
    }

    private void OnRestoreClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AppState state)
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var message = Strings.Get("Restore_Message");
        var title = Strings.Get("Restore_Title");
        var result = owner is null
            ? MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel)
            : MessageBox.Show(owner, message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel);
        if (result != MessageBoxResult.OK)
        {
            return;
        }

        state.RestoreDefaults();
        state.SaveNow();
    }
}
