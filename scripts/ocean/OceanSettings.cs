using System;
using Godot;

namespace Knotical.Ocean;

/// <summary>
/// Authoring knobs for the wave spectrum.
///
/// The spectrum is split in two, and the split is the whole design:
///
/// <b>The skeleton</b> — wavelength, direction, and phase — is built once and never
/// changes. Directions cover the full circle. Phases come from low-discrepancy irrational
/// sequences rather than a random number generator, so the sea is identical on every
/// machine with nothing to synchronise.
///
/// <b>The amplitudes</b> are re-solved every frame from the current wind. This is the only
/// thing wind is allowed to move, and it is safe precisely because amplitude is the only
/// term that can change continuously: a component fading to zero simply flattens out.
/// Moving a direction instead would shift <c>k · dot(dir, worldXZ)</c> by thousands of
/// radians out at the world edge and teleport the entire sea.
///
/// Wind therefore steers the water three ways, all of them pure re-weighting: total
/// energy, which wavelength carries the peak, and how tightly the fan clusters downwind.
/// </summary>
[GlobalClass]
public partial class OceanSettings : Resource
{
    /// <summary>Hard ceiling; must match the array size declared in ocean.gdshader.</summary>
    public const int MaxWaves = 24;

    private const double GoldenConjugate = 0.6180339887498949;
    private const double PlasticConjugate = 0.7548776662466927;
    private const double SilverConjugate = 0.4142135623730951;

    private const float Gravity = 9.81f;

    // --- skeleton -----------------------------------------------------------

    /// <summary>
    /// Shortest wave. Keep this at or above four times the innermost grid cell size
    /// (8 m against 2 m cells). Going shorter buys aliasing, not detail; fine texture
    /// belongs in the shader.
    /// </summary>
    [Export(PropertyHint.Range, "2,60,0.5")]
    public float MinWavelength { get; set; } = 14f;

    /// <summary>
    /// Longest wave. This has to reach the peak wavelength of the heaviest weather you
    /// intend to run, or a storm has nothing long to put its energy into and piles it all
    /// into steep chop instead. A 25 m/s storm peaks near 300 m.
    /// </summary>
    [Export(PropertyHint.Range, "20,600,1")]
    public float MaxWavelength { get; set; } = 320f;

    /// <summary>
    /// Components in the skeleton. Higher counts matter more here than they used to:
    /// directions now span the full circle, so at any moment only the downwind third or
    /// so carries meaningful amplitude.
    /// </summary>
    [Export(PropertyHint.Range, "1,24,1")]
    public int WaveCount { get; set; } = 12;

    // --- wind response ------------------------------------------------------

    /// <summary>
    /// Significant wave height of a fully developed sea, as H = c·U²/g. The
    /// Pierson-Moskowitz value is 0.22, which puts a 10 m/s breeze at 2.2 m and a
    /// 20 m/s gale at 9 m.
    /// </summary>
    [Export(PropertyHint.Range, "0.05,0.6,0.005")]
    public float HeightCoefficient { get; set; } = 0.22f;

    /// <summary>
    /// Peak wavelength of a fully developed sea, as λ = c·U². Around 0.87 for
    /// Pierson-Moskowitz. This is the knob that makes light wind read as ripples and
    /// heavy wind as long ocean swell.
    /// </summary>
    [Export(PropertyHint.Range, "0.2,2.0,0.01")]
    public float PeakWavelengthCoefficient { get; set; } = 0.87f;

    /// <summary>Ceiling on wave height in metres, whatever the wind does.</summary>
    [Export(PropertyHint.Range, "1,20,0.1")]
    public float MaxWaveHeight { get; set; } = 12f;

    /// <summary>
    /// How long the sea takes to catch up to a change in wind, in seconds. Real seas take
    /// hours; this is compressed hard, but keeping it well above gust length is what stops
    /// every puff of wind visibly inflating the water.
    /// </summary>
    [Export(PropertyHint.Range, "1,600,1")]
    public float DevelopmentLagSeconds { get; set; } = 50f;

    /// <summary>
    /// Directional clustering, as cos^(2n) of half the angle off the wind. Higher values
    /// make every crest a long parallel ridge; lower values cross-hatch the interference.
    /// </summary>
    [Export(PropertyHint.Range, "0.5,8,0.1")]
    public float DirectionalExponent { get; set; } = 4f;

    /// <summary>
    /// Floor under the directional weight, so some energy always survives across and
    /// against the wind. Without it a veering wind leaves a suspiciously tidy sea.
    /// </summary>
    [Export(PropertyHint.Range, "0,0.4,0.01")]
    public float SpreadFloor { get; set; } = 0.03f;

    // --- shaping ------------------------------------------------------------

    /// <summary>
    /// Per-wave steepness ceiling (amplitude / wavelength). Real waves break above
    /// about 0.07; clamping here stops any single component self-intersecting. In a
    /// fully developed sea this should rarely bite — height and peak wavelength both
    /// scale as U², so steepness is roughly wind-independent.
    /// </summary>
    [Export(PropertyHint.Range, "0.01,0.07,0.001")]
    public float MaxSteepnessRatio { get; set; } = 0.055f;

    /// <summary>Gerstner horizontal pinch. 0 = pure sine, 1 = maximum chop.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Steepness { get; set; } = 0.6f;

    /// <summary>
    /// Builds the unchanging part of the spectrum. <paramref name="waves"/> packs
    /// xy = unit direction, w = wavelength (m); z is left at zero for
    /// <see cref="SolveAmplitudes"/> to fill. <paramref name="phases"/> holds the per-wave
    /// offset in radians, kept separate only because a Vector4 has no room left.
    /// </summary>
    /// <remarks>
    /// Direction, wavelength, and phase are each drawn from a different irrational so the
    /// three are mutually decorrelated. Sharing one sequence would wind wavelength around
    /// the compass in a visible spiral.
    /// </remarks>
    public void BuildSkeleton(out Vector4[] waves, out float[] phases)
    {
        int count = Mathf.Clamp(WaveCount, 1, MaxWaves);
        waves = new Vector4[count];
        phases = new float[count];

        float band = MaxWavelength / Mathf.Max(MinWavelength, 0.01f);

        for (int i = 0; i < count; i++)
        {
            float angle = (float)(Mathf.Tau * Frac((i + 0.5) * GoldenConjugate));

            // Log-uniform across the band, so the skeleton is evenly dense in octaves
            // rather than crowding the long end.
            float wavelength = MinWavelength * Mathf.Pow(band, (float)Frac((i + 1) * PlasticConjugate));

            waves[i] = new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), 0f, wavelength);
            phases[i] = (float)(Mathf.Tau * Frac((i + 1) * SilverConjugate));
        }
    }

    /// <summary>
    /// Re-solves amplitudes for the current sea state and writes them into
    /// <paramref name="waves"/>.z.
    /// </summary>
    /// <param name="windDirRad">Heading the wind blows toward.</param>
    /// <param name="significantHeight">Target H, already lagged behind the wind.</param>
    /// <param name="peakWavelength">Where the spectrum peaks, already lagged.</param>
    /// <returns>
    /// Σ k·a over the set — the Gerstner steepness normaliser. The shader needs it to
    /// scale horizontal displacement without dividing by per-wave amplitude, which is what
    /// lets a component's pinch vanish along with its height.
    /// </returns>
    public float SolveAmplitudes(Vector4[] waves, float windDirRad, float significantHeight, float peakWavelength)
    {
        var wind = new Vector2(Mathf.Cos(windDirRad), Mathf.Sin(windDirRad));
        float peak = Mathf.Max(peakWavelength, 0.01f);

        Span<float> weights = stackalloc float[MaxWaves];
        float weightSquareSum = 0f;

        for (int i = 0; i < waves.Length; i++)
        {
            float wavelength = waves[i].W;
            var dir = new Vector2(waves[i].X, waves[i].Y);

            // Pierson-Moskowitz, rewritten in wavelength and sampled log-uniformly, comes
            // out as a·λ·exp(-0.625·(λ/λp)²): a gentle tail below the peak and a hard
            // Gaussian cutoff above it. Wind moves λp, and the whole sea moves with it.
            float x = wavelength / peak;
            float spectral = wavelength * Mathf.Exp(-0.625f * x * x);

            // Longuet-Higgins cos^(2n)(θ/2) about the wind heading.
            float cosHalf = Mathf.Sqrt(Mathf.Max(0f, 0.5f + 0.5f * dir.Dot(wind)));
            float directional = Mathf.Lerp(
                SpreadFloor,
                1f,
                Mathf.Pow(cosHalf, 2f * DirectionalExponent));

            weights[i] = spectral * directional;
            weightSquareSum += weights[i] * weights[i];
        }

        // Significant height of a sum of sines is 4·sqrt(Σa²/2). Solve for the scale that
        // lands on the target so the height stays meaningful at any component count.
        float targetRms = Mathf.Min(significantHeight, MaxWaveHeight) * 0.25f;
        float scale = weightSquareSum > 0f
            ? Mathf.Sqrt(2f * targetRms * targetRms / weightSquareSum)
            : 0f;

        float kaSum = 0f;

        for (int i = 0; i < waves.Length; i++)
        {
            float wavelength = waves[i].W;
            float amplitude = Mathf.Min(weights[i] * scale, wavelength * MaxSteepnessRatio);

            waves[i].Z = amplitude;
            kaSum += Mathf.Tau / wavelength * amplitude;
        }

        return kaSum;
    }

    /// <summary>Significant wave height a fully developed sea reaches at this wind speed.</summary>
    public float DevelopedHeight(float windSpeed) =>
        Mathf.Min(HeightCoefficient * windSpeed * windSpeed / Gravity, MaxWaveHeight);

    /// <summary>Peak wavelength a fully developed sea reaches at this wind speed.</summary>
    public float DevelopedPeakWavelength(float windSpeed) =>
        Mathf.Clamp(PeakWavelengthCoefficient * windSpeed * windSpeed, MinWavelength, MaxWavelength);

    private static double Frac(double v) => v - System.Math.Floor(v);
}
