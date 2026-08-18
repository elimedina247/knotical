using Godot;
using Knotical.Boat;

namespace Knotical.Debug;

/// <summary>
/// High following camera for the debug scene. Sits above and astern of the boat and keeps
/// it centred, so wave motion reads as pitch and roll rather than as camera shake.
///
/// Hold right mouse to swing around the boat, wheel to zoom, middle click to return to
/// the resting framing. The swing is held relative to the boat's heading, so once moved
/// the camera keeps that angle as the boat turns instead of drifting back.
///
/// Height and Astern set the resting framing and are read once on ready; the live rig is
/// polar (distance, pitch, yaw) because that is what orbiting needs.
///
/// Position is smoothed but aim is not: chasing the look direction as well makes the
/// horizon swim.
/// </summary>
[GlobalClass]
public partial class EagleCamera : Camera3D
{
    [Export] public NodePath Target { get; set; }

    [Export(PropertyHint.Range, "5,400,1")]
    public float Height { get; set; } = 55f;

    [Export(PropertyHint.Range, "1,400,1")]
    public float Astern { get; set; } = 22f;

    /// <summary>Swing with the boat as it turns. Off keeps a fixed compass bearing.</summary>
    [Export] public bool FollowHeading { get; set; } = true;

    [Export(PropertyHint.Range, "0.01,2,0.01")]
    public float Smoothing { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "1.01,2,0.01")]
    public float ZoomStep { get; set; } = 1.12f;

    [Export(PropertyHint.Range, "0.02,1,0.01")]
    public float OrbitSensitivity { get; set; } = 0.25f;

    [Export] public bool InvertPitch { get; set; }

    [Export(PropertyHint.Range, "1,80,1")]
    public float MinPitchDegrees { get; set; } = 4f;

    [Export(PropertyHint.Range, "10,85,1")]
    public float MaxPitchDegrees { get; set; } = 85f;

    private Node3D _target;
    private bool _placed;
    private bool _orbiting;
    private float _distance;
    private float _pitch;
    private float _yaw;

    public override void _Ready()
    {
        Current = true;
        Rest();
    }

    public override void _ExitTree()
    {
        if (_orbiting) Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void Rest()
    {
        float height = Mathf.Max(Height, 1f);
        float astern = Mathf.Max(Astern, 1f);

        _distance = Mathf.Sqrt(height * height + astern * astern);
        _pitch = Mathf.Clamp(Mathf.Atan2(height, astern),
            Mathf.DegToRad(MinPitchDegrees), Mathf.DegToRad(MaxPitchDegrees));
        _yaw = 0f;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouseButton button && button.Pressed)
        {
            switch (button.ButtonIndex)
            {
                case MouseButton.WheelUp: Zoom(1f / ZoomStep); return;
                case MouseButton.WheelDown: Zoom(ZoomStep); return;
                case MouseButton.Middle: Rest(); return;
            }
        }

        if (@event is InputEventMouseButton right && right.ButtonIndex == MouseButton.Right)
        {
            _orbiting = right.Pressed;
            Input.MouseMode = _orbiting ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
            return;
        }

        if (!_orbiting || @event is not InputEventMouseMotion motion) return;

        float sensitivity = Mathf.DegToRad(OrbitSensitivity);

        _yaw -= motion.Relative.X * sensitivity;
        _pitch += motion.Relative.Y * sensitivity * (InvertPitch ? -1f : 1f);
        _pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(MinPitchDegrees), Mathf.DegToRad(MaxPitchDegrees));
    }

    private void Zoom(float factor)
    {
        _distance = Mathf.Clamp(_distance * factor, 6f, 600f);
    }

    public override void _Process(double delta)
    {
        _target ??= Resolve();
        if (_target == null || !IsInstanceValid(_target)) return;

        Vector3 focus = _target.GlobalPosition;

        Vector3 back = Vector3.Back;
        if (FollowHeading)
        {
            Vector3 heading = _target.GlobalBasis.Z;
            heading.Y = 0f;
            if (heading.LengthSquared() > 0.0001f) back = heading.Normalized();
        }

        Vector3 offset = back.Rotated(Vector3.Up, _yaw) * (Mathf.Cos(_pitch) * _distance)
                       + Vector3.Up * (Mathf.Sin(_pitch) * _distance);

        Vector3 wanted = focus + offset;

        if (_placed)
        {
            float blend = 1f - Mathf.Exp(-(float)delta / Mathf.Max(Smoothing, 0.01f));
            GlobalPosition = GlobalPosition.Lerp(wanted, blend);
        }
        else
        {
            GlobalPosition = wanted;
            _placed = true;
        }

        LookAt(focus, Vector3.Up);
    }

    private Node3D Resolve()
    {
        if (Target != null && !Target.IsEmpty)
        {
            Node3D named = GetNodeOrNull<Node3D>(Target);
            if (named != null) return named;
        }

        if (BoatController.Active.Count > 0) return BoatController.Active[0];

        return BoatHull.Active.Count > 0 ? BoatHull.Active[0] : null;
    }
}
