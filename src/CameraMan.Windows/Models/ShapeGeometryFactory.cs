using System.Windows;
using System.Windows.Media;

namespace CameraMan.Windows.Models;

/// <summary>Turns the shared shape math into WPF geometry for the live window and the settings previews.</summary>
public static class ShapeGeometryFactory
{
    public static Geometry Create(
        ShapeType shape,
        Rect bounds,
        double cornerRadius,
        OrganicBlob? blob = null,
        double time = 0)
    {
        var rect = ShapeMath.GetShapeRect(shape, new RectD(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        var wpfRect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);

        Geometry geometry = shape switch
        {
            ShapeType.Organic => CreateOrganic(bounds, blob ?? OrganicBlob.Default, time),
            ShapeType.Circle => new EllipseGeometry(wpfRect),
            ShapeType.Square or ShapeType.VerticalRectangle or ShapeType.HorizontalRectangle
                => new RectangleGeometry(wpfRect)
                {
                    RadiusX = ShapeMath.ClampCornerRadius(cornerRadius, rect),
                    RadiusY = ShapeMath.ClampCornerRadius(cornerRadius, rect),
                },
            _ => new EllipseGeometry(wpfRect),
        };

        geometry.Freeze();
        return geometry;
    }

    public static Geometry CreateOrganic(Rect bounds, OrganicBlob blob, double time)
    {
        var points = ShapeMath.SampleOrganic(
            blob,
            new RectD(bounds.X, bounds.Y, bounds.Width, bounds.Height),
            time);
        if (points.Count < 3)
        {
            return Geometry.Empty;
        }

        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(points[0].X, points[0].Y), isFilled: true, isClosed: true);
            for (var i = 1; i < points.Count; i++)
            {
                // isStroked must be true: the same geometry is also stroked for the border and the
                // shadow, and segments flagged as not stroked draw nothing there.
                context.LineTo(new Point(points[i].X, points[i].Y), isStroked: true, isSmoothJoin: true);
            }
        }

        geometry.Freeze();
        return geometry;
    }
}


