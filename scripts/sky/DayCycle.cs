using Godot;

namespace Knotical.Sky;

public partial class DayCycle : Node
{
    public static DayCycle Instance { get; private set; }

    [Export] public DayCycleSettings Settings { get; set; }

    public double Time { get; private set; }

    public float DebugTimeScale { get; set; } = 1f;

    public float Phase => (float)(Time / (Settings.DayLengthMinutes * 60.0) % 1.0);

    public bool IsDay => Phase < Settings.DayFraction;

    public Vector3 SunDirection { get; private set; }
    public Vector3 MoonDirection { get; private set; }
    public Vector3 LightDirection { get; private set; }
    public float LightEnergy { get; private set; }
    public Color LightColor { get; private set; }
    public Color WaterHorizonColor { get; private set; }

    private static readonly Color SunLight = new(1f, 0.96f, 0.88f);
    private static readonly Color DuskLight = new(1f, 0.62f, 0.38f);
    private static readonly Color MoonLight = new(0.62f, 0.70f, 0.95f);
    private static readonly Color DayHorizon = new(0.74f, 0.89f, 0.95f);
    private static readonly Color DuskHorizon = new(0.98f, 0.55f, 0.28f);
    private static readonly Color NightHorizon = new(0.012f, 0.02f, 0.04f);

    public override void _EnterTree()
    {
        Instance = this;
        Settings ??= new DayCycleSettings();
        Evaluate();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Process(double delta)
    {
        Time += delta * Settings.TimeScale * DebugTimeScale;
        Evaluate();
    }

    public void SetTime(double time)
    {
        Time = time;
        Evaluate();
    }

    public void SetSettings(DayCycleSettings settings)
    {
        if (settings == null) return;
        Settings = settings;
        Evaluate();
    }

    private void Evaluate()
    {
        float phase = Phase;
        float dayFraction = Mathf.Clamp(Settings.DayFraction, 0.1f, 0.9f);
        float moonPhase = phase >= dayFraction ? phase - dayFraction : phase + 1f - dayFraction;

        SunDirection = Arc(OrbitTheta(phase, dayFraction));
        MoonDirection = Arc(OrbitTheta(moonPhase, 1f - dayFraction));

        float sunUp = Mathf.SmoothStep(0.02f, 0.30f, SunDirection.Y);
        float sunArc = Mathf.SmoothStep(0.02f, 0.75f, SunDirection.Y);
        float moonUp = Mathf.SmoothStep(0.02f, 0.30f, MoonDirection.Y);
        float dusk = Mathf.Max(0f, 1f - Mathf.Abs(SunDirection.Y) / 0.22f);

        LightDirection = SunDirection.Y >= MoonDirection.Y ? SunDirection : MoonDirection;
        LightEnergy = 0.25f * sunUp + 1.05f * sunArc + Settings.MoonEnergy * moonUp * (1f - sunUp);
        LightColor = MoonLight.Lerp(SunLight.Lerp(DuskLight, dusk), sunUp);
        WaterHorizonColor = NightHorizon.Lerp(DayHorizon, sunUp).Lerp(DuskHorizon, dusk * 0.8f);
    }

    private static float OrbitTheta(float u, float upFraction)
    {
        return u < upFraction
            ? Mathf.Pi * (u / upFraction)
            : Mathf.Pi * (1f + (u - upFraction) / (1f - upFraction));
    }

    private Vector3 Arc(float theta)
    {
        float tilt = Mathf.DegToRad(Settings.PathTiltDeg);
        return new Vector3(
            Mathf.Cos(theta),
            Mathf.Sin(theta) * Mathf.Cos(tilt),
            Mathf.Sin(theta) * Mathf.Sin(tilt));
    }
}
