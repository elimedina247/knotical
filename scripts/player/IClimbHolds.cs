using Godot;

namespace Knotical.Player;

public interface IClimbHolds
{
    RigidBody3D Carrier { get; }

    bool Hold(Vector3 near, out Vector3 hold);
}
