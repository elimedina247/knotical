using Godot;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Boat;

/// <summary>
/// Propeller thrust as a foil, so the rig applies it like any other force on the hull.
/// Mounted low at the stern, which is what gives throttle its slight bow-up trim.
///
/// Thrust scales with immersion: when the stern launches off a crest the propeller
/// ventilates and the push dies until it bites again.
/// </summary>
[GlobalClass]
public partial class Motor : Node3D, IFoil
{
    [Export(PropertyHint.Range, "0,100000,100")]
    public float MaxThrust { get; set; } = 12000f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Throttle { get; set; }

    [Export(PropertyHint.Range, "0.1,2,0.05")]
    public float PropDepth { get; set; } = 0.35f;

    public float Immersion { get; private set; } = 1f;

    public Vector3 GlobalCentreOfEffort => GlobalPosition;

    public Vector3 ComputeForce(Vector3 pointVelocity)
    {
        Immersion = Submersion();

        if (Throttle <= 0f || Immersion <= 0f) return Vector3.Zero;

        return -GlobalBasis.Z.Normalized() * (MaxThrust * Throttle * Immersion);
    }

    private float Submersion()
    {
        OceanField ocean = OceanField.Instance;
        if (ocean == null) return 1f;

        Vector3 at = GlobalPosition;
        float water = ocean.GetHeight(new Vector2(at.X, at.Z));

        return Mathf.Clamp((water - (at.Y - PropDepth)) / Mathf.Max(PropDepth, 0.1f), 0f, 1f);
    }
}
