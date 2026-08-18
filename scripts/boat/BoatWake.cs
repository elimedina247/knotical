using System.Collections.Generic;
using Godot;

namespace Knotical.Boat;

[GlobalClass]
public partial class BoatWake : Node
{
    private static readonly List<BoatWake> Registry = new();

    public static IReadOnlyList<BoatWake> Active => Registry;

    public const int MaxTrailPoints = 32;

    [Export(PropertyHint.Range, "1,30,0.5")]
    public float DropSpacing { get; set; } = 6f;

    [Export(PropertyHint.Range, "2,60,0.5")]
    public float TrailLife { get; set; } = 22f;

    [Export(PropertyHint.Range, "0,5,0.1")]
    public float MinSpeed { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0.5,30,0.5")]
    public float FullSpeed { get; set; } = 6f;

    [Export(PropertyHint.Range, "0,3,0.05")]
    public float SpreadRate { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Strength { get; set; } = 0.9f;

    private readonly List<Vector4> _trail = new();
    private BoatHull _hull;
    private Vector2 _lastDrop;
    private bool _hasDrop;

    public IReadOnlyList<Vector4> TrailPoints => _trail;

    public Vector4 Bow { get; private set; }

    public float TrailWidth => _hull?.Beam ?? 10f;

    public float HullLength => _hull?.HullLength ?? 30f;

    public override void _EnterTree() => Registry.Add(this);

    public override void _ExitTree() => Registry.Remove(this);

    public override void _Ready() => _hull = GetParent<BoatHull>();

    public override void _Process(double delta)
    {
        Knotical.Ocean.Ocean ocean = Knotical.Ocean.Ocean.Instance;
        if (_hull == null || ocean == null) return;

        float now = (float)ocean.Time;
        _trail.RemoveAll(p => now - p.Z > TrailLife);

        Vector3 velocity = _hull.LinearVelocity;
        float speed = new Vector2(velocity.X, velocity.Z).Length();
        float fraction = Mathf.Clamp(speed / FullSpeed, 0f, 1f)
                         * Mathf.SmoothStep(MinSpeed, MinSpeed * 2f, speed);

        Basis basis = _hull.GlobalBasis.Orthonormalized();
        var ahead = new Vector2(-basis.Z.X, -basis.Z.Z);
        float heading = Mathf.Atan2(ahead.X, ahead.Y);

        Vector3 bow = _hull.ToGlobal(new Vector3(0f, 0f, -HullLength * 0.5f));
        Bow = new Vector4(bow.X, bow.Z, heading, fraction);

        if (speed < MinSpeed) return;

        Vector3 stern = _hull.ToGlobal(new Vector3(0f, 0f, HullLength * 0.5f));
        var at = new Vector2(stern.X, stern.Z);

        if (_hasDrop && at.DistanceTo(_lastDrop) < DropSpacing) return;

        _hasDrop = true;
        _lastDrop = at;
        _trail.Add(new Vector4(at.X, at.Y, now, fraction));

        while (_trail.Count > MaxTrailPoints) _trail.RemoveAt(0);
    }
}
