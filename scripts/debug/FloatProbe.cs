using Godot;

namespace Knotical.Debug;

/// <summary>
/// Headless measurement helper: prints every sibling RigidBody3D's height, tilt, and
/// speed once a second so a float test can be read from stdout.
/// </summary>
[GlobalClass]
public partial class FloatProbe : Node
{
    private float _tick;

    public override void _PhysicsProcess(double delta)
    {
        _tick += (float)delta;
        if (_tick < 1f) return;
        _tick = 0f;

        foreach (Node sibling in GetParent().GetChildren())
        {
            if (sibling is not RigidBody3D body) continue;

            float tilt = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(body.GlobalBasis.Y.Normalized().Y, -1f, 1f)));

            float water = Knotical.Ocean.Ocean.Instance?.GetHeight(body.GlobalPosition) ?? 0f;

            string buoy = "";
            foreach (Knotical.Boat.Buoyancy b in Knotical.Boat.Buoyancy.Active)
            {
                if (b.Body != body) continue;

                Vector3 total = Vector3.Zero;
                for (int i = 0; i < b.ProbeCount; i++)
                {
                    b.GetProbe(i, out _, out _, out Vector3 force, out _);
                    total += force;
                }

                buoy = $" wet={b.Wetness,4:0.00} fy={total.Y,8:0} N";
            }

            GD.Print($"float {body.Name}: y={body.GlobalPosition.Y,7:0.00} water={water,6:0.00} " +
                     $"tilt={tilt,5:0.0} vy={body.LinearVelocity.Y,6:0.00} spd={body.LinearVelocity.Length(),6:0.00}{buoy}");
        }
    }
}
