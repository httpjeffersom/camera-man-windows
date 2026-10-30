using System.Text.Json.Serialization;

namespace CameraMan.Windows.Models;

[JsonConverter(typeof(JsonStringEnumConverter<WindowSizePreset>))]
public enum WindowSizePreset
{
    Xs,
    Sm,
    Md,
    Lg,
    Full,
}

[JsonConverter(typeof(JsonStringEnumConverter<ScreenEdge>))]
public enum ScreenEdge
{
    TopLeft,
    TopRight,
    BottomRight,
    BottomLeft,
}

public static class WindowSizePresetExtensions
{
    public static readonly WindowSizePreset[] All =
    [
        WindowSizePreset.Xs,
        WindowSizePreset.Sm,
        WindowSizePreset.Md,
        WindowSizePreset.Lg,
        WindowSizePreset.Full,
    ];

    public static readonly ScreenEdge[] Edges =
    [
        ScreenEdge.TopLeft,
        ScreenEdge.TopRight,
        ScreenEdge.BottomRight,
        ScreenEdge.BottomLeft,
    ];

    /// <summary>Fixed size in device-independent pixels; <see cref="WindowSizePreset.Full"/> fills the work area.</summary>
    public static double DefaultSize(this WindowSizePreset preset) => preset switch
    {
        WindowSizePreset.Xs => 200,
        WindowSizePreset.Sm => 300,
        WindowSizePreset.Md => 400,
        WindowSizePreset.Lg => 560,
        WindowSizePreset.Full => 960,
        _ => 400,
    };

    public static string ResourceKey(this WindowSizePreset preset) => preset switch
    {
        WindowSizePreset.Xs => "Size_Xs",
        WindowSizePreset.Sm => "Size_Sm",
        WindowSizePreset.Md => "Size_Md",
        WindowSizePreset.Lg => "Size_Lg",
        WindowSizePreset.Full => "Size_Full",
        _ => "Size_Md",
    };

    public static string ResourceKey(this ScreenEdge edge) => edge switch
    {
        ScreenEdge.TopLeft => "Edge_TopLeft",
        ScreenEdge.TopRight => "Edge_TopRight",
        ScreenEdge.BottomRight => "Edge_BottomRight",
        ScreenEdge.BottomLeft => "Edge_BottomLeft",
        _ => "Edge_TopLeft",
    };
}

/// <summary>Where a size preset was last placed. Coordinates and sizes are physical pixels.</summary>
public sealed class PresetPlacement
{
    public ScreenEdge ScreenEdge { get; set; } = ScreenEdge.TopLeft;

    /// <summary>Saved top-left corner in physical virtual-screen coordinates; null means use the screen corner.</summary>
    public int? OriginX { get; set; }

    public int? OriginY { get; set; }

    /// <summary>Win32 monitor device name (e.g. <c>\\.\DISPLAY1</c>), so a window returns to its monitor.</summary>
    public string? MonitorDeviceName { get; set; }

    /// <summary>Custom physical size after the user resizes; null means the preset's default.</summary>
    public int? WidthPx { get; set; }

    public int? HeightPx { get; set; }

    public PresetPlacement Clone() => new()
    {
        ScreenEdge = ScreenEdge,
        OriginX = OriginX,
        OriginY = OriginY,
        MonitorDeviceName = MonitorDeviceName,
        WidthPx = WidthPx,
        HeightPx = HeightPx,
    };
}
