using Godot;

namespace Knotical.Ocean;

/// <summary>
/// Authoring knobs for the wave spectrum.
///
/// Rather than hand-tuning individual waves, the set is generated from wind. Components
/// are spread geometrically across a wavelength band, weighted by a spectral falloff,
/// fanned around the wind heading, and — critically — given decorrelated phase offsets.
/// Without those offsets every component peaks together at the world origin and the
/// interference pattern is rigidly organised, which reads as marching corduroy rows.
///
/// Distribution uses low-discrepancy irrational sequences rather than a random number
/// generator, so the sea is identical on every machine with nothing to synchronise.
/// </summary>
[GlobalClass]
public partial class OceanSettings : Resource
{
    /// <summary>Hard ceiling; must match the array size declared in ocean.gdshader.</summary>
    public const int MaxWaves = 24;

    private const double GoldenConjugate = 0.6180339887498949;
    private const double PlasticConjugate = 0.7548776662466927;

    /// <summary>Wind heading in degrees. 0 = +X, 90 = +Z.</summary>
    [Export(PropertyHint.Range, "0,360,1")]
    public float WindDirectionDeg { get; set; } = 35f;

    /// <summary>
    /// Approximate significant wave height in metres — the crest-to-trough size of the
    /// larger waves. Amplitudes are normalised to hit this, so it stays meaningful no
    /// matter how many components are in the spectrum.
    /// </summary>
    [Export(PropertyHint.Range, "0,12,0.05")]
    public float WaveHeight { get; set; } = 2.4f;

    /// <summary>
    /// How far components fan from the wind heading, in degrees. Narrow spreads make
    /// every crest a long parallel ridge; wide spreads cross-hatch the interference.
    /// </summary>
    [Export(PropertyHint.Range, "0,120,1")]
    public float SpreadDeg { get; set; } = 78f;

    /// <summary>
    /// Bias of the directional fan toward the wind heading. 1 = uniform across the
    /// spread, higher clusters components downwind while keeping the outliers.
    /// </summary>
    [Export(PropertyHint.Range, "1,3,0.05")]
    public float SpreadBias { get; set; } = 1.4f;

    /// <summary>Longest wave in the spectrum. This sets the swell scale.</summary>
    [Export(PropertyHint.Range, "20,300,1")]
    public float MaxWavelength { get; set; } = 96f;

    /// <summary>
    /// Shortest wave. Keep this at roughly four times the innermost grid cell size —
    /// 8 m against 2 m cells. Going shorter buys aliasing, not detail; fine texture
    /// belongs in the shader.
    /// </summary>
    [Export(PropertyHint.Range, "2,60,0.5")]
    public float MinWavelength { get; set; } = 8f;

    [Export(PropertyHint.Range, "1,24,1")]
    public int WaveCount { get; set; } = 16;

    /// <summary>
    /// Spectral falloff exponent: amplitude scales as wavelength^n. Lower values push
    /// energy into the short chop, higher values into long swell.
    /// </summary>
    [Export(PropertyHint.Range, "0.2,2.0,0.05")]
    public float SpectrumFalloff { get; set; } = 0.8f;

    /// <summary>
    /// Per-wave steepness ceiling (amplitude / wavelength). Real waves break above
    /// about 0.07; clamping here stops any single component self-intersecting.
    /// </summary>
    [Export(PropertyHint.Range, "0.01,0.07,0.001")]
    public float MaxSteepnessRatio { get; set; } = 0.055f;

    /// <summary>Gerstner horizontal pinch. 0 = pure sine, 1 = maximum chop.</summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Steepness { get; set; } = 0.6f;

    /// <summary>
    /// Builds the spectrum for both the CPU sampler and the shader.
    /// <paramref name="waves"/> packs xy = unit direction, z = amplitude (m),
    /// w = wavelength (m). <paramref name="phases"/> holds the per-wave offset in
    /// radians, kept separate only because a Vector4 has no room left.
    /// </summary>
    public void Build(out Vector4[] waves, out float[] phases)
    {
        int count = Mathf.Clamp(WaveCount, 1, MaxWaves);
        waves = new Vector4[count];
        phases = new float[count];

        float windRad = Mathf.DegToRad(WindDirectionDeg);
        float spreadRad = Mathf.DegToRad(SpreadDeg);

        // Geometric spread across the wavelength band, longest first.
        float ratio = count > 1
            ? Mathf.Pow(MinWavelength / MaxWavelength, 1f / (count - 1))
            : 1f;

        var lengths = new float[count];
        var weights = new float[count];
        float weightSquareSum = 0f;

        for (int i = 0; i < count; i++)
        {
            lengths[i] = MaxWavelength * Mathf.Pow(ratio, i);
            weights[i] = Mathf.Pow(lengths[i], SpectrumFalloff);
            weightSquareSum += weights[i] * weights[i];
        }

        // Significant height of a sum of sines is 4 * sqrt(sum(a^2) / 2). Solve for the
        // scale that lands on WaveHeight so the knob means the same thing at any count.
        float targetRms = WaveHeight * 0.25f;
        float scale = weightSquareSum > 0f
            ? Mathf.Sqrt(2f * targetRms * targetRms / weightSquareSum)
            : 0f;

        for (int i = 0; i < count; i++)
        {
            float wavelength = lengths[i];
            float amplitude = Mathf.Min(weights[i] * scale, wavelength * MaxSteepnessRatio);

            // Low-discrepancy fan across the spread, biased toward the wind heading.
            float u = (float)(2.0 * Frac((i + 0.5) * GoldenConjugate) - 1.0);
            float biased = Mathf.Sign(u) * Mathf.Pow(Mathf.Abs(u), SpreadBias);
            float angle = windRad + spreadRad * biased;

            waves[i] = new Vector4(Mathf.Cos(angle), Mathf.Sin(angle), amplitude, wavelength);

            // Decorrelated phase. This is what stops every component peaking together
            // at the origin and organising the whole surface into rows.
            phases[i] = (float)(Mathf.Tau * Frac((i + 1) * PlasticConjugate));
        }
    }

    private static double Frac(double v) => v - System.Math.Floor(v);
}
