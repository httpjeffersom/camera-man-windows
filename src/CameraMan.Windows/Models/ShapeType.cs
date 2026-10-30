using System.Text.Json.Serialization;

namespace CameraMan.Windows.Models;

/// <summary>The outline the camera is cut to. Values mirror the macOS original.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ShapeType>))]
public enum ShapeType
{
    Circle,
    Organic,
    Square,
    VerticalRectangle,
    HorizontalRectangle,
}

public static class ShapeTypeExtensions
{
    public static string ResourceKey(this ShapeType shape) => shape switch
    {
        ShapeType.Circle => "Shape_Circle",
        ShapeType.Organic => "Shape_Organic",
        ShapeType.Square => "Shape_Square",
        ShapeType.VerticalRectangle => "Shape_Vertical",
        ShapeType.HorizontalRectangle => "Shape_Horizontal",
        _ => "Shape_Circle",
    };

    public static string ShortResourceKey(this ShapeType shape) => shape switch
    {
        ShapeType.Circle => "Shape_Circle_Short",
        ShapeType.Organic => "Shape_Organic",
        ShapeType.Square => "Shape_Square",
        ShapeType.VerticalRectangle => "Shape_Vertical_Short",
        ShapeType.HorizontalRectangle => "Shape_Horizontal_Short",
        _ => "Shape_Circle_Short",
    };

    /// <summary>Segoe Fluent Icons glyph used in the settings cards.</summary>
    public static string Glyph(this ShapeType shape) => shape switch
    {
        ShapeType.Circle => "\uEA3A",
        ShapeType.Organic => "\uE8B9",
        ShapeType.Square => "\uE7C4",
        ShapeType.VerticalRectangle => "\uE7C4",
        ShapeType.HorizontalRectangle => "\uE7C4",
        _ => "\uEA3A",
    };

    public static (int Width, int Height)? AspectRatio(this ShapeType shape) => shape switch
    {
        ShapeType.VerticalRectangle => (9, 16),
        ShapeType.HorizontalRectangle => (16, 9),
        _ => null,
    };
}
