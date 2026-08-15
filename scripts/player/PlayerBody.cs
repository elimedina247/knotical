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

    [Export(PropertyHint.Range, "0,8,0.05")]
    public float CarryTime { get; set; } = 2f;

    [Export(PropertyHint.Range, "1,10,0.1")]
    public float JumpSpeed { get; set; } = 4.2f;

    [Export(PropertyHint.Range, "0.15,1.5,0.01")]
    public float MantleTime { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0.02,1,0.01")]
    public float MantleLip { get; set; } = 0.2f;

    [Export(PropertyHint.Range, "0,90,1")]
    public float GripHeel { get; set; } = 12f;

    [Export(PropertyHint.Range, "0,90,1")]
    public float SlipHeel { get; set; } = 38f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float MinTraction { get; set; } = 0.15f;

    [Export(PropertyHint.Range, "0,90,1")]
    public float KnockdownHeel { get; set; } = 48f;

    [Export(PropertyHint.Range, "10,85,1")]
    public float FloorMaxAngle { get; set; } = 55f;

    [Export(PropertyHint.Range, "0.01,1,0.01")]
    public float FootingSmooth { get; set; } = 0.25f;

    [Export] public bool Trace { get; set; }

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
    public float SwimSpeed { get; set; } = 2.2f;

    [Export(PropertyHint.Range, "1,40,0.5")]
    public float SwimAcceleration { get; set; } = 10f;

    [Export(PropertyHint.Range, "0.2,4,0.05")]
    public float SwimVertical { get; set; } = 1.4f;

    [Export(PropertyHint.Range, "0,5,0.05")]
    public float StrokeKick { get; set; } = 2.2f;

    [Export(PropertyHint.Range, "0.4,1.2,0.01")]
    public float DiveBuoyancy { get; set; } = 0.85f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SwellFilter { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0.05,2,0.01")]
    public float SwellFast { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "1,30,0.1")]
    public float SwellSlow { get; set; } = 10f;

    [Export(PropertyHint.Range, "0,0.4,0.005")]
    public float SwellLimit { get; set; } = 0.09f;

    [Export(PropertyHint.Range, "0.05,2,0.01")]
    public float TiltFollowTime { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0,30,0.5")]
    public float GripFovPull { get; set; } = 6f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float GripDrag { get; set; } = 1f;

    [Export] public bool ShowHands { get; set; } = true;

    [Export] public Node3D CameraPivot { get; set; }

    [Export] public PlayerGrab Grab { get; set; }

    [Export] public Camera3D View { get; set; }

    private Node3D _pivot;
    private PlayerGrab _grab;
    private Camera3D _camera;
    private Node3D _deck;
    private GeometryInstance3D[] _skin = System.Array.Empty<GeometryInstance3D>();
    private bool _concealed;
    private float _pivotRest;
    private float _baseFov;
    private float _fovBlend;
    private float _swellFast;
    private float _swellSlow;
    private float _swellBlend;
    private bool _swellReady;
    private float _tilt;
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
    private float _stroke;
    private float _authority = 1f;
    private bool _jump;
    private Vector3 _mantle;
    private float _mantleFor;
    private float _yaw;
    private float _pitch;
    private float _traceTick;
    private bool _wantsCapture = true;

    public bool IsDowned => _downed;
    public bool IsGrounded => _grounded;
    public bool IsSwimming => _swimming;
    public float Submersion => _submersion;
    public Vector3 DeckVelocity => _deckVelocity;
    public Node3D Deck => _deck;
    public float Authority => _authority;

    public float DeckHeel => Mathf.RadToDeg(_groundNormal.AngleTo(Vector3.Up));

    public bool IsFootless => _grounded && DeckHeel > KnockdownHeel;

    public float Traction
    {
        get
        {
            float fade = Mathf.InverseLerp(GripHeel, Mathf.Max(SlipHeel, GripHeel + 0.01f), DeckHeel);
            return Mathf.Lerp(1f, MinTraction, Mathf.Clamp(fade, 0f, 1f));
        }
    }

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
        _grab = Grab ?? GetNodeOrNull<PlayerGrab>("Grab");
        _camera = View ?? _pivot?.GetNodeOrNull<Camera3D>("Camera3D");

        if (_pivot != null) _pivotRest = _pivot.Position.Y;
        if (_camera != null) _baseFov = _camera.Fov;

        CollectSkin();

        _yaw = Rotation.Y;
        CanSleep = false;
        ContactMonitor = true;
        if (MaxContactsReported < 6) MaxContactsReported = 6;
        Capture();
    }

    private void Capture()
    {
        _wantsCapture = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    public override void _Notification(int what)
    {
        base._Notification(what);

        if (what == NotificationApplicationFocusIn && _wantsCapture) Capture();
    }

    private void CollectSkin()
    {
        var parts = new System.Collections.Generic.List<GeometryInstance3D>();
        Gather(this, parts);
        _skin = parts.ToArray();
    }

    private void Gather(Node node, System.Collections.Generic.List<GeometryInstance3D> into)
    {
        foreach (Node child in node.GetChildren())
        {
            if (ShowHands && child is DangleArm) continue;

            if (child is GeometryInstance3D part
                && part.CastShadow != GeometryInstance3D.ShadowCastingSetting.Off)
                into.Add(part);

            Gather(child, into);
        }
    }

    private void Conceal(bool hide)
    {
        if (hide == _concealed) return;
        _concealed = hide;

        GeometryInstance3D.ShadowCastingSetting mode = hide
            ? GeometryInstance3D.ShadowCastingSetting.ShadowsOnly
            : GeometryInstance3D.ShadowCastingSetting.On;

        foreach (GeometryInstance3D part in _skin) part.CastShadow = mode;
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

        if (@event.IsActionPressed("ui_cancel"))
        {
            _wantsCapture = false;
            Input.MouseMode = Input.MouseModeEnum.Visible;
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true }
            && Input.MouseMode != Input.MouseModeEnum.Captured)
        {
            Capture();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        ShapeView((float)delta);

        if (Input.IsActionJustPressed("jump")) _jump = true;

        _stroke = (Input.IsActionPressed("jump") ? 1f : 0f) - (Input.IsActionPressed("dive") ? 1f : 0f);

        Vector2 move = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 forward = new(-Mathf.Sin(_yaw), 0f, -Mathf.Cos(_yaw));
        Vector3 right = new(Mathf.Cos(_yaw), 0f, -Mathf.Sin(_yaw));
        _wish = (right * move.X - forward * move.Y).LimitLength(1f);
    }

    private void ShapeView(float dt)
    {
        if (_pivot != null)
        {
            float follow = _downed || _swimming ? 1f : BodyTiltFollow;
            _tilt = Mathf.MoveToward(_tilt, follow, dt / Mathf.Max(TiltFollowTime, 1e-3f));

            var level = new Quaternion(Basis.FromEuler(new Vector3(_pitch, _yaw, 0f)));
            var ride = new Quaternion(
                GlobalBasis.Orthonormalized() * Basis.FromEuler(new Vector3(_pitch, 0f, 0f)));
            _pivot.GlobalBasis = new Basis(level.Slerp(ride, _tilt));

            Vector3 seat = _pivot.Position;
            _pivot.Position = new Vector3(seat.X, _pivotRest + Swell(dt), seat.Z);
        }

        if (_camera == null) return;

        Conceal(_camera.Current);

        float grip = _grab?.Load ?? 0f;
        _fovBlend = Mathf.MoveToward(_fovBlend, grip, dt / 0.3f);
        _camera.Fov = _baseFov - GripFovPull * _fovBlend;
    }

    private float Swell(float dt)
    {
        float deck = _deck != null && IsInstanceValid(_deck) ? _deck.GlobalPosition.Y : 0f;

        if (!_swellReady)
        {
            _swellFast = deck;
            _swellSlow = deck;
            _swellReady = true;
        }

        _swellFast = Mathf.Lerp(_swellFast, deck, 1f - Mathf.Exp(-dt / Mathf.Max(SwellFast, 1e-3f)));
        _swellSlow = Mathf.Lerp(_swellSlow, deck, 1f - Mathf.Exp(-dt / Mathf.Max(SwellSlow, 1e-3f)));

        float want = _grounded && !_downed && !_swimming ? SwellFilter : 0f;
        _swellBlend = Mathf.MoveToward(_swellBlend, want, dt / 0.25f);

        float limit = Mathf.Max(SwellLimit, 1e-4f);
        float raw = -(_swellFast - _swellSlow) * _swellBlend;

        return limit * System.MathF.Tanh(raw / limit);
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        float dt = state.Step;

        if (_mantleFor > 0f)
        {
            StepMantle(state, dt);
            return;
        }

        SampleWater(state);
        SampleFooting(state, dt);
        UpdateBalanceState(dt, state);

        Launch(state);

        _grab?.Apply(state);

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

        if (_grab == null || !_grab.IsClimbing) ApplyWalk(state);
    }

    private void StepMantle(PhysicsDirectBodyState3D state, float dt)
    {
        _mantleFor = Mathf.Max(_mantleFor - dt, 0f);

        Vector3 delta = _mantle - state.Transform.Origin;

        if (_mantleFor <= 0f || delta.LengthSquared() < 1e-4f)
        {
            _mantleFor = 0f;
            state.LinearVelocity = _deckVelocity;
            _grounded = true;
            _airborneFor = 0f;
            return;
        }

        Vector3 planar = delta - Vector3.Up * delta.Y;
        float over = Mathf.Clamp(1f - delta.Y / Mathf.Max(MantleLip, 1e-3f), 0f, 1f);

        state.LinearVelocity = (Vector3.Up * delta.Y + planar * over) / _mantleFor;
        state.AngularVelocity = Vector3.Zero;
    }

    private void Launch(PhysicsDirectBodyState3D state)
    {
        if (!_jump) return;
        _jump = false;

        if (_grab != null && _grab.IsGripping)
        {
            if (_grab.FindLedge(state.Transform.Origin, out Vector3 stand))
            {
                _mantle = stand;
                _mantleFor = MantleTime;
                _grab.Drop();
                _grounded = false;
                _airborneFor = FootingGrace;
                return;
            }

            if (_grab.Vault(state))
            {
                _grounded = false;
                _airborneFor = FootingGrace;
                return;
            }
        }

        if (_swimming)
        {
            Vector3 water = state.LinearVelocity;
            water.Y += Mathf.Max(StrokeKick * _submersion - (water.Y - _waterRise), 0f);
            state.LinearVelocity = water;
            return;
        }

        if (!_grounded || _downed) return;

        Vector3 velocity = state.LinearVelocity;
        velocity.Y += Mathf.Max(JumpSpeed - (velocity.Y - _deckVelocity.Y), 0f);
        state.LinearVelocity = velocity;

        _grounded = false;
        _airborneFor = FootingGrace;
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
        float buoy = Mathf.Lerp(Buoyancy, DiveBuoyancy, Mathf.Max(-_stroke, 0f));
        Vector3 lift = -state.TotalGravity * (Mass * _submersion * buoy);
        float rise = state.LinearVelocity.Y - _waterRise;
        lift.Y -= rise * (WaterDrag * Mass * _submersion);
        state.ApplyCentralForce(lift);
    }

    private void ApplySwim(PhysicsDirectBodyState3D state)
    {
        Vector3 velocity = state.LinearVelocity;
        Vector3 planar = velocity - Vector3.Up * velocity.Y;
        Vector3 force = (_wish * SwimSpeed - planar) * (SwimAcceleration * Mass * _submersion);

        if (_stroke != 0f)
        {
            float want = _waterRise + _stroke * SwimVertical;
            force.Y = (want - velocity.Y) * (SwimAcceleration * Mass * _submersion);
        }

        state.ApplyCentralForce(force.LimitLength(MaxFootingForce));
    }

    private void SampleFooting(PhysicsDirectBodyState3D state, float dt)
    {
        int contacts = state.GetContactCount();
        float ceiling = GlobalPosition.Y + FootingHeight;
        Vector3 sum = Vector3.Zero;
        int found = 0;

        Vector3 slope = Vector3.Zero;
        Node3D floor = null;

        float standable = Mathf.Cos(Mathf.DegToRad(FloorMaxAngle));

        for (int i = 0; i < contacts; i++)
        {
            if (state.GetContactColliderPosition(i).Y > ceiling) continue;

            Vector3 n = state.GetContactLocalNormal(i);
            if (n.Dot(Vector3.Up) < 0f) n = -n;
            if (n.Dot(Vector3.Up) < standable) continue;

            sum += state.GetContactColliderVelocityAtPosition(i);
            slope += n;
            if (state.GetContactColliderObject(i) is Node3D deck) floor = deck;
            found++;
        }

        if (Trace && found > 0)
        {
            _traceTick += dt;
            if (_traceTick >= 0.5f)
            {
                _traceTick = 0f;
                GD.Print($"player grounded={_grounded} contacts={contacts} floors={found} " +
                         $"heel={DeckHeel,5:0.0}deg ship={(_deck != null ? Mathf.RadToDeg(_deck.GlobalBasis.Y.Normalized().AngleTo(Vector3.Up)) : 0f),5:0.0}deg " +
                         $"footless={IsFootless} downed={_downed} " +
                         $"authority={_authority,4:0.00} traction={Traction,4:0.00}");
            }
        }

        if (found > 0)
        {
            _airborneFor = 0f;
            _grounded = true;
            _deckVelocity = sum / found;
            Vector3 sensed = slope.LengthSquared() > 1e-6f ? slope.Normalized() : Vector3.Up;
            float ease = 1f - Mathf.Exp(-dt / Mathf.Max(FootingSmooth, 1e-3f));
            _groundNormal = _groundNormal.Lerp(sensed, ease).Normalized();
            SetDeck(floor);
            return;
        }

        if (Trace)
        {
            _traceTick += dt;
            if (_traceTick >= 0.5f)
            {
                _traceTick = 0f;
                GD.Print($"player grounded={_grounded} contacts={contacts} floors={found} " +
                         $"heel={DeckHeel,5:0.0}deg ship={(_deck != null ? Mathf.RadToDeg(_deck.GlobalBasis.Y.Normalized().AngleTo(Vector3.Up)) : 0f),5:0.0}deg " +
                         $"footless={IsFootless} downed={_downed} " +
                         $"authority={_authority,4:0.00} traction={Traction,4:0.00}");
            }
        }

        _airborneFor += dt;
        if (_airborneFor < FootingGrace) return;

        _grounded = false;
        _deckVelocity = _deckVelocity.Lerp(Vector3.Zero, 1f - Mathf.Exp(-dt / Mathf.Max(CarryTime, 1e-3f)));
        _groundNormal = _groundNormal.Lerp(Vector3.Up, 1f - Mathf.Exp(-dt / Mathf.Max(FootingSmooth, 1e-3f))).Normalized();
    }

    private void SetDeck(Node3D floor)
    {
        if (floor == null || floor == _deck) return;
        _deck = floor;
        _swellReady = false;
    }

    private void UpdateBalanceState(float dt, PhysicsDirectBodyState3D state)
    {
        float tilt = Mathf.RadToDeg(state.Transform.Basis.Y.AngleTo(Vector3.Up));
        bool footless = IsFootless;

        if (_swimming)
        {
            _downed = false;
            _tiltedFor = 0f;
            _authority = Mathf.Min(1f, _authority + dt / RecoverTime);
            return;
        }

        if (!_downed)
        {
            _tiltedFor = tilt > KnockdownAngle || footless ? _tiltedFor + dt : 0f;

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
        if (_downedFor < DownedTime || footless) return;

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

        if (_grounded) force = force.LimitLength(MaxFootingForce * Traction);
        else force *= AirControl;

        float hands = 1f - (_grab?.Load ?? 0f) * GripDrag;
        state.ApplyCentralForce(force * (_authority * Mathf.Max(hands, 0f)));
    }
}
