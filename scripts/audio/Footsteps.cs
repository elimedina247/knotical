using Godot;
using Knotical.Player;

namespace Knotical.Audio;

[GlobalClass]
public partial class Footsteps : Node3D
{
    [Export] public PlayerBody Body { get; set; }

    [Export] public PlayerGrab Grab { get; set; }

    [Export] public SoundEmitter Emitter { get; set; }

    [Export] public ClipSet Steps { get; set; }

    [Export] public ClipSet Landings { get; set; }

    [Export(PropertyHint.Range, "0.3,2,0.01")]
    public float Stride { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "0,2,0.05")]
    public float MinSpeed { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.2,4,0.05")]
    public float SoftSpeed { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "0.5,8,0.05")]
    public float LoudSpeed { get; set; } = 3.2f;

    [Export(PropertyHint.Range, "-24,0,0.5")]
    public float SoftDb { get; set; } = -10f;

    [Export(PropertyHint.Range, "0.5,8,0.1")]
    public float LandSpeed { get; set; } = 2f;

    [Export(PropertyHint.Range, "1,12,0.1")]
    public float SlamSpeed { get; set; } = 6f;

    [Export(PropertyHint.Range, "-24,0,0.5")]
    public float LandSoftDb { get; set; } = -8f;

    [Export(PropertyHint.Range, "-40,0,0.5")]
    public float ClimbDb { get; set; } = -15f;

    [Export(PropertyHint.Range, "0.5,2,0.01")]
    public float ClimbPitch { get; set; } = 1.2f;

    private PlayerBody _body;
    private PlayerGrab _grab;
    private SoundEmitter _emitter;
    private Node3D _deck;
    private SurfaceSound _surface;
    private float _travel;
    private float _fall;
    private bool _footing;
    private int _beat;
    private bool _climbing;

    public override void _Ready()
    {
        _body = Body ?? GetParentOrNull<PlayerBody>();
        _grab = Grab ?? _body?.GetNodeOrNull<PlayerGrab>("Grab");
        _emitter = Emitter ?? GetNodeOrNull<SoundEmitter>("SoundEmitter");
        _travel = Stride * 0.6f;
    }

    public override void _Process(double delta)
    {
        if (_body == null || _emitter == null) return;

        if (_grab != null && _grab.IsClimbing)
        {
            Climb();
            _travel = Stride * 0.6f;
            _footing = false;
            _fall = 0f;
            return;
        }

        _climbing = false;

        bool footing = _body.IsGrounded && !_body.IsDowned && !_body.IsSwimming;

        if (!footing)
        {
            _fall = Mathf.Max(_fall, _body.DeckVelocity.Y - _body.LinearVelocity.Y);
            _footing = false;
            _travel = Stride * 0.6f;
            return;
        }

        if (!_footing)
        {
            _footing = true;
            if (_fall > LandSpeed) Land(_fall);
            _fall = 0f;
            return;
        }

        float speed = _body.PlanarVelocity.Length();

        if (speed < MinSpeed)
        {
            _travel = Mathf.Max(_travel, Stride * 0.6f);
            return;
        }

        _travel += speed * (float)delta;
        if (_travel < Stride) return;

        _travel -= Stride;

        float effort = Mathf.Clamp(Mathf.InverseLerp(SoftSpeed, LoudSpeed, speed), 0f, 1f);
        _emitter.Emit(ClipSet.Filled(Surface()?.Steps, Steps), Mathf.Lerp(SoftDb, 0f, effort));
    }

    private void Climb()
    {
        int beat = _grab.HandBeat;

        if (!_climbing)
        {
            _climbing = true;
            _beat = beat;
            return;
        }

        if (beat == _beat) return;

        _beat = beat;
        _emitter.Emit(ClipSet.Filled(Surface()?.Steps, Steps), ClimbDb, ClimbPitch);
    }

    private void Land(float fall)
    {
        float weight = Mathf.Clamp(Mathf.InverseLerp(LandSpeed, SlamSpeed, fall), 0f, 1f);
        SurfaceSound surface = Surface();
        ClipSet set = ClipSet.Filled(surface?.Landings, Landings, surface?.Steps, Steps);

        _emitter.Emit(set, Mathf.Lerp(LandSoftDb, 0f, weight), Mathf.Lerp(1f, 0.85f, weight));
        _travel = Stride * 0.6f;
    }

    private SurfaceSound Surface()
    {
        Node3D deck = _body.Deck;
        if (deck == null || !IsInstanceValid(deck)) return null;
        if (deck == _deck) return _surface;

        _deck = deck;
        _surface = SurfaceSound.Of(deck);
        return _surface;
    }
}
