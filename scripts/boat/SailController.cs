using System.Collections.Generic;
using Godot;
using Knotical.Weather;

namespace Knotical.Boat;

[GlobalClass]
public partial class SailController : RigidBody3D, IDeckBody
{
    private const float Gravity = 9.81f;
    private const float SurfaceMaskInset = 0.9f;
    private const float SurfaceMaskRing = 0.7f;

    public static readonly List<SailController> Active = new();

    public Vector3 DeckAcceleration { get; private set; }

    public Vector3 BowWorld => GlobalTransform * new Vector3(0f, 0f, -HullLength * 0.5f - 0.8f);

    public float BeamWidth => Beam;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Leeway { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "20,60,1")]
    public float DriveNoGoDeg { get; set; } = 35f;

    [Export(PropertyHint.Range, "0,0.5,0.01")]
    public float NoGoDrive { get; set; } = 0.05f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float CloseHauledDrive { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ReachDrive { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "0,20,0.1")]
    public float HeelLever { get; set; } = 4f;

    [Export(PropertyHint.Range, "5,45,1")]
    public float MaxHeelDegrees { get; set; } = 18f;

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float KeelLever { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0,6,0.05")]
    public float KeelGrip { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "1,10,0.5")]
    public float RudderAuthority { get; set; } = 3f;

    [Export(PropertyHint.Range, "0,40,0.5")]
    public float GovernorSpeed { get; set; } = 7f;

    [Export(PropertyHint.Range, "0,2,0.05")]
    public float GovernorGain { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "5,60,1")]
    public float MaxAcceleration { get; set; } = 25f;

    [Export(PropertyHint.Range, "3,30,0.5")]
    public float HullLength { get; set; } = 7f;

    [Export(PropertyHint.Range, "1,12,0.05")]
    public float Beam { get; set; } = 2f;

    [Export(PropertyHint.Range, "-4,0,0.05")]
    public float HullBottom { get; set; } = -1f;

    [Export]
    public bool Trace { get; set; }

    private readonly List<IFoil> _foils = new();
    private double _traceClock;

    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint()) Active.Add(this);
    }

    public override void _ExitTree()
    {
        Active.Remove(this);
    }

    public override void _Ready()
    {
        LinearDampMode = DampMode.Replace;
        LinearDamp = 0f;
        AngularDampMode = DampMode.Replace;
        AngularDamp = 0f;
        RefreshFoils();
    }

    public void GetSurfaceMask(out Vector4 frame, out Vector4 extents)
    {
        Transform3D xform = GlobalTransform;
        Basis level = xform.Basis.Orthonormalized();
        var fwd = new Vector2(-level.Z.X, -level.Z.Z);
        fwd = fwd.LengthSquared() > 0.0001f ? fwd.Normalized() : Vector2.Right;

        frame = new Vector4(xform.Origin.X, xform.Origin.Z, fwd.X, fwd.Y);
        extents = new Vector4(HullLength * 0.5f, Beam * 0.5f, SurfaceMaskInset, SurfaceMaskRing);
    }

    public void RefreshFoils()
    {
        _foils.Clear();
        Collect(this);
    }

    private void Collect(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is IFoil foil) _foils.Add(foil);
            Collect(child);
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        Transform3D xform = state.Transform;
        Basis level = xform.Basis.Orthonormalized();
        Vector3 com = xform * state.CenterOfMassLocal;
        Vector3 linear = state.LinearVelocity;
        Vector3 angular = state.AngularVelocity;

        var force = Vector3.Zero;
        var torque = Vector3.Zero;
        Vector3 keelwise = -level.Z;
        var canvas = Vector3.Zero;

        for (int i = 0; i < _foils.Count; i++)
        {
            if (_foils[i] is not Node3D node || !IsInstanceValid(node)) continue;

            Vector3 arm = _foils[i].GlobalCentreOfEffort - com;
            Vector3 pointVelocity = _foils[i] is Rudder
                ? keelwise * linear.Dot(keelwise) + angular.Cross(arm)
                : linear + angular.Cross(arm);
            Vector3 f = _foils[i].ComputeForce(pointVelocity);

            if (_foils[i] is Sail)
            {
                canvas += f;
                continue;
            }

            Vector3 twist = arm.Cross(f);
            if (_foils[i] is Rudder)
            {
                twist += level.Y * (twist.Dot(level.Y) * (RudderAuthority - 1f));
            }

            force += f;
            torque += twist;
        }

        canvas.Y = 0f;

        var ahead = new Vector3(-level.Z.X, 0f, -level.Z.Z);
        bool hasAhead = ahead.LengthSquared() > 0.0001f;
        if (hasAhead)
        {
            ahead = ahead.Normalized();
            Vector3 side = canvas - ahead * canvas.Dot(ahead);
            canvas = ahead * (canvas.Length() * Polar(level)) + side * Leeway;
            torque += HeelTorque(level, side);
        }
        else
        {
            torque += HeelTorque(level, canvas);
        }

        force += canvas;

        var sideFlat = new Vector3(level.X.X, 0f, level.X.Z);
        if (KeelGrip > 0f && sideFlat.LengthSquared() > 0.0001f)
        {
            sideFlat = sideFlat.Normalized();
            var planar = new Vector3(linear.X, 0f, linear.Z);
            float slip = planar.Dot(sideFlat);
            force -= sideFlat * (Mass * KeelGrip * slip);
        }

        float headway = hasAhead ? linear.Dot(ahead) : 0f;
        if (hasAhead && GovernorGain > 0f && GovernorSpeed > 0f)
        {
            float excess = headway - GovernorSpeed * Polar(level);
            if (excess > 0f)
            {
                force -= ahead * (Mass * GovernorGain * excess * excess);
            }
        }

        if (KeelLever > 0f)
        {
            float roll = -Mathf.Asin(Mathf.Clamp(level.X.Y, -1f, 1f));
            torque += level.Z * (Mass * Gravity * KeelLever * roll);
        }

        float maxForce = Mass * MaxAcceleration;
        if (force.LengthSquared() > maxForce * maxForce) force = force.Normalized() * maxForce;

        float maxTorque = maxForce * HullLength * 0.5f;
        if (torque.LengthSquared() > maxTorque * maxTorque) torque = torque.Normalized() * maxTorque;

        DeckAcceleration = new Vector3(force.X, 0f, force.Z) / Mass;

        state.ApplyCentralForce(force);
        state.ApplyTorque(torque);

        if (Trace)
        {
            _traceClock += state.Step;
            if (_traceClock >= 1.0)
            {
                _traceClock = 0.0;
                float heel = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(level.X.Y, -1f, 1f)));
                GD.Print($"sail y={xform.Origin.Y:0.00} spd={linear.Length():0.00} hdwy={headway:0.00} " +
                    $"off={OffWindDeg(level):0} pol={Polar(level):0.00} heel={heel:0.0} " +
                    $"canvas={canvas.Length():0} x={xform.Origin.X:0.0} z={xform.Origin.Z:0.0}");
            }
        }
    }

    public float OffWindDeg(Basis basis)
    {
        Wind wind = Wind.Instance;
        if (wind == null) return 180f;
        Vector2 breeze = wind.Velocity;
        if (breeze.LengthSquared() < 0.01f) return 180f;
        Vector3 fwd = -basis.Z;
        var heading = new Vector2(fwd.X, fwd.Z);
        if (heading.LengthSquared() < 0.0001f) return 180f;
        return Mathf.RadToDeg(Mathf.Acos(
            Mathf.Clamp(heading.Normalized().Dot(-breeze.Normalized()), -1f, 1f)));
    }

    public float Polar(Basis basis)
    {
        Wind wind = Wind.Instance;
        if (wind == null) return 1f;
        Vector2 breeze = wind.Velocity;
        if (breeze.LengthSquared() < 0.01f) return 1f;

        float offWind = OffWindDeg(basis);

        float shape = offWind <= 90f
            ? Mathf.Lerp(CloseHauledDrive, ReachDrive,
                Mathf.SmoothStep(0f, 1f, (offWind - DriveNoGoDeg) / Mathf.Max(90f - DriveNoGoDeg, 1f)))
            : Mathf.Lerp(ReachDrive, 1f, Mathf.SmoothStep(0f, 1f, (offWind - 90f) / 90f));

        float gate = Mathf.SmoothStep(0f, 1f, (offWind - (DriveNoGoDeg - 6f)) / 12f);

        return Mathf.Lerp(NoGoDrive, shape, gate);
    }

    private Vector3 HeelTorque(Basis basis, Vector3 side)
    {
        if (HeelLever <= 0f) return Vector3.Zero;

        float lean = side.Dot(basis.X);
        Vector3 sideForce = basis.X * lean;
        if (sideForce.LengthSquared() < 1f) return Vector3.Zero;

        float roll = -Mathf.Asin(Mathf.Clamp(basis.X.Y, -1f, 1f));
        float toward = roll * Mathf.Sign(lean);
        float progress = Mathf.Clamp(toward / Mathf.DegToRad(MaxHeelDegrees), 0f, 1f);
        float fade = 1f - progress * progress * progress * progress;

        return (basis.Y * (HeelLever * fade)).Cross(sideForce);
    }
}
