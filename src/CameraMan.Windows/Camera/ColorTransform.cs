namespace CameraMan.Windows.Camera;

/// <summary>Brightness, contrast and saturation matching the ranges of the macOS app.</summary>
public readonly record struct ColorAdjustment(double Brightness, double Contrast, double Saturation)
{
    public static ColorAdjustment Neutral => new(0, 1, 1);

    public bool IsNeutral =>
        Math.Abs(Brightness) < 1e-6 && Math.Abs(Contrast - 1) < 1e-6 && Math.Abs(Saturation - 1) < 1e-6;
}

/// <summary>
/// Applies brightness, contrast and saturation to BGRA8 frames. A 256-entry tone table makes brightness/contrast a
/// single lookup; saturation is a fixed-point mix towards luminance. Only used when a value differs from neutral.
/// The work happens on the capture thread, before a frame is published, so the UI thread never filters pixels.
/// </summary>
public sealed class ColorTransform
{
    private readonly byte[] _tone = new byte[256];
    private readonly bool _isNeutral;
    private readonly double _saturation;

    public ColorTransform(ColorAdjustment adjustment)
    {
        _isNeutral = adjustment.IsNeutral;
        _saturation = Math.Clamp(adjustment.Saturation, 0, 2);
        var contrast = Math.Clamp(adjustment.Contrast, 0.5, 2);
        var brightness = Math.Clamp(adjustment.Brightness, -0.5, 0.5);
        for (var i = 0; i < 256; i++)
        {
            var normalized = i / 255.0;
            var corrected = (normalized - 0.5) * contrast + 0.5 + brightness;
            _tone[i] = (byte)Math.Round(Math.Clamp(corrected, 0, 1) * 255);
        }
    }

    public static ColorTransform FromNeutral => new(ColorAdjustment.Neutral);

    public bool IsNeutral => _isNeutral;

    /// <summary>Copies BGRA rows from source to destination, applying the colour controls.</summary>
    public unsafe void Apply(
        byte[] source,
        int sourceStride,
        byte[] destination,
        int destinationStride,
        int width,
        int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        fixed (byte* sourceBase = source)
        fixed (byte* destinationBase = destination)
        {
            Apply(sourceBase, sourceStride, destinationBase, destinationStride, width, height);
        }
    }

    public unsafe void Apply(
        byte* source,
        int sourceStride,
        byte* destination,
        int destinationStride,
        int width,
        int height)
    {
        var rowBytes = width * 4;
        if (_isNeutral)
        {
            for (var y = 0; y < height; y++)
            {
                Buffer.MemoryCopy(
                    source + (long)y * sourceStride,
                    destination + (long)y * destinationStride,
                    destinationStride,
                    rowBytes);
            }

            return;
        }

        var saturation = (float)_saturation;
        for (var y = 0; y < height; y++)
        {
            var src = source + (long)y * sourceStride;
            var dst = destination + (long)y * destinationStride;
            for (var x = 0; x < width; x++)
            {
                var b = _tone[src[0]];
                var g = _tone[src[1]];
                var r = _tone[src[2]];
                if (Math.Abs(saturation - 1f) > 1e-6f)
                {
                    // Integer luminance (Rec. 709 weights) then linear saturation.
                    var luma = (r * 54 + g * 183 + b * 19) >> 8;
                    r = (byte)Math.Clamp((int)(luma + (r - luma) * saturation), 0, 255);
                    g = (byte)Math.Clamp((int)(luma + (g - luma) * saturation), 0, 255);
                    b = (byte)Math.Clamp((int)(luma + (b - luma) * saturation), 0, 255);
                }

                dst[0] = b;
                dst[1] = g;
                dst[2] = r;
                dst[3] = src[3];
                src += 4;
                dst += 4;
            }
        }
    }
}
