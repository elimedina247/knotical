using Godot;

namespace Knotical.Player;

public sealed class HandOverHand
{
    public const string Group = "ClimbHolds";

    public float Reach = 0.62f;
    public float Stride = 0.42f;
    public float SwingTime = 0.32f;
    public float Bulge = 0.14f;
    public float Spread = 0.2f;
    public float Pull = 0.55f;
    public float PullSpeed = 1.1f;
    public float Snap = 0.35f;
    public float Bite = 0.3f;
    public float Damping = 3f;
    public float Correction = 0.3f;
    public float MaxPullSpeed = 6f;

    private sealed class Hand
    {
        public IClimbHolds Source;
        public Node3D Node;
        public Vector3 Local;
        public Vector3 Normal = Vector3.Up;
        public Vector3 Last;
        public bool Planted;

        private bool Rigged => Source is Node node && GodotObject.IsInstanceValid(node);

        private bool Rigid => Source == null && Node != null && GodotObject.IsInstanceValid(Node);

        public bool Live => Planted && (Rigged || Rigid);

        public Vector3 World
        {
            get
            {
                if (!Planted) return Last;

                if (Source != null)
                {
                    if (Rigged && Source.Hold(Last, out Vector3 tracked)) Last = tracked;
                    return Last;
                }

                if (!Rigid) return Last;

                Last = Node.GlobalTransform * Local;
                return Last;
            }
        }

        public void Plant(IClimbHolds source, Node3D node, Vector3 world, Vector3 normal)
        {
            Source = source;
            Node = node;
            Local = node != null && GodotObject.IsInstanceValid(node)
                ? node.GlobalTransform.AffineInverse() * world
                : Vector3.Zero;
            Normal = normal;
            Last = world;
            Planted = true;
        }

        public void Lift()
        {
            Source = null;
            Node = null;
            Planted = false;
        }
    }

    private readonly Hand[] _hands = { new(), new() };
    private ShapeCast3D _cast;
    private RigidBody3D _body;
    private IClimbHolds _reachSource;
    private Node3D _reachNode;
    private Vector3 _reachLocal;
    private Vector3 _reachWorld;
    private Vector3 _reachNormal = Vector3.Up;
    private Vector3 _leaveWorld;
    private int _reaching = -1;
    private float _swing;
    private float _span;
    private float _load;

    public bool Active => _hands[0].Live || _hands[1].Live;

    public bool Climbing { get; private set; }

    public int Beat { get; private set; }

    public float Load => _load;

    public bool OnHolds => Lead?.Source != null;

    public Vector3 Normal => Lead?.Normal ?? Vector3.Up;

    public Vector3 Anchor => Lead?.World ?? Vector3.Zero;

    private Hand Lead => _hands[0].Live ? _hands[0] : _hands[1].Live ? _hands[1] : null;

    public void Attach(ShapeCast3D cast, RigidBody3D body)
    {
        _cast = cast;
        _body = body;
    }

    public void Begin(Node3D node, Vector3 world, Vector3 normal, Vector3 across)
    {
        IClimbHolds source = Nearest(world, out Vector3 snapped);
        if (source != null) world = snapped;

        for (int i = 0; i < 2; i++)
        {
            Vector3 at = world + across * (i == 0 ? -Spread : Spread);
            if (source != null && source.Hold(at, out Vector3 held)) at = held;
            _hands[i].Plant(source, node, at, normal);
        }

        _reaching = -1;
        _swing = 0f;
        _span = Reach;
        Climbing = false;
    }

    public void End()
    {
        _hands[0].Lift();
        _hands[1].Lift();
        _reaching = -1;
        Climbing = false;
        _load = 0f;
    }

    public void Step(float dt, Vector3 step, Vector3 across)
    {
        if (!Active)
        {
            Climbing = false;
            return;
        }

        Climbing = step != Vector3.Zero;

        if (_reaching >= 0)
        {
            _swing += dt / Mathf.Max(SwingTime, 1e-3f);
            if (_swing >= 1f) Land();
            return;
        }

        if (!Climbing) return;

        Swap(step, across);
    }

    private void Swap(Vector3 step, Vector3 across)
    {
        int lead = Leading(step);
        int move = 1 - lead;

        Hand anchor = _hands[lead];
        if (!anchor.Live) return;

        Vector3 want = anchor.World
            + step * Stride
            + across * (move == 0 ? -Spread : Spread);

        if (!Seek(want, anchor.Normal)) return;

        _leaveWorld = _hands[move].Live ? _hands[move].World : anchor.World;
        _hands[move].Lift();
        _reaching = move;
        _swing = 0f;
    }

    private int Leading(Vector3 step)
    {
        bool a = _hands[0].Live;
        bool b = _hands[1].Live;

        if (a && !b) return 0;
        if (b && !a) return 1;

        return _hands[0].World.Dot(step) >= _hands[1].World.Dot(step) ? 0 : 1;
    }

    private bool Seek(Vector3 want, Vector3 normal)
    {
        IClimbHolds source = Nearest(want, out Vector3 snapped);

        if (source != null)
        {
            _reachSource = source;
            _reachNode = null;
            _reachWorld = snapped;
            _reachNormal = normal;
            return true;
        }

        if (_cast == null) return false;

        Vector3 lift = normal * Bite;
        if (!Sweep(want + lift, want - lift, out Node3D node, out Vector3 point, out Vector3 hit)) return false;

        _reachSource = null;
        _reachNode = node;
        _reachLocal = node.GlobalTransform.AffineInverse() * point;
        _reachWorld = point;
        _reachNormal = hit;
        return true;
    }

    private Vector3 Target()
    {
        if (_reachSource != null)
        {
            if (_reachSource.Hold(_reachWorld, out Vector3 tracked)) _reachWorld = tracked;
            return _reachWorld;
        }

        if (_reachNode == null || !GodotObject.IsInstanceValid(_reachNode)) return _reachWorld;

        _reachWorld = _reachNode.GlobalTransform * _reachLocal;
        return _reachWorld;
    }

    private void Land()
    {
        _hands[_reaching].Plant(_reachSource, _reachNode, Target(), _reachNormal);
        _reaching = -1;
        _swing = 0f;
        Beat++;
    }

    public bool Offer(Vector3 from, Vector3 to, out Vector3 hold, out Node3D holder)
    {
        hold = to;
        holder = null;

        float closest = float.MaxValue;

        for (int i = 0; i <= 4; i++)
        {
            Vector3 at = from.Lerp(to, i / 4f);
            if (Nearest(at, out Vector3 candidate) is not Node3D source) continue;

            float distance = at.DistanceSquaredTo(candidate);
            if (distance >= closest) continue;

            closest = distance;
            hold = candidate;
            holder = source;
        }

        return holder != null;
    }

    public void Pose(IHand left, IHand right)
    {
        Place(left, 0);
        Place(right, 1);
    }

    private void Place(IHand arm, int index)
    {
        if (arm == null) return;

        if (_reaching == index)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp(_swing, 0f, 1f));
            Vector3 swept = _leaveWorld.Lerp(Target(), t);
            arm.Grip(swept + _reachNormal * (Mathf.Sin(t * Mathf.Pi) * Bulge));
            return;
        }

        if (!_hands[index].Live)
        {
            arm.Release();
            return;
        }

        arm.Grip(_hands[index].World);
    }

    public void Apply(PhysicsDirectBodyState3D state, Vector3 chest)
    {
        _load = 0f;
        if (!Active) return;

        float dt = state.Step;
        float want = Climbing ? Reach * Pull : Reach;
        _span = Mathf.MoveToward(_span, want, PullSpeed * dt);

        for (int i = 0; i < 2; i++)
        {
            if (!_hands[i].Live) continue;
            Haul(state, chest, _hands[i], dt);
        }
    }

    private void Haul(PhysicsDirectBodyState3D state, Vector3 chest, Hand hand, float dt)
    {
        Vector3 hold = hand.World;
        Vector3 span = hold - chest;
        float distance = span.Length();
        if (distance < 1e-4f) return;

        float error = distance - _span;
        if (error < 0f) return;

        Vector3 direction = span / distance;
        RigidBody3D carrier = Carrier(hand);
        Vector3 relative = state.LinearVelocity - CarrierVelocity(carrier, hold);
        float closing = relative.Dot(direction);

        state.LinearVelocity -= (relative - direction * closing) * Mathf.Min(Damping * dt, 1f);

        float target = Mathf.Min(error * Correction / dt, MaxPullSpeed);
        if (closing >= target) return;

        float invSelf = state.InverseMass;
        float invOther = carrier != null && carrier.Mass > 0f ? 1f / carrier.Mass : 0f;
        float invSum = invSelf + invOther;
        if (invSum <= 0f) return;

        float impulse = (target - closing) / invSum;
        state.LinearVelocity += direction * (impulse * invSelf);
        carrier?.ApplyImpulse(direction * -impulse, hold - Center(carrier));

        _load = Mathf.Max(_load, Mathf.Min(error / Mathf.Max(Reach, 1e-3f), 1f));
    }

    private static RigidBody3D Carrier(Hand hand)
    {
        if (hand.Source != null) return hand.Source.Carrier;
        return hand.Node is RigidBody3D { Freeze: false } rigid ? rigid : null;
    }

    private IClimbHolds Nearest(Vector3 want, out Vector3 hold)
    {
        hold = want;
        if (_body == null) return null;

        IClimbHolds best = null;
        float closest = Snap * Snap;

        foreach (Node node in _body.GetTree().GetNodesInGroup(Group))
        {
            if (node is not IClimbHolds holds) continue;
            if (!holds.Hold(want, out Vector3 candidate)) continue;

            float distance = want.DistanceSquaredTo(candidate);
            if (distance >= closest) continue;

            closest = distance;
            hold = candidate;
            best = holds;
        }

        return best;
    }

    private bool Sweep(Vector3 from, Vector3 to, out Node3D node, out Vector3 point, out Vector3 normal)
    {
        node = null;
        point = Vector3.Zero;
        normal = Vector3.Up;

        _cast.GlobalTransform = new Transform3D(Basis.Identity, from);
        _cast.TargetPosition = to - from;
        _cast.ForceShapecastUpdate();

        if (!_cast.IsColliding()) return false;

        float nearest = float.MaxValue;
        int best = -1;

        for (int i = 0; i < _cast.GetCollisionCount(); i++)
        {
            float distance = from.DistanceSquaredTo(_cast.GetCollisionPoint(i));
            if (distance >= nearest) continue;

            nearest = distance;
            best = i;
        }

        if (best < 0 || _cast.GetCollider(best) is not Node3D hit) return false;

        node = hit;
        point = _cast.GetCollisionPoint(best);
        normal = _cast.GetCollisionNormal(best);
        return true;
    }

    private static Vector3 CarrierVelocity(RigidBody3D body, Vector3 at)
    {
        if (body == null) return Vector3.Zero;
        return body.LinearVelocity + body.AngularVelocity.Cross(at - Center(body));
    }

    private static Vector3 Center(RigidBody3D body) =>
        body.CenterOfMassMode == RigidBody3D.CenterOfMassModeEnum.Custom
            ? body.GlobalTransform * body.CenterOfMass
            : body.GlobalPosition;
}
