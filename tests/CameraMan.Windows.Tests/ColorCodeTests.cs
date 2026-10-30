using System.Windows.Media;
using CameraMan.Windows.Models;

namespace CameraMan.Windows.Tests;

public class ColorCodeTests
{
    [Theory]
    [InlineData("#FF3B82F6", 255, 59, 130, 246)]
    [InlineData("#3B82F6", 255, 59, 130, 246)]
    [InlineData("#F0A", 255, 255, 0, 170)]
    [InlineData("80000000", 128, 0, 0, 0)]
    public void ParseReadsTheSupportedForms(string hex, byte a, byte r, byte g, byte b)
    {
        var color = ColorCode.Parse(hex, Colors.Magenta);

        Assert.Equal(Color.FromArgb(a, r, g, b), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("#12")]
    [InlineData("#GGGGGG")]
    [InlineData("not a colour")]
    public void ParseFallsBackForInvalidInput(string? hex)
    {
        Assert.Equal(Colors.Orange, ColorCode.Parse(hex, Colors.Orange));
    }

    [Fact]
    public void ToHexUsesAlphaFirst()
    {
        Assert.Equal("#80336699", ColorCode.ToHex(Color.FromArgb(0x80, 0x33, 0x66, 0x99)));
    }

    [Fact]
    public void NormalizeExpandsShortFormAndAppliesTheFallback()
    {
        Assert.Equal("#FFFFFFFF", ColorCode.Normalize("#FFF", "#FF000000"));
        Assert.Equal("#FF000000", ColorCode.Normalize("nope", "#FF000000"));
        Assert.Equal("#FF000000", ColorCode.Normalize(null, "#FF000000"));
    }

    [Fact]
    public void WithOpacityKeepsTheRgbChannels()
    {
        var color = ColorCode.WithOpacity(Color.FromArgb(255, 10, 20, 30), 0.5);

        Assert.Equal(128, color.A);
        Assert.Equal(10, color.R);
        Assert.Equal(20, color.G);
        Assert.Equal(30, color.B);
    }

    [Fact]
    public void WithChannelsOfKeepsTheCurrentAlpha()
    {
        // A hue picked in the WinForms dialog (always opaque) must not harden the 50 % shadow.
        var color = ColorCode.WithChannelsOf(
            Color.FromArgb(0x80, 0, 0, 0),
            Color.FromRgb(0x33, 0x66, 0x99));

        Assert.Equal(Color.FromArgb(0x80, 0x33, 0x66, 0x99), color);
    }

    [Fact]
    public void WithChannelsOfKeepsOpaqueAlphaOpaque()
    {
        var color = ColorCode.WithChannelsOf(Colors.Red, Color.FromRgb(1, 2, 3));

        Assert.Equal(Color.FromArgb(255, 1, 2, 3), color);
    }
}
