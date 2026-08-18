using Godot;
using Knotical.Player;

namespace Knotical.Rigging;

[GlobalClass]
public partial class RopeCarrier : Node3D
{
    [Export(PropertyHint.Range, "1,6,0.1")]
    public float TieReach { get; set; } = 3f;

    [Export(PropertyHint.Range, "0.5,2,0.05")]
    public float MinLength { get; set; } = 0.75f;

    [Export(PropertyHint.Range, "0.2,1.5,0.05")]
    public float EndGrabRadius { get; set; } = 0.5f;

    [Export] public Vector3 HandLocal { get; set; } = new(0.28f, 0.85f, -0.1f);

    [Export] public Vector3 CarryLocal { get; set; } = new(-0.32f, 0.78f, 0.05f);

    [Export] public Color MarkerColor { get; set; } = new("#D8B978");

    private RigidBody3D _body;
    private Node3D _pivot;
    private PlayerGrab _grab;
    private DangleArm _armLeft;
    private DangleArm _armRight;
    private MeshInstance3D _marker;
    private RopeCoil _coil;
    private Rope _rope;
    private PackedScene _coilScene;
    private bool _wasPressed;
    private bool _grabMapped;
    private bool _gripMapped;
    private float _wind;
    private int _lastTurn;
    private float _payout;
    private float _lastLength;
    private bool _hadRope;
    private readonly Godot.Collections.Array<Rid> _excludes = new();

    public bool IsBusy => _rope != null || _coil != null;

    public override void _Ready()
    {
        _grabMapped = InputMap.HasAction("grab");
        _gripMapped = InputMap.HasAction("grab_right");
        _coilScene = GD.Load<PackedScene>("res://rope_coil.tscn");

        _body = GetParentOrNull<RigidBody3D>();
        _pivot = _body?.GetNodeOrNull<Node3D>("CameraPivot");
        _grab = _body?.GetNodeOrNull<PlayerGrab>("Grab");
        _armLeft = _body?.GetNodeOrNull<DangleArm>("ArmLeft");
        _armRight = _body?.GetNodeOrNull<DangleArm>("ArmRight");

        if (_body != null) _excludes.Add(_body.GetRid());

        if (_pivot == null)
        {
            GD.PushWarning($"{Name}: rope carrier is inert, no camera pivot.");
            return;
        }

        _marker = new MeshInstance3D
        {
            Name = "TieMarker",
            Mesh = new SphereMesh { Radius = 0.04f, Height = 0.08f, RadialSegments = 12, Rings = 6 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = MarkerColor,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                NoDepthTest = true,
                RenderPriority = 15,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            TopLevel = true,
            Visible = false,
        };

        AddChild(_marker);
    }

    public override void _Process(double delta)
    {
        if (_coil == null || !IsInstanceValid(_coil) || !_coil.Carried || _body == null) return;

        _coil.GlobalPosition = _body.GlobalTransform * CarryLocal;
        _coil.GlobalBasis = _body.GlobalBasis;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_body == null || _pivot == null) return;

        float dt = (float)delta;

        if (_coil != null && !IsInstanceValid(_coil)) _coil = null;
        if (_rope != null && !IsInstanceValid(_rope)) _rope = null;

        bool pressed = _grabMapped ? Input.IsActionPressed("grab") : Input.IsMouseButtonPressed(MouseButton.Left);
        bool click = pressed && !_wasPressed;
        _wasPressed = pressed;

        bool grip = _gripMapped ? Input.IsActionPressed("grab_right") : Input.IsMouseButtonPressed(MouseButton.Right);

        bool hit = AimRay(out Node3D target, out Vector3 point, out bool inReach);

        if (_rope != null) HoldTick(click, grip, hit, target, point, inReach);
        else if (_coil != null) CarryTick(click, hit, target, point, inReach);
        else if (click) EmptyClick(target, point, inReach);

        if (_grab != null) _grab.Locked = IsBusy;

        TrackPayout(dt);
        Mark(hit, point, inReach);
        Pose(grip, dt);
    }

    private void TrackPayout(float dt)
    {
        bool has = _rope != null;

        if (has && _hadRope)
        {
            float length = _rope.WorkingLength;
            float rate = Mathf.Clamp((length - _lastLength) / Mathf.Max(dt, 1e-4f), 0f, 4f);
            _payout = Mathf.Lerp(_payout, rate, Mathf.Min(dt * 8f, 1f));
            _lastLength = length;
        }
        else
        {
            _payout = 0f;
            _lastLength = has ? _rope.WorkingLength : 0f;
        }

        _hadRope = has;
    }

    private void HoldTick(bool click, bool grip, bool hit, Node3D target, Vector3 point, bool inReach)
    {
        Vector3 hand = Hand();
        _rope.Hold(hand, _body);

        bool anchored = _rope.TryStart(out Vector3 anchor);

        if (!grip && anchored && _coil != null)
            _rope.WorkingLength = Mathf.Clamp(
                hand.DistanceTo(anchor) * 1.05f + 0.25f,
                MinLength,
                _coil.Capacity);

        if (!click) return;

        if (NearAim(_rope.EndPoint(false)))
        {
            Recoil();
            return;
        }

        if (hit && inReach && target != null)
        {
            float limit = _coil != null ? _coil.Capacity : _rope.WorkingLength;

            if (anchored)
            {
                float span = anchor.DistanceTo(point);
                if (span > limit) return;

                _rope.WorkingLength = Mathf.Min(
                    Mathf.Max(_rope.WorkingLength, span * 1.02f + 0.15f),
                    limit);
            }

            _rope.BindEnd(target, point);
            _rope = null;

            if (_coil == null) return;
            _coil.QueueFree();
            _coil = null;
        }
        else if (!hit)
        {
            if (_coil != null)
            {
                _coil.Drop(Hand(), _body.LinearVelocity);
                _coil.Tail = _rope;
                _rope.BindEnd(_coil, _coil.GlobalPosition + Vector3.Up * 0.12f);
            }
            else
            {
                _rope.ReleaseHold();
            }

            _rope = null;
            _coil = null;
        }
    }

    private void Recoil()
    {
        if (_coil == null && _coilScene != null)
        {
            var coil = _coilScene.Instantiate<RopeCoil>();
            coil.Capacity = Mathf.Max(_rope.WorkingLength, MinLength);
            GetTree().CurrentScene.AddChild(coil);
            coil.Carry();
            _coil = coil;
        }

        _rope.QueueFree();
        _rope = null;
    }

    private void CarryTick(bool click, bool hit, Node3D target, Vector3 point, bool inReach)
    {
        if (!click) return;

        if (hit && inReach && target != null)
        {
            Vector3 hand = Hand();
            if (hand.DistanceTo(point) > _coil.Capacity) return;

            var rope = new Rope();
            GetTree().CurrentScene.AddChild(rope);
            rope.BindStart(target, point);

            rope.Hold(hand, _body);
            rope.WorkingLength = Mathf.Clamp(
                hand.DistanceTo(point) * 1.05f + 0.25f,
                MinLength,
                _coil.Capacity);

            _rope = rope;
        }
        else if (!hit)
        {
            _coil.Drop(Hand(), _body.LinearVelocity);
            _coil = null;
        }
    }

    private void EmptyClick(Node3D target, Vector3 point, bool inReach)
    {
        RopeCoil coil = FindCoil(target);

        if (coil != null && inReach && !coil.Carried)
        {
            _coil = coil;
            coil.Carry();

            Rope tail = coil.Tail;
            coil.Tail = null;

            if (tail != null && IsInstanceValid(tail))
            {
                bool endB = tail.EndBody(true) == coil;
                if (endB || tail.EndBody(false) == coil)
                {
                    tail.TakeEnd(endB, Hand(), _body);
                    _rope = tail;
                }
            }

            return;
        }

        TakeNearbyEnd();
    }

    private void TakeNearbyEnd()
    {
        Vector3 from = _pivot.GlobalPosition;
        Vector3 direction = -_pivot.GlobalBasis.Z;
        Vector3 chest = Hand();

        Rope best = null;
        bool bestEnd = false;
        float nearest = EndGrabRadius;

        foreach (Node node in GetTree().GetNodesInGroup("Ropes"))
        {
            if (node is not Rope rope) continue;

            for (int side = 0; side < 2; side++)
            {
                bool endB = side == 1;
                if (endB && rope.Held) continue;

                Vector3 end = rope.EndPoint(endB);
                if (chest.DistanceTo(end) > TieReach + 0.5f) continue;

                float along = Mathf.Clamp((end - from).Dot(direction), 0f, TieReach + 1.5f);
                float offAim = (from + direction * along).DistanceTo(end);
                if (offAim >= nearest) continue;

                nearest = offAim;
                best = rope;
                bestEnd = endB;
            }
        }

        if (best == null) return;

        if (best.EndBody(bestEnd) is RopeCoil owner && owner.Tail == best) owner.Tail = null;

        best.TakeEnd(bestEnd, Hand(), _body);
        _rope = best;
    }

    private bool NearAim(Vector3 point)
    {
        if (Hand().DistanceTo(point) > TieReach + 0.5f) return false;

        Vector3 from = _pivot.GlobalPosition;
        Vector3 direction = -_pivot.GlobalBasis.Z;
        float along = Mathf.Clamp((point - from).Dot(direction), 0f, TieReach + 1.5f);

        return (from + direction * along).DistanceTo(point) < EndGrabRadius;
    }

    private void Pose(bool grip, float dt)
    {
        if (_rope != null)
        {
            if (_payout > 0.3f)
            {
                _wind += Mathf.Tau * _payout * dt / 0.9f;
                int turn = (int)(_wind / Mathf.Tau);
                float slide = _wind / Mathf.Tau - turn;

                if (turn != _lastTurn)
                {
                    _lastTurn = turn;
                    _armRight?.Release();
                }

                Vector3 near = _body.GlobalTransform * new Vector3(-0.1f, 0.8f, -0.08f);
                _armRight?.Grip(near.Lerp(_rope.PointFromEnd(0.55f), slide));
            }
            else
            {
                _armRight?.Grip(_rope.PointFromEnd(0.3f));
            }

            if (grip) _armLeft?.Grip(_rope.PointFromEnd(0.7f));
            else if (_coil != null) _armLeft?.Grip(_coil.GlobalPosition + Vector3.Up * 0.05f);
            else _armLeft?.Release();
            return;
        }

        if (_coil != null)
        {
            _armLeft?.Grip(_coil.GlobalPosition + Vector3.Up * 0.05f);
            _armRight?.Release();
        }
    }

    private void Mark(bool hit, Vector3 point, bool inReach)
    {
        if (_marker == null) return;

        bool show = IsBusy && hit && inReach;
        _marker.Visible = show;
        if (show) _marker.GlobalPosition = point;
    }

    private Vector3 Hand() => _body.GlobalTransform * HandLocal;

    private bool AimRay(out Node3D target, out Vector3 point, out bool inReach)
    {
        target = null;
        point = Vector3.Zero;
        inReach = false;

        Vector3 from = _pivot.GlobalPosition;
        Vector3 to = from - _pivot.GlobalBasis.Z * 60f;

        var query = PhysicsRayQueryParameters3D.Create(from, to, uint.MaxValue, _excludes);
        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return false;

        target = hit["collider"].AsGodotObject() as Node3D;
        point = (Vector3)hit["position"];
        inReach = Hand().DistanceTo(point) <= TieReach;
        return true;
    }

    private static RopeCoil FindCoil(Node node)
    {
        for (Node step = node; step != null; step = step.GetParent())
            if (step is RopeCoil coil) return coil;

        return null;
    }
}
