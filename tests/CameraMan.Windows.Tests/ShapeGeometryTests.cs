using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using CameraMan.Windows.Models;

namespace CameraMan.Windows.Tests;

/// <summary>
/// The border and the shadow stroke the same geometry the camera is clipped to, so every shape
/// outline has to be strokable.
/// </summary>
public sealed class ShapeGeometryTests
{
    [Theory]
    [InlineData(ShapeType.Circle)]
    [InlineData(ShapeType.Organic)]
    [InlineData(ShapeType.Square)]
    [InlineData(ShapeType.VerticalRectangle)]
    [InlineData(ShapeType.HorizontalRectangle)]
    public void EveryShapeOutlineCanBeStroked(ShapeType shape)
    {
        RunOnSta(() =>
        {
            var bounds = new Rect(0, 0, 200, 200);
            var geometry = ShapeGeometryFactory.Create(shape, bounds, 24, OrganicBlob.Default, 1.5);
            var outline = geometry.Bounds;
            var stroke = geometry.GetWidenedPathGeometry(new Pen(Brushes.Black, 10)).Bounds;

            Assert.False(stroke.IsEmpty, $"{shape} produced no stroke geometry at all.");
            // A 10 px pen grows the outline by 5 px on every side.
            Assert.True(
                stroke.Width >= outline.Width + 8,
                $"{shape}: stroke width {stroke.Width:0.##} does not cover outline width {outline.Width:0.##}.");
            Assert.True(
                stroke.Height >= outline.Height + 8,
                $"{shape}: stroke height {stroke.Height:0.##} does not cover outline height {outline.Height:0.##}.");
        });
    }

    [Fact]
    public void BreathingFramesStayStrokable()
    {
        RunOnSta(() =>
        {
            var bounds = new Rect(0, 0, 200, 200);
            foreach (var time in new[] { 0d, 0.7, 1.9, 4.2 })
            {
                var geometry = ShapeGeometryFactory.Create(
                    ShapeType.Organic, bounds, 0, OrganicBlob.Default, time);
                var outline = geometry.Bounds;
                var stroke = geometry.GetWidenedPathGeometry(new Pen(Brushes.Black, 6)).Bounds;

                Assert.True(
                    stroke.Width >= outline.Width + 4,
                    $"t={time}: breathing outline is not strokable (stroke {stroke.Width:0.##}).");
            }
        });
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "STA geometry test timed out.");

        if (error is not null)
        {
            ExceptionDispatchInfo.Capture(error).Throw();
        }
    }
}
