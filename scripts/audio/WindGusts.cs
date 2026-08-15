using Godot;
using Knotical.Weather;

namespace Knotical.Audio;

[GlobalClass]
public partial class WindGusts : AudioStreamPlayer
{
    [Export] public ClipSet Gusts { get; set; }

    [Export(PropertyHint.Range, "0,30,0.5")]
    public float QuietSpeed { get; set; } = 4f;

    [Export(PropertyHint.Range, "1,40,0.5")]
    public float FullSpeed { get; set; } = 18f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float QuietChance { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float FullChance { get; set; } = 1f;

    [Export(PropertyHint.Range, "1,60,0.5")]
    public float QuietGap { get; set; } = 16f;

    [Export(PropertyHint.Range, "0.5,20,0.1")]
    public float FullGap { get; set; } = 3.5f;

    [Export(PropertyHint.Range, "0.1,5,0.05")]
    public float Excess { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "1,30,0.5")]
    public float MeanTime { get; set; } = 9f;

    [Export(PropertyHint.Range, "-30,0,0.5")]
    public float QuietDb { get; set; } = -14f;

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float QuietPitch { get; set; } = 1.06f;

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float LoudPitch { get; set; } = 0.92f;

    [Export] public string DefaultBus { get; set; } = "Ambience";

    private readonly RandomNumberGenerator _rng = new();
    private readonly GustSense _sense = new();
    private float _gap;

    public override void _Ready()
    {
        _rng.Randomize();

        if (MaxPolyphony < 2) MaxPolyphony = 2;
        if (Bus == "Master" && AudioServer.GetBusIndex(DefaultBus) >= 0) Bus = DefaultBus;
        if (AudioServer.GetBusIndex(Bus) < 0) Bus = "Master";
    }

    public override void _Process(double delta)
    {
        Wind wind = Wind.Instance;
        if (wind == null || Gusts == null || Gusts.IsEmpty) return;

        float speed = wind.Speed;
        float dt = (float)delta;

        _sense.MeanTime = MeanTime;
        _sense.Excess = Excess;

        bool rising = _sense.Rising(speed, dt);

        _gap -= dt;

        if (!rising || _gap > 0f || speed < QuietSpeed) return;

        float weight = Mathf.Clamp(
            Mathf.InverseLerp(QuietSpeed, Mathf.Max(FullSpeed, QuietSpeed + 0.1f), speed), 0f, 1f);

        _gap = Mathf.Lerp(QuietGap, FullGap, weight);

        if (_rng.Randf() > Mathf.Lerp(QuietChance, FullChance, weight)) return;

        Stream = Gusts.Pick(_rng);
        VolumeDb = Gusts.Gain(_rng) + Mathf.Lerp(QuietDb, 0f, weight);
        PitchScale = Mathf.Clamp(Gusts.Pitch(_rng) * Mathf.Lerp(QuietPitch, LoudPitch, weight), 0.1f, 4f);
        Play();
    }
}
