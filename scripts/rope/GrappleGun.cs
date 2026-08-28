using Godot;
using Knotical.Player;

namespace Knotical.Rigging;

[GlobalClass]
public partial class GrappleGun : Node3D
{
    private enum Mode
    {
        Idle,
        Casting,
        Tethered,
        Retracting,
    }

    [Export(PropertyHint.Range, "10,80,1")]
    public float HookSpeed { get; set; } = 32f;

    [Export(PropertyHint.Range, "5,60,0.5")]
    public float Capacity { get; set; } = 30f;

    [Export(PropertyHint.Range, "0.5,2,0.05")]
    public float MinLength { get; set; } = 1f;

    [Export(PropertyHint.Range, "0.5,10,0.1")]
    public float PayoutRate { get; set; } = 3f;

    [Export(PropertyHint.Range, "0.5,10,0.1")]
    public float WinchRate { get; set; } = 2f;

    [Export(PropertyHint.Range, "1,10,0.5")]
    public float FlightTimeout { get; set; } = 4f;

    [Export(PropertyHint.Range, "5,80,1")]
    public float RetractSpeed { get; set; } = 40f;

    [Export(PropertyHint.Range, "0,150,1")]
    public float LineTug { get; set; } = 40f;

    [Export(PropertyHint.Range, "0.1,2,0.05")]
    public float CastSlack { get; set; } = 0.35f;

    [Export] public Vector3 GunLocal { get; set; } = new(0.24f, -0.18f, -0.42f);

    private RigidBody3D _body;
    private Node3D _pivot;
    private PlayerGrab _grab;
    private RopeCarrier _carrier;
    private DangleArm _armLeft;
    private DangleArm _armRight;
    private Node3D _gunRoot;
    private GrappleReticle _reticle;
    private Rope _rope;
    private GrappleHook _hook;
    private Mode _mode = Mode.Idle;
    private bool _drawn;
    private bool _wasFire;
    private bool _wasDrop;
    private bool _wasToggle;
    private float _flight;
    private float _payout;
    private bool _fireMapped;
    private bool _dropMapped;
    private bool _toggleMapped;
    private bool _payoutMapped;
    private bool _winchMapped;
    private readonly Godot.Collections.Array<Rid> _excludes = new();

    public override void _Ready()
    {
        _fireMapped = InputMap.HasAction("grab");
        _dropMapped = InputMap.HasAction("grab_right");
        _toggleMapped = InputMap.HasAction("tool_toggle");
        _payoutMapped = InputMap.HasAction("rope_payout");
        _winchMapped = InputMap.HasAction("rope_winch");

        _body = GetParentOrNull<RigidBody3D>();
        _pivot = _body?.GetNodeOrNull<Node3D>("CameraPivot");
        _grab = _body?.GetNodeOrNull<PlayerGrab>("Grab");
        _carrier = _body?.GetNodeOrNull<RopeCarrier>("RopeCarrier");
        _armLeft = _body?.GetNodeOrNull<DangleArm>("ArmLeft");
        _armRight = _body?.GetNodeOrNull<DangleArm>("ArmRight");

        if (_body != null) _excludes.Add(_body.GetRid());

        if (_pivot == null)
        {
            GD.PushWarning($"{Name}: grapple gun is inert, no camera pivot.");
            return;
        }

        BuildGun();
        BuildReticle();
    }

    private void BuildGun()
    {
        _gunRoot = new Node3D
        {
            Name = "GunModel",
            Position = GunLocal,
            TopLevel = true,
            Visible = false,
        };
        _pivot.AddChild(_gunRoot);

        var metal = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.28f, 0.29f, 0.32f),
            Roughness = 0.55f,
            Metallic = 0.4f,
        };

        _gunRoot.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.06f, 0.07f, 0.3f) },
            MaterialOverride = metal,
            Position = new Vector3(0f, 0f, -0.05f),
        });

        _gunRoot.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.045f, 0.13f, 0.06f) },
            MaterialOverride = metal,
            Position = new Vector3(0f, -0.09f, 0.06f),
        });
    }

    private void BuildReticle()
    {
        var layer = new CanvasLayer { Name = "ReticleLayer" };
        AddChild(layer);

        _reticle = new GrappleReticle
        {
            Name = "Reticle",
            Visible = false,
        };
        layer.AddChild(_reticle);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_body == null || _pivot == null) return;

        float dt = (float)delta;

        if (_rope != null && !IsInstanceValid(_rope)) _rope = null;
        if (_hook != null && !IsInstanceValid(_hook)) _hook = null;

        bool toggle = Edge(Pressed(_toggleMapped, "tool_toggle", Key.G), ref _wasToggle);
        bool fire = Edge(Pressed(_fireMapped, "grab", MouseButton.Left), ref _wasFire);
        bool drop = Edge(Pressed(_dropMapped, "grab_right", MouseButton.Right), ref _wasDrop);
        bool payout = Pressed(_payoutMapped, "rope_payout", Key.E);
        bool winch = Pressed(_winchMapped, "rope_winch", Key.R);

        if (toggle)
        {
            if (_drawn) Holster();
            else if (_carrier == null || !_carrier.IsBusy) Draw();
        }

        if (!_drawn) return;

        if (_grab != null) _grab.Locked = true;

        PoseArm();

        switch (_mode)
        {
            case Mode.Idle:
                if (fire) Fire();
                break;
            case Mode.Casting:
                CastingTick(dt);
                break;
            case Mode.Tethered:
                TetheredTick(dt, fire, drop, payout, winch);
                break;
            case Mode.Retracting:
                RetractTick(dt);
                break;
        }
    }

    private void Draw()
    {
        _drawn = true;
        _gunRoot.Visible = true;
        _reticle.Visible = true;

        if (_carrier != null) _carrier.Enabled = false;
    }

    private void Holster()
    {
        _armRight?.Release();
        _armLeft?.Release();
        Clear(dropRope: false);
        _drawn = false;
        _gunRoot.Visible = false;
        _reticle.Visible = false;

        if (_grab != null) _grab.Locked = false;
        if (_carrier != null) _carrier.Enabled = true;
    }

    private void Fire()
    {
        Vector3 muzzle = Muzzle();
        Vector3 direction = AimDirection(muzzle);

        _hook = new GrappleHook { Name = "GrappleHook" };
        GetTree().CurrentScene.AddChild(_hook);
        _hook.Launch(muzzle, direction * HookSpeed, _body);

        _rope = new Rope { Name = "GrappleLine", Breaks = false, Beads = Rope.BeadsFor(Capacity), MaxWraps = 24 };
        GetTree().CurrentScene.AddChild(_rope);
        _rope.WorkingLength = MinLength;
        _rope.BindStart(_hook, muzzle);
        _rope.Hold(muzzle, _body);

        _payout = MinLength;
        _flight = 0f;
        _mode = Mode.Casting;
    }

    private void CastingTick(float dt)
    {
        if (_hook == null || _rope == null)
        {
            Clear(dropRope: false);
            return;
        }

        if (_hook.Landed)
        {
            _rope.BindStart(_hook.Target, _hook.Point);
            _rope.Hold(Muzzle(), _body);
            PlantHook();
            _mode = Mode.Tethered;
            return;
        }

        _flight += dt;
        Vector3 muzzle = Muzzle();
        Vector3 at = _hook.GlobalPosition;

        float feed = (_hook.LinearVelocity.Length() + 2f) * dt;
        float demand = muzzle.DistanceTo(at) + CastSlack;
        _payout = Mathf.Min(Mathf.Max(_payout, Mathf.Min(_payout + feed, demand)), Capacity);

        if (_flight > FlightTimeout || muzzle.DistanceTo(at) > Capacity * 1.2f)
        {
            Clear(dropRope: false);
            return;
        }

        _rope.Hold(muzzle, _body);
        _rope.WorkingLength = Mathf.Max(_payout, MinLength);
        TugHook(at);
    }

    private void TugHook(Vector3 at)
    {
        int beads = _rope.BeadCount;
        if (beads < 4) return;

        float rest = _rope.WorkingLength / (beads - 1);
        if (rest * 2f < 0.1f) return;

        Vector3 previous = _rope.BeadAt(0);
        float along = 0f;
        for (int i = 1; i <= 2; i++)
        {
            Vector3 bead = _rope.BeadAt(i);
            along += previous.DistanceTo(bead);
            previous = bead;
        }

        float strain = along / (rest * 2f) - 1f;
        if (strain <= 0f) return;

        Vector3 direction = previous - at;
        if (direction.LengthSquared() < 1e-8f) return;

        float bite = Mathf.Min(strain * 20f, 1f);
        _hook.ApplyCentralForce(direction.Normalized() * (bite * LineTug * _hook.Mass));
    }

    private void TetheredTick(float dt, bool fire, bool drop, bool payout, bool winch)
    {
        if (_rope == null || !_rope.BoundStart)
        {
            Clear(dropRope: false);
            return;
        }

        if (fire || drop)
        {
            StartRetract();
            return;
        }

        _rope.Hold(Muzzle(), _body);

        if (payout && !winch)
            _rope.WorkingLength = Mathf.Min(_rope.WorkingLength + PayoutRate * dt, Capacity);
        else if (winch && !payout)
            _rope.WorkingLength = Mathf.Max(_rope.WorkingLength - WinchRate * dt, MinLength);
    }

    private void StartRetract()
    {
        if (_rope == null)
        {
            Clear(dropRope: false);
            return;
        }

        Vector3 point = _rope.TryStart(out Vector3 anchor) ? anchor : Muzzle();

        FreeHook();

        _hook = new GrappleHook { Name = "RetractHook" };
        GetTree().CurrentScene.AddChild(_hook);
        _hook.Launch(point, Vector3.Zero, _body);
        _hook.Freeze = true;

        _rope.BindStart(_hook, point);
        _mode = Mode.Retracting;
    }

    private void RetractTick(float dt)
    {
        if (_rope == null)
        {
            Clear(dropRope: false);
            return;
        }

        Vector3 muzzle = Muzzle();

        if (_hook == null || !IsInstanceValid(_hook))
        {
            FinishRetract();
            return;
        }

        Vector3 toMuzzle = muzzle - _hook.GlobalPosition;
        float travel = RetractSpeed * (float)dt;

        if (toMuzzle.Length() <= travel + 1.5f)
        {
            FinishRetract();
            return;
        }

        _hook.GlobalPosition += toMuzzle.Normalized() * travel;

        _rope.Hold(muzzle, _body);
        _rope.BindStart(_hook, _hook.GlobalPosition);
        _rope.WorkingLength = Mathf.Max(muzzle.DistanceTo(_hook.GlobalPosition), MinLength);
    }

    private void FinishRetract()
    {
        FreeHook();

        if (_rope != null && IsInstanceValid(_rope)) _rope.QueueFree();
        _rope = null;
        _mode = Mode.Idle;
    }

    private void Clear(bool dropRope)
    {
        FreeHook();

        if (_rope != null && IsInstanceValid(_rope))
        {
            if (dropRope && _rope.BoundStart) _rope.ReleaseHold();
            else _rope.QueueFree();
        }

        _rope = null;
        _mode = Mode.Idle;
    }

    private void PlantHook()
    {
        _hook?.Plant(_rope);
        FreeHook();
    }

    private void FreeHook()
    {
        if (_hook != null && IsInstanceValid(_hook)) _hook.QueueFree();
        _hook = null;
    }

    private void PoseArm()
    {
        Vector3 aim = -_pivot.GlobalBasis.Z;

        if (_armRight != null)
        {
            _armRight.Grip(_armRight.Root + aim * (_armRight.Length * 0.95f));
            _gunRoot.GlobalTransform = new Transform3D(_pivot.GlobalBasis, _armRight.Tip + aim * 0.08f);
        }
        else
        {
            _gunRoot.GlobalTransform = new Transform3D(_pivot.GlobalBasis, _pivot.GlobalTransform * GunLocal);
        }
    }

    private Vector3 Muzzle() => _gunRoot.GlobalTransform * new Vector3(0f, 0f, -0.22f);

    private Vector3 AimDirection(Vector3 muzzle)
    {
        Vector3 from = _pivot.GlobalPosition;
        Vector3 forward = -_pivot.GlobalBasis.Z;
        Vector3 target = from + forward * 60f;

        var query = PhysicsRayQueryParameters3D.Create(from, target, uint.MaxValue, _excludes);
        Godot.Collections.Dictionary hit = GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count != 0) target = (Vector3)hit["position"];

        Vector3 direction = target - muzzle;
        return direction.LengthSquared() > 1e-6f ? direction.Normalized() : forward;
    }

    private static bool Pressed(bool mapped, string action, Key fallback) =>
        mapped ? Input.IsActionPressed(action) : Input.IsPhysicalKeyPressed(fallback);

    private static bool Pressed(bool mapped, string action, MouseButton fallback) =>
        mapped ? Input.IsActionPressed(action) : Input.IsMouseButtonPressed(fallback);

    private static bool Edge(bool pressed, ref bool was)
    {
        bool click = pressed && !was;
        was = pressed;
        return click;
    }
}
