namespace CameraMan.Windows.Services;

/// <summary>
/// A single running CameraMan. A second launch signals the first process (which brings its window back) and exits.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly string _eventName;
    private readonly Mutex _mutex;
    private readonly EventWaitHandle? _activateEvent;
    private readonly Thread? _listener;
    private volatile bool _disposed;

    public SingleInstance(string applicationId = "CameraMan.Windows")
    {
        var mutexName = $@"Local\{applicationId}.Mutex";
        _eventName = $@"Local\{applicationId}.Activate";
        _mutex = new Mutex(initiallyOwned: true, mutexName, out var createdNew);
        IsFirstInstance = createdNew;
        if (!createdNew)
        {
            try
            {
                if (EventWaitHandle.TryOpenExisting(_eventName, out var existing))
                {
                    using (existing)
                    {
                        existing.Set();
                    }
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
            {
                // The other instance is starting up; it will still be visible shortly.
            }

            return;
        }

        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, _eventName);
        _listener = new Thread(Listen)
        {
            IsBackground = true,
            Name = "CameraMan single-instance listener",
        };
        _listener.Start();
    }

    public bool IsFirstInstance { get; }

    /// <summary>Raised on the listener thread. Marshal to the UI dispatcher in the handler.</summary>
    public event Action? ActivationRequested;

    private void Listen()
    {
        while (!_disposed)
        {
            if (_activateEvent?.WaitOne(500) == true)
            {
                ActivationRequested?.Invoke();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (IsFirstInstance)
        {
            try
            {
                _activateEvent?.Set();
                _listener?.Join(300);
            }
            catch (Exception)
            {
                // Shutdown only.
            }

            _activateEvent?.Dispose();
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Not the owner (should not happen).
            }

            _mutex.Dispose();
        }
        else
        {
            _mutex.Dispose();
        }
    }
}
