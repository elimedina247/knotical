using Godot;
using Knotical.Boat;
using Knotical.Weather;

namespace Knotical.Audio;

[GlobalClass]
public partial class SailSound : Node3D
{
    [Export] public Sail Canvas { get; set; }

    [Export] public AudioStreamPlayer3D Bed { get; set; }

    [Export] public AudioStreamPlayer3D CalmBed { get; set; }

    [Export] public SoundEmitter Emitter { get; set; }

    [Export] public ClipSet Flutters { get; set; }

    [Export] public ClipSet Snaps { get; set; }

    [Export(PropertyHint.Range, "-60,6,0.5")]
    public float BedPeakDb { get; set; } = -3f;

    [Export(PropertyHint.Range, "-60,6,0.5")]
    public float CalmPeakDb { get; set; } = -8f;

    [Export] public Vector2 CalmCross { get; set; } = new(0.15f, 0.5f);

    [Export(PropertyHint.Range, "-80,-20,1")]
    public float BedFloorDb { get; set; } = -60f;

    [Export(PropertyHint.Range, "0.1,20,0.1")]
    public float BedFade { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0.5,2,0.01")]
    public float BedCalmPitch { get; set; } = 0.95f;

    [Export(PropertyHint.Range, "0.5,2,0.01")]
    public float BedGalePitch { get; set; } = 1.1f;

    [Export(PropertyHint.Range, "1,60,0.5")]
    public float CalmInterval { get; set; } = 16f;

    [Export(PropertyHint.Range, "0.2,20,0.1")]
    public float BusyInterval { get; set; } = 2.2f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public float IntervalJitter { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float DrawnRate { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "-40,0,0.5")]
    public float FlutterQuietDb { get; set; } = -9f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SnapMinStrength { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.5,20,0.1")]
    public float SnapGap { get; set; } = 3f;

    [Export(PropertyHint.Range, "-40,0,0.5")]
    public float SnapQuietDb { get; set; } = -12f;

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float SnapLightPitch { get; set; } = 1.08f;

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float SnapHeavyPitch { get; set; } = 0.9f;

    private readonly RandomNumberGenerator _rng = new();
    private readonly GustSense _sense = new();
    private Sail _sail;
    private AudioStreamPlayer3D _bed;
    private AudioStreamPlayer3D _calm;
    private SoundEmitter _emitter;
    private float _weight;
    private float _calmWeight;
    private float _wait;
    private float _snap;

    public override void _Ready()
    {
        _rng.Randomize();

        _sail = Canvas ?? GetParentOrNull<Sail>();
        _bed = Bed ?? GetNodeOrNull<AudioStreamPlayer3D>("Bed");
        _calm = CalmBed ?? GetNodeOrNull<AudioStreamPlayer3D>("CalmBed");
        _emitter = Emitter ?? GetNodeOrNull<SoundEmitter>("SoundEmitter");
        _wait = _rng.RandfRange(1f, CalmInterval);

        Start(_bed);
        Start(_calm);
    }

    private static void Start(AudioStreamPlayer3D bed)
    {
        if (bed == null) return;

        if (AudioServer.GetBusIndex(bed.Bus) < 0) bed.Bus = "Master";
        if (bed.Stream == null) return;

        bed.Play();

        float length = (float)bed.Stream.GetLength();
        if (length > 0f) bed.Seek(GD.Randf() * length);
    }

    public override void _Process(double delta)
    {
        if (_sail == null || !IsInstanceValid(_sail)) return;

        float dt = (float)delta;
        float set = Mathf.Clamp(_sail.Deployment, 0f, 1f);
        float strength = Mathf.Clamp(_sail.WindStrength, 0f, 1f);
        float luff = 1f - Mathf.Clamp(Mathf.Abs(_sail.Pressure) / Mathf.Max(_sail.LuffKnee, 0.01f), 0f, 1f);

        float lively = CalmCross.Y > CalmCross.X
            ? Mathf.SmoothStep(CalmCross.X, CalmCross.Y, strength)
            : 1f;

        Breathe(dt, ref _weight, _bed, set * lively, BedPeakDb, BedCalmPitch, BedGalePitch);
        Breathe(dt, ref _calmWeight, _calm, set * (1f - lively), CalmPeakDb, 1f, 1f);

        Flutter(dt, set * strength * Mathf.Lerp(DrawnRate, 1f, luff));
        Snap(dt, set, strength, luff);
    }

    private void Breathe(float dt, ref float weight, AudioStreamPlayer3D bed, float target,
        float peakDb, float lowPitch, float highPitch)
    {
        if (bed == null) return;

        weight = Mathf.Lerp(weight, target, 1f - Mathf.Exp(-dt / Mathf.Max(BedFade, 1e-3f)));

        bed.VolumeDb = Mathf.Max(peakDb + Mathf.LinearToDb(Mathf.Max(weight, 1e-4f)), BedFloorDb);
        bed.PitchScale = Mathf.Lerp(lowPitch, highPitch, weight);
    }

    private void Flutter(float dt, float activity)
    {
        if (_emitter == null) return;

        _wait -= dt;
        if (_wait > 0f) return;

        _wait = Mathf.Lerp(CalmInterval, BusyInterval, activity)
              * (1f + _rng.RandfRange(-IntervalJitter, IntervalJitter));

        if (activity <= 0.02f) return;

        _emitter.Emit(Flutters, Mathf.Lerp(FlutterQuietDb, 0f, activity));
    }

    private void Snap(float dt, float set, float strength, float luff)
    {
        bool rising = _sense.Rising(Wind.Instance?.Speed ?? 0f, dt);

        _snap = Mathf.Max(_snap - dt, 0f);

        if (!rising || _snap > 0f || _emitter == null) return;
        if (set < 0.1f || strength < SnapMinStrength) return;

        _snap = SnapGap;

        float force = Mathf.Clamp(strength * Mathf.Lerp(0.5f, 1f, luff), 0f, 1f);

        _emitter.Emit(Snaps, Mathf.Lerp(SnapQuietDb, 0f, force),
            Mathf.Lerp(SnapLightPitch, SnapHeavyPitch, force));
    }
}
