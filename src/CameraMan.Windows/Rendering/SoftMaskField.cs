namespace CameraMan.Windows.Rendering;

/// <summary>
/// Turns a shape coverage raster into the soft-edge opacity mask used by the camera surface.
/// </summary>
/// <remarks>
/// The feather must be measured from the outline of the shape, never from the window bounds:
/// every pixel becomes <c>clamp(distanceToOutline / ramp, 0, 1)</c>, so the fade is exactly
/// zero on the contour, grows linearly with the pixel distance and keeps a constant width along
/// the whole shape (curves, corners, blob waves included).
/// <para>
/// The distance field is seeded from the pixels just outside the shape (coverage below half),
/// then relaxed with a two-pass chamfer transform. Rasterising the shape with a margin of empty
/// pixels matters: when the outline runs along the raster edge (circle tangencies, rectangle
/// sides touching the window) there would be no outside pixels to seed the field, the nearest
/// seeds would sit far away and the fade would collapse into a hard edge exactly there.
/// </para>
/// </remarks>
public static class SoftMaskField
{
    /// <summary>Distance transform weight for horizontal/vertical steps.</summary>
    private const float Ortho = 1f;

    /// <summary>Distance transform weight for diagonal steps.</summary>
    private const float Diagonal = 1.41421356f;

    /// <summary>Sentinel for pixels the relaxation has not reached yet.</summary>
    private const float Far = 1e6f;

    /// <summary>Coverage below this value counts as outside the shape and seeds distance zero.</summary>
    private const byte OutsideCoverage = 128;

    /// <summary>
    /// Fills the alpha channel of <paramref name="pixels"/> with the feather mask.
    /// </summary>
    /// <param name="pixels">BGRA coverage raster in place; the alpha channel holds the coverage.</param>
    /// <param name="width">Raster width in pixels.</param>
    /// <param name="height">Raster height in pixels.</param>
    /// <param name="rampPixels">
    /// Distance in raster pixels over which the fade goes from transparent (on the outline) to
    /// fully opaque. Values above this distance are fully opaque.
    /// </param>
    public static void Apply(byte[] pixels, int width, int height, float rampPixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (width <= 0 || height <= 0 || pixels.Length < width * height * 4)
        {
            throw new ArgumentException("Pixel buffer is smaller than the raster.", nameof(pixels));
        }

        var ramp = Math.Max(0.5f, rampPixels);
        var length = width * height;
        var distance = new float[length];
        for (var i = 0; i < length; i++)
        {
            distance[i] = pixels[i * 4 + 3] < OutsideCoverage ? 0f : Far;
        }

        // Pass 1 (top-left to bottom-right): propagate seeds from the left and from above.
        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 0; x < width; x++)
            {
                var d = distance[row + x];
                if (d == 0f)
                {
                    continue;
                }

                if (x > 0)
                {
                    var n = distance[row + x - 1] + Ortho;
                    if (n < d)
                    {
                        d = n;
                    }
                }

                if (y > 0)
                {
                    var up = row - width;
                    var n = distance[up + x] + Ortho;
                    if (n < d)
                    {
                        d = n;
                    }

                    if (x > 0)
                    {
                        n = distance[up + x - 1] + Diagonal;
                        if (n < d)
                        {
                            d = n;
                        }
                    }

                    if (x < width - 1)
                    {
                        n = distance[up + x + 1] + Diagonal;
                        if (n < d)
                        {
                            d = n;
                        }
                    }
                }

                distance[row + x] = d;
            }
        }

        // Pass 2 (bottom-right to top-left): propagate seeds from the right and from below.
        for (var y = height - 1; y >= 0; y--)
        {
            var row = y * width;
            for (var x = width - 1; x >= 0; x--)
            {
                var d = distance[row + x];
                if (d == 0f)
                {
                    continue;
                }

                if (x < width - 1)
                {
                    var n = distance[row + x + 1] + Ortho;
                    if (n < d)
                    {
                        d = n;
                    }
                }

                if (y < height - 1)
                {
                    var down = row + width;
                    var n = distance[down + x] + Ortho;
                    if (n < d)
                    {
                        d = n;
                    }

                    if (x < width - 1)
                    {
                        n = distance[down + x + 1] + Diagonal;
                        if (n < d)
                        {
                            d = n;
                        }
                    }

                    if (x > 0)
                    {
                        n = distance[down + x - 1] + Diagonal;
                        if (n < d)
                        {
                            d = n;
                        }
                    }
                }

                distance[row + x] = d;
            }
        }

        // Write the feather: zero on the outline, rising linearly with distance. Multiplying by
        // the coverage keeps the antialiased rim of the raster smooth (outside pixels have both
        // distance zero and zero coverage, so they always stay fully transparent).
        for (var i = 0; i < length; i++)
        {
            var coverage = pixels[i * 4 + 3];
            var alpha = (byte)(Math.Clamp(distance[i] / ramp, 0f, 1f) * coverage);
            var offset = i * 4;
            pixels[offset + 0] = alpha;
            pixels[offset + 1] = alpha;
            pixels[offset + 2] = alpha;
            pixels[offset + 3] = alpha;
        }
    }
}
