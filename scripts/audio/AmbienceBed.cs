using Godot;
using Knotical.Weather;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Audio;

public enum AmbienceDriver
{
    WindSpeed,
    SeaState,
    BoatSpeed,
}

[GlobalClass]
public partial class AmbienceBed : AudioStreamPlayer
{
    [Export] public AmbienceDriver Driver { get; set; } = AmbienceDriver.WindSpeed;

    [Export] public RigidBody3D Hull { get; set; }

    [Export] public Vector2 Rise { get; set; } = new(0f, 6f);

    [Export] public Vector2 Fall { get; set; } = Vector2.Zero;

    [Export(PropertyHint.Range, "-40,6,0.5")]
    public float PeakDb { get; set; } = -8f;

    [Export(PropertyHint.Range, "-80,-20,1")]
    public float FloorDb { get; set; } = -60f;

    [Export(PropertyHint.Range, "0.1,20,0.1")]
    public float FadeTime { get; set; } = 3f;

    [Export(PropertyHint.Range, "0.5,2,0.01")]
    public float PitchAtRise { get; set; } = 1f;

    [Export(PropertyHint.Range, "0.5,2,0.01")]
    public float PitchAtPeak { get; set; } = 1f;

    [Export] public string DefaultBus { get; set; } = "Ambience";

    private float _weight;

    public float Weight => _weight;

    public override void _Ready()
    {
        if (Bus == "Master" && AudioServer.GetBusIndex(DefaultBus) >= 0) Bus = DefaultBus;
        if (AudioServer.GetBusIndex(Bus) < 0) Bus = "Master";

        _weight = Weigh(Read());
        Apply();

        if (Stream == null) return;

        Play();

        float length = (float)Stream.GetLength();
        if (length > 0f) Seek(GD.Randf() * length);
    }

    public override void _Process(double delta)
    {
        float target = Weigh(Read());
        _weight = Mathf.Lerp(_weight, target, 1f - Mathf.Exp(-(float)delta / Mathf.Max(FadeTime, 1e-3f)));
        Apply();
    }

    private void Apply()
    {
        VolumeDb = Mathf.Max(PeakDb + Mathf.LinearToDb(Mathf.Max(_weight, 1e-4f)), FloorDb);
        PitchScale = Mathf.Lerp(PitchAtRise, PitchAtPeak, _weight);
    }

    private float Weigh(float value)
    {
        float up = Rise.Y > Rise.X ? Mathf.SmoothStep(Rise.X, Rise.Y, value) : 1f;
        float down = Fall.Y > Fall.X ? 1f - Mathf.SmoothStep(Fall.X, Fall.Y, value) : 1f;
        return Mathf.Clamp(up * down, 0f, 1f);
    }

    private float Read() => Driver switch
    {
        AmbienceDriver.WindSpeed => Wind.Instance?.Speed ?? 0f,
        AmbienceDriver.SeaState => OceanField.Instance?.SignificantHeight ?? 0f,
        AmbienceDriver.BoatSpeed => HullSpeed(),
        _ => 0f,
    };

    private float HullSpeed()
    {
        if (Hull == null || !IsInstanceValid(Hull)) return 0f;

        Vector3 velocity = Hull.LinearVelocity;
        return new Vector2(velocity.X, velocity.Z).Length();
    }
}
