using Godot;

namespace Knotical.Player;

public struct GrabHold
{
    public Vector3 Point;

    public Vector3 Chest;

    public Vector3 Aim;
}

public interface IGrabbable
{
    bool Anchors { get; }

    Vector3 Attach(Vector3 globalPoint);

    Vector3 Track(GrabHold hold, float dt);

    void Detach();
}
