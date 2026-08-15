using Godot;

namespace Knotical.Audio;

[GlobalClass]
public partial class HullCreaks : Node3D
{
    [Export] public RigidBody3D Hull { get; set; }

    [Export] public SoundEmitter Emitter { get; set; }

    [Export] public ClipSet Creaks { get; set; }

    [Export(PropertyHint.Range, "1,40,0.5")]
    public float RestInterval { get; set; } = 11f;

    [Export(PropertyHint.Range, "0.2,10,0.1")]
    public float WorkInterval { get; set; } = 1.4f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public float IntervalJitter { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "1,45,0.5")]
    public float FullHeel { get; set; } = 16f;

    [Export(PropertyHint.Range, "0.05,3,0.01")]
    public float FullRoll { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float HeelShare { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0,30,0.5")]
    public float Spread { get; set; } = 7f;

    [Export(PropertyHint.Range, "0,6,0.1")]
    public float Scatter { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "-30,0,0.5")]
    public float RestDb { get; set; } = -14f;

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float RestPitch { get; set; } = 1.1f;

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float WorkPitch { get; set; } = 0.88f;

    private readonly RandomNumberGenerator _rng = new();
    private RigidBody3D _hull;
    private SoundEmitter _emitter;
    private float _wait;

    public override void _Ready()
    {
        _rng.Randomize();
        _hull = Hull ?? GetParentOrNull<RigidBody3D>();
        _emitter = Emitter ?? GetNodeOrNull<SoundEmitter>("SoundEmitter");
        _wait = _rng.RandfRange(1f, RestInterval);
    }

    public override void _Process(double delta)
    {
        if (_hull == null || _emitter == null || !IsInstanceValid(_hull)) return;

        float load = Load();

        _wait -= (float)delta;
        if (_wait > 0f) return;

        float interval = Mathf.Lerp(RestInterval, WorkInterval, load);
        _wait = interval * (1f + _rng.RandfRange(-IntervalJitter, IntervalJitter));

        _emitter.Position = new Vector3(
            _rng.RandfRange(-Scatter, Scatter),
            _rng.RandfRange(-Scatter, Scatter) * 0.5f,
            _rng.RandfRange(-Spread, Spread));

        _emitter.Emit(Creaks, Mathf.Lerp(RestDb, 0f, load), Mathf.Lerp(RestPitch, WorkPitch, load));
    }

    private float Load()
    {
        Basis basis = _hull.GlobalBasis.Orthonormalized();

        float heel = Mathf.RadToDeg(basis.Y.AngleTo(Vector3.Up));
        float roll = Mathf.Abs(_hull.AngularVelocity.Dot(basis.Z));

        float fromHeel = Mathf.Clamp(heel / Mathf.Max(FullHeel, 0.1f), 0f, 1f);
        float fromRoll = Mathf.Clamp(roll / Mathf.Max(FullRoll, 0.01f), 0f, 1f);

        return Mathf.Clamp(fromHeel * HeelShare + fromRoll * (1f - HeelShare), 0f, 1f);
    }
}
