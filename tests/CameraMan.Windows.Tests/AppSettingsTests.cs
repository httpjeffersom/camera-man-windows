using CameraMan.Windows.Models;

namespace CameraMan.Windows.Tests;

public class AppSettingsTests
{
    [Fact]
    public void NormalizeClampsOutOfRangeValues()
    {
        var settings = new AppSettings
        {
            ShapeCornerRadius = 999,
            SquareCornerRadius = 999,
            VerticalCornerRadius = -3,
            HorizontalCornerRadius = 150.5,
            SoftEdge = -4,
            OffsetX = 500,
            OffsetY = -500,
            Scale = 9,
            BorderWidth = 100,
            BorderShadowRadius = 100,
            Brightness = 7,
            Contrast = 0,
            Saturation = -1,
        };

        settings.Normalize();

        Assert.Equal(150, settings.ShapeCornerRadius);
        Assert.Equal(150, settings.SquareCornerRadius);
        Assert.Equal(0, settings.VerticalCornerRadius);
        Assert.Equal(150, settings.HorizontalCornerRadius);
        Assert.Equal(0, settings.SoftEdge);
        Assert.Equal(50, settings.OffsetX);
        Assert.Equal(-50, settings.OffsetY);
        Assert.Equal(2, settings.Scale);
        Assert.Equal(20, settings.BorderWidth);
        Assert.Equal(24, settings.BorderShadowRadius);
        Assert.Equal(0.5, settings.Brightness);
        Assert.Equal(0.5, settings.Contrast);
        Assert.Equal(0, settings.Saturation);
    }

    [Fact]
    public void NormalizeMigratesLegacySharedCornerRadiusPerShape()
    {
        // Older settings files only carry the shared "shapeCornerRadius"; each shape must inherit it.
        var legacy = new AppSettings { ShapeCornerRadius = 24 };
        legacy.Normalize();

        Assert.Equal(24, legacy.SquareCornerRadius);
        Assert.Equal(24, legacy.VerticalCornerRadius);
        Assert.Equal(24, legacy.HorizontalCornerRadius);

        // A partially migrated file keeps the per-shape values that were already written.
        var partial = new AppSettings { ShapeCornerRadius = 24, HorizontalCornerRadius = 60 };
        partial.Normalize();

        Assert.Equal(24, partial.SquareCornerRadius);
        Assert.Equal(24, partial.VerticalCornerRadius);
        Assert.Equal(60, partial.HorizontalCornerRadius);
    }

    [Fact]
    public void NormalizeReplacesNonFiniteValues()
    {
        var settings = new AppSettings
        {
            SoftEdge = double.NaN,
            OffsetX = double.PositiveInfinity,
            Scale = double.NegativeInfinity,
        };

        settings.Normalize();

        Assert.Equal(0, settings.SoftEdge);
        Assert.Equal(-50, settings.OffsetX);
        Assert.Equal(1, settings.Scale);
    }

    [Fact]
    public void NormalizeRepairsEnumsColorsAndLanguage()
    {
        var settings = new AppSettings
        {
            ShapeType = (ShapeType)99,
            WindowSizePreset = (WindowSizePreset)99,
            SpaceSlot1 = (WindowSizePreset)99,
            SpaceSlot2 = (WindowSizePreset)99,
            ScreenEdge = (ScreenEdge)99,
            Language = "klingon",
            BorderColorHex = "not-a-colour",
            BorderGradientStartHex = "#abc",
        };

        settings.Normalize();

        Assert.Equal(ShapeType.Circle, settings.ShapeType);
        Assert.Equal(WindowSizePreset.Md, settings.WindowSizePreset);
        Assert.Equal(WindowSizePreset.Sm, settings.SpaceSlot1);
        Assert.Equal(WindowSizePreset.Lg, settings.SpaceSlot2);
        Assert.Equal(ScreenEdge.TopLeft, settings.ScreenEdge);
        Assert.Equal("system", settings.Language);
        Assert.Equal("#FF3B82F6", settings.BorderColorHex);
        Assert.Equal("#FFAABBCC", settings.BorderGradientStartHex);
    }

    [Fact]
    public void NormalizeCreatesEveryPresetPlacement()
    {
        var settings = new AppSettings { Placements = null! };

        settings.Normalize();

        Assert.NotNull(settings.Placements);
        foreach (var preset in WindowSizePresetExtensions.All)
        {
            Assert.True(settings.Placements.ContainsKey(AppSettings.KeyFor(preset)));
        }
    }

    [Fact]
    public void NormalizeKeepsAUsableOrganicBlob()
    {
        var empty = new AppSettings { OrganicBlob = new OrganicBlob() };
        empty.Normalize();
        Assert.True(empty.OrganicBlob.Waves.Count > 0);

        var missing = new AppSettings { OrganicBlob = null! };
        missing.Normalize();
        Assert.True(missing.OrganicBlob.Waves.Count > 0);
    }

    [Fact]
    public void NormalizeSanitizesHandEditedBlobWaves()
    {
        // A null entry would throw while cloning at startup; the extreme values would make the outline degenerate
        // or slow to sample.
        var settings = new AppSettings
        {
            OrganicBlob = new OrganicBlob
            {
                Waves =
                [
                    null!,
                    new OrganicBlob.Wave(999, 40, 1e9),
                    new OrganicBlob.Wave(0, double.NaN, double.PositiveInfinity),
                ],
            },
        };

        settings.Normalize();

        var waves = settings.OrganicBlob.Waves;
        Assert.Equal(2, waves.Count);
        Assert.All(waves, wave =>
        {
            Assert.InRange(wave.K, 1, 8);
            Assert.InRange(wave.Amplitude, -0.25, 0.25);
            Assert.InRange(wave.Phase, -Math.Tau, Math.Tau);
        });
        Assert.Equal(0.25, waves[0].Amplitude);
        Assert.Equal(0, waves[1].Amplitude);
        Assert.Equal(0, waves[1].Phase);
    }

    [Fact]
    public void NormalizeLimitsTheWaveCount()
    {
        var settings = new AppSettings
        {
            OrganicBlob = new OrganicBlob(
                Enumerable.Range(0, 64).Select(i => new OrganicBlob.Wave(2, 0.01, i)).ToArray()),
        };

        settings.Normalize();

        Assert.Equal(8, settings.OrganicBlob.Waves.Count);
    }

    [Fact]
    public void NormalizeMergesPlacementKeysCaseInsensitively()
    {
        // "MD" and "md" are the same preset; keeping both used to make the state constructor throw.
        var settings = new AppSettings
        {
            Placements = new Dictionary<string, PresetPlacement>(StringComparer.Ordinal)
            {
                ["MD"] = new() { OriginX = 10, OriginY = 20 },
                ["md"] = new() { OriginX = 30, OriginY = 40 },
            },
        };

        settings.Normalize();

        Assert.Equal(
            WindowSizePresetExtensions.All.Select(AppSettings.KeyFor).OrderBy(k => k),
            settings.Placements.Keys.OrderBy(k => k));
        Assert.Equal(10, settings.PlacementFor(WindowSizePreset.Md).OriginX);
    }

    [Fact]
    public void PlacementForCreatesMissingEntries()
    {
        var settings = new AppSettings();
        settings.Placements.Clear();

        var placement = settings.PlacementFor(WindowSizePreset.Xs);

        Assert.Equal(ScreenEdge.TopLeft, placement.ScreenEdge);
        Assert.Contains("xs", settings.Placements.Keys);
    }

    [Fact]
    public void PresetKeysMatchTheJsonContract()
    {
        Assert.Equal("xs", AppSettings.KeyFor(WindowSizePreset.Xs));
        Assert.Equal("sm", AppSettings.KeyFor(WindowSizePreset.Sm));
        Assert.Equal("md", AppSettings.KeyFor(WindowSizePreset.Md));
        Assert.Equal("lg", AppSettings.KeyFor(WindowSizePreset.Lg));
        Assert.Equal("full", AppSettings.KeyFor(WindowSizePreset.Full));
    }
}
