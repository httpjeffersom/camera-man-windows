namespace CameraMan.Windows.Models;

/// <summary>
/// A soft, organic outline: a circle whose radius wobbles gently with the angle.
/// The outline is <c>r(θ) = 1 + Σ amplitude·cos(k·θ + phase)</c>, sampled densely. This is a direct port of the
/// macOS <c>OrganicBlob</c> model so both apps share presets and motion.
/// </summary>
public sealed class OrganicBlob : IEquatable<OrganicBlob>
{
    public sealed class Wave
    {
        public int K { get; set; }
        public double Amplitude { get; set; }
        public double Phase { get; set; }

        public Wave() { }

        public Wave(int k, double amplitude, double phase)
        {
            K = k;
            Amplitude = amplitude;
            Phase = phase;
        }

        public Wave Clone() => new(K, Amplitude, Phase);
    }

    public List<Wave> Waves { get; set; } = [];

    public OrganicBlob() { }

    public OrganicBlob(params Wave[] waves) => Waves = [.. waves];

    public double Radius(double theta, double time = 0)
    {
        var radius = 1.0;
        foreach (var wave in Waves)
        {
            radius += wave.Amplitude * Math.Cos(wave.K * theta + wave.Phase + time * BreathingSpeed(wave.K));
        }

        return radius;
    }

    /// <summary>Slow, and different per wave, so the motion never visibly repeats.</summary>
    public static double BreathingSpeed(int k) => k switch
    {
        2 => 0.45,
        3 => -0.32,
        _ => 0.21,
    };

    public double MaxRadius(double time = 0)
    {
        var max = 0.0;
        for (var i = 0; i < 240; i++)
        {
            max = Math.Max(max, Radius(2 * Math.PI * i / 240.0, time));
        }

        return max <= 0 ? 1 : max;
    }

    public OrganicBlob Clone() => new(Waves.Select(w => w.Clone()).ToArray());

    public bool Equals(OrganicBlob? other)
    {
        if (other is null || other.Waves.Count != Waves.Count)
        {
            return false;
        }

        for (var i = 0; i < Waves.Count; i++)
        {
            if (Waves[i].K != other.Waves[i].K ||
                Math.Abs(Waves[i].Amplitude - other.Waves[i].Amplitude) > 1e-9 ||
                Math.Abs(Waves[i].Phase - other.Waves[i].Phase) > 1e-9)
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => Equals(obj as OrganicBlob);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var wave in Waves)
        {
            hash.Add(wave.K);
            hash.Add(Math.Round(wave.Amplitude, 9));
            hash.Add(Math.Round(wave.Phase, 9));
        }

        return hash.ToHashCode();
    }

    // MARK: Presets (same hand-tuned values as the macOS app)

    public static readonly (string ResourceKey, string Name, OrganicBlob Blob)[] Presets =
    [
        ("Blob_Pebble", "Pebble", new OrganicBlob(
            new Wave(2, 0.055, 0.3),
            new Wave(3, 0.025, 2.1))),
        ("Blob_Cloud", "Cloud", new OrganicBlob(
            new Wave(3, 0.04, 1.6),
            new Wave(5, 0.018, 0.4))),
        ("Blob_Bean", "Bean", new OrganicBlob(
            new Wave(2, 0.075, 2.6),
            new Wave(3, 0.035, 0.9))),
        ("Blob_Drop", "Drop", new OrganicBlob(
            new Wave(1, 0.06, -1.57),
            new Wave(2, 0.035, 0),
            new Wave(3, 0.015, 1.2))),
        ("Blob_Wave", "Wave", new OrganicBlob(
            new Wave(2, 0.035, 1.0),
            new Wave(4, 0.022, 0.2),
            new Wave(5, 0.01, 2.4))),
    ];

    public static OrganicBlob Default => Presets[0].Blob.Clone();

    public static bool IsPreset(OrganicBlob blob) => Presets.Any(p => p.Blob.Equals(blob));

    /// <summary>A new blob in the same gentle range as the presets.</summary>
    public static OrganicBlob CreateRandom(Random? random = null)
    {
        random ??= Random.Shared;
        return new OrganicBlob(
            new Wave(2, 0.025 + random.NextDouble() * 0.045, random.NextDouble() * 2 * Math.PI),
            new Wave(3, 0.012 + random.NextDouble() * 0.023, random.NextDouble() * 2 * Math.PI),
            new Wave(4, random.NextDouble() * 0.015, random.NextDouble() * 2 * Math.PI));
    }
}
