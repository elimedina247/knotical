using Godot;

namespace Knotical.Debug;

/// <summary>
/// Throwaway free camera for looking at things before there is a boat to stand on.
///
/// Reads keys directly rather than through the input map so it needs no project
/// configuration. Hold right mouse to look; release to get the cursor back.
/// </summary>
[GlobalClass]
public partial class DebugFlyCamera : Camera3D
{
    [Export] public float Speed { get; set; } = 25f;
    [Export] public float BoostMultiplier { get; set; } = 6f;
    [Export] public float MouseSensitivity { get; set; } = 0.0025f;

    private float _yaw;
    private float _pitch;

    public override void _Ready()
    {
        _yaw = Rotation.Y;
        _pitch = Rotation.X;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Right)
        {
            Input.MouseMode = button.Pressed
                ? Input.MouseModeEnum.Captured
                : Input.MouseModeEnum.Visible;
        }

        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            _yaw -= motion.Relative.X * MouseSensitivity;
            _pitch = Mathf.Clamp(_pitch - motion.Relative.Y * MouseSensitivity, -1.4f, 1.4f);
            Rotation = new Vector3(_pitch, _yaw, 0f);
        }
    }

    public override void _Process(double delta)
    {
        var move = Vector3.Zero;

        if (Input.IsPhysicalKeyPressed(Key.W)) move -= Basis.Z;
        if (Input.IsPhysicalKeyPressed(Key.S)) move += Basis.Z;
        if (Input.IsPhysicalKeyPressed(Key.A)) move -= Basis.X;
        if (Input.IsPhysicalKeyPressed(Key.D)) move += Basis.X;
        if (Input.IsPhysicalKeyPressed(Key.E)) move += Vector3.Up;
        if (Input.IsPhysicalKeyPressed(Key.Q)) move += Vector3.Down;

        if (move == Vector3.Zero) return;

        float speed = Speed * (Input.IsPhysicalKeyPressed(Key.Shift) ? BoostMultiplier : 1f);
        GlobalPosition += move.Normalized() * speed * (float)delta;
    }
}
