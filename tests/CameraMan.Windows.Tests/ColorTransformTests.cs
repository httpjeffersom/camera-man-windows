using CameraMan.Windows.Camera;

namespace CameraMan.Windows.Tests;

public class ColorTransformTests
{
    private static byte[] Pixel(byte b, byte g, byte r, byte a = 255) => [b, g, r, a];

    [Fact]
    public void NeutralCopiesEveryByte()
    {
        var transform = ColorTransform.FromNeutral;
        var source = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var destination = new byte[64];

        transform.Apply(source, 32, destination, 32, 8, 2);

        Assert.True(transform.IsNeutral);
        Assert.Equal(source, destination);
    }

    [Fact]
    public void NeutralHonoursDifferentStrides()
    {
        var transform = ColorTransform.FromNeutral;
        var source = new byte[24];
        for (var i = 0; i < source.Length; i++)
        {
            source[i] = (byte)(i + 1);
        }

        var destination = new byte[32];
        transform.Apply(source, 12, destination, 16, 2, 2);

        // Only the 2 * 4 = 8 pixel bytes of each row are copied; the stride padding is left untouched.
        Assert.Equal(source[..8], destination[..8]);
        Assert.Equal(source[12..20], destination[16..24]);
    }

    [Fact]
    public void SaturationZeroProducesGrey()
    {
        var transform = new ColorTransform(new ColorAdjustment(0, 1, 0));
        var source = Pixel(20, 80, 200);
        var destination = new byte[4];

        transform.Apply(source, 4, destination, 4, 1, 1);

        Assert.Equal(destination[0], destination[1]);
        Assert.Equal(destination[1], destination[2]);
        Assert.Equal(255, destination[3]);
    }

    [Fact]
    public void PositiveBrightnessLiftsMidtones()
    {
        var neutral = RunNeutral(128);
        var brighter = Run(128, new ColorAdjustment(0.2, 1, 1));

        Assert.True(brighter > neutral);
        Assert.Equal(brighter, Run(128, new ColorAdjustment(0.2, 1, 1)));
    }

    [Fact]
    public void HigherContrastDarkensShadowsAndLiftsHighlights()
    {
        var shadow = Run(64, new ColorAdjustment(0, 2, 1));
        var highlight = Run(192, new ColorAdjustment(0, 2, 1));

        Assert.True(shadow < 64);
        Assert.True(highlight > 192);
    }

    [Fact]
    public void AlphaIsPreserved()
    {
        var transform = new ColorTransform(new ColorAdjustment(0.4, 2, 0));
        var source = Pixel(10, 20, 30, 77);
        var destination = new byte[4];

        transform.Apply(source, 4, destination, 4, 1, 1);

        Assert.Equal(77, destination[3]);
    }

    private static byte Run(byte value, ColorAdjustment adjustment)
    {
        var transform = new ColorTransform(adjustment);
        var source = Pixel(value, value, value);
        var destination = new byte[4];
        transform.Apply(source, 4, destination, 4, 1, 1);
        return destination[0];
    }

    private static byte RunNeutral(byte value) =>
        Run(value, ColorAdjustment.Neutral);
}
