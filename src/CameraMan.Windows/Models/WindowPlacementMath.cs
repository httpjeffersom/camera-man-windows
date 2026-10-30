namespace CameraMan.Windows.Models;

public readonly record struct MonitorWorkArea(
    string DeviceName,
    int X,
    int Y,
    int Width,
    int Height,
    uint Dpi,
    bool Primary)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public double DpiScale => Dpi / 96.0;
}

public static class WindowPlacementMath
{
    public static int DipToPixels(double dip, uint dpi) =>
        (int)Math.Round(dip * dpi / 96.0, MidpointRounding.AwayFromZero);

    public static double PixelsToDip(int pixels, uint dpi) => pixels * 96.0 / dpi;

    /// <summary>Physical size for a preset on a monitor, honouring a user resize and never exceeding the work area.</summary>
    public static (int Width, int Height) ComputeSize(
        WindowSizePreset preset,
        MonitorWorkArea work,
        int? customWidthPx,
        int? customHeightPx)
    {
        if (preset == WindowSizePreset.Full)
        {
            return (work.Width, work.Height);
        }

        var dip = preset.DefaultSize();
        var width = customWidthPx is > 120 ? customWidthPx.Value : DipToPixels(dip, work.Dpi);
        var height = customHeightPx is > 120 ? customHeightPx.Value : DipToPixels(dip, work.Dpi);
        width = Math.Clamp(width, 1, work.Width);
        height = Math.Clamp(height, 1, work.Height);
        return (width, height);
    }

    /// <summary>
    /// Top-left corner in physical virtual-screen pixels. A saved origin is clamped back onto the work area (so a
    /// monitor change never strands the window); otherwise the chosen screen corner is used.
    /// </summary>
    public static (int X, int Y) ComputeOrigin(
        MonitorWorkArea work,
        ScreenEdge edge,
        int? savedX,
        int? savedY,
        int width,
        int height)
    {
        if (savedX.HasValue && savedY.HasValue)
        {
            return (
                Math.Clamp(savedX.Value, work.X, Math.Max(work.X, work.Right - width)),
                Math.Clamp(savedY.Value, work.Y, Math.Max(work.Y, work.Bottom - height)));
        }

        return edge switch
        {
            ScreenEdge.TopRight => (Math.Max(work.X, work.Right - width), work.Y),
            ScreenEdge.BottomRight => (Math.Max(work.X, work.Right - width), Math.Max(work.Y, work.Bottom - height)),
            ScreenEdge.BottomLeft => (work.X, Math.Max(work.Y, work.Bottom - height)),
            _ => (work.X, work.Y),
        };
    }

    /// <summary>Work-area test used when restoring a window to a monitor that has been unplugged.</summary>
    public static bool Contains(MonitorWorkArea work, int x, int y) =>
        x >= work.X && x < work.Right && y >= work.Y && y < work.Bottom;
}
