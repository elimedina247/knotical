using System.Collections.Generic;
using Godot;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class BoatHull : RigidBody3D
{
    private const float Gravity = 9.81f;

    private readonly HullForm _form = new();
    private readonly List<IFoil> _foils = new();
    private Vector3[] _local;
    private Vector3[] _world;
    private float[] _depth;
    private int[] _indices;
    private OceanField _ocean;
    private Vector3 _com;
    private Vector3 _linear;
    private Vector3 _angular;
    private Vector3 _force;
    private Vector3 _torque;

    [Export(PropertyHint.Range, "4,300,0.05")]
    public float HullLength { get => _form.Length; set { _form.Length = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,120,0.05")]
    public float Beam { get => _form.Beam; set { _form.Beam = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,50,0.05")]
    public float Draft { get => _form.Draft; set { _form.Draft = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,50,0.05")]
    public float Freeboard { get => _form.Freeboard; set { _form.Freeboard = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,30,0.05")]
    public float BowSheerRise { get => _form.BowSheerRise; set { _form.BowSheerRise = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,30,0.05")]
    public float SternSheerRise { get => _form.SternSheerRise; set { _form.SternSheerRise = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,6,0.05")]
    public float SheerPower { get => _form.SheerPower; set { _form.SheerPower = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,25,0.05")]
    public float Rocker { get => _form.Rocker; set { _form.Rocker = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,6,0.05")]
    public float RockerPower { get => _form.RockerPower; set { _form.RockerPower = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,6,0.05")]
    public float BowSharpness { get => _form.BowSharpness; set { _form.BowSharpness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,6,0.05")]
    public float SternSharpness { get => _form.SternSharpness; set { _form.SternSharpness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float TransomWidth { get => _form.TransomWidth; set { _form.TransomWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-10,15,0.05")]
    public float TransomRake { get => _form.TransomRake; set { _form.TransomRake = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.15,1.5,0.01")]
    public float BilgeFullness { get => _form.BilgeFullness; set { _form.BilgeFullness = value; Rebuild(); } }

    private int _solidStations = 17;
    private int _solidRings = 7;
    private int _collisionSlices = 7;

    [Export(PropertyHint.Range, "5,49,2")]
    public int SolidStations { get => _solidStations; set { _solidStations = value; Rebuild(); } }

    [Export(PropertyHint.Range, "3,17,1")]
    public int SolidRings { get => _solidRings; set { _solidRings = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,16,1")]
    public int CollisionSlices { get => _collisionSlices; set { _collisionSlices = value; Rebuild(); } }

    private float _deckHeight = 2.7f;

    [Export(PropertyHint.Range, "0,20,0.05")]
    public float DeckHeight { get => _deckHeight; set { _deckHeight = value; Rebuild(); } }

    private float _bulwarkThickness = 0.54f;

    [Export(PropertyHint.Range, "0.05,2,0.01")]
    public float BulwarkThickness { get => _bulwarkThickness; set { _bulwarkThickness = value; Rebuild(); } }

    private float _surfaceOffset = 0.075f;

    [Export(PropertyHint.Range, "0,2.5,0.005")]
    public float SurfaceOffset { get => _surfaceOffset; set { _surfaceOffset = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-20,15,0.05")]
    public float CenterOfMassHeight { get; set; } = -0.8f;

    [Export(PropertyHint.Range, "-30,30,0.05")]
    public float CenterOfMassLength { get; set; } = 0.13f;

    [Export] public float WaterDensity { get; set; } = 1025f;

    private float _displacement = 1f;

    [Export(PropertyHint.Range, "0.2,1.4,0.005")]
    public float Displacement { get => _displacement; set { _displacement = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,6,0.05")]
    public float NormalDrag { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0,0.5,0.001")]
    public float SkinDrag { get; set; } = 0.05f;

    [Export(PropertyHint.Range, "0,4,0.05")]
    public float AddedMass { get; set; } = 1f;

    [Export(PropertyHint.Range, "0,8,0.05")]
    public float AddedInertia { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "5,200,1")]
    public float MaxAcceleration { get; set; } = 60f;

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    [Export] public bool Trace { get; set; }

    private double _tick;
    private float _commanded;
    private float _lastVy;
    private float _lift;
    private Vector3 _liftCentre;

    public float SubmergedVolume { get; private set; }

    public override void _Ready()
    {
        Rebuild();
        RefreshRig();
    }

    public void RefreshRig()
    {
        _foils.Clear();
        CollectFoils(this);
    }

    private void CollectFoils(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is IFoil foil) _foils.Add(foil);
            CollectFoils(child);
        }
    }

    private void Rebuild()
    {
        _form.BuildSolid(_solidStations, _solidRings, _surfaceOffset, out _local, out _indices);
        _world = new Vector3[_local.Length];
        _depth = new float[_local.Length];

        CenterOfMassMode = CenterOfMassModeEnum.Custom;
        CenterOfMass = new Vector3(0f, CenterOfMassHeight, CenterOfMassLength);
        GravityScale = 0f;
        Mass = Mathf.Max(1f, WaterDensity * _displacement * _form.VolumeBelow(0f, _surfaceOffset, 192, 192));

        if (IsInsideTree()) BuildCollision();
    }

    private void BuildCollision()
    {
        foreach (Node child in GetChildren())
        {
            if (child is CollisionShape3D existing && existing.Name.ToString().StartsWith("HullSlice"))
            {
                RemoveChild(existing);
                existing.QueueFree();
            }
        }

        int stations = Mathf.Max(3, _solidStations);
        int levels = Mathf.Max(2, _solidRings);
        int slices = Mathf.Clamp(_collisionSlices, 1, stations - 1);
        int span = Mathf.CeilToInt((float)(stations - 1) / slices);

        var points = new List<Vector3>();

        for (int s = 0; s < slices; s++)
        {
            int start = s * span;
            int end = Mathf.Min(stations - 1, start + span);
            if (start >= end) break;

            points.Clear();
            for (int j = start; j <= end; j++)
            {
                float t = (float)j / (stations - 1);
                float top = _form.VAtHeight(t, DeckHeight);
                for (int r = 0; r < levels; r++)
                {
                    Vector3 p = _form.Shell(t, top * r / (levels - 1), _surfaceOffset, 1f);
                    points.Add(p);
                    points.Add(new Vector3(-p.X, p.Y, p.Z));
                }
            }
            AddSlice($"HullSlice{s}", points);

            for (int side = -1; side <= 1; side += 2)
            {
                points.Clear();
                for (int j = start; j <= end; j++)
                {
                    float t = (float)j / (stations - 1);
                    float low = _form.VAtHeight(t, DeckHeight);
                    for (int r = 0; r < 2; r++)
                    {
                        float v = Mathf.Lerp(low, 1f, r);
                        points.Add(_form.Shell(t, v, _surfaceOffset, side));
                        points.Add(_form.Shell(t, v, _surfaceOffset - BulwarkThickness, side));
                    }
                }
                AddSlice($"HullSliceRail{(side < 0 ? "P" : "S")}{s}", points);
            }
        }

        AddEndWall("HullSliceTransom", 0f);
        AddEndWall("HullSliceStem", 1f);
    }

    private void AddEndWall(string name, float t)
    {
        float low = _form.VAtHeight(t, DeckHeight);
        float inward = t < 0.5f ? -BulwarkThickness : BulwarkThickness;
        var points = new List<Vector3>();

        for (int r = 0; r < 2; r++)
        {
            Vector3 edge = _form.Shell(t, Mathf.Lerp(low, 1f, r), _surfaceOffset, 1f);
            points.Add(edge);
            points.Add(new Vector3(-edge.X, edge.Y, edge.Z));
            points.Add(new Vector3(edge.X, edge.Y, edge.Z + inward));
            points.Add(new Vector3(-edge.X, edge.Y, edge.Z + inward));
        }

        AddSlice(name, points);
    }

    private void AddSlice(string name, List<Vector3> points)
    {
        AddChild(new CollisionShape3D
        {
            Name = name,
            Shape = new ConvexPolygonShape3D { Points = points.ToArray() }
        });
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        Transform3D xform = state.Transform;

        _ocean = OceanField.Instance;
        _com = xform * state.CenterOfMassLocal;
        _linear = state.LinearVelocity;
        _angular = state.AngularVelocity;
        _force = Vector3.Zero;
        _torque = Vector3.Zero;
        _lift = 0f;
        _liftCentre = Vector3.Zero;
        SubmergedVolume = 0f;

        if (_ocean != null && _local != null && _indices != null)
        {
            for (int i = 0; i < _local.Length; i++)
            {
                Vector3 p = xform * _local[i];
                _world[i] = p;
                _depth[i] = _ocean.GetHeight(new Vector2(p.X, p.Z)) - p.Y;
            }

            for (int i = 0; i < _indices.Length; i += 3)
            {
                Clip(_indices[i], _indices[i + 1], _indices[i + 2]);
            }
        }

        Vector3 hull = _force;

        for (int i = 0; i < _foils.Count; i++)
        {
            if (_foils[i] is not Node3D node || !IsInstanceValid(node)) continue;

            Vector3 arm = _foils[i].GlobalCentreOfEffort - _com;
            Vector3 f = _foils[i].ComputeForce(_linear + _angular.Cross(arm));

            _force += f;
            _torque += arm.Cross(f);
        }

        Vector3 rig = _force - hull;

        _force.Y -= Mass * Gravity;

        float entrained = WaterDensity * SubmergedVolume;
        _force *= Mass / (Mass + AddedMass * entrained);
        _torque *= Mass / (Mass + AddedInertia * entrained);

        float maxForce = Mass * MaxAcceleration;
        if (_force.LengthSquared() > maxForce * maxForce) _force = _force.Normalized() * maxForce;

        float maxTorque = maxForce * _form.Length * 0.5f;
        if (_torque.LengthSquared() > maxTorque * maxTorque) _torque = _torque.Normalized() * maxTorque;

        if (Trace) Report(state, hull, rig);

        state.ApplyCentralForce(_force);
        state.ApplyTorque(_torque);
    }

    private void Report(PhysicsDirectBodyState3D state, Vector3 hull, Vector3 rig)
    {
        float vy = state.LinearVelocity.Y;

        _commanded += _force.Y / Mass * state.Step;
        _tick += state.Step;
        if (_tick < 0.25) return;

        Vector3 at = state.Transform.Origin;
        float sea = _ocean != null ? _ocean.GetHeight(new Vector2(at.X, at.Z)) : float.NaN;

        Vector3 cob = Mathf.Abs(_lift) > 1f ? _liftCentre / _lift : _com;
        Vector3 local = state.Transform.AffineInverse() * cob;
        float pitch = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(-state.Transform.Basis.Z.Y, -1f, 1f)));

        GD.Print(
            $"hull y={at.Y:F2} sea={sea:F2} vol={SubmergedVolume:F0} pitch={pitch:F1} " +
            $"cobz={local.Z:F2} comz={CenterOfMass.Z:F2} trim={local.Z - CenterOfMass.Z:F2} " +
            $"buoy={hull.Y:F0} rig={rig.Y:F0} net={_force.Y:F0} vy={vy:F2} " +
            $"commanded={_commanded:F2} actual={vy - _lastVy:F2} " +
            $"mass={Mass:F0} enginemass={(state.InverseMass > 0f ? 1f / state.InverseMass : 0f):F0}");

        _tick = 0.0;
        _commanded = 0f;
        _lastVy = vy;
    }

    private void Clip(int i0, int i1, int i2)
    {
        Vector3 a = _world[i0], b = _world[i1], c = _world[i2];
        float da = _depth[i0], db = _depth[i1], dc = _depth[i2];

        int under = (da > 0f ? 1 : 0) + (db > 0f ? 1 : 0) + (dc > 0f ? 1 : 0);
        if (under == 0) return;

        if (under == 3)
        {
            Face(a, da, b, db, c, dc, Vector3.Zero, 0f, false);
            return;
        }

        if (under == 1)
        {
            if (db > 0f) (a, da, b, db, c, dc) = (b, db, c, dc, a, da);
            else if (dc > 0f) (a, da, b, db, c, dc) = (c, dc, a, da, b, db);

            Face(a, da, Cut(a, da, b, db), 0f, Cut(a, da, c, dc), 0f, Vector3.Zero, 0f, false);
            return;
        }

        if (db <= 0f) (a, da, b, db, c, dc) = (b, db, c, dc, a, da);
        else if (dc <= 0f) (a, da, b, db, c, dc) = (c, dc, a, da, b, db);

        Face(Cut(b, db, a, da), 0f, b, db, c, dc, Cut(c, dc, a, da), 0f, true);
    }

    private static Vector3 Cut(Vector3 wet, float wetDepth, Vector3 dry, float dryDepth)
    {
        float t = wetDepth / (wetDepth - dryDepth);
        return wet + (dry - wet) * t;
    }

    private void Face(Vector3 a, float da, Vector3 b, float db, Vector3 c, float dc, Vector3 d, float dd, bool quad)
    {
        Vector3 cross = (b - a).Cross(c - a);
        float twiceArea = cross.Length();
        if (twiceArea < 1e-8f) return;

        Vector3 normal = cross / twiceArea;

        float area = twiceArea * 0.5f;
        Vector3 centroid = (a + b + c) * (area / 3f);
        float depth = (da + db + dc) * (area / 3f);

        if (quad)
        {
            float second = (c - a).Cross(d - a).Length() * 0.5f;
            area += second;
            centroid += (a + c + d) * (second / 3f);
            depth += (da + dc + dd) * (second / 3f);
        }

        if (area < 1e-8f) return;
        centroid /= area;
        depth /= area;
        if (depth <= 0f) return;

        SubmergedVolume += depth * area * -normal.Y;

        Vector3 f = normal * (-WaterDensity * Gravity * depth * area);

        _lift += f.Y;
        _liftCentre += centroid * f.Y;

        Vector3 arm = centroid - _com;
        Vector3 relative = _linear + _angular.Cross(arm);
        relative.Y -= _ocean.GetVerticalVelocity(new Vector2(centroid.X, centroid.Z));

        float alongNormal = relative.Dot(normal);
        f += normal * (-NormalDrag * WaterDensity * area * alongNormal * Mathf.Abs(alongNormal));
        f += (relative - normal * alongNormal) * (-SkinDrag * WaterDensity * area);

        _force += f;
        _torque += arm.Cross(f);
    }
}
