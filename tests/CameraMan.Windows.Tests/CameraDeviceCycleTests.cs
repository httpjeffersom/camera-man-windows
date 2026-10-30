using CameraMan.Windows.Models;

namespace CameraMan.Windows.Tests;

public class CameraDeviceCycleTests
{
    private static readonly CameraDevice A = new("a", "Camera A");
    private static readonly CameraDevice B = new("b", "Camera B");
    private static readonly CameraDevice C = new("c", "Camera C");
    private static readonly IReadOnlyList<CameraDevice> Devices = [A, B, C];

    [Theory]
    [InlineData("a", "b")]
    [InlineData("b", "c")]
    [InlineData("c", "a")]
    [InlineData("missing", "a")]
    [InlineData(null, "a")]
    public void NextWrapsAroundAndFallsBackToTheFirst(string? current, string expected)
    {
        Assert.Equal(expected, CameraDeviceCycle.Next(Devices, current));
    }

    [Fact]
    public void NextReturnsNullWithoutDevices()
    {
        Assert.Null(CameraDeviceCycle.Next([], "a"));
    }

    [Theory]
    [InlineData("b", "b")]
    [InlineData("missing", "a")]
    [InlineData(null, "a")]
    public void ResolveKeepsAnAvailableDeviceOrFallsBack(string? preferred, string expected)
    {
        Assert.Equal(expected, CameraDeviceCycle.Resolve(Devices, preferred));
    }

    [Fact]
    public void ResolveReturnsNullWithoutDevices()
    {
        Assert.Null(CameraDeviceCycle.Resolve([], "a"));
    }
}
