using Godot;

namespace Knotical.Weather;

/// <summary>
/// The single source of truth for wind.
///
/// Built on the same bargain as the ocean: wind is a pure function of time, so it costs
/// one synced float rather than a replicated weather simulation. Speed and heading are
/// sums of slow incommensurable oscillators, which never repeat and never need a seed.
///
/// Register as an autoload named "Wind", ahead of "Ocean" — Ocean reads this every frame.
/// </summary>
public partial class Wind : Node
{
    public static Wind Instance { get; private set; }

    [Export] public WindSettings Settings { get; set; }

    /// <summary>
    /// Seconds of weather time. Separate from <see cref="Ocean.Ocean.Time"/> only because
    /// the two systems are independent today; both are the same kind of clock and should
    /// fold into one world time when networking lands.
    /// </summary>
    public double Time { get; private set; }

    /// <summary>Heading in radians, the direction wind blows toward. 0 = +X, TAU/4 = +Z.</summary>
    public float DirectionRad { get; private set; }

    /// <summary>Instantaneous speed in m/s, gusts included.</summary>
    public float Speed { get; private set; }

    /// <summary>Unit heading on the XZ plane.</summary>
    public Vector2 Direction => new(Mathf.Cos(DirectionRad), Mathf.Sin(DirectionRad));

    /// <summary>Heading scaled by speed. What sails and drag will want.</summary>
    public Vector2 Velocity => Direction * Speed;

    public Vector2 AccumulatedDrift { get; private set; }

    /// <summary>Debug offset added to speed, in m/s. Not part of the replicated function.</summary>
    public float SpeedBias { get; set; }

    /// <summary>Debug offset added to heading, in degrees.</summary>
    public float HeadingBiasDeg { get; set; }

    /// <summary>Debug multiplier on top of <see cref="WindSettings.TimeScale"/>.</summary>
    public float DebugTimeScale { get; set; } = 1f;

    // Incommensurable periods in seconds, so the sum never cycles. Veer is slower than
    // gusting: direction wanders over minutes, strength flickers over seconds.
    private static readonly float[] VeerPeriods = { 137f, 61f, 23.3f };
    private static readonly float[] VeerWeights = { 1f, 0.45f, 0.18f };
    private static readonly float[] GustPeriods = { 47f, 17.1f, 6.3f, 2.9f };
    private static readonly float[] GustWeights = { 1f, 0.5f, 0.28f, 0.14f };

    private const double PlasticConjugate = 0.7548776662466927;

    private const float DriftKneeSpeed = 12f;

    private static readonly string[] BeaufortNames =
    {
        "Calm", "Light air", "Light breeze", "Gentle breeze", "Moderate breeze",
        "Fresh breeze", "Strong breeze", "Near gale", "Gale", "Strong gale",
        "Storm", "Violent storm", "Hurricane"
    };

    private static readonly float[] BeaufortUpperBounds =
    {
        0.5f, 1.6f, 3.4f, 5.5f, 8.0f, 10.8f, 13.9f, 17.2f, 20.8f, 24.5f, 28.5f, 32.7f
    };

    public override void _EnterTree()
    {
        Instance = this;
        Settings ??= new WindSettings();

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            string[] pair = arg.TrimPrefix("--").Split('=');
            if (pair.Length != 2 || !float.TryParse(pair[1], out float value)) continue;

            switch (pair[0])
            {
                case "wind": Settings.BaseSpeed = value; break;
                case "winddir": Settings.BaseDirectionDeg = value; break;
                case "gust": Settings.Gustiness = value; break;
            }
        }

        Sample();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Process(double delta)
    {
        double dt = delta * Settings.TimeScale * DebugTimeScale;
        Time += dt;
        Sample();

        float driftSpeed = Speed * DriftKneeSpeed / (DriftKneeSpeed + Speed);
        AccumulatedDrift += Direction * driftSpeed * (float)dt;
    }

    /// <summary>Overrides weather time. Used by clients to adopt the host's clock.</summary>
    public void SetTime(double time)
    {
        Time = time;
        Sample();
    }

    public void SetSettings(WindSettings settings)
    {
        if (settings == null) return;
        Settings = settings;
        Sample();
    }

    public void ResetOverrides()
    {
        SpeedBias = 0f;
        HeadingBiasDeg = 0f;
        DebugTimeScale = 1f;
    }

    /// <summary>Beaufort force for the current speed, 0–12.</summary>
    public int BeaufortForce => BeaufortForceFor(Speed);

    /// <summary>Descriptive name for the current force, e.g. "Fresh breeze".</summary>
    public string BeaufortName => BeaufortNames[BeaufortForce];

    public static int BeaufortForceFor(float speed)
    {
        for (int i = 0; i < BeaufortUpperBounds.Length; i++)
        {
            if (speed < BeaufortUpperBounds[i]) return i;
        }

        return BeaufortNames.Length - 1;
    }

    /// <summary>
    /// Evaluates the wind function at the current time. Called every frame, and again
    /// whenever the clock is forced, so the pair is never a frame out of step.
    /// </summary>
    private void Sample()
    {
        float t = (float)Time;

        float veer = WeightedOscillator(t, VeerPeriods, VeerWeights, 0);
        float gust = WeightedOscillator(t, GustPeriods, GustWeights, VeerPeriods.Length);

        DirectionRad = Mathf.DegToRad(Settings.BaseDirectionDeg + HeadingBiasDeg)
                     + Mathf.DegToRad(Settings.VeerDeg) * veer;

        Speed = Mathf.Max(0f, Settings.BaseSpeed * (1f + Settings.Gustiness * gust) + SpeedBias);
    }

    /// <summary>
    /// Sum of sines normalised to roughly -1..1, with decorrelated phases so nothing
    /// special happens at t = 0. <paramref name="phaseOffset"/> keeps the veer and gust
    /// stacks from drawing the same phases.
    /// </summary>
    private static float WeightedOscillator(float t, float[] periods, float[] weights, int phaseOffset)
    {
        float sum = 0f;
        float total = 0f;

        for (int i = 0; i < periods.Length; i++)
        {
            float phase = (float)(Mathf.Tau * Frac((i + phaseOffset + 1) * PlasticConjugate));
            sum += weights[i] * Mathf.Sin(Mathf.Tau * t / periods[i] + phase);
            total += weights[i];
        }

        return total > 0f ? sum / total : 0f;
    }

    private static double Frac(double v) => v - System.Math.Floor(v);

    /// <summary>
    /// Wind at a world position. Uniform today; the signature exists so squalls and
    /// island wind shadows can become local without touching every caller.
    /// </summary>
    public Vector2 GetVelocity(Vector2 worldXZ) => Velocity;
}
