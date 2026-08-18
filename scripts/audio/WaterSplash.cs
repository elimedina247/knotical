using Godot;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Audio;

[GlobalClass]
public partial class WaterSplash : Node3D
{
    [Export] public Node3D Body { get; set; }

    [Export] public SoundEmitter Emitter { get; set; }

    [Export] public ClipSet Splashes { get; set; }

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float MinSpeed { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "1,30,0.5")]
    public float FullSpeed { get; set; } = 9f;

    [Export(PropertyHint.Range, "-30,0,0.5")]
    public float QuietDb { get; set; } = -16f;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float EntryDepth { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0,3,0.01")]
    public float ExitHeight { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "0,5,0.05")]
    public float Rearm { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float SmallPitch { get; set; } = 1.15f;

    [Export(PropertyHint.Range, "0.5,1.5,0.01")]
    public float BigPitch { get; set; } = 0.88f;

    private Node3D _body;
    private RigidBody3D _rigid;
    private SoundEmitter _emitter;
    private float _lastHeight;
    private bool _tracked;
    private bool _wet;
    private float _wait;

    public override void _Ready()
    {
        _body = Body ?? GetParentOrNull<Node3D>();
        _rigid = _body as RigidBody3D;
        _emitter = Emitter ?? GetNodeOrNull<SoundEmitter>("SoundEmitter");
    }

    public override void _PhysicsProcess(double delta)
    {
        OceanField ocean = OceanField.Instance;
        if (ocean == null || _body == null || _emitter == null || !IsInstanceValid(_body)) return;

        Vector3 at = _body.GlobalPosition;
        float surface = ocean.GetRenderedHeight(at);
        float depth = surface - at.Y;
        float fall = Fall(at, (float)delta, ocean);

        _wait = Mathf.Max(_wait - (float)delta, 0f);

        if (_wet)
        {
            if (depth < -ExitHeight) _wet = false;
            return;
        }

        if (depth < EntryDepth) return;

        _wet = true;
        if (_wait > 0f || fall < MinSpeed) return;

        _wait = Rearm;

        float weight = Mathf.Clamp(
            Mathf.InverseLerp(MinSpeed, Mathf.Max(FullSpeed, MinSpeed + 0.1f), fall), 0f, 1f);

        _emitter.GlobalPosition = new Vector3(at.X, surface, at.Z);
        _emitter.Emit(Splashes, Mathf.Lerp(QuietDb, 0f, weight), Mathf.Lerp(SmallPitch, BigPitch, weight));
    }

    private float Fall(Vector3 at, float dt, OceanField ocean)
    {
        float rise = ocean.GetRenderedVerticalVelocity(new Vector2(at.X, at.Z));

        if (_rigid != null) return rise - _rigid.LinearVelocity.Y;

        float speed = _tracked && dt > 0f ? rise - (at.Y - _lastHeight) / dt : 0f;
        _lastHeight = at.Y;
        _tracked = true;

        return speed;
    }
}
