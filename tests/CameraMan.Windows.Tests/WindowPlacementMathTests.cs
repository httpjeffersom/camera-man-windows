using CameraMan.Windows.Models;

namespace CameraMan.Windows.Tests;

public class WindowPlacementMathTests
{
    private static readonly MonitorWorkArea Work = new("\\\\.\\DISPLAY1", 0, 0, 1920, 1040, 96, true);

    [Theory]
    [InlineData(400, 96, 400)]
    [InlineData(400, 120, 500)]
    [InlineData(400, 144, 600)]
    [InlineData(400, 192, 800)]
    public void DipToPixelsUsesTheMonitorDpi(double dip, uint dpi, int expected)
    {
        Assert.Equal(expected, WindowPlacementMath.DipToPixels(dip, dpi));
    }

    [Fact]
    public void PixelsToDipIsTheInverse()
    {
        Assert.Equal(400, WindowPlacementMath.PixelsToDip(600, 144), 6);
    }

    [Fact]
    public void ComputeSizeUsesThePresetDefault()
    {
        var size = WindowPlacementMath.ComputeSize(WindowSizePreset.Md, Work, null, null);

        Assert.Equal((400, 400), size);
    }

    [Fact]
    public void ComputeSizeHonoursAUserResize()
    {
        var size = WindowPlacementMath.ComputeSize(WindowSizePreset.Sm, Work, 512, 256);

        Assert.Equal((512, 256), size);
    }

    [Fact]
    public void ComputeSizeNeverExceedsTheWorkArea()
    {
        var size = WindowPlacementMath.ComputeSize(WindowSizePreset.Lg, Work, 4000, 4000);

        Assert.Equal((1920, 1040), size);
    }

    [Fact]
    public void FullPresetFillsTheWorkArea()
    {
        Assert.Equal((1920, 1040), WindowPlacementMath.ComputeSize(WindowSizePreset.Full, Work, 200, 200));
    }

    [Theory]
    [InlineData(ScreenEdge.TopLeft, 0, 0)]
    [InlineData(ScreenEdge.TopRight, 1520, 0)]
    [InlineData(ScreenEdge.BottomRight, 1520, 640)]
    [InlineData(ScreenEdge.BottomLeft, 0, 640)]
    public void UnplacedWindowsUseTheChosenCorner(ScreenEdge edge, int expectedX, int expectedY)
    {
        var origin = WindowPlacementMath.ComputeOrigin(Work, edge, null, null, 400, 400);

        Assert.Equal((expectedX, expectedY), origin);
    }

    [Fact]
    public void SavedOriginsAreClampedBackOntoTheWorkArea()
    {
        Assert.Equal((1520, 640), WindowPlacementMath.ComputeOrigin(Work, ScreenEdge.TopLeft, 5000, 5000, 400, 400));
        Assert.Equal((0, 0), WindowPlacementMath.ComputeOrigin(Work, ScreenEdge.BottomRight, -500, -500, 400, 400));
    }

    [Fact]
    public void SavedOriginsWinOverTheCorner()
    {
        Assert.Equal((123, 456), WindowPlacementMath.ComputeOrigin(Work, ScreenEdge.BottomRight, 123, 456, 400, 400));
    }

    [Fact]
    public void ContainsChecksTheWorkArea()
    {
        Assert.True(WindowPlacementMath.Contains(Work, 0, 0));
        Assert.True(WindowPlacementMath.Contains(Work, 1919, 1039));
        Assert.False(WindowPlacementMath.Contains(Work, 1920, 500));
        Assert.False(WindowPlacementMath.Contains(Work, -1, 500));
    }
}
