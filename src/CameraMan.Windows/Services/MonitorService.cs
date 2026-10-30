using System.Runtime.InteropServices;
using CameraMan.Windows.Interop;
using CameraMan.Windows.Models;

namespace CameraMan.Windows.Services;

/// <summary>Enumerates monitors in physical pixels and reports their effective DPI.</summary>
public sealed class MonitorService
{
    public IReadOnlyList<MonitorWorkArea> GetMonitors()
    {
        var monitors = new List<MonitorWorkArea>();
        NativeMethods.MonitorEnumProc callback = (hMonitor, _, _, _) =>
        {
            var monitor = Read(hMonitor);
            if (monitor is not null)
            {
                monitors.Add(monitor.Value);
            }

            return true;
        };

        NativeMethods.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        GC.KeepAlive(callback);
        return monitors;
    }

    public MonitorWorkArea Primary()
    {
        var monitors = GetMonitors();
        return monitors.FirstOrDefault(m => m.Primary) is { Width: > 0 } primary
            ? primary
            : monitors.FirstOrDefault();
    }

    public MonitorWorkArea? FindByDeviceName(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
        {
            return null;
        }

        return GetMonitors().FirstOrDefault(m =>
            string.Equals(m.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
    }

    public MonitorWorkArea Nearest(IntPtr hwnd)
    {
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var result = Read(monitor);
        if (result is not null)
        {
            return result.Value;
        }

        return Primary();
    }

    public MonitorWorkArea NearestToPoint(int x, int y)
    {
        var monitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT { X = x, Y = y }, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var result = Read(monitor);
        if (result is not null)
        {
            return result.Value;
        }

        return Primary();
    }

    private static MonitorWorkArea? Read(IntPtr hMonitor)
    {
        if (hMonitor == IntPtr.Zero)
        {
            return null;
        }

        var info = new NativeMethods.MONITORINFOEX
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>(),
            szDevice = string.Empty,
        };
        if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
        {
            return null;
        }

        uint dpi = 96;
        if (NativeMethods.GetDpiForMonitor(hMonitor, NativeMethods.MDT_EFFECTIVE_DPI, out var dpiX, out _) == 0 &&
            dpiX > 0)
        {
            dpi = dpiX;
        }

        return new MonitorWorkArea(
            info.szDevice,
            info.rcWork.Left,
            info.rcWork.Top,
            info.rcWork.Right - info.rcWork.Left,
            info.rcWork.Bottom - info.rcWork.Top,
            dpi,
            (info.dwFlags & NativeMethods.MONITORINFOF_PRIMARY) != 0);
    }
}
