using Godot;

namespace Knotical.Rigging;

[GlobalClass]
public partial class Rope : Node3D
{
    private const int Sides = 6;
    private static readonly float[] RingCos = new float[Sides];
    private static readonly float[] RingSin = new float[Sides];

    static Rope()
    {
        for (int s = 0; s < Sides; s++)
        {
            float angle = Mathf.Tau * s / Sides;
            RingCos[s] = Mathf.Cos(angle);
            RingSin[s] = Mathf.Sin(angle);
        }
    }

    [Export(PropertyHint.Range, "8,64,1")]
    public int Beads { get; set; } = 24;

    [Export(PropertyHint.Range, "0.01,0.1,0.005")]
    public float Radius { get; set; } = 0.03f;

    [Export(PropertyHint.Range, "0,40,0.5")]
    public float Gravity { get; set; } = 12f;

    [Export(PropertyHint.Range, "0.01,1,0.01")]
    public float Damping { get; set; } = 0.1f;

    [Export(PropertyHint.Range, "2,20,1")]
    public int Iterations { get; set; } = 10;

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float Bias { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0.2,20,0.1")]
    public float MaxSnapSpeed { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float SwayDamping { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "200,50000,100")]
    public float TensileStrength { get; set; } = 4000f;

    [Export] public bool Breaks { get; set; } = true;

    [Export(PropertyHint.Range, "1,20,0.5")]
    public float PullStrength { get; set; } = 8f;

    [Export] public bool PullScalesWithPlayers { get; set; } = true;

    [Export(PropertyHint.Layers3DPhysics)]
    public uint GroundMask { get; set; } = 3;

    private Vector3[] _points;
    private Vector3[] _previous;
    private Vector3[] _trail;
    private Vector3[] _normals;
    private Vector3[] _binormals;
    private Node3D _bodyA;
    private Node3D _bodyB;
    private Vector3 _localA;
    private Vector3 _localB;
    private bool _boundA;
    private bool _boundB;
    private bool _held;
    private Vector3 _hand;
    private RigidBody3D _holder;
    private float _length = 2f;
    private float _tension;
    private bool _taut;
    private float _straight;
    private int _overload;
    private MeshInstance3D _skin;
    private ImmediateMesh _mesh;
    private StandardMaterial3D _material;
    private SphereShape3D _probe;
    private PhysicsShapeQueryParameters3D _probeQuery;
    private readonly Godot.Collections.Array<Rid> _excludes = new();

    public float WorkingLength
    {
        get => _length;
        set => _length = Mathf.Max(value, 0.5f);
    }

    public float Tension => _tension;

    public bool Taut => _taut;

    public bool Held => _held;

    public bool BoundStart => _boundA;

    public bool BoundEnd => _boundB;

    public override void _Ready()
    {
        AddToGroup("Ropes");

        _mesh = new ImmediateMesh();
        _material = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.72f, 0.6f, 0.42f),
            Roughness = 1f,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };

        _skin = new MeshInstance3D
        {
            Name = "Skin",
            Mesh = _mesh,
            TopLevel = true,
            Visible = false,
        };

        AddChild(_skin);
        _skin.GlobalTransform = Transform3D.Identity;

        _probe = new SphereShape3D { Radius = Radius };
        _probeQuery = new PhysicsShapeQueryParameters3D
        {
            Shape = _probe,
            Margin = 0.004f,
        };
    }

    public void BindStart(Node3D body, Vector3 worldPoint)
    {
        _bodyA = body;
        _localA = body.GlobalTransform.AffineInverse() * worldPoint;
        _boundA = true;
    }

    public void BindEnd(Node3D body, Vector3 worldPoint)
    {
        _bodyB = body;
        _localB = body.GlobalTransform.AffineInverse() * worldPoint;
        _boundB = true;
        ReleaseHold();
    }

    public void Hold(Vector3 hand, RigidBody3D holder)
    {
        _hand = hand;
        _held = true;
        _boundB = false;
        _bodyB = null;

        if (_holder == holder) return;

        _holder = holder;
        _excludes.Clear();
        if (holder != null) _excludes.Add(holder.GetRid());
    }

    public void ReleaseHold()
    {
        _held = false;
        _holder = null;
        _excludes.Clear();
    }

    public void TakeEnd(bool endB, Vector3 hand, RigidBody3D holder)
    {
        if (!endB) Reverse();

        _boundB = false;
        _bodyB = null;
        Hold(hand, holder);
    }

    public Node3D EndBody(bool endB) => endB ? _bodyB : _bodyA;

    public bool TryStart(out Vector3 point)
    {
        if (_boundA && IsInstanceValid(_bodyA))
        {
            point = _bodyA.GlobalTransform * _localA;
            return true;
        }

        point = _points != null ? _points[0] : GlobalPosition;
        return false;
    }

    public Vector3 EndPoint(bool endB)
    {
        if (_points != null) return endB ? _points[^1] : _points[0];
        return endB && _held ? _hand : GlobalPosition;
    }

    public Vector3 PointFromEnd(float back)
    {
        if (_points == null) return _held ? _hand : GlobalPosition;

        float remaining = back;
        for (int i = _points.Length - 1; i > 0; i--)
        {
            Vector3 span = _points[i - 1] - _points[i];
            float step = span.Length();
            if (step >= remaining && step > 1e-5f)
                return _points[i] + span * (remaining / step);
            remaining -= step;
        }

        return _points[0];
    }

    private void Reverse()
    {
        if (_points != null)
        {
            System.Array.Reverse(_points);
            System.Array.Reverse(_previous);
        }

        (_bodyA, _bodyB) = (_bodyB, _bodyA);
        (_localA, _localB) = (_localB, _localA);
        (_boundA, _boundB) = (_boundB, _boundA);
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        Validate();

        bool haveA = _boundA;
        Vector3 from = haveA ? _bodyA.GlobalTransform * _localA : Vector3.Zero;

        bool haveB = _boundB || _held;
        Vector3 to = _boundB ? _bodyB.GlobalTransform * _localB : _hand;

        if (!haveA && !haveB)
        {
            _taut = false;
            Decay(dt);
            return;
        }

        if (_points == null) Seed(from, to, haveA, haveB);
        if (!haveA) from = _points[0];
        if (!haveB) to = _points[^1];

        Constrain(dt, haveA, haveB, from, to);
        Simulate(dt, haveA, from, haveB, to);
    }

    private void Validate()
    {
        if (_boundA && !IsInstanceValid(_bodyA))
        {
            _boundA = false;
            _bodyA = null;
        }

        if (_boundB && !IsInstanceValid(_bodyB))
        {
            _boundB = false;
            _bodyB = null;
        }

        if (_held && (_holder == null || !IsInstanceValid(_holder))) ReleaseHold();
    }

    private void Seed(Vector3 from, Vector3 to, bool haveA, bool haveB)
    {
        int count = Mathf.Max(Beads, 4);
        _points = new Vector3[count];
        _previous = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            float t = (float)i / (count - 1);

            if (haveA && haveB) _points[i] = from.Lerp(to, t);
            else if (haveA) _points[i] = from + Vector3.Down * (_length * t * 0.5f);
            else _points[i] = to + Vector3.Down * (_length * (1f - t) * 0.5f);

            _previous[i] = _points[i];
        }
    }

    private void Decay(float dt)
    {
        _tension = Mathf.Lerp(_tension, 0f, Mathf.Min(dt * 10f, 1f));
        _overload = 0;
    }

    private void Constrain(float dt, bool haveA, bool haveB, Vector3 from, Vector3 to)
    {
        _taut = false;

        if (!haveA || !haveB)
        {
            _straight = 0f;
            Decay(dt);
            return;
        }

        float span = Mathf.Max(_length, 1e-3f);
        float distance = from.DistanceTo(to);
        _straight = Mathf.Clamp((distance - span * 0.97f) / (span * 0.03f), 0f, 1f);

        float error = distance - _length;

        if (error <= 0f || distance < 1e-4f)
        {
            Decay(dt);
            return;
        }

        _taut = true;

        Vector3 direction = (to - from) / distance;
        var rigidA = _bodyA as RigidBody3D;
        RigidBody3D rigidB = _held ? _holder : _bodyB as RigidBody3D;

        if (!_held && _bodyA == _bodyB)
        {
            Decay(dt);
            return;
        }

        float grip = _held ? Grip() : 1f;
        float invA = rigidA != null && rigidA.Mass > 0f ? 1f / rigidA.Mass : 0f;
        float invB = rigidB != null && rigidB.Mass > 0f ? 1f / (rigidB.Mass * grip) : 0f;
        float invSum = invA + invB;

        if (invSum <= 0f)
        {
            Decay(dt);
            return;
        }

        Vector3 relative = Velocity(rigidB, to) - Velocity(rigidA, from);
        float stretch = relative.Dot(direction);

        if (SwayDamping > 0f)
        {
            Vector3 sway = relative - direction * stretch;
            Vector3 brake = sway * (Mathf.Min(SwayDamping * dt, 1f) / invSum);
            rigidA?.ApplyImpulse(brake, from - Center(rigidA));
            rigidB?.ApplyImpulse(-brake / grip, to - Center(rigidB));
        }

        float give = Mathf.Min(_length * 0.01f, 0.06f);
        float want = -Mathf.Min(Mathf.Max(error - give, 0f) * Bias / dt, MaxSnapSpeed);
        float excess = stretch - want;

        if (excess <= 0f)
        {
            Decay(dt);
            return;
        }

        float impulse = excess / invSum;
        rigidA?.ApplyImpulse(direction * impulse, from - Center(rigidA));
        rigidB?.ApplyImpulse(direction * (-impulse / grip), to - Center(rigidB));

        _tension = Mathf.Lerp(_tension, impulse / (dt * grip), Mathf.Min(dt * 10f, 1f));

        if (!Breaks || _tension < TensileStrength)
        {
            _overload = 0;
            return;
        }

        _overload++;
        if (_overload >= 4) Snap();
    }

    private float Grip()
    {
        float strength = PullStrength;

        if (PullScalesWithPlayers)
            strength /= Mathf.Max(GetTree().GetNodesInGroup("Players").Count, 1);

        return Mathf.Max(strength, 1f);
    }

    private void Snap()
    {
        GD.Print($"{Name}: rope parted at {_tension:0} N");

        _overload = 0;
        _tension = 0f;
        _taut = false;

        int last = _points.Length - 1;
        int cut = last / 2;

        if (cut >= 2 && _boundA)
        {
            var debris = new Rope
            {
                Beads = cut + 1,
                Radius = Radius,
                Gravity = Gravity,
                Damping = Damping,
                Iterations = Iterations,
                Bias = Bias,
                MaxSnapSpeed = MaxSnapSpeed,
                TensileStrength = TensileStrength,
                Breaks = Breaks,
                PullStrength = PullStrength,
                PullScalesWithPlayers = PullScalesWithPlayers,
                GroundMask = GroundMask,
                WorkingLength = _length * cut / last,
                _points = _points[..(cut + 1)],
                _previous = _previous[..(cut + 1)],
                _bodyA = _bodyA,
                _localA = _localA,
                _boundA = true,
            };

            GetParent().AddChild(debris);
        }

        _points = _points[cut..];
        _previous = _previous[cut..];
        _length = Mathf.Max(_length * (last - cut) / last, 0.5f);
        _boundA = false;
        _bodyA = null;
    }

    private void Simulate(float dt, bool pinA, Vector3 from, bool pinB, Vector3 to)
    {
        int last = _points.Length - 1;
        float retain = Mathf.Pow(Mathf.Clamp(Damping, 0.001f, 1f), dt);
        Vector3 fall = Vector3.Down * (Gravity * dt * dt);

        if (_trail == null || _trail.Length != _points.Length) _trail = new Vector3[_points.Length];
        System.Array.Copy(_points, _trail, _points.Length);

        for (int i = 0; i <= last; i++)
        {
            Vector3 velocity = (_points[i] - _previous[i]) * retain;
            _previous[i] = _points[i];
            _points[i] += velocity + fall;
        }

        Relax(Iterations, pinA, from, pinB, to);

        if (_straight > 0f) Straighten(0.85f * _straight, from, to);

        PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
        Collide(space, pinA, pinB);
        Depenetrate(space, pinA, pinB);
        Relax(Mathf.Max(Iterations / 3, 2), pinA, from, pinB, to);
        Depenetrate(space, pinA, pinB);
    }

    private void Relax(int iterations, bool pinA, Vector3 from, bool pinB, Vector3 to)
    {
        int last = _points.Length - 1;
        float segment = _length / last;

        for (int pass = 0; pass < iterations; pass++)
        {
            if (pinA) _points[0] = from;
            if (pinB) _points[last] = to;

            for (int i = 0; i < last; i++)
            {
                bool lockLow = i == 0 && pinA;
                bool lockHigh = i + 1 == last && pinB;
                if (lockLow && lockHigh) continue;

                Vector3 link = _points[i + 1] - _points[i];
                float distance = link.Length();
                if (distance < 1e-5f) continue;

                Vector3 push = link * ((distance - segment) / distance);
                if (lockLow) _points[i + 1] -= push;
                else if (lockHigh) _points[i] += push;
                else
                {
                    _points[i] += push * 0.5f;
                    _points[i + 1] -= push * 0.5f;
                }
            }
        }

        if (pinA) _points[0] = from;
        if (pinB) _points[last] = to;
    }

    private void Collide(PhysicsDirectSpaceState3D space, bool pinA, bool pinB)
    {
        int last = _points.Length - 1;

        for (int i = 0; i <= last; i++)
        {
            if (i == 0 && pinA) continue;
            if (i == last && pinB) continue;

            Vector3 from = _trail[i];
            Vector3 motion = _points[i] - from;
            float length = motion.Length();
            if (length < 1e-5f) continue;

            Vector3 direction = motion / length;
            var query = PhysicsRayQueryParameters3D.Create(
                from,
                from + direction * (length + Radius),
                GroundMask,
                _excludes);

            Godot.Collections.Dictionary hit = space.IntersectRay(query);
            if (hit.Count == 0) continue;

            Vector3 normal = (Vector3)hit["normal"];
            if (normal.LengthSquared() < 1e-6f) continue;

            Vector3 surface = (Vector3)hit["position"] + normal * Radius;
            Vector3 slide = motion - normal * motion.Dot(normal);

            _points[i] = surface;
            _previous[i] = surface - slide * 0.35f;
        }
    }

    private void Depenetrate(PhysicsDirectSpaceState3D space, bool pinA, bool pinB)
    {
        _probe.Radius = Radius;
        _probeQuery.CollisionMask = GroundMask;
        _probeQuery.Exclude = _excludes;

        int last = _points.Length - 1;

        for (int i = 0; i <= last; i++)
        {
            if (i == 0 && pinA) continue;
            if (i == last && pinB) continue;

            _probeQuery.Transform = new Transform3D(Basis.Identity, _points[i]);
            Godot.Collections.Dictionary rest = space.GetRestInfo(_probeQuery);
            if (rest.Count == 0) continue;

            Vector3 normal = (Vector3)rest["normal"];
            if (normal.LengthSquared() < 1e-6f) continue;

            Vector3 contact = (Vector3)rest["point"];
            float depth = Radius - normal.Dot(_points[i] - contact);
            if (depth <= 0f) continue;

            Vector3 motion = _points[i] - _previous[i];
            Vector3 slide = motion - normal * motion.Dot(normal);

            _points[i] += normal * depth;
            _previous[i] = _points[i] - slide * 0.35f;
        }
    }

    private void Straighten(float amount, Vector3 from, Vector3 to)
    {
        int last = _points.Length - 1;

        for (int i = 1; i < last; i++)
        {
            Vector3 line = from.Lerp(to, (float)i / last);
            _points[i] = _points[i].Lerp(line, amount);
            _previous[i] = _points[i];
        }
    }

    public override void _Process(double delta)
    {
        if (_points == null || _points.Length < 2)
        {
            if (_skin != null) _skin.Visible = false;
            return;
        }

        _skin.Visible = true;
        BuildTube();
    }

    private void BuildTube()
    {
        int count = _points.Length;

        if (_normals == null || _normals.Length < count)
        {
            _normals = new Vector3[count];
            _binormals = new Vector3[count];
        }

        Vector3 normal = Vector3.Right;

        for (int i = 0; i < count; i++)
        {
            Vector3 ahead = _points[Mathf.Min(i + 1, count - 1)] - _points[Mathf.Max(i - 1, 0)];
            Vector3 tangent = ahead.LengthSquared() > 1e-8f ? ahead.Normalized() : Vector3.Up;

            normal -= tangent * normal.Dot(tangent);
            if (normal.LengthSquared() < 1e-6f) normal = tangent.Cross(Vector3.Up);
            if (normal.LengthSquared() < 1e-6f) normal = tangent.Cross(Vector3.Right);
            normal = normal.Normalized();

            _normals[i] = normal;
            _binormals[i] = tangent.Cross(normal);
        }

        _mesh.ClearSurfaces();
        _mesh.SurfaceBegin(Godot.Mesh.PrimitiveType.Triangles, _material);

        for (int i = 0; i < count - 1; i++)
        {
            for (int s = 0; s < Sides; s++)
            {
                int n = (s + 1) % Sides;

                Vector3 dirA = _normals[i] * RingCos[s] + _binormals[i] * RingSin[s];
                Vector3 dirB = _normals[i] * RingCos[n] + _binormals[i] * RingSin[n];
                Vector3 dirC = _normals[i + 1] * RingCos[s] + _binormals[i + 1] * RingSin[s];
                Vector3 dirD = _normals[i + 1] * RingCos[n] + _binormals[i + 1] * RingSin[n];

                Vector3 a = _points[i] + dirA * Radius;
                Vector3 b = _points[i] + dirB * Radius;
                Vector3 c = _points[i + 1] + dirC * Radius;
                Vector3 d = _points[i + 1] + dirD * Radius;

                _mesh.SurfaceSetNormal(dirA);
                _mesh.SurfaceAddVertex(a);
                _mesh.SurfaceSetNormal(dirC);
                _mesh.SurfaceAddVertex(c);
                _mesh.SurfaceSetNormal(dirD);
                _mesh.SurfaceAddVertex(d);

                _mesh.SurfaceSetNormal(dirA);
                _mesh.SurfaceAddVertex(a);
                _mesh.SurfaceSetNormal(dirD);
                _mesh.SurfaceAddVertex(d);
                _mesh.SurfaceSetNormal(dirB);
                _mesh.SurfaceAddVertex(b);
            }
        }

        _mesh.SurfaceEnd();
    }

    private static Vector3 Velocity(RigidBody3D body, Vector3 at)
    {
        if (body == null) return Vector3.Zero;
        return body.LinearVelocity + body.AngularVelocity.Cross(at - Center(body));
    }

    private static Vector3 Center(RigidBody3D body) =>
        body.CenterOfMassMode == RigidBody3D.CenterOfMassModeEnum.Custom
            ? body.GlobalTransform * body.CenterOfMass
            : body.GlobalPosition;
}
