using Godot;

namespace Knotical.Ocean;

[GlobalClass]
public partial class OceanSettings : Resource
{
    public const int MaxWaves = 24;

    [Export(PropertyHint.Range, "1,24,1")]
    public int WaveCount { get; set; } = 18;

    [Export(PropertyHint.Range, "0,9999,1")]
    public int Seed { get; set; } = 7;

    [Export(PropertyHint.Range, "2,200,0.5")]
    public float MinWavelength { get; set; } = 4f;

    [Export(PropertyHint.Range, "20,800,1")]
    public float MaxWavelength { get; set; } = 100f;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float MinAmplitude { get; set; } = 0.02f;

    [Export(PropertyHint.Range, "0,6,0.01")]
    public float MaxAmplitude { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float MinSteepness { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float MaxSteepness { get; set; } = 1f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float DirectionDeg { get; set; } = 35f;

    [Export(PropertyHint.Range, "0,180,1")]
    public float SpreadDeg { get; set; } = 100f;

    [Export(PropertyHint.Range, "0.25,4,0.05")]
    public float Distribution { get; set; } = 2.4f;

    public int ClampedWaveCount => Mathf.Clamp(WaveCount, 1, MaxWaves);

    public void Build(out Vector4[] waves, out float[] steepness, out float[] phases)
    {
        int count = ClampedWaveCount;
        waves = new Vector4[count];
        steepness = new float[count];
        phases = new float[count];

        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
        float centre = Mathf.DegToRad(DirectionDeg);
        float halfSpread = Mathf.DegToRad(SpreadDeg) * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float u = (i + rng.Randf()) / count;
            float t = Mathf.Pow(u, Distribution);

            float wavelength = Mathf.Lerp(MinWavelength, MaxWavelength, t);
            float amplitude = Mathf.Lerp(MinAmplitude, MaxAmplitude, t);
            float angle = centre + rng.RandfRange(-halfSpread, halfSpread);

            waves[i] = new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), amplitude, wavelength);
            steepness[i] = rng.RandfRange(MinSteepness, MaxSteepness);
            phases[i] = rng.RandfRange(0f, Mathf.Tau);
        }
    }
}
