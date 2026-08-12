using Godot;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Player;

[GlobalClass]
public partial class PlayerBody : RigidBody3D
{
    [Export(PropertyHint.Range, "0.5,8,0.1")]
    public float WalkSpeed { get; set; } = 3.2f;

    [Export(PropertyHint.Range, "1,60,0.5")]
    public float Acceleration { get; set; } = 18f;

    [Export(PropertyHint.Range, "100,4000,10")]
    public float MaxFootingForce { get; set; } = 1100f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float AirControl { get; set; } = 0.1f;

    [Export(PropertyHint.Range, "1,60,0.5")]
    public float UprightStiffness { get; set; } = 14f;

    [Export(PropertyHint.Range, "0.5,30,0.1")]
    public float UprightDamping { get; set; } = 4.5f;

    [Export(PropertyHint.Range, "1,60,0.5")]
    public float YawStiffness { get; set; } = 20f;

    [Export(PropertyHint.Range, "0.1,8,0.05")]
    public float YawDamping { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float YawFadeBelow { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "200,12000,50")]
    public float MaxTorque { get; set; } = 2500f;

    [Export(PropertyHint.Range, "0,0.6,0.01")]
    public float FootingGrace { get; set; } = 0.15f;

    [Export(PropertyHint.Range, "0,0.6,0.01")]
    public float KnockdownDwell { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "20,90,1")]
    public float KnockdownAngle { get; set; } = 55f;

    [Export(PropertyHint.Range, "5,80,1")]
    public float RecoverAngle { get; set; } = 30f;

    [Export(PropertyHint.Range, "0,6,0.1")]
    public float DownedTime { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0.1,4,0.05")]
    public float RecoverTime { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float FootingHeight { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.0005,0.01,0.0001")]
    public float MouseSensitivity { get; set; } = 0.0022f;

    [Export(PropertyHint.Range, "40,89,1")]
    public float PitchLimit { get; set; } = 85f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float BodyTiltFollow { get; set; } = 0f;

    [Export(PropertyHint.Range, "0.5,4,0.01")]
    public float BodyHeight { get; set; } = 1.19f;

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float SwimEntry { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "1,2,0.01")]
    public float Buoyancy { get; set; } = 1.15f;

    [Export(PropertyHint.Range, "0,20,0.1")]
    public float WaterDrag { get; set; } = 3.5f;

    [Export(PropertyHint.Range, "0.2,6,0.05")]
    public float SwimSpeed { get; set; } = 1.6f;

    [Export(PropertyHint.Range, "1,40,0.5")]
    public float SwimAcceleration { get; set; } = 8f;

    [Export] public Node3D CameraPivot { get; set; }

    private Node3D _pivot;
    private Vector3 _deckVelocity;
    private Vector3 _groundNormal = Vector3.Up;
    private Vector3 _wish;
    private bool _grounded;
    private bool _downed;
    private float _downedFor;
    private float _airborneFor;
    private float _tiltedFor;
    private float _submersion;
    private float _waterRise;
    private bool _swimming;
    private float _authority = 1f;
    private float _yaw;
    private float _pitch;

    public bool IsDowned => _downed;
    public bool IsGrounded => _grounded;
    public bool IsSwimming => _swimming;
    public float Submersion => _submersion;
    public Vector3 DeckVelocity => _deckVelocity;
    public float Authority => _authority;

    public Vector3 PlanarVelocity
    {
        get
        {
            Vector3 relative = LinearVelocity - _deckVelocity;
            return relative - Vector3.Up * relative.Dot(Vector3.Up);
        }
    }

    public override void _Ready()
    {
        _pivot = CameraPivot ?? GetNodeOrNull<Node3D>("CameraPivot");
        _yaw = Rotation.Y;
        CanSleep = false;
        ContactMonitor = true;
        if (MaxContactsReported < 6) MaxContactsReported = 6;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(
                _pitch - motion.Relative.Y * MouseSensitivity,
                -Mathf.DegToRad(PitchLimit),
                Mathf.DegToRad(PitchLimit));
        }

        if (@event.IsActionPressed("ui_cancel")) Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _Process(double delta)
    {
        if (_pivot != null)
        {
            var level = new Quaternion(Basis.FromEuler(new Vector3(_pitch, _yaw, 0f)));
            var ride = new Quaternion(
                GlobalBasis.Orthonormalized() * Basis.FromEuler(new Vector3(_pitch, 0f, 0f)));
            _pivot.GlobalBasis = new Basis(level.Slerp(ride, _downed ? 1f : BodyTiltFollow));
        }

        Vector2 move = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 forward = new(-Mathf.Sin(_yaw), 0f, -Mathf.Cos(_yaw));
        Vector3 right = new(Mathf.Cos(_yaw), 0f, -Mathf.Sin(_yaw));
        _wish = (right * move.X - forward * move.Y).LimitLength(1f);
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float dt = state.Step;

        SampleWater(state);
        SampleFooting(state, dt);
        UpdateBalanceState(dt, state);

        if (_swimming)
        {
            ApplyBuoyancy(state);
            ApplySwim(state);
            ApplyUpright(state, 1f - _submersion * 0.7f);
            ApplyYaw(state);
            return;
        }

        if (_authority <= 0f) return;

        ApplyUpright(state, 1f);
        ApplyYaw(state);
        ApplyWalk(state);
    }

    private void SampleWater(PhysicsDirectBodyState3D state)
    {
        OceanField ocean = OceanField.Instance;
        if (ocean == null)
        {
            _submersion = 0f;
            _waterRise = 0f;
            _swimming = false;
            return;
        }

        Vector3 at = state.Transform.Origin;
        float surface = ocean.GetHeight(at);

        _waterRise = ocean.GetVerticalVelocity(new Vector2(at.X, at.Z));
        _submersion = Mathf.Clamp((surface - at.Y) / Mathf.Max(BodyHeight, 0.1f), 0f, 1f);
        _swimming = _submersion > (_swimming ? SwimEntry * 0.6f : SwimEntry);
    }

    private void ApplyBuoyancy(PhysicsDirectBodyState3D state)
    {
        Vector3 lift = -state.TotalGravity * (Mass * _submersion * Buoyancy);
        float rise = state.LinearVelocity.Y - _waterRise;
        lift.Y -= rise * (WaterDrag * Mass * _submersion);
        state.ApplyCentralForce(lift);
    }

    private void ApplySwim(PhysicsDirectBodyState3D state)
    {
        Vector3 velocity = state.LinearVelocity;
        Vector3 planar = velocity - Vector3.Up * velocity.Y;
        Vector3 force = (_wish * SwimSpeed - planar) * (SwimAcceleration * Mass * _submersion);
        state.ApplyCentralForce(force.LimitLength(MaxFootingForce));
    }

    private void SampleFooting(PhysicsDirectBodyState3D state, float dt)
    {
        int contacts = state.GetContactCount();
        float ceiling = GlobalPosition.Y + FootingHeight;
        Vector3 sum = Vector3.Zero;
        int found = 0;

        Vector3 slope = Vector3.Zero;

        for (int i = 0; i < contacts; i++)
        {
            if (state.GetContactColliderPosition(i).Y > ceiling) continue;
            sum += state.GetContactColliderVelocityAtPosition(i);
            Vector3 n = state.GetContactLocalNormal(i);
            slope += n.Dot(Vector3.Up) < 0f ? -n : n;
            found++;
        }

        if (found > 0)
        {
            _airborneFor = 0f;
            _grounded = true;
            _deckVelocity = sum / found;
            _groundNormal = slope.LengthSquared() > 1e-6f ? slope.Normalized() : Vector3.Up;
            return;
        }

        _airborneFor += dt;
        if (_airborneFor < FootingGrace) return;

        _grounded = false;
        _deckVelocity = Vector3.Zero;
        _groundNormal = Vector3.Up;
    }

    private void UpdateBalanceState(float dt, PhysicsDirectBodyState3D state)
    {
        float tilt = Mathf.RadToDeg(state.Transform.Basis.Y.AngleTo(Vector3.Up));

        if (_swimming)
        {
            _downed = false;
            _tiltedFor = 0f;
            _authority = Mathf.Min(1f, _authority + dt / RecoverTime);
            return;
        }

        if (!_downed)
        {
            _tiltedFor = tilt > KnockdownAngle ? _tiltedFor + dt : 0f;

            if (_tiltedFor >= KnockdownDwell)
            {
                _downed = true;
                _downedFor = 0f;
                _tiltedFor = 0f;
                _authority = 0f;
                return;
            }

            _authority = Mathf.Min(1f, _authority + dt / RecoverTime);
            return;
        }

        _downedFor += dt;
        if (_downedFor < DownedTime) return;

        _authority = Mathf.Min(1f, _authority + dt / RecoverTime);
        if (_authority >= 1f && tilt < RecoverAngle) _downed = false;
    }

    private void ApplyUpright(PhysicsDirectBodyState3D state, float scale)
    {
        Vector3 up = state.Transform.Basis.Y;
        Vector3 axis = up.Cross(Vector3.Up);
        float sine = axis.Length();
        if (sine > 1e-5f) axis /= sine;

        float angle = Mathf.Atan2(sine, up.Dot(Vector3.Up));
        Vector3 spin = state.AngularVelocity;
        Vector3 tiltSpin = spin - Vector3.Up * spin.Dot(Vector3.Up);

        Vector3 torque = (axis * (angle * UprightStiffness) - tiltSpin * UprightDamping) * Mass;
        state.ApplyTorque((torque * (_authority * scale)).LimitLength(MaxTorque));
    }

    private void ApplyYaw(PhysicsDirectBodyState3D state)
    {
        Vector3 facing = -state.Transform.Basis.Z;
        Vector3 planar = facing - Vector3.Up * facing.Dot(Vector3.Up);
        float lean = planar.Length();
        if (lean < 1e-3f) return;

        Vector3 want = new(-Mathf.Sin(_yaw), 0f, -Mathf.Cos(_yaw));
        float error = (planar / lean).SignedAngleTo(want, Vector3.Up);
        float rate = state.AngularVelocity.Dot(Vector3.Up);
        float fade = Mathf.SmoothStep(0f, YawFadeBelow, lean);

        float torque = (error * YawStiffness - rate * YawDamping) * Mass * fade;
        state.ApplyTorque(Vector3.Up * Mathf.Clamp(torque * _authority, -MaxTorque, MaxTorque));
    }

    private void ApplyWalk(PhysicsDirectBodyState3D state)
    {
        Vector3 ground = _grounded ? _groundNormal : Vector3.Up;
        Vector3 relative = state.LinearVelocity - _deckVelocity;
        Vector3 planar = relative - ground * relative.Dot(ground);

        Vector3 aim = _wish - ground * _wish.Dot(ground);
        if (aim.LengthSquared() > 1e-6f) aim = aim.Normalized() * _wish.Length();
        Vector3 target = aim * WalkSpeed;

        Vector3 force = (target - planar) * (Acceleration * Mass);

        if (_grounded) force = force.LimitLength(MaxFootingForce);
        else force *= AirControl;

        state.ApplyCentralForce(force * _authority);
    }
}
