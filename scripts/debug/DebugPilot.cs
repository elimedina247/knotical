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

    [Export(PropertyHint.Range, "0.2,8,0.05")]
    public float ThrottleRate { get; set; } = 0.8f;

    private Knotical.Boat.Helm _wheel;
    private Rudder _rudder;
    private Motor _motor;
    private BoatController _controller;
    private float _helm;
    private float _throttle;
    private float _forcedThrottle;
    private float _forcedSteer;

    public float Helm => _helm;

    public float Throttle => _throttle;

    public override void _Ready()
    {
        _controller = Find<BoatController>(GetParent());
        _wheel = Find<Knotical.Boat.Helm>(GetParent());
        _motor = Find<Motor>(GetParent());

        if (Blade != null && !Blade.IsEmpty) _rudder = GetNodeOrNull<Rudder>(Blade);
        _rudder ??= Find<Rudder>(GetParent());

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--throttle=") && float.TryParse(arg["--throttle=".Length..], out float forced))
                _forcedThrottle = Mathf.Clamp(forced, 0f, 1f);

            if (arg.StartsWith("--steer=") && float.TryParse(arg["--steer=".Length..], out float steer))
                _forcedSteer = Mathf.Clamp(steer, -1f, 1f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        float steer = (Input.IsPhysicalKeyPressed(Key.D) ? 1f : 0f)
                    - (Input.IsPhysicalKeyPressed(Key.A) ? 1f : 0f);
        if (steer == 0f) steer = _forcedSteer;

        float rate = Mathf.IsZeroApprox(steer) ? HelmReturnRate : HelmRate;
        _helm = Mathf.MoveToward(_helm, steer, rate * dt);

        float want = Input.IsPhysicalKeyPressed(Key.W) ? 1f
            : Input.IsPhysicalKeyPressed(Key.S) ? -1f
            : _forcedThrottle;
        _throttle = Mathf.MoveToward(_throttle, want, ThrottleRate * dt);

        if (_controller != null && IsInstanceValid(_controller))
        {
            _controller.Throttle = _throttle;
            _controller.Steer = _helm;
            return;
        }

        if (_motor != null && IsInstanceValid(_motor))
        {
            _motor.Throttle = Mathf.Max(_throttle, 0f);
        }

        if (_wheel != null && IsInstanceValid(_wheel))
        {
            _wheel.ForceSteering(_helm);
            return;
        }

        if (_rudder != null && IsInstanceValid(_rudder)) _rudder.Steering = _helm;
    }

    private static T Find<T>(Node node) where T : Node
    {
        if (node == null) return null;
        if (node is T match) return match;

        foreach (Node child in node.GetChildren())
        {
            T found = Find<T>(child);
            if (found != null) return found;
        }

        return null;
    }
}
