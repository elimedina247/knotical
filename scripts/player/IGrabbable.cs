using Godot;

namespace Knotical.Player;

public interface IGrabbable
{
    bool Anchors { get; }

    Vector3 Attach(Vector3 globalPoint);

    Vector3 Track(Vector3 globalHandPoint, float dt);

    void Detach();
}
