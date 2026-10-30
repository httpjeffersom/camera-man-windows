namespace CameraMan.Windows.Camera;

public readonly record struct FrameInfo(int Width, int Height, int Stride, long Version);

/// <summary>
/// A two-slot frame buffer between the capture thread and the UI. The producer writes into a private slot and swaps
/// it in under a lock; the UI copies the latest published frame under the same lock. No frame is ever written while
/// the UI reads it, and the buffers are reused for the lifetime of the capture session.
/// </summary>
public sealed class FrameBuffer
{
    private readonly object _gate = new();
    private byte[] _write = [];
    private byte[] _read = [];
    private int _width;
    private int _height;
    private int _stride;
    private long _version;
    private bool _hasFrame;

    public long Version
    {
        get
        {
            lock (_gate)
            {
                return _version;
            }
        }
    }

    public FrameInfo? LatestInfo
    {
        get
        {
            lock (_gate)
            {
                return _hasFrame ? new FrameInfo(_width, _height, _stride, _version) : null;
            }
        }
    }

    /// <summary>Producer thread only: gets a buffer large enough for the next frame.</summary>
    public byte[] BeginWrite(int requiredBytes)
    {
        if (_write.Length < requiredBytes)
        {
            _write = new byte[requiredBytes];
        }

        return _write;
    }

    /// <summary>Producer thread only: publishes the buffer filled after <see cref="BeginWrite"/>.</summary>
    public void CommitWrite(int width, int height, int stride)
    {
        lock (_gate)
        {
            (_write, _read) = (_read, _write);
            _width = width;
            _height = height;
            _stride = stride;
            _version++;
            _hasFrame = true;
        }
    }

    /// <summary>UI thread: copies the latest frame, if any, into <paramref name="destination"/>.</summary>
    public bool TryCopyLatest(byte[] destination, out FrameInfo info)
    {
        lock (_gate)
        {
            if (!_hasFrame || _read.Length == 0)
            {
                info = default;
                return false;
            }

            var required = _stride * _height;
            if (destination.Length < required)
            {
                info = default;
                return false;
            }

            Buffer.BlockCopy(_read, 0, destination, 0, required);
            info = new FrameInfo(_width, _height, _stride, _version);
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _hasFrame = false;
            _version++;
        }
    }
}
