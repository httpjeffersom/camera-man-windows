using System.Windows;
using System.Windows.Interop;
using CameraMan.Windows.Interop;
using CameraMan.Windows.Models;
using CameraMan.Windows.ViewModels;

namespace CameraMan.Windows.Services;

/// <summary>
/// Places the camera window in physical pixels, remembers each preset's monitor/position and exposes the native
/// resize hit testing used by the frameless window.
/// </summary>
public sealed class WindowPlacementService : IDisposable
{
    private readonly AppState _state;
    private readonly MonitorService _monitors = new();
    private Window? _window;
    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _applying;

    public WindowPlacementService(AppState state)
    {
        _state = state;
    }

    public IntPtr Handle => _hwnd;

    /// <summary>Raised when Windows reports a display or device change.</summary>
    public event EventHandler? DisplayChanged;

    public void Attach(Window window)
    {
        _window = window;
        _hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(_hwnd);
        _source?.AddHook(WndProc);
    }

    public void ApplyCurrentPreset()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        var preset = _state.WindowSizePreset;
        var placement = _state.GetPlacement(preset);
        var monitor = _monitors.FindByDeviceName(placement.MonitorDeviceName) ?? _monitors.Nearest(_hwnd);
        if (monitor.Width <= 0 || monitor.Height <= 0)
        {
            monitor = _monitors.Primary();
        }

        if (monitor.Width <= 0 || monitor.Height <= 0)
        {
            return;
        }

        var size = WindowPlacementMath.ComputeSize(preset, monitor, placement.WidthPx, placement.HeightPx);
        var origin = WindowPlacementMath.ComputeOrigin(
            monitor, placement.ScreenEdge, placement.OriginX, placement.OriginY, size.Width, size.Height);

        // EffectiveAlwaysOnTop, not AlwaysOnTop: while the settings window is open the camera must stay
        // behind it. Moving or resizing the window (including the padding changes a border/shadow slider
        // triggers) re-applies the z-order, and using the saved preference here would push the camera back
        // above Settings every time a control is dragged.
        var topmost = _state.EffectiveAlwaysOnTop;
        _applying = true;
        try
        {
            NativeMethods.SetWindowPos(
                _hwnd,
                topmost ? NativeMethods.HWND_TOPMOST : NativeMethods.HWND_NOTOPMOST,
                origin.X,
                origin.Y,
                size.Width,
                size.Height,
                NativeMethods.SWP_NOACTIVATE);
            if (_window is not null && _window.Topmost != topmost)
            {
                _window.Topmost = topmost;
            }
        }
        finally
        {
            _applying = false;
        }
    }

    /// <summary>Persists the rectangle after a user move/resize.</summary>
    public void SaveCurrentRect()
    {
        if (_hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(_hwnd, out var rect))
        {
            return;
        }

        var monitor = _monitors.Nearest(_hwnd);
        _state.SetWindowRectForCurrentPreset(
            rect.Left,
            rect.Top,
            Math.Max(1, rect.Right - rect.Left),
            Math.Max(1, rect.Bottom - rect.Top),
            monitor.DeviceName);
    }

    public MonitorWorkArea MonitorForWindow() => _monitors.Nearest(_hwnd);

    /// <summary>Physical work area of the monitor containing the window (used by the Full preset).</summary>
    public MonitorWorkArea WorkArea => _monitors.Nearest(_hwnd);

    public bool IsOnScreen()
    {
        if (_hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(_hwnd, out var rect))
        {
            return false;
        }

        var monitors = _monitors.GetMonitors();
        return monitors.Any(m =>
            rect.Left < m.Right && rect.Right > m.X && rect.Top < m.Bottom && rect.Bottom > m.Y);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case NativeMethods.WM_EXITSIZEMOVE:
                if (!_applying)
                {
                    SaveCurrentRect();
                }

                break;
            case NativeMethods.WM_DISPLAYCHANGE:
            case NativeMethods.WM_DEVICECHANGE:
                if (msg == NativeMethods.WM_DISPLAYCHANGE)
                {
                    DisplayChanged?.Invoke(this, EventArgs.Empty);
                }

                break;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Native resize edges for a window with <c>WindowStyle=None</c> and transparency, where WPF has no non-client
    /// frame. Returns <see cref="NativeMethods.HTCLIENT"/> when the point is not on an edge.
    /// </summary>
    public static int HitTestResize(IntPtr hwnd, IntPtr lParam, double borderDip = 7)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return NativeMethods.HTCLIENT;
        }

        var screenX = (short)((long)lParam & 0xFFFF);
        var screenY = (short)(((long)lParam >> 16) & 0xFFFF);
        var dpi = NativeMethods.GetDpiForWindow(hwnd);
        if (dpi == 0)
        {
            dpi = 96;
        }

        var border = Math.Max(4, WindowPlacementMath.DipToPixels(borderDip, dpi));
        if (screenX < rect.Left || screenX >= rect.Right || screenY < rect.Top || screenY >= rect.Bottom)
        {
            return NativeMethods.HTCLIENT;
        }

        var left = screenX < rect.Left + border;
        var right = screenX >= rect.Right - border;
        var top = screenY < rect.Top + border;
        var bottom = screenY >= rect.Bottom - border;

        if (top && left) return NativeMethods.HTTOPLEFT;
        if (top && right) return NativeMethods.HTTOPRIGHT;
        if (bottom && left) return NativeMethods.HTBOTTOMLEFT;
        if (bottom && right) return NativeMethods.HTBOTTOMRIGHT;
        if (left) return NativeMethods.HTLEFT;
        if (right) return NativeMethods.HTRIGHT;
        if (top) return NativeMethods.HTTOP;
        if (bottom) return NativeMethods.HTBOTTOM;
        return NativeMethods.HTCLIENT;
    }

    public void Dispose()
    {
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }
}

/// <summary>Small Win32 window customisations that have no first-class WPF property.</summary>
public static class WindowAppearance
{
    public static void ApplyDarkTitleBar(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var value = dark ? 1 : 0;
        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd, NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        var corners = 2; // DWMWCP_ROUND
        _ = NativeMethods.DwmSetWindowAttribute(
            hwnd, NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE, ref corners, sizeof(int));
    }

    /// <summary>Keeps the floating camera out of Alt+Tab and the taskbar.</summary>
    public static void MakeToolWindow(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var style = NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE);
        style |= NativeMethods.WS_EX_TOOLWINDOW;
        style &= ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(hwnd, NativeMethods.GWL_EXSTYLE, style);
    }
}
