using Godot;
using Knotical.Boat;

namespace Knotical.Debug;

/// <summary>
/// Keyboard helm for the debug scene. A and D swing the rudder, and that is the whole of
/// it: the wind drives the boat, the player only steers.
/// </summary>
[GlobalClass]
public partial class DebugPilot : Node
{
    [Export] public NodePath Blade { get; set; }

    [Export(PropertyHint.Range, "0.2,8,0.05")]
    public float HelmRate { get; set; } = 1.6f;

    [Export(PropertyHint.Range, "0.2,8,0.05")]
    public float HelmReturnRate { get; set; } = 2.4f;

    private Rudder _rudder;
    private float _helm;

    public float Helm => _helm;

    public override void _Ready()
    {
        if (Blade != null && !Blade.IsEmpty) _rudder = GetNodeOrNull<Rudder>(Blade);
        _rudder ??= FindRudder(GetParent());
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_rudder == null || !IsInstanceValid(_rudder)) return;

        float steer = (Input.IsPhysicalKeyPressed(Key.D) ? 1f : 0f)
                    - (Input.IsPhysicalKeyPressed(Key.A) ? 1f : 0f);

        float rate = Mathf.IsZeroApprox(steer) ? HelmReturnRate : HelmRate;
        _helm = Mathf.MoveToward(_helm, steer, rate * (float)delta);

        _rudder.Steering = _helm;
    }

    private static Rudder FindRudder(Node node)
    {
        if (node == null) return null;
        if (node is Rudder blade) return blade;

        foreach (Node child in node.GetChildren())
        {
            Rudder found = FindRudder(child);
            if (found != null) return found;
        }

        return null;
    }
}
