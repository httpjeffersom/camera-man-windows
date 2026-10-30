using System.ComponentModel;
using System.Windows;
using CameraMan.Windows.Camera;
using CameraMan.Windows.Models;
using CameraMan.Windows.ViewModels;

namespace CameraMan.Windows.Services;

/// <summary>
/// Coordinates the camera session with <see cref="AppState"/>: enumeration, fallback selection, pause/resume,
/// colour settings and failure recovery. All state writes are marshalled to the UI thread.
/// </summary>
public sealed class CameraCoordinator : IAsyncDisposable
{
    private readonly CameraService _camera = new();
    private readonly AppState _state;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CancellationTokenSource? _startCts;
    private long _requestId;
    private bool _disposed;
    private bool _subscribed;
    private int _handlingFrameError;
    private string? _activeDeviceId;

    public CameraCoordinator(AppState state)
    {
        _state = state;
        _camera.FrameError += OnFrameError;
        _state.PropertyChanged += OnStatePropertyChanged;
        _subscribed = true;
    }

    public CameraService Camera => _camera;

    public async Task InitializeAsync()
    {
        await RefreshDevicesAsync(announce: false);
        if (_state.HasDevices && !_camera.IsRunning)
        {
            var deviceId = CameraDeviceCycle.Resolve(_state.VideoDevices.ToList(), _state.SelectedDeviceId);
            await StartAsync(deviceId, announce: false);
        }
        else if (!_state.HasDevices)
        {
            _state.SetCameraState(CameraStatus.NotFound, message: null);
        }
    }

    /// <summary>Re-enumerates devices. The active camera keeps running unless it disappeared.</summary>
    public async Task RefreshDevicesAsync(bool announce = false)
    {
        if (_disposed)
        {
            return;
        }

        await _refreshGate.WaitAsync();
        try
        {
            IReadOnlyList<CameraDevice> devices;
            try
            {
                devices = await CameraService.ListDevicesAsync();
            }
            catch (CameraCaptureException ex)
            {
                await OnUiAsync(() => _state.SetCameraState(
                    ex.Kind == CameraErrorKind.PermissionDenied ? CameraStatus.PermissionDenied : CameraStatus.Error,
                    message: ex.Message));
                return;
            }

            await OnUiAsync(() =>
            {
                _state.ReplaceDevices(devices);
                if (devices.Count == 0)
                {
                    _state.SetCameraState(CameraStatus.NotFound);
                }
            });

            var current = _activeDeviceId;
            // An empty id is the "System default" choice and stays valid as long as any camera exists.
            var currentStillExists = string.IsNullOrEmpty(current) || devices.Any(d => d.Id == current);
            if (_camera.IsRunning && currentStillExists)
            {
                await OnUiAsync(() => _state.SetCameraState(CameraStatus.Connected, _camera.ActiveFormat));
                return;
            }

            if (devices.Count == 0)
            {
                await StopAsync();
                await OnUiAsync(() => _state.SetCameraState(CameraStatus.NotFound));
                return;
            }

            var next = string.IsNullOrEmpty(_state.SelectedDeviceId)
                ? string.Empty
                : CameraDeviceCycle.Resolve(devices, _state.SelectedDeviceId);
            await StartAsync(next, announce);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    public async Task StartAsync(string? deviceId, bool announce = false)
    {
        if (_disposed)
        {
            return;
        }

        var requestId = Interlocked.Increment(ref _requestId);
        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _startCts, cts);
        previous?.Cancel();
        previous?.Dispose();

        _camera.SetColorAdjustment(CurrentColorAdjustment());
        await OnUiAsync(() => _state.SetCameraState(CameraStatus.Loading));

        try
        {
            await _camera.StartAsync(deviceId, cts.Token);
            if (requestId != Interlocked.Read(ref _requestId) || cts.IsCancellationRequested)
            {
                // A newer selection already arrived; that request owns the session now.
                return;
            }

            _activeDeviceId = deviceId;
            var format = _camera.ActiveFormat;
            await OnUiAsync(() =>
            {
                if (!string.Equals(_state.SelectedDeviceId, deviceId, StringComparison.Ordinal))
                {
                    _state.SelectedDeviceId = deviceId;
                }

                _state.SetCameraState(CameraStatus.Connected, format);
                var name = _state.VideoDevices.FirstOrDefault(d => d.Id == deviceId)?.Name;
                if (announce && !string.IsNullOrWhiteSpace(name))
                {
                    _state.ShowToast(name);
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer selection.
        }
        catch (CameraCaptureException ex)
        {
            if (requestId != Interlocked.Read(ref _requestId))
            {
                return;
            }

            _activeDeviceId = null;
            await OnUiAsync(() =>
            {
                var status = ex.Kind switch
                {
                    CameraErrorKind.PermissionDenied => CameraStatus.PermissionDenied,
                    CameraErrorKind.NotFound => CameraStatus.NotFound,
                    CameraErrorKind.Disconnected => CameraStatus.Disconnected,
                    CameraErrorKind.Busy => CameraStatus.Error,
                    _ => CameraStatus.Error,
                };
                _state.SetCameraState(status, message: ex.Message);
            });
        }
        catch (Exception ex)
        {
            if (requestId != Interlocked.Read(ref _requestId))
            {
                return;
            }

            _activeDeviceId = null;
            await OnUiAsync(() => _state.SetCameraState(CameraStatus.Error, message: ex.Message));
        }
    }

    public async Task StopAsync()
    {
        Interlocked.Increment(ref _requestId);
        _startCts?.Cancel();
        _activeDeviceId = null;
        try
        {
            await _camera.StopAsync();
        }
        catch (Exception)
        {
            // Teardown failures must not crash the app.
        }
    }

    public Task PauseAsync() => StopAsync();

    public async Task ResumeAsync()
    {
        if (_disposed || !_state.IsWindowVisible || _camera.IsRunning)
        {
            return;
        }

        var deviceId = _state.SelectedDeviceId
            ?? CameraDeviceCycle.Resolve(_state.VideoDevices.ToList(), null);
        if (deviceId is not null)
        {
            await StartAsync(deviceId);
        }
    }

    public async Task CycleCameraAsync()
    {
        var next = CameraDeviceCycle.Next(_state.VideoDevices.ToList(), _state.SelectedDeviceId);
        if (next is null)
        {
            return;
        }

        await SelectCameraAsync(next);
    }

    public async Task SelectCameraAsync(string? deviceId)
    {
        // Setting the property lets the change handler start exactly one session. When the value is already selected
        // (for example after a failed start) start explicitly so the user can retry.
        if (string.Equals(deviceId, _state.SelectedDeviceId, StringComparison.Ordinal))
        {
            await StartAsync(deviceId, announce: true);
        }
        else
        {
            _state.SelectedDeviceId = deviceId;
        }
    }

    public void ApplyCurrentColorAdjustment() =>
        _camera.SetColorAdjustment(CurrentColorAdjustment());

    private ColorAdjustment CurrentColorAdjustment() =>
        new(_state.Brightness, _state.Contrast, _state.Saturation);

    private void OnStatePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(AppState.Brightness):
            case nameof(AppState.Contrast):
            case nameof(AppState.Saturation):
                ApplyCurrentColorAdjustment();
                break;
            case nameof(AppState.SelectedDeviceId):
                // The UI/tray changes this when the user picks a camera. A request from inside StartAsync sets the
                // same id and is ignored here.
                if (!string.Equals(_state.SelectedDeviceId, _activeDeviceId, StringComparison.Ordinal))
                {
                    _ = StartAsync(_state.SelectedDeviceId, announce: true);
                }

                break;
        }
    }

    private void OnFrameError(object? sender, CameraCaptureException ex)
    {
        if (_disposed || Interlocked.Exchange(ref _handlingFrameError, 1) == 1)
        {
            return;
        }

        _ = Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            try
            {
                _activeDeviceId = null;
                _state.SetCameraState(CameraStatus.Disconnected, message: ex.Message);
                await _camera.StopAsync();
                await Task.Delay(400);
                await RefreshDevicesAsync();
            }
            catch (Exception)
            {
                // Recovery is best effort; the tray and settings still offer a manual retry.
            }
            finally
            {
                Interlocked.Exchange(ref _handlingFrameError, 0);
            }
        });
    }

    private static Task OnUiAsync(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_subscribed)
        {
            _state.PropertyChanged -= OnStatePropertyChanged;
            _camera.FrameError -= OnFrameError;
        }

        Interlocked.Increment(ref _requestId);
        _startCts?.Cancel();
        _startCts?.Dispose();
        _startCts = null;
        await _camera.DisposeAsync();
        _refreshGate.Dispose();
    }
}
