using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CameraMan.Windows.Resources;
using CameraMan.Windows.Services;
using CameraMan.Windows.ViewModels;
using CameraMan.Windows.Views;
using CameraMan.Windows.Views.Settings;
using Microsoft.Win32;

namespace CameraMan.Windows;

/// <summary>
/// Composition root and lifetime owner: settings, theme and language, the camera window, camera capture, global
/// shortcuts, the tray icon and session/power pause-resume all meet here.
/// </summary>
public partial class App : Application
{
    private SingleInstance? _singleInstance;
    private SettingsStore? _store;
    private AppState? _state;
    private CameraCoordinator? _camera;
    private WindowPlacementService? _placement;
    private GlobalHotKeyService? _hotKeys;
    private TrayIconService? _tray;
    private CameraWindow? _cameraWindow;
    private SettingsWindow? _settingsWindow;
    private readonly StartupService _startup = new();
    private bool _shuttingDown;
    private bool _syncingStartup;

    public CameraCoordinator Camera =>
        _camera ?? throw new InvalidOperationException("The camera coordinator is not ready yet.");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        _singleInstance = new SingleInstance();
        if (!_singleInstance.IsFirstInstance)
        {
            _singleInstance.Dispose();
            _singleInstance = null;
            Shutdown();
            return;
        }

        _singleInstance.ActivationRequested += OnActivationRequested;

        try
        {
            Start();
        }
        catch (Exception ex)
        {
            LogError(ex);
            MessageBox.Show(
                Strings.Format("Error_Unexpected", ex.Message),
                Strings.Get("Error_StartupTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private void Start()
    {
        _store = new SettingsStore();
        var settings = _store.Load(out var loadWarning);
        Strings.ApplyLanguage(settings.Language);
        ThemeService.Apply(this, ThemeService.IsDark());

        _state = new AppState(settings, _store);
        _state.ReduceMotion = !SystemParameters.ClientAreaAnimation;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        var startupEnabled = _startup.IsEnabled();
        if (_state.OpenAtLogin != startupEnabled)
        {
            _syncingStartup = true;
            try
            {
                _state.OpenAtLogin = startupEnabled;
            }
            finally
            {
                _syncingStartup = false;
            }
        }

        _camera = new CameraCoordinator(_state);
        _placement = new WindowPlacementService(_state);
        _placement.DisplayChanged += (_, _) => Dispatcher.BeginInvoke(ApplyPlacement);

        _cameraWindow = new CameraWindow(_state, _camera, _placement, OpenSettings, Quit);
        _cameraWindow.HideRequested += (_, _) => OnCameraHideRequested();

        _settingsWindow = new SettingsWindow(_state);
        _settingsWindow.IsVisibleChanged += (_, _) =>
        {
            if (_state is not null)
            {
                _state.SettingsOpen = _settingsWindow.IsVisible;
            }
        };

        _hotKeys = new GlobalHotKeyService
        {
            ToggleCamera = ToggleCamera,
            SwitchSize = SwitchSize,
        };

        _state.LayoutSettingsChanged += (_, _) => ApplyPlacement();
        _state.GlobalShortcutsSettingChanged += (_, _) => ApplyHotKeys();
        _state.PropertyChanged += OnStatePropertyChanged;

        ShowCamera(activate: true);
        _hotKeys.Attach(_placement.Handle);
        ApplyHotKeys();

        _tray = new TrayIconService(
            _state,
            _camera,
            ToggleCamera,
            OpenSettings,
            RestoreDefaults,
            Quit);

        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        _ = InitializeCameraAsync(loadWarning);
        RefreshStartupRegistration();
    }

    private async Task InitializeCameraAsync(string? loadWarning)
    {
        if (_camera is null || _state is null)
        {
            return;
        }

        try
        {
            await _camera.InitializeAsync();
        }
        catch (Exception ex)
        {
            _state.SetCameraState(Models.CameraStatus.Error, message: ex.Message);
            _state.ShowToast(ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(loadWarning))
        {
            MessageBox.Show(
                Strings.Format("Error_Unexpected", loadWarning),
                Strings.Get("Error_StartupTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void RefreshStartupRegistration()
    {
        var executable = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executable) && _state?.OpenAtLogin == true)
        {
            _startup.RefreshIfEnabled(executable);
        }
    }

    private void ShowCamera(bool activate)
    {
        if (_cameraWindow is null || _state is null)
        {
            return;
        }

        if (!_state.IsWindowVisible)
        {
            _state.IsWindowVisible = true;
        }

        if (!_cameraWindow.IsVisible)
        {
            _cameraWindow.Show();
        }

        if (_cameraWindow.WindowState == WindowState.Minimized)
        {
            _cameraWindow.WindowState = WindowState.Normal;
        }

        if (activate)
        {
            _cameraWindow.Activate();
            _cameraWindow.Focus();
        }

        if (_camera is not null)
        {
            _ = _camera.ResumeAsync();
        }

        _tray?.Refresh();
    }

    private void HideCamera()
    {
        if (_cameraWindow is null || _state is null)
        {
            return;
        }

        _state.IsWindowVisible = false;
        _cameraWindow.Hide();
        if (_camera is not null)
        {
            _ = _camera.PauseAsync();
        }

        _tray?.Refresh();
    }

    private void ToggleCamera()
    {
        if (_state?.IsWindowVisible == true)
        {
            HideCamera();
        }
        else
        {
            ShowCamera(activate: false);
        }
    }

    private void OnCameraHideRequested()
    {
        if (_state is null)
        {
            return;
        }

        _state.IsWindowVisible = false;
        if (_camera is not null)
        {
            _ = _camera.PauseAsync();
        }

        _tray?.Refresh();
    }

    private void SwitchSize()
    {
        if (_state is null)
        {
            return;
        }

        _state.WindowSizePreset = _state.SpaceToggleTargetPreset();
        ApplyPlacement();
    }

    private void ApplyPlacement() => _placement?.ApplyCurrentPreset();

    private void ApplyHotKeys()
    {
        if (_hotKeys is null || _state is null)
        {
            return;
        }

        _hotKeys.Apply(_state.GlobalShortcutsEnabled);
        _state.GlobalHotKeyError = _hotKeys.LastError;
    }

    private void OpenSettings()
    {
        OpenSettingsPage(SettingsPage.Camera);
    }

    private void OpenSettingsPage(SettingsPage page)
    {
        if (_settingsWindow is null)
        {
            return;
        }

        _settingsWindow.ShowPage(page);
        if (!_settingsWindow.IsVisible)
        {
            _settingsWindow.Show();
        }

        if (_settingsWindow.WindowState == WindowState.Minimized)
        {
            _settingsWindow.WindowState = WindowState.Normal;
        }

        _settingsWindow.Activate();
        _settingsWindow.Focus();
    }

    private void RestoreDefaults()
    {
        if (_state is null)
        {
            return;
        }

        var result = MessageBox.Show(
            _settingsWindow?.IsVisible == true ? _settingsWindow : _cameraWindow,
            Strings.Get("Restore_Message"),
            Strings.Get("Restore_Title"),
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        if (result != MessageBoxResult.OK)
        {
            return;
        }

        _state.RestoreDefaults();
        _state.SaveNow();
        ApplyPlacement();
        _tray?.Refresh();
    }

    private void Quit()
    {
        if (_shuttingDown)
        {
            return;
        }

        _shuttingDown = true;
        _state?.SaveNow();
        try
        {
            _cameraWindow?.CloseForShutdown();
            if (_settingsWindow?.IsVisible == true)
            {
                _settingsWindow.CloseForShutdown();
            }
        }
        catch (InvalidOperationException)
        {
            // A window that was never shown has nothing to close.
        }

        Shutdown();
    }

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_state is null)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AppState.OpenAtLogin) when !_syncingStartup:
                ApplyStartupSetting(_state.OpenAtLogin);
                break;
            case nameof(AppState.Language):
                _state.ShowToast(Strings.Get("General_LanguageRestart"));
                break;
        }
    }

    private void ApplyStartupSetting(bool enabled)
    {
        if (_state is null)
        {
            return;
        }

        var executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            return;
        }

        var error = _startup.SetEnabled(enabled, executable);
        if (error is null)
        {
            _state.StartupError = null;
            return;
        }

        _state.StartupError = Strings.Format("General_LoginError", error);
        _syncingStartup = true;
        try
        {
            _state.OpenAtLogin = !enabled;
        }
        finally
        {
            _syncingStartup = false;
        }
    }

    private void OnActivationRequested()
    {
        Dispatcher.BeginInvoke(() => ShowCamera(activate: true));
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.RemoteDisconnect:
                if (_camera is not null)
                {
                    _ = _camera.PauseAsync();
                }

                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.RemoteConnect:
                if (_camera is not null && _state?.IsWindowVisible == true)
                {
                    _ = _camera.ResumeAsync();
                }

                break;
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                if (_camera is not null)
                {
                    _ = _camera.PauseAsync();
                }

                break;
            case PowerModes.Resume:
                if (_camera is not null && _state?.IsWindowVisible == true)
                {
                    _ = _camera.ResumeAsync();
                }

                break;
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(ApplyPlacement);
    }

    /// <summary>Mirrors the Windows "Animation effects" accessibility setting; changes apply without a restart.</summary>
    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.ClientAreaAnimation))
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            if (_state is not null)
            {
                _state.ReduceMotion = !SystemParameters.ClientAreaAnimation;
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;

        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;

        if (_state is not null)
        {
            _state.PropertyChanged -= OnStatePropertyChanged;
            _state.SaveNow();
        }

        _tray?.Dispose();
        _hotKeys?.Dispose();
        _placement?.Dispose();

        if (_camera is not null)
        {
            try
            {
                // Run off the UI thread so no WinRT continuation needs the dispatcher while we wait for teardown.
                Task.Run(async () => await _camera.DisposeAsync()).Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex) when (ex is AggregateException or TimeoutException)
            {
                LogError(ex);
            }
        }

        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        LogError(e.Exception);
        try
        {
            MessageBox.Show(
                Strings.Format("Error_Unexpected", e.Exception.Message),
                Strings.Get("App_Name"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception)
        {
            // Nothing left to show the message on.
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogError(ex);
        }
    }

    private void LogError(Exception ex)
    {
        try
        {
            var directory = _store?.Directory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CameraMan");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "error.log");
            // Keep one previous log. Without a cap, a driver that fails on every frame could fill the disk over
            // a long session.
            const long maxBytes = 256 * 1024;
            if (File.Exists(path) && new FileInfo(path).Length > maxBytes)
            {
                File.Move(path, path + ".1", overwrite: true);
            }

            File.AppendAllText(
                path,
                $"[{DateTimeOffset.Now:O}] {ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (Exception)
        {
            // Logging must never take the app down.
        }
    }
}
