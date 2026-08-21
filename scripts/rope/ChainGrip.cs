using Godot;
using Knotical.Player;

namespace Knotical.Rigging;

[GlobalClass]
public partial class ChainGrip : Node3D, IGrabbable
{
    private RigidBody3D _link;
    private Vector3 _local;

    public bool Anchors => true;

    public Vector3 Attach(Vector3 globalPoint)
    {
        _link = Nearest(globalPoint);
        if (_link == null) return globalPoint;

        _local = _link.GlobalTransform.AffineInverse() * globalPoint;
        return globalPoint;
    }

    public Vector3 Track(GrabHold hold, float dt)
    {
        if (!IsInstanceValid(_link)) return hold.Point;
        return _link.GlobalTransform * _local;
    }

    public void Detach()
    {
        _link = null;
    }

    private RigidBody3D Nearest(Vector3 point)
    {
        RigidBody3D best = null;
        float closest = float.MaxValue;

        foreach (Node child in GetChildren())
        {
            if (child is not RigidBody3D link) continue;

            float distance = link.GlobalPosition.DistanceSquaredTo(point);
            if (distance >= closest) continue;

            closest = distance;
            best = link;
        }

        return best;
    }
}
