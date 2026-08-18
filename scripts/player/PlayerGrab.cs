using Godot;

namespace Knotical.Player;

[GlobalClass]
public partial class PlayerGrab : Node3D
{
    [Export(PropertyHint.Range, "0.2,2.5,0.01")]
    public float Reach { get; set; } = 0.62f;

    [Export(PropertyHint.Range, "0.3,4,0.01")]
    public float AimDistance { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "0.02,0.5,0.005")]
    public float CastRadius { get; set; } = 0.09f;

    [Export(PropertyHint.Range, "0.5,20,0.1")]
    public float MaxPullSpeed { get; set; } = 6f;

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float Correction { get; set; } = 0.2f;

    [Export(PropertyHint.Range, "0,20,0.1")]
    public float SwingDamping { get; set; } = 3f;

    [Export(PropertyHint.Range, "500,80000,100")]
    public float BreakForce { get; set; } = 6500f;

    [Export] public bool Breaks { get; set; }

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float MinSpan { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0.05,3,0.01")]
    public float ClimbSpeed { get; set; } = 0.9f;

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float ClimbBite { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "1,100,0.5")]
    public float ClimbMassRatio { get; set; } = 8f;

    [Export(PropertyHint.Range, "1,90,1")]
    public float ClimbEdgeAngle { get; set; } = 50f;

    [Export(PropertyHint.Range, "0.4,2.5,0.01")]
    public float CarryDistance { get; set; } = 1.1f;

    [Export(PropertyHint.Range, "0.2,3,0.05")]
    public float CarrySlip { get; set; } = 1f;

    [Export(PropertyHint.Range, "5,200,1")]
    public float CarryAccel { get; set; } = 30f;

    [Export(PropertyHint.Range, "0.05,0.6,0.01")]
    public float HandSpread { get; set; } = 0.24f;

    [Export(PropertyHint.Range, "0,0.3,0.005")]
    public float HangSpread { get; set; } = 0.05f;

    [Export(PropertyHint.Range, "0.1,1.2,0.01")]
    public float HandStride { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float VaultClearance { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,6,0.1")]
    public float VaultReach { get; set; } = 1.6f;

    [Export(PropertyHint.Range, "0.1,2,0.05")]
    public float VaultPause { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0.2,2.5,0.01")]
    public float LedgeHigh { get; set; } = 1.6f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float LedgeLow { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float LedgeDepth { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0.1,1,0.01")]
    public float LedgeFlat { get; set; } = 0.7f;

    [Export(PropertyHint.Layers3DPhysics)]
    public uint GrabMask { get; set; } = 0xFFFFFFFF;

    [Export] public bool ShowMarker { get; set; } = true;

    [Export(PropertyHint.Range, "0.01,0.2,0.005")]
    public float MarkerRadius { get; set; } = 0.045f;

    [Export] public Color MarkerColor { get; set; } = new("#3FA9F5");

    [Export] public Node3D Pivot { get; set; }

    [Export] public TorsoShape Shape { get; set; }

    [Export] public DangleArm ArmLeft { get; set; }

    [Export] public DangleArm ArmRight { get; set; }

    private RigidBody3D _body;
    private Node3D _pivot;
    private ShapeCast3D _cast;
    private MeshInstance3D _marker;
    private bool _mapped;
    private bool _attached;
    private bool _rearm;
    private bool _climbing;
    private Node3D _node;
    private RigidBody3D _carried;
    private IGrabbable _handle;
    private Vector3 _local;
    private Vector3 _normal = Vector3.Up;
    private Vector3 _lastAnchor;
    private Vector3 _lastChest;
    private Vector3 _step;
    private float _span;
    private float _phase;
    private float _pause;
    private float _load;
    private Vector3 _holdLeft;
    private Vector3 _holdRight;
    private Node3D _holdNode;
    private int _stride;
    private bool _wasClimbing;
    private bool _engaged;
    private float _hang;

    public float Load => _load;

    public bool Locked { get; set; }

    public bool IsGripping => _attached;

    public bool HandsEngaged => _engaged;

    public bool IsClimbing => _climbing;

    public float HandPhase => _phase;

    public override void _Ready()
    {
        _mapped = InputMap.HasAction("grab");

        _body = GetParentOrNull<RigidBody3D>();
        _pivot = Pivot ?? _body?.GetNodeOrNull<Node3D>("CameraPivot");
        ArmLeft ??= _body?.GetNodeOrNull<DangleArm>("ArmLeft");
        ArmRight ??= _body?.GetNodeOrNull<DangleArm>("ArmRight");
        Shape ??= _body?.GetNodeOrNull<TorsoShape>("Shape");

        if (_pivot == null)
        {
            GD.PushWarning($"{Name}: grab is inert, no camera pivot.");
            return;
        }

        _cast = new ShapeCast3D
        {
            Name = "GrabCast",
            Shape = new SphereShape3D { Radius = CastRadius },
            CollisionMask = GrabMask,
            CollideWithAreas = false,
            CollideWithBodies = true,
            Enabled = false,
        };

        AddChild(_cast);
        if (_body != null) _cast.AddException(_body);

        _marker = new MeshInstance3D
        {
            Name = "GrabMarker",
            Mesh = new SphereMesh { Radius = MarkerRadius, Height = MarkerRadius * 2f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = MarkerColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                DepthDrawMode = BaseMaterial3D.DepthDrawModeEnum.Disabled,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                NoDepthTest = true,
                RenderPriority = 15,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true,
            Visible = false,
        };

        AddChild(_marker);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_cast == null) return;

        if (Locked)
        {
            _engaged = false;
            Release();
            Mark(null);
            ArmLeft?.Release();
            ArmRight?.Release();
            return;
        }

        float dt = (float)delta;
        bool held = _mapped ? Input.IsActionPressed("grab") : Input.IsMouseButtonPressed(MouseButton.Left);

        _pause = Mathf.Max(_pause - dt, 0f);
        if (!held) _rearm = false;

        if (!held || _rearm || _pause > 0f)
        {
            _engaged = false;
            Release();
            Pose(false, dt);
            return;
        }

        _engaged = true;

        if (!_attached) Catch();

        Vector2 move = Input.GetVector("move_left", "move_right", "move_forward", "move_back");

        _climbing = _attached && Climbable() && move.LengthSquared() > 0.01f;
        _step = _climbing ? Surface(move) : Vector3.Zero;

        if (_step != Vector3.Zero && !Crawl(dt))
        {
            _climbing = false;
            _step = Vector3.Zero;
        }

        Pose(true, dt);
    }

    private void Catch()
    {
        float reach = Grasp();
        Vector3 chest = ChestWorld();
        Vector3 tip = chest + Aim(chest) * reach;

        bool found = Sweep(chest, tip, true, out Node3D node, out Vector3 point, out Vector3 normal);

        if (!found)
            found = Sweep(chest, chest + Vector3.Up * reach, true, out node, out point, out normal);

        if (!found)
            found = Sweep(chest, chest + Vector3.Down * reach, true, out node, out point, out normal);

        if (!found) return;

        _handle = FindHandle(node);
        if (_handle != null) point = _handle.Attach(point);
        else if (node is RigidBody3D { Freeze: false } rigid && rigid.Mass < OwnMass() * ClimbMassRatio)
        {
            _carried = rigid;
            _body?.AddCollisionExceptionWith(rigid);
        }

        _attached = true;
        _node = node;
        _normal = normal;
        _local = node.GlobalTransform.AffineInverse() * point;
        _lastAnchor = point;
        _lastChest = chest;
        _span = Mathf.Min((point - chest).Length(), reach);
        _load = 0f;
    }

    private bool Crawl(float dt)
    {
        if (!IsInstanceValid(_node)) return false;

        Vector3 goal = _node.GlobalTransform * _local + _step * (ClimbSpeed * dt);
        Vector3 lift = _normal * ClimbBite;

        if (!Sweep(goal + lift, goal - lift, out Node3D node, out Vector3 point, out Vector3 normal)) return false;
        if (normal.Dot(_normal) < Mathf.Cos(Mathf.DegToRad(ClimbEdgeAngle))) return false;

        _node = node;
        _normal = normal;
        _local = node.GlobalTransform.AffineInverse() * point;
        return true;
    }

    private void Pose(bool held, float dt)
    {
        if (!_attached || !IsInstanceValid(_node))
        {
            _wasClimbing = false;

            if (!held)
            {
                Mark(null);
                ArmLeft?.Release();
                ArmRight?.Release();
                return;
            }

            Vector3 chest = ChestWorld();
            Vector3 tip = chest + Aim(chest) * Grasp();
            Vector3 side = _pivot.GlobalBasis.X * HandSpread;

            Mark(tip);
            ArmLeft?.Grip(tip - side);
            ArmRight?.Grip(tip + side);
            return;
        }

        Vector3 anchor = _node.GlobalTransform * _local;
        Mark(Hold(out Vector3 grip) ? grip : anchor);

        Vector3 across = Flatten(_pivot.GlobalBasis.X, _normal);
        if (across == Vector3.Zero) across = _pivot.GlobalBasis.X;

        if (!_climbing)
        {
            _wasClimbing = false;
            float spread = Mathf.Lerp(HandSpread, HangSpread, Hanging(anchor, dt));
            ArmLeft?.Grip(anchor - across * spread);
            ArmRight?.Grip(anchor + across * spread);
            return;
        }

        if (!_wasClimbing || _holdNode != _node) Rebase(anchor, across);
        _wasClimbing = true;

        _phase += Mathf.Tau * ClimbSpeed * dt / Mathf.Max(HandStride, 1e-3f);
        int half = (int)(_phase / Mathf.Pi);

        if (half != _stride)
        {
            _stride = half;
            bool left = (half & 1) == 0;
            Vector3 ahead = anchor
                + _step * (HandStride * 0.5f)
                + across * (left ? -HandSpread : HandSpread);
            Vector3 local = _node.GlobalTransform.AffineInverse() * ahead;

            if (left)
            {
                _holdLeft = local;
                ArmLeft?.Release();
            }
            else
            {
                _holdRight = local;
                ArmRight?.Release();
            }
        }

        ArmLeft?.Grip(_node.GlobalTransform * _holdLeft);
        ArmRight?.Grip(_node.GlobalTransform * _holdRight);
    }

    private float Hanging(Vector3 anchor, float dt)
    {
        float want = 0f;
        Vector3 lift = anchor - ChestWorld();

        if (_body is PlayerBody { IsGrounded: false } && lift.LengthSquared() > 1e-6f)
            want = Mathf.SmoothStep(0.4f, 0.85f, lift.Normalized().Dot(Vector3.Up));

        _hang = Mathf.MoveToward(_hang, want, dt / 0.15f);
        return _hang;
    }

    private void Rebase(Vector3 anchor, Vector3 across)
    {
        _phase = 0f;
        _stride = 0;
        _holdNode = _node;

        Transform3D inverse = _node.GlobalTransform.AffineInverse();
        _holdLeft = inverse * (anchor - across * HandSpread);
        _holdRight = inverse * (anchor + across * HandSpread);
    }

    private void Mark(Vector3? at)
    {
        if (_marker == null) return;

        bool show = ShowMarker && at.HasValue;
        _marker.Visible = show;

        if (show) _marker.GlobalPosition = at.Value;
    }

    private bool Climbable()
    {
        if (_handle != null) return _handle.Anchors;
        return _carried == null;
    }

    private float OwnMass() => _body != null && _body.Mass > 0f ? _body.Mass : 70f;

    private Vector3 Surface(Vector2 move)
    {
        Basis view = _pivot.GlobalBasis;

        Vector3 along = Flatten(view.Y, _normal);
        if (along == Vector3.Zero) along = Flatten(-view.Z, _normal);
        if (along == Vector3.Zero) return Vector3.Zero;

        Vector3 across = Flatten(view.X, _normal);
        if (across == Vector3.Zero) across = along.Cross(_normal);

        Vector3 step = across * move.X - along * move.Y;

        return step.LengthSquared() > 1e-6f ? step.Normalized() : Vector3.Zero;
    }

    private static Vector3 Flatten(Vector3 axis, Vector3 normal)
    {
        Vector3 flat = axis - normal * axis.Dot(normal);
        return flat.LengthSquared() > 1e-4f ? flat.Normalized() : Vector3.Zero;
    }

    public void Drop()
    {
        _pause = VaultPause;
        _rearm = true;
        Release();
    }

    private void Release()
    {
        _climbing = false;
        _step = Vector3.Zero;

        if (!_attached) return;

        _handle?.Detach();

        if (_carried != null)
        {
            if (IsInstanceValid(_carried)) _body?.RemoveCollisionExceptionWith(_carried);
            _carried = null;
        }

        _attached = false;
        _node = null;
        _handle = null;
        _load = 0f;
    }

    private bool Hold(out Vector3 anchor)
    {
        anchor = _lastAnchor;
        if (!_attached || !IsInstanceValid(_node)) return false;

        if (_handle == null) anchor = _node.GlobalTransform * _local;
        return true;
    }

    public bool FindLedge(Vector3 origin, out Vector3 stand)
    {
        stand = Vector3.Zero;
        if (_cast == null || _carried != null || !Hold(out Vector3 face)) return false;

        Vector3 forward = -_pivot.GlobalBasis.Z;
        forward -= Vector3.Up * forward.Y;
        if (forward.LengthSquared() < 1e-4f) return false;

        Vector3 lip = face + forward.Normalized() * LedgeDepth;
        Vector3 from = new(lip.X, origin.Y + LedgeHigh, lip.Z);
        Vector3 to = new(lip.X, face.Y - LedgeLow, lip.Z);

        if (!Sweep(from, to, out _, out Vector3 top, out Vector3 normal)) return false;
        if (normal.Dot(Vector3.Up) < LedgeFlat) return false;
        if (top.Y - origin.Y > LedgeHigh) return false;

        stand = new Vector3(lip.X, top.Y, lip.Z);
        return true;
    }

    public bool Vault(PhysicsDirectBodyState3D state)
    {
        if (_carried != null || !Hold(out Vector3 top)) return false;

        Vector3 origin = state.Transform.Origin;
        Vector3 over = top - origin;
        Vector3 lead = over - Vector3.Up * over.Y;

        float gravity = Mathf.Max(state.TotalGravity.Length(), 0.1f);
        float lift = Mathf.Max(over.Y + VaultClearance, 0f);

        Vector3 velocity = state.LinearVelocity;
        velocity.Y = Mathf.Max(velocity.Y, 0f) + Mathf.Sqrt(2f * gravity * lift);
        if (lead.LengthSquared() > 1e-6f) velocity += lead.Normalized() * VaultReach;

        state.LinearVelocity = velocity;

        Drop();
        return true;
    }

    public void Apply(PhysicsDirectBodyState3D state)
    {
        _load = 0f;

        if (!_attached) return;

        if (!IsInstanceValid(_node))
        {
            Release();
            return;
        }

        float dt = state.Step;
        Vector3 chest = state.Transform * ChestLocal();
        Vector3 anchor;

        if (_handle != null)
        {
            anchor = _handle.Track(
                new GrabHold
                {
                    Point = _lastAnchor + (chest - _lastChest),
                    Chest = chest,
                    Aim = Aim(chest),
                },
                dt);
        }
        else
        {
            anchor = _node.GlobalTransform * _local;
        }

        _lastChest = chest;
        _lastAnchor = anchor;

        if (_carried != null)
        {
            Carry(state, chest);
            return;
        }

        if (_handle != null && !_handle.Anchors) return;

        if (_climbing) _span = Mathf.MoveToward(_span, Grasp() * MinSpan, ClimbSpeed * dt);

        Vector3 span = anchor - chest;
        float distance = span.Length();
        if (distance < 1e-4f) return;

        Vector3 direction = span / distance;
        Vector3 relative = state.LinearVelocity - AnchorVelocity(_node, anchor);
        float closing = relative.Dot(direction);
        float error = distance - _span;

        if (error < 0f) return;

        state.LinearVelocity -= (relative - direction * closing) * Mathf.Min(SwingDamping * dt, 1f);

        float want = Mathf.Min(error * Correction / dt, MaxPullSpeed);
        if (closing >= want) return;

        RigidBody3D other = _node is RigidBody3D { Freeze: false } rigid ? rigid : null;
        float invSelf = state.InverseMass;
        float invOther = other != null && other.Mass > 0f ? 1f / other.Mass : 0f;
        float invSum = invSelf + invOther;
        if (invSum <= 0f) return;

        float impulse = (want - closing) / invSum;
        state.LinearVelocity += direction * (impulse * invSelf);
        other?.ApplyImpulse(direction * -impulse, anchor - CenterOfMass(other));

        _load = Mathf.Min(impulse / (dt * Mathf.Max(BreakForce, 1f)), 1f);

        if (Breaks && _load >= 1f) Release();
    }

    private void Carry(PhysicsDirectBodyState3D state, Vector3 chest)
    {
        if (!IsInstanceValid(_carried))
        {
            Release();
            return;
        }

        Vector3 target = chest + Aim(chest) * CarryDistance;
        Vector3 to = target - CenterOfMass(_carried);
        float distance = to.Length();

        if (distance > CarryDistance + CarrySlip)
        {
            Release();
            return;
        }

        float dt = state.Step;
        Vector3 correction = distance > 1e-4f
            ? to / distance * Mathf.Min(distance * Correction / dt, MaxPullSpeed)
            : Vector3.Zero;

        Vector3 change = state.LinearVelocity + correction - _carried.LinearVelocity;
        _carried.ApplyCentralImpulse(change.LimitLength(CarryAccel * dt) * _carried.Mass);
        _carried.AngularVelocity *= Mathf.Max(1f - SwingDamping * dt, 0f);

        _load = Mathf.Clamp(_carried.Mass / (OwnMass() * ClimbMassRatio), 0f, 1f);
    }

    private float Grasp()
    {
        float arms = Mathf.Max(ArmLeft?.Length ?? 0f, ArmRight?.Length ?? 0f);
        return arms > 1e-3f ? Mathf.Min(Reach, arms) : Reach;
    }

    private Vector3 ChestLocal() =>
        Shape != null
            ? (Shape.Shoulder(false) + Shape.Shoulder(true)) * 0.5f
            : new Vector3(0f, 0.9f, 0f);

    private Vector3 ChestWorld() =>
        _body == null ? ChestLocal() : _body.GlobalTransform * ChestLocal();

    private Vector3 Aim(Vector3 chest)
    {
        Vector3 forward = -_pivot.GlobalBasis.Z;
        Vector3 span = _pivot.GlobalPosition + forward * AimDistance - chest;

        return span.LengthSquared() > 1e-6f ? span.Normalized() : forward;
    }

    private bool Sweep(Vector3 from, Vector3 to, out Node3D node, out Vector3 point, out Vector3 normal) =>
        Sweep(from, to, false, out node, out point, out normal);

    private bool Sweep(Vector3 from, Vector3 to, bool handles, out Node3D node, out Vector3 point, out Vector3 normal)
    {
        node = null;
        point = Vector3.Zero;
        normal = Vector3.Up;

        _cast.GlobalTransform = new Transform3D(Basis.Identity, from);
        _cast.TargetPosition = to - from;
        _cast.ForceShapecastUpdate();

        if (!_cast.IsColliding()) return false;

        float nearest = float.MaxValue;
        float handled = float.MaxValue;
        int best = -1;
        int grip = -1;

        for (int i = 0; i < _cast.GetCollisionCount(); i++)
        {
            float distance = from.DistanceSquaredTo(_cast.GetCollisionPoint(i));

            if (distance < nearest)
            {
                nearest = distance;
                best = i;
            }

            if (!handles || distance >= handled) continue;
            if (FindHandle(_cast.GetCollider(i) as Node) == null) continue;

            handled = distance;
            grip = i;
        }

        if (grip >= 0) best = grip;

        if (best < 0 || _cast.GetCollider(best) is not Node3D hit) return false;

        node = hit;
        point = _cast.GetCollisionPoint(best);
        normal = _cast.GetCollisionNormal(best);
        return true;
    }

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
