using System.Collections.Generic;
using Godot;
using Knotical.Weather;

namespace Knotical.Boat;

/// <summary>
/// The wind side of a boat: collects the foils under a hull and turns their forces into
/// drive, heel, and steering torque. Split out of BoatHull so the hull owns water and
/// this owns wind; the maths and its ordering are unchanged from when they were one
/// class, and the tuning exports deliberately remain on the hull so existing scenes keep
/// their values.
///
/// BoatHull creates one automatically at runtime when the scene does not provide it.
/// </summary>
[GlobalClass]
public partial class SailingRig : Node
{
    private const float Gravity = 9.81f;

    private BoatHull _hull;
    private readonly List<IFoil> _foils = new();

    public int FoilCount => _foils.Count;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Leeway { get; set; } = 0.35f;

    public override void _Ready()
    {
        _hull = GetParentOrNull<BoatHull>();
        Refresh();
    }

    public void Refresh()
    {
        _foils.Clear();
        if (_hull != null) Collect(_hull);
    }

    private void Collect(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is IFoil foil) _foils.Add(foil);
            Collect(child);
        }
    }

    public void Accumulate(Basis level, Vector3 com, Vector3 linear, Vector3 angular,
        ref Vector3 force, ref Vector3 torque, out Vector3 rigForce, out float steerTorque)
    {
        rigForce = Vector3.Zero;
        steerTorque = 0f;

        if (_hull == null) return;

        Vector3 rig = Vector3.Zero;
        Vector3 canvas = Vector3.Zero;

        Vector3 keelwise = -level.Z;

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
                twist += level.Y * (twist.Dot(level.Y) * (_hull.RudderAuthority - 1f));
                steerTorque = twist.Dot(level.Y);
            }

            rig += f;
            force += f;
            torque += twist;
        }

        canvas.Y = 0f;

        var ahead = new Vector3(-level.Z.X, 0f, -level.Z.Z);
        if (ahead.LengthSquared() > 0.0001f)
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

        rig += canvas;
        force += canvas;

        rigForce = rig;
    }

    public float Polar(Basis basis)
    {
        Wind wind = Wind.Instance;
        if (wind == null || _hull == null) return 1f;

        Vector2 breeze = wind.Velocity;
        if (breeze.LengthSquared() < 0.01f) return 1f;

        Vector3 fwd = -basis.Z;
        var heading = new Vector2(fwd.X, fwd.Z);
        if (heading.LengthSquared() < 0.0001f) return 1f;

        float offWind = Mathf.RadToDeg(Mathf.Acos(
            Mathf.Clamp(heading.Normalized().Dot(-breeze.Normalized()), -1f, 1f)));

        float shape = offWind <= 90f
            ? Mathf.Lerp(_hull.CloseHauledDrive, _hull.ReachDrive,
                Mathf.SmoothStep(0f, 1f, (offWind - _hull.DriveNoGoDeg) / Mathf.Max(90f - _hull.DriveNoGoDeg, 1f)))
            : Mathf.Lerp(_hull.ReachDrive, 1f, Mathf.SmoothStep(0f, 1f, (offWind - 90f) / 90f));

        float gate = Mathf.SmoothStep(0f, 1f, (offWind - (_hull.DriveNoGoDeg - 6f)) / 12f);

        return Mathf.Lerp(_hull.NoGoDrive, shape, gate);
    }

    private Vector3 HeelTorque(Basis basis, Vector3 canvas)
    {
        if (_hull.HeelLever <= 0f) return Vector3.Zero;

        float lean = canvas.Dot(basis.X);
        Vector3 side = basis.X * lean;
        if (side.LengthSquared() < 1f) return Vector3.Zero;

        float roll = -Mathf.Asin(Mathf.Clamp(basis.X.Y, -1f, 1f));
        float toward = roll * Mathf.Sign(lean);
        float progress = Mathf.Clamp(toward / Mathf.DegToRad(_hull.MaxHeelDegrees), 0f, 1f);
        float fade = 1f - progress * progress * progress * progress;

        return (basis.Y * (_hull.HeelLever * fade)).Cross(side);
    }
}
