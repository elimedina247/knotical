using System;
using Godot;

namespace Knotical.Ocean;

[GlobalClass]
public partial class OceanSettings : Resource
{
    public const int MaxWaves = 24;

    private const double GoldenConjugate = 0.6180339887498949;
    private const double PlasticConjugate = 0.7548776662466927;
    private const double SilverConjugate = 0.4142135623730951;

    [Export(PropertyHint.Range, "0,8,1")]
    public int SwellCount { get; set; } = 3;

    [Export(PropertyHint.Range, "60,300,1")]
    public float SwellMinWavelength { get; set; } = 150f;

    [Export(PropertyHint.Range, "150,800,1")]
    public float SwellMaxWavelength { get; set; } = 420f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float SwellDirectionDeg { get; set; } = 35f;

    [Export(PropertyHint.Range, "0,60,1")]
    public float SwellFanDeg { get; set; } = 18f;

    [Export(PropertyHint.Range, "0,12,0.05")]
    public float SwellHeight { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "60,800,1")]
    public float SwellPeakWavelength { get; set; } = 260f;

    [Export(PropertyHint.Range, "0.01,0.07,0.001")]
    public float SwellMaxSteepnessRatio { get; set; } = 0.04f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SwellSetDepth { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0,8,1")]
    public int MediumCount { get; set; } = 6;

    [Export(PropertyHint.Range, "20,120,1")]
    public float MediumMinWavelength { get; set; } = 40f;

    [Export(PropertyHint.Range, "80,600,1")]
    public float MediumMaxWavelength { get; set; } = 260f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float MediumDirectionDeg { get; set; } = 35f;

    [Export(PropertyHint.Range, "0,60,1")]
    public float MediumFanDeg { get; set; } = 24f;

    [Export(PropertyHint.Range, "0,12,0.05")]
    public float MediumHeight { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "20,600,1")]
    public float MediumPeakWavelength { get; set; } = 80f;

    [Export(PropertyHint.Range, "0.01,0.15,0.001")]
    public float MediumMaxSteepnessRatio { get; set; } = 0.05f;

    [Export(PropertyHint.Range, "0,16,1")]
    public int ChopCount { get; set; } = 10;

    [Export(PropertyHint.Range, "2,12,0.5")]
    public float ChopMinWavelength { get; set; } = 4f;

    [Export(PropertyHint.Range, "8,40,0.5")]
    public float ChopMaxWavelength { get; set; } = 22f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float ChopDirectionDeg { get; set; } = 35f;

    [Export(PropertyHint.Range, "0,90,1")]
    public float ChopSpreadDeg { get; set; } = 55f;

    [Export(PropertyHint.Range, "0,1.5,0.01")]
    public float ChopHeight { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0.01,0.09,0.001")]
    public float ChopMaxSteepnessRatio { get; set; } = 0.06f;

    [Export(PropertyHint.Range, "0.1,1,0.01")]
    public float SpectrumWidth { get; set; } = 0.32f;

    [Export(PropertyHint.Range, "0.5,8,0.1")]
    public float DirectionalExponent { get; set; } = 2f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SpreadFloor { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Steepness { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SwellPhysicsWeight { get; set; } = 1f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float MediumPhysicsWeight { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ChopPhysicsWeight { get; set; }

    [Export(PropertyHint.Range, "1,4,0.05")]
    public float MaxSeaScale { get; set; } = 2f;

    public int ClampedSwellCount => Mathf.Clamp(SwellCount, 0, 8);

    public int ClampedMediumCount => Mathf.Clamp(MediumCount, 0, Mathf.Min(8, MaxWaves - ClampedSwellCount));

    public int ClampedChopCount => Mathf.Clamp(ChopCount, 0, MaxWaves - ClampedSwellCount - ClampedMediumCount);

    public int PhysicsWaveCount =>
        ClampedSwellCount + ClampedMediumCount + (ChopPhysicsWeight > 0f ? ClampedChopCount : 0);

    public float CombinedHeight =>
        Mathf.Sqrt(SwellHeight * SwellHeight + MediumHeight * MediumHeight + ChopHeight * ChopHeight);

    public void BuildSkeleton(out Vector4[] waves, out float[] phases)
    {
        int swell = ClampedSwellCount;
        int medium = ClampedMediumCount;
        int chop = ClampedChopCount;
        int count = swell + medium + chop;

        waves = new Vector4[count];
        phases = new float[count];

        FillBand(waves, phases, 0, swell, SwellMinWavelength, SwellMaxWavelength, SwellDirectionDeg, SwellFanDeg, GoldenConjugate);
        FillBand(waves, phases, swell, medium, MediumMinWavelength, MediumMaxWavelength, MediumDirectionDeg, MediumFanDeg, GoldenConjugate);
        FillBand(waves, phases, swell + medium, chop, ChopMinWavelength, ChopMaxWavelength, ChopDirectionDeg, ChopSpreadDeg, PlasticConjugate);
    }

    private static void FillBand(Vector4[] waves, float[] phases, int start, int count,
        float minWavelength, float maxWavelength, float directionDeg, float fanDeg, double sequence)
    {
        if (count <= 0) return;

        float centre = Mathf.DegToRad(directionDeg);
        float band = maxWavelength / Mathf.Max(minWavelength, 0.5f);

        for (int j = 0; j < count; j++)
        {
            int i = start + j;
            float wavelength = minWavelength * Mathf.Pow(band, (j + 0.5f) / count);
            float offset = Mathf.DegToRad(fanDeg) * (2f * (float)Frac((j + 0.5) * sequence) - 1f);
            float angle = centre + offset;

            waves[i] = new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), 0f, wavelength);
            phases[i] = (float)(Mathf.Tau * Frac((i + 1) * SilverConjugate));
        }
    }

    public float SolveAmplitudes(Vector4[] waves, float swellScale = 1f)
    {
        Span<float> weights = stackalloc float[MaxWaves];

        int swell = Mathf.Min(ClampedSwellCount, waves.Length);
        int medium = Mathf.Min(ClampedMediumCount, waves.Length - swell);
        int chop = Mathf.Min(ClampedChopCount, waves.Length - swell - medium);

        float kaSum = SolveBand(waves, weights, 0, swell, SwellPeakWavelength, SwellDirectionDeg, SwellHeight * swellScale, SwellMaxSteepnessRatio);
        kaSum += SolveBand(waves, weights, swell, medium, MediumPeakWavelength, MediumDirectionDeg, MediumHeight, MediumMaxSteepnessRatio);
        kaSum += SolveChop(waves, weights, swell + medium, chop);

        return kaSum;
    }

    private float SolveBand(Vector4[] waves, Span<float> weights, int start, int count,
        float peakWavelength, float directionDeg, float targetHeight, float steepnessCap)
    {
        if (count <= 0) return 0f;

        Vector2 centre = DirectionVector(directionDeg);
        float peak = Mathf.Max(peakWavelength, 1f);
        float sigma = Mathf.Max(SpectrumWidth, 0.05f);

        float squareSum = 0f;
        for (int j = 0; j < count; j++)
        {
            int i = start + j;
            float x = Mathf.Log(waves[i].W / peak);
            float spectral = Mathf.Exp(-x * x / (2f * sigma * sigma));

            var dir = new Vector2(waves[i].X, waves[i].Y);
            float cosHalf = Mathf.Sqrt(Mathf.Max(0f, 0.5f + 0.5f * dir.Dot(centre)));
            float directional = Mathf.Lerp(SpreadFloor, 1f, Mathf.Pow(cosHalf, 2f * DirectionalExponent));

            weights[i] = spectral * directional;
            squareSum += weights[i] * weights[i];
        }

        return Assign(waves, weights, start, start + count, squareSum, targetHeight, steepnessCap);
    }

    private float SolveChop(Vector4[] waves, Span<float> weights, int start, int count)
    {
        if (count <= 0) return 0f;

        Vector2 centre = DirectionVector(ChopDirectionDeg);

        float squareSum = 0f;
        for (int j = 0; j < count; j++)
        {
            int i = start + j;
            var dir = new Vector2(waves[i].X, waves[i].Y);
            float cosHalf = Mathf.Sqrt(Mathf.Max(0f, 0.5f + 0.5f * dir.Dot(centre)));
            float directional = Mathf.Lerp(0.4f, 1f, cosHalf * cosHalf);

            weights[i] = directional * Mathf.Pow(waves[i].W / ChopMaxWavelength, 0.3f);
            squareSum += weights[i] * weights[i];
        }

        return Assign(waves, weights, start, start + count, squareSum, ChopHeight, ChopMaxSteepnessRatio);
    }

    private static float Assign(Vector4[] waves, Span<float> weights, int from, int to, float squareSum, float targetHeight, float steepnessCap)
    {
        float targetRms = targetHeight * 0.25f;
        float scale = squareSum > 0f
            ? Mathf.Sqrt(2f * targetRms * targetRms / squareSum)
            : 0f;

        float kaSum = 0f;
        for (int i = from; i < to; i++)
        {
            float wavelength = waves[i].W;
            float amplitude = Mathf.Min(weights[i] * scale, wavelength * steepnessCap);

            waves[i].Z = amplitude;
            kaSum += Mathf.Tau / wavelength * amplitude;
        }

        return kaSum;
    }

    private static Vector2 DirectionVector(float degrees)
    {
        float rad = Mathf.DegToRad(degrees);
        return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private static double Frac(double v) => v - System.Math.Floor(v);
}
