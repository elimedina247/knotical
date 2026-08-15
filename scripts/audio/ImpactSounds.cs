using Godot;

namespace Knotical.Audio;

[GlobalClass]
public partial class ImpactSounds : Node3D
{
    [Export] public RigidBody3D Body { get; set; }

    [Export] public SoundEmitter Emitter { get; set; }

    [Export] public ClipSet Fallback { get; set; }

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float MinSpeed { get; set; } = 1.8f;

    [Export(PropertyHint.Range, "1,30,0.5")]
    public float FullSpeed { get; set; } = 8f;

    [Export(PropertyHint.Range, "-30,0,0.5")]
    public float QuietDb { get; set; } = -18f;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float Rearm { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float LightPitch { get; set; } = 1.12f;

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float HeavyPitch { get; set; } = 0.9f;

    private RigidBody3D _body;
    private SoundEmitter _emitter;
    private Vector3 _approach;
    private float _wait;

    public override void _Ready()
    {
        _body = Body ?? GetParentOrNull<RigidBody3D>();
        _emitter = Emitter ?? GetNodeOrNull<SoundEmitter>("SoundEmitter");

        if (_body == null) return;

        _body.ContactMonitor = true;
        if (_body.MaxContactsReported < 4) _body.MaxContactsReported = 4;
        _body.BodyEntered += OnTouch;
    }

    public override void _ExitTree()
    {
        if (_body != null && IsInstanceValid(_body)) _body.BodyEntered -= OnTouch;
    }

    public override void _PhysicsProcess(double delta)
    {
        _wait = Mathf.Max(_wait - (float)delta, 0f);
        if (_body != null && IsInstanceValid(_body)) _approach = _body.LinearVelocity;
    }

    private void OnTouch(Node struck)
    {
        if (_emitter == null || _wait > 0f) return;

        Vector3 theirs = struck is RigidBody3D other ? other.LinearVelocity : Vector3.Zero;
        float speed = (_approach - theirs).Length();
        if (speed < MinSpeed) return;

        _wait = Rearm;

        float weight = Mathf.Clamp(
            Mathf.InverseLerp(MinSpeed, Mathf.Max(FullSpeed, MinSpeed + 0.1f), speed), 0f, 1f);

        ClipSet set = ClipSet.Filled(SurfaceSound.Of(struck)?.Impacts, Fallback);
        _emitter.Emit(set, Mathf.Lerp(QuietDb, 0f, weight), Mathf.Lerp(LightPitch, HeavyPitch, weight));
    }
}
