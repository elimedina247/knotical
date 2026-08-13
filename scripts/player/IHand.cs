using Godot;

namespace Knotical.Player;

public interface IHand
{
    Vector3 Root { get; }

    Vector3 Tip { get; }

    bool IsGripping { get; }

    void Grip(Vector3 globalTarget);

    void Release();
}
