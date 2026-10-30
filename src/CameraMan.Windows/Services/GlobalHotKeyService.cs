using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using CameraMan.Windows.Interop;

namespace CameraMan.Windows.Services;

/// <summary>
/// System-wide show/hide and size shortcuts. Windows reserves most Win-key chords, so the macOS ⌃⌥⌘C/⌃⌥⌘S become
/// Ctrl+Alt+C and Ctrl+Alt+S. Registration failures (another app already owns the chord) are reported in Settings.
/// </summary>
public sealed class GlobalHotKeyService : IDisposable
{
    private const int ToggleCameraId = 0xC0DE;
    private const int SwitchSizeId = 0xC0DF;

    private HwndSource? _source;
    private IntPtr _hwnd;

    public Action? ToggleCamera { get; set; }

    public Action? SwitchSize { get; set; }

    public string? LastError { get; private set; }

    public void Attach(IntPtr hwnd)
    {
        Detach();
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);
    }

    public void Apply(bool enabled)
    {
        UnregisterAll();
        LastError = null;
        if (!enabled || _hwnd == IntPtr.Zero)
        {
            return;
        }

        var errors = new List<string>();
        if (!NativeMethods.RegisterHotKey(
                _hwnd,
                ToggleCameraId,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
                NativeMethods.VK_C))
        {
            errors.Add(Describe("Ctrl+Alt+C", Marshal.GetLastWin32Error()));
        }

        if (!NativeMethods.RegisterHotKey(
                _hwnd,
                SwitchSizeId,
                NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT | NativeMethods.MOD_NOREPEAT,
                NativeMethods.VK_S))
        {
            errors.Add(Describe("Ctrl+Alt+S", Marshal.GetLastWin32Error()));
        }

        LastError = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
        if (errors.Count > 0)
        {
            UnregisterAll();
        }
    }

    private static string Describe(string chord, int error)
    {
        var reason = error == 1409
            ? "another application already uses this shortcut"
            : new Win32Exception(error).Message;
        return $"{chord}: {reason}";
    }

    public void UnregisterAll()
    {
        if (_hwnd != IntPtr.Zero)
        {
            NativeMethods.UnregisterHotKey(_hwnd, ToggleCameraId);
            NativeMethods.UnregisterHotKey(_hwnd, SwitchSizeId);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != NativeMethods.WM_HOTKEY)
        {
            return IntPtr.Zero;
        }

        switch (wParam.ToInt32())
        {
            case ToggleCameraId:
                handled = true;
                ToggleCamera?.Invoke();
                break;
            case SwitchSizeId:
                handled = true;
                SwitchSize?.Invoke();
                break;
        }

        return IntPtr.Zero;
    }

    private void Detach()
    {
        UnregisterAll();
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }

        _hwnd = IntPtr.Zero;
    }

    public void Dispose() => Detach();
}
