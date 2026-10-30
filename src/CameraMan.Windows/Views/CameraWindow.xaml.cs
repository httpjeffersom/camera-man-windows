using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using CameraMan.Windows.Models;
using CameraMan.Windows.Services;
using CameraMan.Windows.ViewModels;
using CameraMan.Windows.Interop;
using CameraMan.Windows.Resources;

namespace CameraMan.Windows.Views;

/// <summary>
/// The frameless, transparent floating camera. Closing hides it (the camera turns off) instead of ending the app, and
/// every keyboard shortcut from the macOS original is handled here while the window has focus.
/// </summary>
public partial class CameraWindow : Window
{
    private readonly AppState _state;
    private readonly CameraCoordinator _camera;
    private readonly WindowPlacementService _placement;
    private readonly Action _openAppSettings;
    private readonly Action _quit;
    private HwndSource? _source;
    private bool _allowClose;

    public CameraWindow(
        AppState state,
        CameraCoordinator camera,
        WindowPlacementService placement,
        Action openAppSettings,
        Action quit)
    {
        _state = state;
        _camera = camera;
        _placement = placement;
        _openAppSettings = openAppSettings;
        _quit = quit;

        DataContext = state;
        InitializeComponent();

        Chrome.FrameSource = camera.Camera.Frames;
        Chrome.MouseLeftButtonDown += OnChromeMouseLeftButtonDown;
        _state.PropertyChanged += OnStatePropertyChanged;

        UpdateStatusOverlay();
    }

    /// <summary>Raised when the user closes the window; the app hides the camera and stops capture.</summary>
    public event EventHandler? HideRequested;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _placement.Attach(this);
        WindowAppearance.MakeToolWindow(this);
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(WndProc);
        _placement.ApplyCurrentPreset();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (e.Key == Key.OemComma)
            {
                _openAppSettings();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Q)
            {
                _quit();
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            base.OnKeyDown(e);
            return;
        }

        switch (e.Key)
        {
            case Key.Left:
                _state.AdjustOffset(-1, 0);
                break;
            case Key.Right:
                _state.AdjustOffset(1, 0);
                break;
            case Key.Down:
                _state.AdjustOffset(0, -1);
                break;
            case Key.Up:
                _state.AdjustOffset(0, 1);
                break;
            case Key.Add:
            case Key.OemPlus:
                _state.ZoomIn();
                break;
            case Key.Subtract:
            case Key.OemMinus:
                _state.ZoomOut();
                break;
            case Key.R:
                _state.ResetZoom();
                break;
            case Key.OemQuestion:
                _state.FlipHorizontal = !_state.FlipHorizontal;
                break;
            case Key.V:
                _state.FlipVertical = !_state.FlipVertical;
                break;
            case Key.O:
                _state.CycleShape();
                break;
            case Key.Back:
                _ = _camera.CycleCameraAsync();
                break;
            case Key.Space:
                _state.WindowSizePreset = _state.SpaceToggleTargetPreset();
                break;
            default:
                base.OnKeyDown(e);
                return;
        }

        e.Handled = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideRequested?.Invoke(this, EventArgs.Empty);
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _state.PropertyChanged -= OnStatePropertyChanged;
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        base.OnClosed(e);
    }

    /// <summary>Used only while the application is shutting down.</summary>
    public void CloseForShutdown()
    {
        _allowClose = true;
        Close();
    }

    private void OnChromeMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.ButtonState != MouseButtonState.Pressed)
        {
            return;
        }

        Focus();
        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The button was released between the event and DragMove; nothing to drag.
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_NCHITTEST)
        {
            var hit = WindowPlacementService.HitTestResize(hwnd, lParam, borderDip: 8);
            if (hit != NativeMethods.HTCLIENT)
            {
                handled = true;
                return new IntPtr(hit);
            }
        }

        return IntPtr.Zero;
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.CameraStatus) or nameof(AppState.CameraErrorMessage))
        {
            UpdateStatusOverlay();
        }
    }

    private void UpdateStatusOverlay()
    {
        var status = _state.CameraStatus;
        if (status == CameraStatus.Connected)
        {
            StatusOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        StatusOverlay.Visibility = Visibility.Visible;
        RetryButton.Visibility = status is CameraStatus.Loading ? Visibility.Collapsed : Visibility.Visible;
        OpenSettingsButton.Visibility = status == CameraStatus.PermissionDenied
            ? Visibility.Visible
            : Visibility.Collapsed;

        switch (status)
        {
            case CameraStatus.Loading:
                StatusGlyph.Text = "\uE714";
                StatusTitle.Text = Strings.Get("Camera_LoadingText");
                StatusDetail.Text = string.Empty;
                break;
            case CameraStatus.PermissionDenied:
                StatusGlyph.Text = "\uE72E";
                StatusTitle.Text = Strings.Get("Camera_PermissionTitle");
                StatusDetail.Text = Strings.Get("Camera_PermissionMessage");
                break;
            case CameraStatus.Disconnected:
                StatusGlyph.Text = "\uE7F4";
                StatusTitle.Text = Strings.Get("Camera_DisconnectedTitle");
                StatusDetail.Text = Strings.Get("Camera_Disconnected");
                break;
            case CameraStatus.Error:
                StatusGlyph.Text = "\uE7BA";
                StatusTitle.Text = Strings.Get("Camera_ErrorTitle");
                StatusDetail.Text = _state.CameraErrorMessage ?? Strings.Get("Camera_Error");
                break;
            default:
                StatusGlyph.Text = "\uE714";
                StatusTitle.Text = Strings.Get("Camera_NotFoundTitle");
                StatusDetail.Text = Strings.Get("Camera_NotFoundMessage");
                break;
        }

        StatusDetail.Visibility = string.IsNullOrWhiteSpace(StatusDetail.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _camera.RefreshDevicesAsync();
        }
        catch (Exception ex)
        {
            _state.ShowToast(ex.Message);
        }
    }

    private void OnOpenSettingsClick(object sender, RoutedEventArgs e) =>
        SystemSettingsService.OpenCameraPrivacySettings();
}
