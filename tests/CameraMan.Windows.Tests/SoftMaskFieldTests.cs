using CameraMan.Windows.Rendering;

namespace CameraMan.Windows.Tests;

public class SoftMaskFieldTests
{
    private static byte[] Coverage(int width, int height, Func<int, int, bool> inside)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!inside(x, y))
                {
                    continue;
                }

                var offset = (y * width + x) * 4;
                pixels[offset] = 255;
                pixels[offset + 1] = 255;
                pixels[offset + 2] = 255;
                pixels[offset + 3] = 255;
            }
        }

        return pixels;
    }

    private static int Alpha(byte[] pixels, int width, int x, int y) =>
        pixels[(y * width + x) * 4 + 3];

    [Fact]
    public void HalfPlaneGetsALinearFeatherFromTheOutlineInward()
    {
        const int width = 48;
        const int height = 24;
        const int edge = 16;
        const int ramp = 8;
        var pixels = Coverage(width, height, (x, _) => x >= edge);

        SoftMaskField.Apply(pixels, width, height, ramp);

        var middle = height / 2;
        Assert.Equal(0, Alpha(pixels, width, edge - 1, middle));

        // Distance 1..8 px from the outline maps to 1/8..8/8 of full alpha.
        var expected = new[] { 31, 63, 95, 127, 159, 191, 223, 255 };
        for (var step = 0; step < expected.Length; step++)
        {
            Assert.InRange(Alpha(pixels, width, edge + step, middle), expected[step] - 2, expected[step] + 2);
        }

        Assert.Equal(255, Alpha(pixels, width, edge + ramp, middle));
    }

    [Fact]
    public void FeatherKeepsAConstantWidthAllAroundACircle()
    {
        const int size = 72;
        const int center = 36;
        const int radius = 24;
        const int ramp = 10;
        var pixels = Coverage(
            size,
            size,
            (x, y) => (x - center) * (x - center) + (y - center) * (y - center) <= radius * radius);

        SoftMaskField.Apply(pixels, size, size, ramp);

        // Walk inward along eight directions and find the depth where the feather reaches half
        // alpha: a constant-width fade must reach it at the same distance in every direction.
        var halfWidthDepths = new List<int>();
        for (var step = 0; step < 8; step++)
        {
            var angle = step * Math.PI / 4;
            var depth = -1;
            for (var t = 0; t <= radius; t++)
            {
                var x = (int)Math.Round(center + Math.Cos(angle) * (radius - t));
                var y = (int)Math.Round(center + Math.Sin(angle) * (radius - t));
                if (Alpha(pixels, size, x, y) >= 128)
                {
                    depth = t;
                    break;
                }
            }

            Assert.True(depth >= 0, $"no half-alpha pixel along direction {step}");
            halfWidthDepths.Add(depth);
        }

        Assert.InRange(halfWidthDepths.Max() - halfWidthDepths.Min(), 0, 1);
    }

    [Fact]
    public void PixelsOutsideTheShapeStayCompletelyTransparent()
    {
        const int size = 40;
        var pixels = Coverage(size, size, (x, _) => x >= 20);

        SoftMaskField.Apply(pixels, size, size, 12);

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                Assert.Equal(0, Alpha(pixels, size, x, y));
            }
        }
    }

    [Fact]
    public void FeatherGrowsMonotonicallyAlongTheInwardDistance()
    {
        const int size = 40;
        var pixels = Coverage(size, size, (_, y) => y >= 20);

        SoftMaskField.Apply(pixels, size, size, 10);

        var previous = -1;
        for (var y = 20; y < size; y++)
        {
            var alpha = Alpha(pixels, size, size / 2, y);
            Assert.True(alpha >= previous, $"feather decreased at row {y}");
            previous = alpha;
        }

        Assert.Equal(0, Alpha(pixels, size, size / 2, 19));
        Assert.True(Alpha(pixels, size, size / 2, 20) < 40);
        Assert.Equal(255, Alpha(pixels, size, size / 2, 29));
    }

    [Fact]
    public void RejectsBuffersSmallerThanTheRaster()
    {
        var pixels = new byte[16 * 16 * 4];

        Assert.Throws<ArgumentException>(() => SoftMaskField.Apply(pixels, 64, 64, 8));
    }
}
