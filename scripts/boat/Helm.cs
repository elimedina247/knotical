using Godot;
using Knotical.Player;

namespace Knotical.Boat;

[GlobalClass]
public partial class Helm : Node3D, IGrabbable
{
    [Export] public Wheel Wheel { get; set; }

    [Export] public Rudder Rudder { get; set; }

    [Export] public HelmPointer Pointer { get; set; }

    [Export(PropertyHint.Range, "0.25,6,0.05")]
    public float TurnsToLock { get; set; } = 0.75f;

    [Export(PropertyHint.Range, "0.5,400,0.5")]
    public float Inertia { get; set; } = 3f;

    [Export(PropertyHint.Range, "1,600,0.5")]
    public float GripStiffness { get; set; } = 260f;

    [Export(PropertyHint.Range, "0,300,0.5")]
    public float GripDamping { get; set; } = 55f;

    [Export(PropertyHint.Range, "0,4000,5")]
    public float GripTorque { get; set; } = 1200f;

    [Export(PropertyHint.Range, "0,200,0.5")]
    public float Friction { get; set; } = 6f;

    [Export(PropertyHint.Range, "0,500,0.5")]
    public float Stiction { get; set; } = 14f;

    [Export(PropertyHint.Range, "0,1,0.0005")]
    public float Feedback { get; set; } = 0.0006f;

    [Export(PropertyHint.Range, "0,4000,5")]
    public float MaxFeedbackTorque { get; set; } = 45f;

    [Export] public bool HoldsPosition { get; set; } = true;

    [Export(PropertyHint.Range, "0,0.4,0.01")]
    public float CentreDetent { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0,4,0.05")]
    public float CentreSnap { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float StopBounce { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0.5,60,0.1")]
    public float MaxSpin { get; set; } = 12f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float DeadZone { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.2,6,0.05")]
    public float AimRange { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float EdgeOn { get; set; } = 0.15f;

    [Export] public bool Trace { get; set; }

    private float _forced;
    private float _turn;
    private float _spin;
    private float _demand;
    private float _grip;
    private float _tick;
    private bool _held;

    public bool Anchors => false;

    public float Limit => Mathf.Max(TurnsToLock, 0.01f) * Mathf.Tau;

    public float Angle => _turn;

    public float Spin => _spin;

    public float Steering => Mathf.Clamp(_turn / Limit, -1f, 1f);

    public bool IsManned => _held;

    public override void _Ready()
    {
        Wheel ??= GetNodeOrNull<Wheel>("Mount/Wheel");
        Pointer ??= GetNodeOrNull<HelmPointer>("Pointer");
        Rudder ??= GetParent()?.GetNodeOrNull<Rudder>("Rudder") ?? Hunt(GetParent());

        if (Wheel == null) GD.PushWarning($"{Name}: helm has no wheel to turn.");
        if (Rudder == null) GD.PushWarning($"{Name}: helm has no rudder to steer.");
        else GD.Print($"{Name}: steering {Rudder.GetPath()}");

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (!arg.StartsWith("--helm=")) continue;
            if (float.TryParse(arg["--helm=".Length..], out float forced)) _forced = forced;
        }

        Drive();
    }

    public override void _PhysicsProcess(double delta)
    {
        Step((float)delta);
    }

    public Vector3 Attach(Vector3 globalPoint)
    {
        if (Wheel == null) return globalPoint;

        _held = true;
        _demand = 0f;
        _grip = AngleOf(globalPoint);

        return RimPoint(_grip);
    }

    public Vector3 Track(GrabHold hold, float dt)
    {
        if (Wheel == null) return hold.Point;

        if (Demand(hold, out float want))
            _demand = Mathf.PosMod(_grip - want + Mathf.Pi, Mathf.Tau) - Mathf.Pi;

        return RimPoint(_grip);
    }

    public void Detach()
    {
        _held = false;
        _demand = 0f;
    }

    private static Rudder Hunt(Node node)
    {
        if (node == null) return null;
        if (node is Rudder blade) return blade;

        foreach (Node child in node.GetChildren())
        {
            Rudder found = Hunt(child);
            if (found != null) return found;
        }

        return null;
    }

    private void Step(float dt)
    {
        if (dt <= 0f) return;

        if (_forced != 0f)
        {
            _turn = Mathf.Clamp(_forced, -1f, 1f) * Limit;
            Drive();
            return;
        }

        float inertia = Mathf.Max(Inertia, 0.01f);
        float weather = HoldsPosition ? 0f : Weather();
        float hand = _held
            ? Mathf.Clamp(GripStiffness * _demand - GripDamping * _spin, -GripTorque, GripTorque)
            : 0f;

        if (Trace) Report(dt, weather, hand);

        _spin = Mathf.Clamp(_spin + (weather + hand) / inertia * dt, -MaxSpin, MaxSpin);
        _spin = Mathf.MoveToward(_spin, 0f, (Friction * Mathf.Abs(_spin) + Stiction) / inertia * dt);
        if (HoldsPosition && !_held) _spin = 0f;
        _turn += _spin * dt;

        if (Mathf.Abs(_turn) > Limit)
        {
            _turn = Mathf.Clamp(_turn, -Limit, Limit);
            _spin = -_spin * StopBounce;
        }

        if (!_held && Mathf.Abs(_turn) < CentreDetent * Limit)
        {
            _turn = Mathf.MoveToward(_turn, 0f, CentreSnap * Limit * dt);
            if (_turn == 0f) _spin = 0f;
        }

        _demand = 0f;
        Drive();
    }

    private void Report(float dt, float weather, float hand)
    {
        _tick += dt;
        if (_tick < 0.25f) return;
        _tick = 0f;

        GD.Print(
            $"helm wheel={(Wheel != null ? "ok" : "null")} held={_held} demand={_demand:F3} " +
            $"hand={hand:F1} weather={weather:F1} stock={(Rudder != null ? Rudder.StockTorque : 0f):F0} " +
            $"flow={(Rudder != null ? Rudder.Flow : 0f):F1} turn={_turn:F2}/{Limit:F2} spin={_spin:F2}");
    }

    private float Weather()
    {
        if (Rudder == null || Feedback <= 0f) return 0f;

        float gear = Mathf.DegToRad(Rudder.MaxAngleDegrees) / Limit;

        return Mathf.Clamp(Rudder.StockTorque * gear * Feedback, -MaxFeedbackTorque, MaxFeedbackTorque);
    }

    private bool Demand(GrabHold hold, out float angle)
    {
        angle = 0f;

        Basis basis = Wheel.GlobalBasis;
        Vector3 axis = basis.Z.Normalized();
        Vector3 centre = Wheel.GlobalPosition;

        float facing = hold.Aim.Dot(axis);
        Vector3 point = hold.Point;

        if (Mathf.Abs(facing) > EdgeOn)
        {
            float travel = (centre - hold.Chest).Dot(axis) / facing;
            point = hold.Chest + hold.Aim * Mathf.Clamp(travel, 0f, AimRange);
        }

        Vector3 flat = point - centre;
        flat -= axis * flat.Dot(axis);

        if (flat.Length() < Wheel.Radius * DeadZone) return false;

        angle = Mathf.Atan2(flat.Dot(basis.Y), flat.Dot(basis.X));
        return true;
    }

    private void Drive()
    {
        if (Wheel != null) Wheel.Rotation = new Vector3(0f, 0f, -_turn);
        if (Rudder != null) Rudder.Steering = Steering;

        Pointer?.Show(Rudder != null ? Rudder.Steering : Steering);
    }

    private float AngleOf(Vector3 globalPoint)
    {
        Basis basis = Wheel.GlobalBasis;
        Vector3 flat = globalPoint - Wheel.GlobalPosition;
        flat -= basis.Z.Normalized() * flat.Dot(basis.Z.Normalized());

        if (flat.LengthSquared() < 1e-8f) return 0f;

        return Mathf.Atan2(flat.Dot(basis.Y), flat.Dot(basis.X));
    }

    private Vector3 RimPoint(float angle)
    {
        Basis basis = Wheel.GlobalBasis;
        Vector3 rim = basis.X * Mathf.Cos(angle) + basis.Y * Mathf.Sin(angle);

        return Wheel.GlobalPosition + rim * Wheel.Radius;
    }
}
