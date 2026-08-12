using Godot;

namespace Knotical.Boat;

[GlobalClass]
public partial class Helm : Node3D
{
    [Export] public Node3D WheelPivot { get; set; }

    [Export] public Node3D RudderPivot { get; set; }

    [Export(PropertyHint.Range, "-1,1")] public float SteeringInput { get; set; }

    [Export] public float MaxRudderAngleDegrees { get; set; } = 35f;

    [Export] public float WheelTurnsAtFullLock { get; set; } = 1.5f;

    public override void _Process(double delta)
    {
        if (WheelPivot != null)
            WheelPivot.Rotation = new Vector3(0f, 0f, -SteeringInput * WheelTurnsAtFullLock * Mathf.Tau);

        if (RudderPivot != null)
            RudderPivot.Rotation = new Vector3(0f, -SteeringInput * Mathf.DegToRad(MaxRudderAngleDegrees), 0f);
    }
}
