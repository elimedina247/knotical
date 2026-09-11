using Godot;

namespace Knotical.Boat;

[GlobalClass]
public partial class DeckCargo : RigidBody3D
{
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Follow { get; set; } = 1f;

    [Export(PropertyHint.Range, "0,1,0.05")]
    public float CarryTime { get; set; } = 0.25f;

    private IDeckBody _hull;
    private float _held;

    public IDeckBody Hull => _hull;

    public override void _Ready()
    {
        ContactMonitor = true;
        MaxContactsReported = Mathf.Max(MaxContactsReported, 8);

        for (Node step = GetParent(); step != null; step = step.GetParent())
        {
            if (step is RigidBody3D)
            {
                TopLevel = true;
                GD.PushWarning($"{Name}: cargo parented under a rigid body, detached to world space.");
                break;
            }
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        IDeckBody touching = null;

        for (int i = 0; i < state.GetContactCount(); i++)
        {
            if (state.GetContactColliderObject(i) is IDeckBody hull)
            {
                touching = hull;
                break;
            }

            if (state.GetContactColliderObject(i) is DeckCargo cargo && cargo.Hull != null)
                touching ??= cargo.Hull;
        }

        if (touching != null)
        {
            _hull = touching;
            _held = CarryTime;
        }
        else
        {
            _held -= (float)state.Step;

            if (_held <= 0f || _hull is not GodotObject alive || !IsInstanceValid(alive))
            {
                _hull = null;
                return;
            }
        }

        state.ApplyCentralForce(_hull.DeckAcceleration * (Mass * Follow));
    }
}
