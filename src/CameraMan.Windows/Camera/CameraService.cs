using System.Runtime.InteropServices;
using CameraMan.Windows.Models;
using WinRT;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace CameraMan.Windows.Camera;

public enum CameraErrorKind
{
    PermissionDenied,
    NotFound,
    Busy,
    Disconnected,
    Unknown,
}

public sealed class CameraCaptureException : Exception
{
    public CameraCaptureException(CameraErrorKind kind, string message, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
    }

    public CameraErrorKind Kind { get; }

    public static CameraCaptureException FromException(Exception ex)
    {
        if (ex is CameraCaptureException captured)
        {
            return captured;
        }

        if (ex is UnauthorizedAccessException)
        {
            return new CameraCaptureException(CameraErrorKind.PermissionDenied, ex.Message, ex);
        }

        var hr = ex.HResult;
        if (hr == unchecked((int)0x80070005))
        {
            return new CameraCaptureException(CameraErrorKind.PermissionDenied, ex.Message, ex);
        }

        if (hr is unchecked((int)0xC00D3E86) or unchecked((int)0x80070490) or unchecked((int)0x80070002))
        {
            return new CameraCaptureException(CameraErrorKind.Disconnected, ex.Message, ex);
        }

        if (hr == unchecked((int)0x8007001F))
        {
            return new CameraCaptureException(CameraErrorKind.Busy, ex.Message, ex);
        }

        return new CameraCaptureException(CameraErrorKind.Unknown, ex.Message, ex);
    }
}

/// <summary>
/// Owns the Windows camera session: enumeration, format choice, MediaFrameReader and a reusable BGRA frame buffer.
/// Start/stop/switch calls are serialized, so two sessions can never run at once. Frames are never written to disk
/// or sent anywhere.
/// </summary>
public sealed class CameraService : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly FrameBuffer _frames = new();
    private MediaCapture? _capture;
    private MediaFrameReader? _reader;
    private ColorTransform _transform = ColorTransform.FromNeutral;
    private int _frameErrorRaised;
    private bool _disposed;

    public FrameBuffer Frames => _frames;

    public string? ActiveDeviceId { get; private set; }

    public string? ActiveFormat { get; private set; }

    public bool IsRunning => _reader is not null;

    public void SetColorAdjustment(ColorAdjustment adjustment) =>
        Volatile.Write(ref _transform, new ColorTransform(adjustment));

    public static async Task<IReadOnlyList<CameraDevice>> ListDevicesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).AsTask(cancellationToken);
            return devices
                .Where(d => !string.IsNullOrWhiteSpace(d.Id))
                .Select(d => new CameraDevice(d.Id, string.IsNullOrWhiteSpace(d.Name) ? d.Id : d.Name))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw CameraCaptureException.FromException(ex);
        }
    }

    public async Task StartAsync(string? deviceId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopCoreAsync();

            MediaCapture? capture = null;
            try
            {
                capture = await InitializeCaptureAsync(deviceId, cancellationToken);
                var source = ChooseColorSource(capture)
                    ?? throw new CameraCaptureException(CameraErrorKind.NotFound, "No colour video source was found.");

                var format = ChooseFormat(source);
                if (format is not null)
                {
                    try
                    {
                        await source.SetFormatAsync(format);
                    }
                    catch (Exception)
                    {
                        // Some drivers refuse a format they advertised; the current format is still usable.
                    }
                }

                var reader = await CreateReaderAsync(capture, source, cancellationToken);
                reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
                reader.FrameArrived += OnFrameArrived;
                var startStatus = await reader.StartAsync();
                if (startStatus != MediaFrameReaderStartStatus.Success)
                {
                    reader.FrameArrived -= OnFrameArrived;
                    reader.Dispose();
                    throw new CameraCaptureException(
                        CameraErrorKind.Busy,
                        $"The camera could not start ({startStatus}).");
                }

                _capture = capture;
                _reader = reader;
                Volatile.Write(ref _frameErrorRaised, 0);
                ActiveDeviceId = deviceId;
                var video = format?.VideoFormat;
                ActiveFormat = video is null
                    ? null
                    : $"{video.Width}×{video.Height} · {FormatFps(format!):0.#} fps";
                capture = null; // ownership moved
            }
            catch (CameraCaptureException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw CameraCaptureException.FromException(ex);
            }
            finally
            {
                capture?.Dispose();
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopCoreAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopCoreAsync()
    {
        _frames.Clear();
        var reader = _reader;
        var capture = _capture;
        _reader = null;
        _capture = null;
        ActiveDeviceId = null;
        ActiveFormat = null;

        if (reader is not null)
        {
            reader.FrameArrived -= OnFrameArrived;
            try
            {
                await reader.StopAsync();
            }
            catch (Exception)
            {
                // The device may already be gone.
            }

            reader.Dispose();
        }

        if (capture is not null)
        {
            try
            {
                capture.Dispose();
            }
            catch (Exception)
            {
                // Best effort; a disconnected camera can throw during teardown.
            }
        }
    }

    private static async Task<MediaCapture> InitializeCaptureAsync(string? deviceId, CancellationToken cancellationToken)
    {
        var settings = new MediaCaptureInitializationSettings
        {
            StreamingCaptureMode = StreamingCaptureMode.Video,
            SharingMode = MediaCaptureSharingMode.SharedReadOnly,
            MemoryPreference = MediaCaptureMemoryPreference.Cpu,
        };
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            settings.VideoDeviceId = deviceId;
        }

        var capture = new MediaCapture();
        try
        {
            await capture.InitializeAsync(settings).AsTask(cancellationToken);
            return capture;
        }
        catch (UnauthorizedAccessException)
        {
            capture.Dispose();
            throw;
        }
        catch (Exception first) when (first is not OperationCanceledException)
        {
            // Some drivers do not implement shared access. Retry once exclusively before giving up.
            capture.Dispose();
            settings.SharingMode = MediaCaptureSharingMode.ExclusiveControl;
            capture = new MediaCapture();
            try
            {
                await capture.InitializeAsync(settings).AsTask(cancellationToken);
                return capture;
            }
            catch (Exception ex)
            {
                capture.Dispose();
                throw CameraCaptureException.FromException(ex);
            }
        }
    }

    private static MediaFrameSource? ChooseColorSource(MediaCapture capture)
    {
        return capture.FrameSources.Values
            .Where(s => s.Info.SourceKind == MediaFrameSourceKind.Color)
            .OrderByDescending(s => s.Info.MediaStreamType == MediaStreamType.VideoRecord)
            .ThenBy(s => s.Info.MediaStreamType == MediaStreamType.VideoPreview)
            .FirstOrDefault();
    }

    private static MediaFrameFormat? ChooseFormat(MediaFrameSource source)
    {
        static double Fps(MediaFrameFormat f)
        {
            var rate = f.FrameRate;
            return rate.Denominator == 0 ? 0 : (double)rate.Numerator / rate.Denominator;
        }

        return source.SupportedFormats
            .Where(f => f.VideoFormat is { Width: > 0, Height: > 0 })
            .Where(f =>
            {
                var v = f.VideoFormat;
                return v.Width <= 1920 && v.Height <= 1080 && v.Width >= 160 && v.Height >= 120;
            })
            .OrderByDescending(f =>
            {
                var v = f.VideoFormat;
                var fps = Fps(f);
                var score = (long)v.Width * v.Height;
                if (Math.Abs((double)v.Width / v.Height - 16.0 / 9.0) < 0.06)
                {
                    score += 2_000_000; // prefer widescreen
                }

                if (fps is >= 24 and <= 32)
                {
                    score += 1_000_000; // smooth without doubling the copy cost
                }
                else if (fps > 32)
                {
                    score -= 250_000;
                }

                return score;
            })
            .FirstOrDefault();
    }

    private static double FormatFps(MediaFrameFormat format) =>
        format.FrameRate.Denominator == 0
            ? 0
            : (double)format.FrameRate.Numerator / format.FrameRate.Denominator;

    private static async Task<MediaFrameReader> CreateReaderAsync(
        MediaCapture capture,
        MediaFrameSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            return await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8).AsTask(cancellationToken);
        }
        catch (Exception first) when (first is not OperationCanceledException)
        {
            try
            {
                // A driver without BGRA conversion still works; frames are converted per sample below.
                return await capture.CreateFrameReaderAsync(source).AsTask(cancellationToken);
            }
            catch (Exception ex)
            {
                throw CameraCaptureException.FromException(ex);
            }
        }
    }

    private unsafe void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (Volatile.Read(ref _disposed))
        {
            return;
        }

        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (bitmap is null)
            {
                return;
            }

            SoftwareBitmap? converted = null;
            try
            {
                // The reader normally hands over straight BGRA8 already. Camera frames are opaque, so a conversion
                // would only cost a full-frame copy; only convert when a driver ignores the requested subtype.
                if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
                {
                    converted = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
                    bitmap = converted;
                }

                var width = bitmap.PixelWidth;
                var height = bitmap.PixelHeight;
                if (width <= 0 || height <= 0)
                {
                    return;
                }

                using var buffer = bitmap.LockBuffer(BitmapBufferAccessMode.Read);
                using var reference = buffer.CreateReference();
                var plane = buffer.GetPlaneDescription(0);

                // With CsWinRT the projected IMemoryBufferReference is a managed WinRT wrapper, so a plain CLR cast
                // to the raw COM interface fails. As<T> performs the QueryInterface and hands back the raw accessor.
                var access = reference.As<IMemoryBufferByteAccess>();
                access.GetBuffer(out var source, out _);
                // The RCW is created per frame by As<T>; release it immediately, the buffer stays alive via reference.
                Marshal.ReleaseComObject(access);
                var destinationStride = width * 4;
                var destination = _frames.BeginWrite(destinationStride * height);
                var transform = Volatile.Read(ref _transform);
                fixed (byte* destinationBase = destination)
                {
                    transform.Apply(
                        source + plane.StartIndex,
                        plane.Stride,
                        destinationBase,
                        destinationStride,
                        width,
                        height);
                }

                _frames.CommitWrite(width, height, destinationStride);
                Volatile.Write(ref _frameErrorRaised, 0);
            }
            finally
            {
                converted?.Dispose();
            }
        }
        catch (Exception ex)
        {
            // A failing driver would otherwise raise this for every frame; report once until a frame succeeds again.
            if (Interlocked.Exchange(ref _frameErrorRaised, 1) == 0)
            {
                FrameError?.Invoke(this, CameraCaptureException.FromException(ex));
            }
        }
    }

    /// <summary>Raised on the capture thread when a frame cannot be read (for example the camera was unplugged).</summary>
    public event EventHandler<CameraCaptureException>? FrameError;

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync();
        _gate.Dispose();
    }
}

[ComImport]
[Guid("5B0D3235-4DBA-4D44-865E-8F1D0E4FD04D")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal unsafe interface IMemoryBufferByteAccess
{
    [PreserveSig]
    int GetBuffer(out byte* buffer, out uint capacity);
}
