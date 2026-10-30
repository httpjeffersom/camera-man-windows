using CameraMan.Windows.Models;

namespace CameraMan.Windows.Tests;

public class OrganicBlobTests
{
    [Fact]
    public void DefaultIsThePebblePreset()
    {
        Assert.True(OrganicBlob.IsPreset(OrganicBlob.Default));
        Assert.Equal(OrganicBlob.Presets[0].Blob, OrganicBlob.Default);
    }

    [Fact]
    public void CloneIsEqualButIndependent()
    {
        var original = OrganicBlob.Default;
        var clone = original.Clone();

        Assert.Equal(original, clone);
        clone.Waves[0].Amplitude += 0.01;
        Assert.NotEqual(original, clone);
    }

    [Fact]
    public void RadiusIsFiniteAndMaxRadiusIsPositive()
    {
        var blob = OrganicBlob.Default;

        Assert.True(double.IsFinite(blob.Radius(0.5, 2)));
        Assert.True(blob.MaxRadius(2) > 0);
        Assert.True(double.IsFinite(blob.MaxRadius(2)));
    }

    [Fact]
    public void PresetsHaveAReasonableOutline()
    {
        foreach (var (_, _, blob) in OrganicBlob.Presets)
        {
            for (var i = 0; i < 32; i++)
            {
                var radius = blob.Radius(2 * Math.PI * i / 32.0);
                Assert.InRange(radius, 0.6, 1.4);
            }
        }
    }

    [Fact]
    public void RandomBlobsStayWithinThePresetRange()
    {
        var random = new Random(1234);
        for (var i = 0; i < 20; i++)
        {
            var blob = OrganicBlob.CreateRandom(random);
            Assert.Equal(3, blob.Waves.Count);
            Assert.All(blob.Waves, wave =>
            {
                Assert.InRange(wave.K, 2, 4);
                Assert.InRange(wave.Amplitude, 0, 0.08);
                Assert.InRange(wave.Phase, 0, 2 * Math.PI);
            });
        }
    }

    [Fact]
    public void EqualityNeedsTheSameWavesInTheSameOrder()
    {
        var a = new OrganicBlob(new OrganicBlob.Wave(2, 0.05, 0.1));
        var b = new OrganicBlob(new OrganicBlob.Wave(2, 0.05, 0.1));
        var c = new OrganicBlob(new OrganicBlob.Wave(3, 0.05, 0.1));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, c);
    }
}
