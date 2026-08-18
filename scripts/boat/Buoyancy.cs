using System.Collections.Generic;
using Godot;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Boat;

/// <summary>
/// Floats any RigidBody3D it is parented under. Marker3D children are the pontoons;
/// with none, a single pontoon sits at this node's origin.
///
/// Forces are measured against the physics wave surface, same as BoatHull, so loose
/// cargo and hulls ride the same water and stay consistent when they touch. Gravity is
/// left to the body; this only adds water forces, so it composes with DeckCargo.
/// </summary>
[GlobalClass]
public partial class Buoyancy : Node3D
{
    private const float Gravity = 9.81f;
    private const float WaterDensity = 1025f;
    private const float Reserve = 1.5f;

    public static readonly List<Buoyancy> Active = new();

    /// <summary>Full displaceable envelope in cubic metres, split evenly across pontoons.</summary>
    [Export(PropertyHint.Range, "0.01,500,0.01")]
    public float BodyVolume { get; set; } = 1f;

    /// <summary>Vertical metres over which a pontoon goes from dry to fully wet.</summary>
    [Export(PropertyHint.Range, "0.1,10,0.05")]
    public float ProbeSpan { get; set; } = 1f;

    [Export(PropertyHint.Range, "0,8,0.05")]
    public float HeaveDamping { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0,4,0.05")]
    public float WaterDrag { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "0,2,0.05")]
    public float WaterDragLinear { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SlopePush { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0,4,0.05")]
    public float SpinDamping { get; set; } = 1.2f;

    private RigidBody3D _body;
    private Vector3[] _local = System.Array.Empty<Vector3>();
    private Vector3[] _world = System.Array.Empty<Vector3>();
    private float[] _wet = System.Array.Empty<float>();
    private Vector3[] _force = System.Array.Empty<Vector3>();

    public RigidBody3D Body => _body;

    public int ProbeCount => _local.Length;

    public float Wetness { get; private set; }

    public void GetProbe(int i, out Vector3 world, out float wet, out Vector3 force, out float span)
    {
        world = _world[i];
        wet = _wet[i];
        force = _force[i];
        span = ProbeSpan;
    }

    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint()) Active.Add(this);
    }

    public override void _ExitTree()
    {
        Active.Remove(this);
    }

    public override void _Ready()
    {
        _body = GetParentOrNull<RigidBody3D>();

        var locals = new List<Vector3>();
        foreach (Node child in GetChildren())
        {
            if (child is Marker3D marker) locals.Add(marker.Position);
        }

        if (locals.Count == 0) locals.Add(Vector3.Zero);

        _local = locals.ToArray();
        _world = new Vector3[_local.Length];
        _wet = new float[_local.Length];
        _force = new Vector3[_local.Length];
    }

    public override void _PhysicsProcess(double delta)
    {
        OceanField ocean = OceanField.Instance;
        if (ocean == null || _body == null || !IsInstanceValid(_body)) return;

        Transform3D xform = GlobalTransform;
        Vector3 origin = _body.GlobalPosition;
        Vector3 linear = _body.LinearVelocity;
        Vector3 angular = _body.AngularVelocity;

        int count = _local.Length;
        float shareVolume = BodyVolume / count;
        float shareMass = _body.Mass / count;
        float shareArea = Mathf.Pow(BodyVolume, 2f / 3f) / count;

        float wetSum = 0f;

        for (int i = 0; i < count; i++)
        {
            Vector3 p = xform * _local[i];
            float wasWet = _wet[i];
            _world[i] = p;

            var flat = new Vector2(p.X, p.Z);
            float depth = ocean.GetHeight(flat) - p.Y;
            float s = Mathf.Clamp(depth / ProbeSpan, 0f, Reserve);

            if (s <= 0f)
            {
                _wet[i] = 0f;
                _force[i] = Vector3.Zero;
                continue;
            }

            float wet = Mathf.Min(s, 1f);
            _wet[i] = wet;
            wetSum += wet;

            Vector3 entryRel = linear + angular.Cross(p - origin) - ocean.GetFlow(p);
            if (wasWet <= 0f && entryRel.Y < -1.5f)
            {
                Knotical.Vfx.SplashEmitter.Request(
                    new Vector3(p.X, depth + p.Y, p.Z), -entryRel.Y,
                    Mathf.Sqrt(Mathf.Max(shareArea, 0.05f)));
            }

            Vector3 lift = SlopePush > 0f
                ? Vector3.Up.Lerp(ocean.GetNormal(flat), SlopePush).Normalized()
                : Vector3.Up;

            Vector3 f = lift * (WaterDensity * Gravity * shareVolume * s);

            Vector3 rel = linear + angular.Cross(p - origin) - ocean.GetFlow(p);
            Vector3 drag = -rel * (WaterDragLinear * shareMass)
                         - rel * (0.5f * WaterDrag * WaterDensity * shareArea * rel.Length());
            drag.Y -= HeaveDamping * shareMass * rel.Y;

            f += drag * wet;

            _force[i] = f;
            _body.ApplyForce(f, p - origin);
        }

        Wetness = wetSum / count;

        if (SpinDamping > 0f && Wetness > 0f)
        {
            float lever = Mathf.Pow(BodyVolume, 2f / 3f) / 6f;
            _body.ApplyTorque(-angular * (SpinDamping * Wetness * _body.Mass * lever));
        }
    }
}
