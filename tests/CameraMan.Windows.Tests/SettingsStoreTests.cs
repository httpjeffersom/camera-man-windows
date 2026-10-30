using System.IO;
using CameraMan.Windows.Models;
using CameraMan.Windows.Services;

namespace CameraMan.Windows.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void SaveAndLoadRoundTripEveryImportantSetting()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore(temp.Path);
        var settings = new AppSettings
        {
            SelectedDeviceId = "cam-2",
            ShapeType = ShapeType.HorizontalRectangle,
            ShapeCornerRadius = 24,
            SoftEdge = 12,
            OffsetX = -8,
            OffsetY = 9,
            Scale = 1.75,
            FlipHorizontal = true,
            ShowBorder = true,
            BorderWidth = 5,
            BorderColorHex = "#FF112233",
            BorderUseGradient = true,
            ShowShadow = true,
            BorderShadowRadius = 10,
            Brightness = -0.2,
            Contrast = 1.4,
            Saturation = 0.75,
            WindowSizePreset = WindowSizePreset.Lg,
            ScreenEdge = ScreenEdge.BottomRight,
            SpaceSlot1 = WindowSizePreset.Md,
            SpaceSlot2 = WindowSizePreset.Xs,
            AlwaysOnTop = false,
            GlobalShortcutsEnabled = false,
            Language = "en",
        };
        settings.PlacementFor(WindowSizePreset.Lg).OriginX = 1234;

        store.Save(settings);
        var loaded = store.Load(out var warning);

        Assert.Null(warning);
        Assert.Equal("cam-2", loaded.SelectedDeviceId);
        Assert.Equal(ShapeType.HorizontalRectangle, loaded.ShapeType);
        Assert.Equal(24, loaded.ShapeCornerRadius);
        Assert.Equal(12, loaded.SoftEdge);
        Assert.Equal(-8, loaded.OffsetX);
        Assert.Equal(9, loaded.OffsetY);
        Assert.Equal(1.75, loaded.Scale);
        Assert.True(loaded.FlipHorizontal);
        Assert.Equal("#FF112233", loaded.BorderColorHex);
        Assert.True(loaded.BorderUseGradient);
        Assert.Equal(10, loaded.BorderShadowRadius);
        Assert.Equal(-0.2, loaded.Brightness);
        Assert.Equal(1.4, loaded.Contrast);
        Assert.Equal(0.75, loaded.Saturation);
        Assert.Equal(WindowSizePreset.Lg, loaded.WindowSizePreset);
        Assert.Equal(ScreenEdge.BottomRight, loaded.ScreenEdge);
        Assert.Equal(WindowSizePreset.Md, loaded.SpaceSlot1);
        Assert.False(loaded.AlwaysOnTop);
        Assert.False(loaded.GlobalShortcutsEnabled);
        Assert.Equal("en", loaded.Language);
        Assert.Equal(1234, loaded.PlacementFor(WindowSizePreset.Lg).OriginX);
    }

    [Fact]
    public void FileUsesCamelCaseAndStringEnums()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore(temp.Path);
        store.Save(new AppSettings { ShapeType = ShapeType.Organic, WindowSizePreset = WindowSizePreset.Sm });

        var json = File.ReadAllText(store.FilePath);

        Assert.Contains("\"shapeType\": \"organic\"", json);
        Assert.Contains("\"windowSizePreset\": \"sm\"", json);
        Assert.DoesNotContain("\"ShapeType\"", json);
    }

    [Fact]
    public void MissingFileGivesDefaultsWithoutAWarning()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore(temp.Path);

        var loaded = store.Load(out var warning);

        Assert.Null(warning);
        Assert.Equal(ShapeType.Circle, loaded.ShapeType);
        Assert.Equal(WindowSizePreset.Md, loaded.WindowSizePreset);
    }

    [Fact]
    public void CorruptFileFallsBackToDefaultsAndIsPreserved()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore(temp.Path);
        File.WriteAllText(store.FilePath, "{ this is not json");

        var loaded = store.Load(out var warning);

        Assert.NotNull(warning);
        Assert.Equal(ShapeType.Circle, loaded.ShapeType);
        Assert.Single(Directory.GetFiles(temp.Path, "settings.json.corrupt-*"));
    }

    [Fact]
    public void SettingsFileWithANullBlobWaveIsRepairedOnLoad()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore(temp.Path);
        File.WriteAllText(store.FilePath, """
            { "organicBlob": { "waves": [ null, { "k": 2, "amplitude": 0.05, "phase": 0.3 } ] } }
            """);

        var loaded = store.Load(out var warning);

        Assert.Null(warning);
        Assert.Single(loaded.OrganicBlob.Waves);
        Assert.Equal(2, loaded.OrganicBlob.Waves[0].K);
    }

    [Fact]
    public void SaveIsAtomicAndLeavesNoTemporaryFile()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore(temp.Path);

        store.Save(new AppSettings());
        store.Save(new AppSettings { ShapeType = ShapeType.Square });

        Assert.False(File.Exists(store.FilePath + ".tmp"));
        Assert.Equal(ShapeType.Square, store.Load(out _).ShapeType);
    }

    [Fact]
    public void DeleteRemovesTheSettingsFile()
    {
        using var temp = new TempDirectory();
        var store = new SettingsStore(temp.Path);
        store.Save(new AppSettings());

        store.Delete();

        Assert.False(File.Exists(store.FilePath));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "CameraManTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // A failed cleanup must not fail the test.
            }
        }
    }
}
