using Godot;

namespace Knotical.Boat;

public interface IDeckBody
{
    Vector3 DeckAcceleration { get; }

    Vector3 BowWorld { get; }

    float HullLength { get; }

    float BeamWidth { get; }
}
