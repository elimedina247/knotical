using Godot;

namespace Knotical.Player;

[GlobalClass]
public partial class PlayerGrab : Node3D
{
    private sealed class Grip
    {
        public bool Active;
        public Node3D Node;
        public IGrabbable Handle;
        public Vector3 Local;
        public Vector3 LastShoulder;
        public Vector3 LastAnchor;
        public float Slack;
        public float Load;
    }

    [Export(PropertyHint.Range, "0.2,2.5,0.01")]
    public float Reach { get; set; } = 0.62f;

    [Export(PropertyHint.Range, "0.3,4,0.01")]
    public float CastRange { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0.02,0.5,0.005")]
    public float CastRadius { get; set; } = 0.14f;

    [Export(PropertyHint.Range, "1,400,1")]
    public float Stiffness { get; set; } = 120f;

    [Export(PropertyHint.Range, "0,60,0.5")]
    public float Damping { get; set; } = 14f;

    [Export(PropertyHint.Range, "500,80000,100")]
    public float MaxForce { get; set; } = 9000f;

    [Export(PropertyHint.Range, "500,80000,100")]
    public float BreakForce { get; set; } = 6500f;

    [Export(PropertyHint.Range, "0.2,1,0.01")]
    public float Slack { get; set; } = 0.8f;

    [Export] public bool Climb { get; set; } = true;

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float MinSlack { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0.05,3,0.01")]
    public float ReelSpeed { get; set; } = 0.7f;

    [Export(PropertyHint.Layers3DPhysics)]
    public uint GrabMask { get; set; } = 0xFFFFFFFF;

    [Export] public Node3D Pivot { get; set; }

    [Export] public TorsoShape Shape { get; set; }

    [Export] public DangleArm ArmLeft { get; set; }

    [Export] public DangleArm ArmRight { get; set; }

    private readonly Grip[] _grips = { new(), new() };
    private RigidBody3D _body;
    private PlayerBody _player;
    private Node3D _pivot;
    private ShapeCast3D _cast;
    private float _load;

    public float Load => _load;

    public bool IsGripping => _grips[0].Active || _grips[1].Active;

    public override void _Ready()
    {
        _body = GetParentOrNull<RigidBody3D>();
        _player = _body as PlayerBody;
        _pivot = Pivot ?? _body?.GetNodeOrNull<Node3D>("CameraPivot");

        if (_pivot == null) return;

        _cast = new ShapeCast3D
        {
            Name = "GrabCast",
            Shape = new SphereShape3D { Radius = CastRadius },
            TargetPosition = new Vector3(0f, 0f, -CastRange),
            CollisionMask = GrabMask,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Enabled = false,
        };

        _pivot.AddChild(_cast);
        if (_body != null) _cast.AddException(_body);
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("grab_left")) Take(0);
        if (Input.IsActionJustReleased("grab_left")) Drop(0);
        if (Input.IsActionJustPressed("grab_right")) Take(1);
        if (Input.IsActionJustReleased("grab_right")) Drop(1);
    }

    public void Apply(PhysicsDirectBodyState3D state)
    {
        float mass = state.InverseMass > 0f ? 1f / state.InverseMass : 1f;
        _load = 0f;

        Solve(0, ArmLeft, false, state, mass);
        Solve(1, ArmRight, true, state, mass);
    }

    private void Take(int index)
    {
        DangleArm hand = index == 0 ? ArmLeft : ArmRight;
        if (hand == null || _cast == null) return;

        _cast.ForceShapecastUpdate();
        if (!_cast.IsColliding()) return;

        Vector3 from = _cast.GlobalPosition;
        float nearest = float.MaxValue;
        int best = -1;

        for (int i = 0; i < _cast.GetCollisionCount(); i++)
        {
            float distance = from.DistanceSquaredTo(_cast.GetCollisionPoint(i));
            if (distance >= nearest) continue;
            nearest = distance;
            best = i;
        }

        if (best < 0 || _cast.GetCollider(best) is not Node3D node) return;

        Vector3 point = _cast.GetCollisionPoint(best);
        IGrabbable handle = FindHandle(node);
        if (handle != null) point = handle.Attach(point);

        Grip grip = _grips[index];
        grip.Active = true;
        grip.Node = node;
        grip.Handle = handle;
        grip.Local = node.GlobalTransform.AffineInverse() * point;
        grip.LastShoulder = ShoulderWorld(index == 1);
        grip.LastAnchor = point;
        grip.Slack = 1f;
        grip.Load = 0f;

        hand.Grip(point);
    }

    private void Drop(int index) => Release(_grips[index], index == 0 ? ArmLeft : ArmRight);

    private static void Release(Grip grip, DangleArm hand)
    {
        if (!grip.Active) return;

        grip.Handle?.Detach();
        grip.Active = false;
        grip.Node = null;
        grip.Handle = null;
        hand?.Release();
    }

    private void Solve(int index, DangleArm hand, bool right, PhysicsDirectBodyState3D state, float mass)
    {
        Grip grip = _grips[index];
        if (!grip.Active || hand == null) return;

        if (!IsInstanceValid(grip.Node))
        {
            Release(grip, hand);
            return;
        }

        float dt = state.Step;
        Vector3 shoulder = state.Transform * ShoulderLocal(right);
        Vector3 anchor;

        if (grip.Handle != null)
        {
            Vector3 request = grip.LastAnchor + (shoulder - grip.LastShoulder);
            anchor = grip.Handle.Track(request, dt);
        }
        else
        {
            anchor = grip.Node.GlobalTransform * grip.Local;
        }

        Vector3 span = anchor - shoulder;
        float distance = span.Length();

        hand.Grip(shoulder + span.LimitLength(Reach));
        grip.LastShoulder = shoulder;
        grip.LastAnchor = anchor;

        if (grip.Handle != null && !grip.Handle.Anchors) return;

        bool footed = _player == null || _player.IsGrounded;
        grip.Slack = Climb && !footed
            ? Mathf.MoveToward(grip.Slack, MinSlack, ReelSpeed * dt)
            : Mathf.MoveToward(grip.Slack, 1f, ReelSpeed * dt);

        float limit = Reach * Mathf.Clamp(Slack * grip.Slack, 0.05f, 1f);

        if (distance <= limit || distance < 1e-4f)
        {
            grip.Load = 0f;
            return;
        }

        Vector3 direction = span / distance;
        Vector3 offset = shoulder - state.CenterOfMass;
        Vector3 pointVelocity = state.LinearVelocity + state.AngularVelocity.Cross(offset);
        float closing = (pointVelocity - AnchorVelocity(grip.Node, anchor)).Dot(direction);

        float pull = (distance - limit) * Stiffness - closing * Damping;
        Vector3 force = (direction * (pull * mass)).LimitLength(MaxForce);

        state.ApplyForce(force, offset);

        if (grip.Node is RigidBody3D rigid)
            rigid.ApplyForce(-force, anchor - CenterOfMass(rigid));

        grip.Load = force.Length() / Mathf.Max(BreakForce, 1f);
        _load = Mathf.Max(_load, Mathf.Min(grip.Load, 1f));

        if (grip.Load >= 1f) Release(grip, hand);
    }

    private Vector3 ShoulderLocal(bool right)
    {
        if (Shape != null) return Shape.Shoulder(right);

        DangleArm arm = right ? ArmRight : ArmLeft;
        return arm?.Position ?? new Vector3(right ? 0.23f : -0.23f, 0.9f, 0f);
    }

    private Vector3 ShoulderWorld(bool right) =>
        _body == null ? ShoulderLocal(right) : _body.GlobalTransform * ShoulderLocal(right);

    private static Vector3 AnchorVelocity(Node3D node, Vector3 at)
    {
        if (node is not RigidBody3D rigid) return Vector3.Zero;
        return rigid.LinearVelocity + rigid.AngularVelocity.Cross(at - CenterOfMass(rigid));
    }

    private static Vector3 CenterOfMass(RigidBody3D body) =>
        body.CenterOfMassMode == RigidBody3D.CenterOfMassModeEnum.Custom
            ? body.GlobalTransform * body.CenterOfMass
            : body.GlobalPosition;

    private static IGrabbable FindHandle(Node node)
    {
        for (Node step = node; step != null; step = step.GetParent())
            if (step is IGrabbable handle) return handle;

        return null;
    }
}
