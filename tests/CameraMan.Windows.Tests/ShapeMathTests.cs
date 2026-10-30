using CameraMan.Windows.Models;

namespace CameraMan.Windows.Tests;

public class ShapeMathTests
{
    private static readonly RectD Bounds = new(0, 0, 400, 300);

    [Theory]
    [InlineData(ShapeType.Circle)]
    [InlineData(ShapeType.Square)]
    [InlineData(ShapeType.Organic)]
    public void SquareShapesAreCentredAndUseTheShortestSide(ShapeType shape)
    {
        var rect = ShapeMath.GetShapeRect(shape, Bounds);

        Assert.Equal(300, rect.Width);
        Assert.Equal(300, rect.Height);
        Assert.Equal(50, rect.X);
        Assert.Equal(0, rect.Y);
    }

    [Fact]
    public void VerticalRectangleFitsItsAspectInsideTheBounds()
    {
        var rect = ShapeMath.GetShapeRect(ShapeType.VerticalRectangle, Bounds);

        Assert.Equal(300 * 9.0 / 16.0, rect.Width, 6);
        Assert.Equal(300, rect.Height);
        Assert.Equal(200, rect.CenterX, 6);
        Assert.Equal(150, rect.CenterY, 6);
    }

    [Fact]
    public void HorizontalRectangleFitsItsAspectInsideTheBounds()
    {
        var rect = ShapeMath.GetShapeRect(ShapeType.HorizontalRectangle, Bounds);

        Assert.Equal(400, rect.Width);
        Assert.Equal(225, rect.Height);
        Assert.Equal(0, rect.X);
        Assert.Equal(37.5, rect.Y, 6);
    }

    [Fact]
    public void EmptyBoundsAreReturnedUnchanged()
    {
        var empty = new RectD(10, 20, 0, 50);

        Assert.Equal(empty, ShapeMath.GetShapeRect(ShapeType.Circle, empty));
        Assert.Equal(empty, ShapeMath.FitAspect(empty, 16, 9));
    }

    [Theory]
    [InlineData(0, 50, 0)]
    [InlineData(500, 50, 25)]
    [InlineData(25, 50, 25)]
    public void CornerRadiusIsClampedToHalfTheShortestSide(double radius, double side, double expected)
    {
        var rect = new RectD(0, 0, 200, side);

        Assert.Equal(expected, ShapeMath.ClampCornerRadius(radius, rect));
    }

    [Fact]
    public void SampledOrganicOutlineStaysInsideItsRect()
    {
        var blob = OrganicBlob.Default;
        var rect = ShapeMath.GetShapeRect(ShapeType.Organic, Bounds);
        var points = ShapeMath.SampleOrganic(blob, Bounds, time: 3.25, samples: 120);

        Assert.Equal(120, points.Count);
        Assert.All(points, point =>
        {
            Assert.InRange(point.X, rect.X - 1e-6, rect.Right + 1e-6);
            Assert.InRange(point.Y, rect.Y - 1e-6, rect.Bottom + 1e-6);
        });
    }

    [Fact]
    public void FitAspectKeepsTheTargetRatio()
    {
        var rect = ShapeMath.FitAspect(new RectD(0, 0, 1000, 400), 9, 16);

        Assert.Equal(225, rect.Width, 6);
        Assert.Equal(400, rect.Height, 6);
    }
}
