using System.Collections.Generic;
using Godot;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Boat;

/// <summary>
/// Floats any RigidBody3D it is parented under. Marker3D children are the pontoons;
/// with none, a single pontoon sits at this node's origin. Each pontoon is a sphere of
/// <see cref="Radius"/>: submersion drives an upward buoyant force, vertical damping
/// kills the bobbing, and planar drag resists sliding while wet. Gravity is left to the
/// body; this only adds water forces, so it composes with DeckCargo.
/// </summary>
[GlobalClass]
public partial class Buoyancy : Node3D
{
    private const float Gravity = 9.81f;
    private const float WaterDensity = 1025f;
    private const float SplashEntrySpeed = 1.5f;

    public static readonly List<Buoyancy> Active = new();

    [Export(PropertyHint.Range, "0.05,4,0.01")]
    public float Radius { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0.1,10,0.05")]
    public float Coefficient { get; set; } = 1f;

    [Export(PropertyHint.Range, "0,1000000,100")]
    public float MaxForce { get; set; }

    [Export(PropertyHint.Range, "0,8,0.05")]
    public float DampingFactor1 { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "0,4,0.05")]
    public float DampingFactor2 { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,8,0.05")]
    public float DragCoefficient { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "0,8,0.05")]
    public float DragCoefficient2 { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0.5,30,0.5")]
    public float MaxDragSpeed { get; set; } = 8f;

    [Export(PropertyHint.Range, "0,8,0.05")]
    public float AngularDrag { get; set; } = 0.8f;

    [Export] public bool ApplyWaveNormal { get; set; }

    [Export(PropertyHint.Range, "0,20,0.1")]
    public float WaveNormalGain { get; set; } = 3f;

    [Export] public bool SnapToWaterOnActivation { get; set; }

    [Export(PropertyHint.Range, "1,8,1")]
    public int SnapToWaterIterations { get; set; } = 3;

    private RigidBody3D _body;
    private Vector3[] _local = System.Array.Empty<Vector3>();
    private Vector3[] _world = System.Array.Empty<Vector3>();
    private float[] _wet = System.Array.Empty<float>();
    private Vector3[] _force = System.Array.Empty<Vector3>();
    private float _lever = 0.5f;
    private bool _snapDone;

    public RigidBody3D Body => _body;

    public int ProbeCount => _local.Length;

    public float Wetness { get; private set; }

    public void GetProbe(int i, out Vector3 world, out float wet, out Vector3 force, out float span)
    {
        world = _world[i];
        wet = _wet[i];
        force = _force[i];
        span = Radius;
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

        float spread = 0f;
        foreach (Vector3 p in _local) spread += new Vector2(p.X, p.Z).Length();
        _lever = Mathf.Max(spread / _local.Length, 0.25f);

        _snapDone = !SnapToWaterOnActivation;
    }

    public override void _PhysicsProcess(double delta)
    {
        OceanField ocean = OceanField.Instance;
        if (ocean == null || _body == null || !IsInstanceValid(_body)) return;

        if (!_snapDone) SnapToWater(ocean);

        Transform3D xform = GlobalTransform;
        Vector3 origin = _body.GlobalPosition;
        Vector3 linear = _body.LinearVelocity;
        Vector3 angular = _body.AngularVelocity;

        int count = _local.Length;
        float pontoonVolume = 4f / 3f * Mathf.Pi * Radius * Radius * Radius;
        float capacity = WaterDensity * pontoonVolume * Coefficient;

        float wetSum = 0f;

        for (int i = 0; i < count; i++)
        {
            Vector3 p = xform * _local[i];
            float wasWet = _wet[i];
            _world[i] = p;

            var flat = new Vector2(p.X, p.Z);
            float water = ocean.GetHeight(flat);
            float s = Mathf.Clamp((water - (p.Y - Radius)) / (2f * Radius), 0f, 1f);
            _wet[i] = s;

            if (s <= 0f)
            {
                _force[i] = Vector3.Zero;
                continue;
            }

            wetSum += s;

            Vector3 velocity = linear + angular.Cross(p - origin);

            if (wasWet <= 0f && velocity.Y < -SplashEntrySpeed)
            {
                Knotical.Vfx.SplashEmitter.Request(new Vector3(p.X, water, p.Z), -velocity.Y, Radius);
            }

            Vector3 f = Vector3.Up * (Gravity * capacity * s);

            f.Y -= (DampingFactor1 * velocity.Y + DampingFactor2 * velocity.Y * Mathf.Abs(velocity.Y))
                * s * capacity;

            var planar = new Vector3(velocity.X, 0f, velocity.Z);
            float blend = Mathf.Min(planar.Length() / MaxDragSpeed, 1f);
            f -= planar * ((DragCoefficient + DragCoefficient2 * blend) * s * capacity);

            if (MaxForce > 0f && f.Length() > MaxForce) f = f.Normalized() * MaxForce;

            _force[i] = f;
            _body.ApplyForce(f, p - origin);
        }

        Wetness = wetSum / count;

        if (Wetness <= 0f) return;

        float inertia = _body.Mass * _lever * _lever;

        if (AngularDrag > 0f)
        {
            _body.ApplyTorque(-angular * (AngularDrag * Wetness * inertia));
        }

        if (ApplyWaveNormal && WaveNormalGain > 0f)
        {
            Vector3 up = _body.GlobalBasis.Y.Normalized();
            Vector3 normal = ocean.GetNormal(new Vector2(origin.X, origin.Z));
            _body.ApplyTorque(up.Cross(normal) * (WaveNormalGain * Wetness * inertia));
        }
    }

    private void SnapToWater(OceanField ocean)
    {
        _snapDone = true;

        float totalVolume = 4f / 3f * Mathf.Pi * Radius * Radius * Radius * _local.Length;
        float rest = Mathf.Clamp(
            _body.Mass / (WaterDensity * Mathf.Max(totalVolume, 0.0001f) * Coefficient), 0f, 1f);
        float restDepth = 2f * Radius * rest - Radius;

        for (int iter = 0; iter < SnapToWaterIterations; iter++)
        {
            Transform3D xform = GlobalTransform;
            float waterMean = 0f;
            float pontoonMean = 0f;

            foreach (Vector3 local in _local)
            {
                Vector3 p = xform * local;
                waterMean += ocean.GetHeight(new Vector2(p.X, p.Z));
                pontoonMean += p.Y;
            }

            waterMean /= _local.Length;
            pontoonMean /= _local.Length;

            _body.GlobalPosition += Vector3.Up * (waterMean - restDepth - pontoonMean);
        }

        _body.LinearVelocity = Vector3.Zero;
        _body.AngularVelocity = Vector3.Zero;
    }
}
