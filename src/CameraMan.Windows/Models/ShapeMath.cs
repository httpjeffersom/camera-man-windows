namespace CameraMan.Windows.Models;

/// <summary>Double-precision rectangle, kept free of WPF types so it can be unit tested.</summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public RectD Deflate(double amount)
    {
        var width = Math.Max(0, Width - amount * 2);
        var height = Math.Max(0, Height - amount * 2);
        return new RectD(X + amount, Y + amount, width, height);
    }

    public static RectD FromSize(double width, double height) => new(0, 0, width, height);
}

public static class ShapeMath
{
    /// <summary>
    /// The rectangle of the actual outline inside the available bounds. Circle and square are centred squares;
    /// the rectangle shapes fit their 9:16 / 16:9 aspect ratio; organic fills the largest centred square.
    /// </summary>
    public static RectD GetShapeRect(ShapeType shape, RectD bounds)
    {
        if (bounds.IsEmpty)
        {
            return bounds;
        }

        if (shape.AspectRatio() is { } aspect)
        {
            return FitAspect(bounds, aspect.Width, aspect.Height);
        }

        var side = Math.Min(bounds.Width, bounds.Height);
        return new RectD(bounds.CenterX - side / 2, bounds.CenterY - side / 2, side, side);
    }

    /// <summary>A centred rect with the given aspect ratio that fits inside <paramref name="bounds"/>.</summary>
    public static RectD FitAspect(RectD bounds, double widthRatio, double heightRatio)
    {
        if (bounds.IsEmpty || widthRatio <= 0 || heightRatio <= 0)
        {
            return bounds;
        }

        var targetAspect = widthRatio / heightRatio;
        var currentAspect = bounds.Width / bounds.Height;
        double width;
        double height;
        if (currentAspect > targetAspect)
        {
            height = bounds.Height;
            width = height * targetAspect;
        }
        else
        {
            width = bounds.Width;
            height = width / targetAspect;
        }

        return new RectD(bounds.CenterX - width / 2, bounds.CenterY - height / 2, width, height);
    }

    /// <summary>Corner radius cannot exceed half the shortest side.</summary>
    public static double ClampCornerRadius(double radius, RectD rect) =>
        Math.Clamp(radius, 0, Math.Min(rect.Width, rect.Height) / 2);

    /// <summary>Radius of the organic outline at angle <paramref name="theta"/>, scaled to fit the rect.</summary>
    public static double OrganicPointRadius(OrganicBlob blob, double theta, double time, double scale) =>
        blob.Radius(theta, time) * scale;

    public static IReadOnlyList<(double X, double Y)> SampleOrganic(
        OrganicBlob blob, RectD bounds, double time, int samples = 240)
    {
        samples = Math.Max(12, samples);
        var rect = GetShapeRect(ShapeType.Organic, bounds);
        var maxRadius = Math.Max(1e-9, blob.MaxRadius(time));
        var scale = Math.Min(rect.Width, rect.Height) / 2 / maxRadius;
        var points = new List<(double, double)>(samples);
        for (var i = 0; i < samples; i++)
        {
            var theta = 2 * Math.PI * i / samples;
            var radius = blob.Radius(theta, time) * scale;
            points.Add((rect.CenterX + radius * Math.Cos(theta), rect.CenterY + radius * Math.Sin(theta)));
        }

        return points;
    }
}
