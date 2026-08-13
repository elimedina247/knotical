using Godot;

namespace Knotical.Boat;

public interface IFoil
{
    Vector3 GlobalCentreOfEffort { get; }

    Vector3 ComputeForce(Vector3 pointVelocity);
}
