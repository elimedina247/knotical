using System.Collections.Generic;
using Godot;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Boat;

[GlobalClass]
public partial class BoatController : RigidBody3D
{
    public static readonly List<BoatController> Active = new();

    [Export(PropertyHint.Range, "0,200000,100")]
    public float PropellerStrength { get; set; } = 16000f;

    [Export(PropertyHint.Range, "0,200000,100")]
    public float RudderStrength { get; set; } = 20000f;

    [Export(PropertyHint.Range, "1,40,0.5")]
    public float MaxSpeed { get; set; } = 10f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ReverseFraction { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0.1,1,0.05")]
    public float PropDepth { get; set; } = 0.4f;

    [Export] public bool Trace { get; set; }

    public float Throttle { get; set; }

    public float Steer { get; set; }

    public float MotorImmersion { get; private set; }

    private Node3D _motor;
    private float _traceTick;

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
        _motor = GetNodeOrNull<Node3D>("Motor");
    }

    public override void _PhysicsProcess(double delta)
    {
        OceanField ocean = OceanField.Instance;
        if (ocean == null) return;

        Vector3 forward = -GlobalBasis.Z.Normalized();
        float headway = LinearVelocity.Dot(forward);

        Vector3 motorPos = _motor?.GlobalPosition ?? GlobalPosition;
        float water = ocean.GetHeight(new Vector2(motorPos.X, motorPos.Z));
        MotorImmersion = Mathf.Clamp(
            (water - (motorPos.Y - PropDepth)) / Mathf.Max(PropDepth, 0.1f), 0f, 1f);

        if (Throttle != 0f && MotorImmersion > 0f)
        {
            float limit = Throttle > 0f ? MaxSpeed : -MaxSpeed * ReverseFraction;
            bool underLimit = Throttle > 0f ? headway < limit : headway > limit;

            if (underLimit)
            {
                float strength = Throttle > 0f ? PropellerStrength : PropellerStrength * ReverseFraction;
                ApplyForce(forward * (strength * Throttle * MotorImmersion),
                    motorPos - GlobalPosition);
            }
        }

        if (Steer != 0f)
        {
            float authority = Mathf.Clamp(headway / 2.5f, -1f, 1f);
            ApplyTorque(Vector3.Up * (-Steer * RudderStrength * authority));
        }

        if (!Trace) return;

        _traceTick += (float)delta;
        if (_traceTick < 1f) return;
        _traceTick = 0f;

        Vector3 up = GlobalBasis.Y.Normalized();
        float tilt = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(up.Y, -1f, 1f)));
        float yaw = Mathf.RadToDeg(Mathf.Atan2(forward.X, -forward.Z));

        GD.Print($"boat y={GlobalPosition.Y,6:0.00} water={water,6:0.00} spd={headway,5:0.00} " +
                 $"tilt={tilt,5:0.0} yaw={yaw,6:0.0} thr={Throttle,4:0.00} steer={Steer,5:0.00} " +
                 $"prop={MotorImmersion,4:0.00} x={GlobalPosition.X,7:0.0} z={GlobalPosition.Z,7:0.0}");
    }
}
