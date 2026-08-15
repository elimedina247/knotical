using Godot;
using Knotical.Sky;
using Knotical.Weather;

namespace Knotical.Audio;

[GlobalClass]
public partial class GullCalls : Node3D
{
    [Export] public SoundEmitter Emitter { get; set; }

    [Export] public ClipSet Calls { get; set; }

    [Export(PropertyHint.Range, "2,120,1")]
    public float MinInterval { get; set; } = 16f;

    [Export(PropertyHint.Range, "4,300,1")]
    public float MaxInterval { get; set; } = 70f;

    [Export(PropertyHint.Range, "5,200,1")]
    public float MinRange { get; set; } = 12f;

    [Export(PropertyHint.Range, "10,400,1")]
    public float MaxRange { get; set; } = 60f;

    [Export(PropertyHint.Range, "0,60,0.5")]
    public float MinHeight { get; set; } = 4f;

    [Export(PropertyHint.Range, "0,120,0.5")]
    public float MaxHeight { get; set; } = 25f;

    [Export(PropertyHint.Range, "1,6,1")]
    public int MaxBurst { get; set; } = 3;

    [Export(PropertyHint.Range, "0.1,2,0.05")]
    public float BurstGap { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,40,0.5")]
    public float SilencedWind { get; set; } = 14f;

    [Export] public bool DaylightOnly { get; set; } = true;

    private readonly RandomNumberGenerator _rng = new();
    private SoundEmitter _emitter;
    private float _wait;
    private int _burst;

    public override void _Ready()
    {
        _rng.Randomize();
        _emitter = Emitter ?? GetNodeOrNull<SoundEmitter>("SoundEmitter");
        _wait = _rng.RandfRange(MinInterval, MaxInterval);
    }

    public override void _Process(double delta)
    {
        if (_emitter == null) return;

        _wait -= (float)delta;
        if (_wait > 0f) return;

        if (_burst > 0)
        {
            _burst--;
            Call(false);
            _wait = _burst > 0 ? BurstGap * _rng.RandfRange(0.7f, 1.6f) : Rest();
            return;
        }

        _wait = Rest();

        if (!Allowed()) return;

        _burst = _rng.RandiRange(1, Mathf.Max(MaxBurst, 1)) - 1;
        Call(true);
        if (_burst > 0) _wait = BurstGap * _rng.RandfRange(0.7f, 1.6f);
    }

    private float Rest() => _rng.RandfRange(MinInterval, Mathf.Max(MaxInterval, MinInterval));

    private bool Allowed()
    {
        if (DaylightOnly && DayCycle.Instance != null && !DayCycle.Instance.IsDay) return false;

        float wind = Wind.Instance?.Speed ?? 0f;
        float hush = Mathf.Clamp(Mathf.InverseLerp(SilencedWind * 0.6f, SilencedWind, wind), 0f, 1f);

        return _rng.Randf() > hush;
    }

    private void Call(bool reposition)
    {
        if (reposition)
        {
            float angle = _rng.RandfRange(0f, Mathf.Tau);
            float range = _rng.RandfRange(MinRange, Mathf.Max(MaxRange, MinRange));

            Position = new Vector3(
                Mathf.Cos(angle) * range,
                _rng.RandfRange(MinHeight, Mathf.Max(MaxHeight, MinHeight)),
                Mathf.Sin(angle) * range);
        }
        else
        {
            Position += new Vector3(
                _rng.RandfRange(-2f, 2f),
                _rng.RandfRange(-1f, 1f),
                _rng.RandfRange(-2f, 2f));
        }

        _emitter.Emit(Calls);
    }
}
