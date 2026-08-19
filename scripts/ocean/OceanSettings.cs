using Godot;

namespace Knotical.Ocean;

[GlobalClass]
public partial class OceanSettings : Resource
{
    public const int MaxWaves = 24;

    [Export(PropertyHint.Range, "0,9999,1")]
    public int Seed { get; set; } = 7;

    [ExportGroup("Swell")]
    [Export(PropertyHint.Range, "0,8,1")]
    public int SwellCount { get; set; } = 3;

    [Export(PropertyHint.Range, "100,900,5")]
    public float SwellMinWavelength { get; set; } = 200f;

    [Export(PropertyHint.Range, "100,900,5")]
    public float SwellMaxWavelength { get; set; } = 400f;

    [Export(PropertyHint.Range, "0,8,0.01")]
    public float SwellMinAmplitude { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0,8,0.01")]
    public float SwellMaxAmplitude { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SwellMinSteepness { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SwellMaxSteepness { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float SwellDirectionDeg { get; set; } = 20f;

    [Export(PropertyHint.Range, "0,180,1")]
    public float SwellSpreadDeg { get; set; } = 20f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SwellDepthResponse { get; set; } = 1f;

    [ExportGroup("Medium")]
    [Export(PropertyHint.Range, "0,12,1")]
    public int MediumCount { get; set; } = 8;

    [Export(PropertyHint.Range, "10,300,1")]
    public float MediumMinWavelength { get; set; } = 25f;

    [Export(PropertyHint.Range, "10,300,1")]
    public float MediumMaxWavelength { get; set; } = 100f;

    [Export(PropertyHint.Range, "0,4,0.01")]
    public float MediumMinAmplitude { get; set; } = 0.15f;

    [Export(PropertyHint.Range, "0,4,0.01")]
    public float MediumMaxAmplitude { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float MediumMinSteepness { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float MediumMaxSteepness { get; set; } = 0.95f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float MediumDirectionDeg { get; set; } = 35f;

    [Export(PropertyHint.Range, "0,180,1")]
    public float MediumSpreadDeg { get; set; } = 70f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float MediumDepthResponse { get; set; } = 0.6f;

    [ExportGroup("Chop")]
    [Export(PropertyHint.Range, "0,14,1")]
    public int ChopCount { get; set; } = 10;

    [Export(PropertyHint.Range, "2,60,0.5")]
    public float ChopMinWavelength { get; set; } = 4f;

    [Export(PropertyHint.Range, "2,60,0.5")]
    public float ChopMaxWavelength { get; set; } = 25f;

    [Export(PropertyHint.Range, "0,1,0.005")]
    public float ChopMinAmplitude { get; set; } = 0.02f;

    [Export(PropertyHint.Range, "0,1,0.005")]
    public float ChopMaxAmplitude { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChopMinSteepness { get; set; } = 0.75f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChopMaxSteepness { get; set; } = 1f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float ChopDirectionDeg { get; set; } = 35f;

    [Export(PropertyHint.Range, "0,180,1")]
    public float ChopSpreadDeg { get; set; } = 120f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChopDepthResponse { get; set; } = 0.15f;

    public void Build(out Vector4[] waves, out float[] steepness, out float[] phases, out float[] depthResponse)
    {
        int swell = Mathf.Clamp(SwellCount, 0, MaxWaves);
        int medium = Mathf.Clamp(MediumCount, 0, MaxWaves - swell);
        int chop = Mathf.Clamp(ChopCount, 0, MaxWaves - swell - medium);
        int count = swell + medium + chop;

        waves = new Vector4[count];
        steepness = new float[count];
        phases = new float[count];
        depthResponse = new float[count];

        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };

        FillBand(rng, waves, steepness, phases, depthResponse, 0, swell,
            SwellMinWavelength, SwellMaxWavelength, SwellMinAmplitude, SwellMaxAmplitude,
            SwellMinSteepness, SwellMaxSteepness, SwellDirectionDeg, SwellSpreadDeg, SwellDepthResponse);

        FillBand(rng, waves, steepness, phases, depthResponse, swell, medium,
            MediumMinWavelength, MediumMaxWavelength, MediumMinAmplitude, MediumMaxAmplitude,
            MediumMinSteepness, MediumMaxSteepness, MediumDirectionDeg, MediumSpreadDeg, MediumDepthResponse);

        FillBand(rng, waves, steepness, phases, depthResponse, swell + medium, chop,
            ChopMinWavelength, ChopMaxWavelength, ChopMinAmplitude, ChopMaxAmplitude,
            ChopMinSteepness, ChopMaxSteepness, ChopDirectionDeg, ChopSpreadDeg, ChopDepthResponse);
    }

    private static void FillBand(RandomNumberGenerator rng, Vector4[] waves, float[] steepness,
        float[] phases, float[] depthResponse, int start, int count,
        float minWavelength, float maxWavelength, float minAmplitude, float maxAmplitude,
        float minSteepness, float maxSteepness, float directionDeg, float spreadDeg, float response)
    {
        float centre = Mathf.DegToRad(directionDeg);
        float halfSpread = Mathf.DegToRad(spreadDeg) * 0.5f;

        for (int j = 0; j < count; j++)
        {
            int i = start + j;
            float t = (j + rng.Randf()) / count;

            float wavelength = Mathf.Lerp(minWavelength, maxWavelength, t);
            float amplitude = Mathf.Lerp(minAmplitude, maxAmplitude, t);
            float angle = centre + rng.RandfRange(-halfSpread, halfSpread);

            waves[i] = new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), amplitude, wavelength);
            steepness[i] = rng.RandfRange(minSteepness, maxSteepness);
            phases[i] = rng.RandfRange(0f, Mathf.Tau);
            depthResponse[i] = response;
        }
    }
}
