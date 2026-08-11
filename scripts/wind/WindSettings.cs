using Godot;

namespace Knotical.Weather;

/// <summary>
/// Authoring knobs for the ambient wind.
///
/// Like the ocean, wind is a pure function of time — no RNG, no state to replicate.
/// These values describe the shape of that function, not a current condition.
/// </summary>
[GlobalClass]
public partial class WindSettings : Resource
{
    /// <summary>Mean wind speed in m/s. 9 m/s is a fresh breeze — Beaufort 5.</summary>
    [Export(PropertyHint.Range, "0,35,0.5")]
    public float BaseSpeed { get; set; } = 9f;

    /// <summary>Mean heading in degrees, the direction wind blows toward. 0 = +X, 90 = +Z.</summary>
    [Export(PropertyHint.Range, "0,360,1")]
    public float BaseDirectionDeg { get; set; } = 35f;

    /// <summary>Peak wander either side of the mean heading, in degrees.</summary>
    [Export(PropertyHint.Range, "0,90,1")]
    public float VeerDeg { get; set; } = 22f;

    /// <summary>
    /// Gust depth as a fraction of base speed. Real gust factors sit around 0.3–0.5 over
    /// open water, so 0.35 gives lulls and squalls without the wind ever dropping out.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Gustiness { get; set; } = 0.35f;

    /// <summary>
    /// Multiplier on wind time. Weather at 1.0 turns over on a scale of minutes; raise it
    /// to watch a day's worth of conditions pass in a sitting.
    /// </summary>
    [Export(PropertyHint.Range, "0.1,20,0.1")]
    public float TimeScale { get; set; } = 1f;
}
