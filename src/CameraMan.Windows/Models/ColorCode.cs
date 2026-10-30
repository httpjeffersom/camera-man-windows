using System.Globalization;
using System.Windows.Media;

namespace CameraMan.Windows.Models;

/// <summary>Persists colours as <c>#AARRGGBB</c> (sRGB), like the macOS original.</summary>
public static class ColorCode
{
    public static string ToHex(Color color) =>
        string.Create(CultureInfo.InvariantCulture, $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");

    public static Color Parse(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        var text = hex.Trim().TrimStart('#');
        try
        {
            return text.Length switch
            {
                3 => FromBytes(255,
                    (byte)(Convert.ToInt32(text[..1], 16) * 17),
                    (byte)(Convert.ToInt32(text.Substring(1, 1), 16) * 17),
                    (byte)(Convert.ToInt32(text.Substring(2, 1), 16) * 17)),
                6 => FromBytes(255,
                    Convert.ToByte(text[..2], 16),
                    Convert.ToByte(text.Substring(2, 2), 16),
                    Convert.ToByte(text.Substring(4, 2), 16)),
                8 => FromBytes(
                    Convert.ToByte(text[..2], 16),
                    Convert.ToByte(text.Substring(2, 2), 16),
                    Convert.ToByte(text.Substring(4, 2), 16),
                    Convert.ToByte(text.Substring(6, 2), 16)),
                _ => fallback,
            };
        }
        catch (FormatException)
        {
            return fallback;
        }
        catch (OverflowException)
        {
            return fallback;
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    public static string Normalize(string? hex, string fallback)
    {
        var fallbackColor = Parse(fallback, Colors.Transparent);
        var parsed = Parse(hex, fallbackColor);
        return ToHex(parsed);
    }

    public static Color WithOpacity(Color color, double opacity) =>
        Color.FromArgb((byte)Math.Round(Math.Clamp(opacity, 0, 1) * 255), color.R, color.G, color.B);

    /// <summary>
    /// Replaces the RGB channels of <paramref name="current"/> with those of <paramref name="picked"/>
    /// and keeps the current alpha. The WinForms colour dialog has no alpha channel, so without this a
    /// hue change would turn the 50 % black default shadow into a solid one. macOS takes opacity from
    /// the system colour panel instead.
    /// </summary>
    public static Color WithChannelsOf(Color current, Color picked) =>
        Color.FromArgb(current.A, picked.R, picked.G, picked.B);

    private static Color FromBytes(byte a, byte r, byte g, byte b) => Color.FromArgb(a, r, g, b);
}
