namespace CameraMan.Windows.Models;

/// <summary>
/// Everything CameraMan remembers between launches. Kept deliberately flat and JSON-friendly so the file can be
/// inspected and repaired by hand. Colours use <c>#AARRGGBB</c>, the same order as the macOS app.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    // Camera
    public string? SelectedDeviceId { get; set; }

    // Shape
    public ShapeType ShapeType { get; set; } = ShapeType.Circle;
    public double ShapeCornerRadius { get; set; }
    public double? SquareCornerRadius { get; set; }
    public double? VerticalCornerRadius { get; set; }
    public double? HorizontalCornerRadius { get; set; }
    public OrganicBlob OrganicBlob { get; set; } = OrganicBlob.Default;
    public bool OrganicBreathing { get; set; }
    public double SoftEdge { get; set; }

    // Framing
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
    public double Scale { get; set; } = 1.0;
    public bool FlipHorizontal { get; set; }
    public bool FlipVertical { get; set; }

    // Border
    public bool ShowBorder { get; set; }
    public double BorderWidth { get; set; }
    public string BorderColorHex { get; set; } = "#FF3B82F6";
    public bool BorderUseGradient { get; set; }
    public string BorderGradientStartHex { get; set; } = "#FF3B82F6";
    public string BorderGradientEndHex { get; set; } = "#FF8B5CF6";

    // Shadow
    public bool ShowShadow { get; set; }
    public double BorderShadowRadius { get; set; }
    public string BorderShadowColorHex { get; set; } = "#80000000";

    // Image
    public double Brightness { get; set; }
    public double Contrast { get; set; } = 1.0;
    public double Saturation { get; set; } = 1.0;

    // Window
    public WindowSizePreset WindowSizePreset { get; set; } = WindowSizePreset.Md;
    public ScreenEdge ScreenEdge { get; set; } = ScreenEdge.TopLeft;
    public Dictionary<string, PresetPlacement> Placements { get; set; } = [];
    public WindowSizePreset SpaceSlot1 { get; set; } = WindowSizePreset.Sm;
    public WindowSizePreset SpaceSlot2 { get; set; } = WindowSizePreset.Lg;
    public bool AlwaysOnTop { get; set; } = true;

    // Application
    public bool GlobalShortcutsEnabled { get; set; } = true;
    public bool OpenAtLogin { get; set; }
    public bool ShowSettingsOnStart { get; set; }

    /// <summary><c>system</c>, <c>en</c> or <c>pt-BR</c>.</summary>
    public string Language { get; set; } = "system";

    public PresetPlacement PlacementFor(WindowSizePreset preset)
    {
        var key = KeyFor(preset);
        if (!Placements.TryGetValue(key, out var placement))
        {
            placement = new PresetPlacement { ScreenEdge = ScreenEdge };
            Placements[key] = placement;
        }

        return placement;
    }

    public static string KeyFor(WindowSizePreset preset) => preset switch
    {
        WindowSizePreset.Xs => "xs",
        WindowSizePreset.Sm => "sm",
        WindowSizePreset.Md => "md",
        WindowSizePreset.Lg => "lg",
        WindowSizePreset.Full => "full",
        _ => "md",
    };

    /// <summary>Repairs values that would make the UI unusable after a hand-edited or older file.</summary>
    public void Normalize()
    {
        if (Version < 1)
        {
            Version = CurrentVersion;
        }

        ShapeCornerRadius = Clamp(ShapeCornerRadius, 0, 150);
        // The corner radius is now per shape; older settings only carry the shared value.
        SquareCornerRadius = Clamp(SquareCornerRadius ?? ShapeCornerRadius, 0, 150);
        VerticalCornerRadius = Clamp(VerticalCornerRadius ?? ShapeCornerRadius, 0, 150);
        HorizontalCornerRadius = Clamp(HorizontalCornerRadius ?? ShapeCornerRadius, 0, 150);
        SoftEdge = Clamp(SoftEdge, 0, 40);
        OffsetX = Clamp(OffsetX, -50, 50);
        OffsetY = Clamp(OffsetY, -50, 50);
        Scale = Clamp(Scale, 1, 2);
        BorderWidth = Clamp(BorderWidth, 0, 20);
        BorderShadowRadius = Clamp(BorderShadowRadius, 0, 24);
        Brightness = Clamp(Brightness, -0.5, 0.5);
        Contrast = Clamp(Contrast, 0.5, 2);
        Saturation = Clamp(Saturation, 0, 2);

        OrganicBlob = SanitizeBlob(OrganicBlob);

        if (!Enum.IsDefined(ScreenEdge))
        {
            ScreenEdge = ScreenEdge.TopLeft;
        }

        Placements = NormalizePlacements(Placements);

        if (!Enum.IsDefined(ShapeType))
        {
            ShapeType = ShapeType.Circle;
        }

        if (!Enum.IsDefined(WindowSizePreset))
        {
            WindowSizePreset = WindowSizePreset.Md;
        }

        if (!Enum.IsDefined(SpaceSlot1))
        {
            SpaceSlot1 = WindowSizePreset.Sm;
        }

        if (!Enum.IsDefined(SpaceSlot2))
        {
            SpaceSlot2 = WindowSizePreset.Lg;
        }

        if (Language is not ("system" or "en" or "pt-BR"))
        {
            Language = "system";
        }

        BorderColorHex = ColorCode.Normalize(BorderColorHex, "#FF3B82F6");
        BorderGradientStartHex = ColorCode.Normalize(BorderGradientStartHex, "#FF3B82F6");
        BorderGradientEndHex = ColorCode.Normalize(BorderGradientEndHex, "#FF8B5CF6");
        BorderShadowColorHex = ColorCode.Normalize(BorderShadowColorHex, "#80000000");
    }

    /// <summary>
    /// Keeps a blob inside the range the editor and the presets produce. A hand-edited file can otherwise carry a
    /// wave count, harmonic or amplitude that makes the outline degenerate (or makes sampling it very slow), and a
    /// <c>null</c> entry in <c>waves</c> would throw while cloning it at startup.
    /// </summary>
    private static OrganicBlob SanitizeBlob(OrganicBlob? blob)
    {
        const int maxWaves = 8;
        var waves = new List<OrganicBlob.Wave>(maxWaves);
        foreach (var wave in blob?.Waves ?? [])
        {
            if (wave is null || waves.Count == maxWaves)
            {
                continue;
            }

            waves.Add(new OrganicBlob.Wave(
                Math.Clamp(wave.K, 1, 8),
                FiniteOr(wave.Amplitude, 0, -0.25, 0.25),
                FiniteOr(wave.Phase, 0, -Math.Tau, Math.Tau)));
        }

        return waves.Count > 0 ? new OrganicBlob { Waves = waves } : OrganicBlob.Default;
    }

    /// <summary>
    /// Rebuilds the placement map with exactly the canonical preset keys, filling the missing ones. Matching is
    /// case-insensitive, so a hand-edited file with both "md" and "MD" ends up with a single entry instead of a
    /// duplicate-key crash when the state is built.
    /// </summary>
    private Dictionary<string, PresetPlacement> NormalizePlacements(
        Dictionary<string, PresetPlacement>? source)
    {
        var incoming = source ?? [];
        var normalized = new Dictionary<string, PresetPlacement>(StringComparer.Ordinal);
        foreach (var preset in WindowSizePresetExtensions.All)
        {
            var key = KeyFor(preset);
            var placement = incoming
                .Where(pair => pair.Value is not null
                    && string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value)
                .FirstOrDefault()
                ?? new PresetPlacement { ScreenEdge = ScreenEdge };

            if (!Enum.IsDefined(placement.ScreenEdge))
            {
                placement.ScreenEdge = ScreenEdge.TopLeft;
            }

            normalized[key] = placement;
        }

        return normalized;
    }

    private static double Clamp(double value, double min, double max) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : min;

    private static double FiniteOr(double value, double fallback, double min, double max) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
