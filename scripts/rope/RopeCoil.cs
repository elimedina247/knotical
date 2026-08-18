using Godot;
using Knotical.Boat;

namespace Knotical.Rigging;

[GlobalClass]
public partial class RopeCoil : DeckCargo
{
    [Export(PropertyHint.Range, "2,60,0.5")]
    public float Capacity { get; set; } = 20f;

    public Rope Tail { get; set; }

    public bool Carried { get; private set; }

    private uint _layer;
    private uint _mask;

    public void Carry()
    {
        if (Carried) return;

        Carried = true;
        _layer = CollisionLayer;
        _mask = CollisionMask;
        CollisionLayer = 0;
        CollisionMask = 0;
        Freeze = true;
    }

    public void Drop(Vector3 at, Vector3 velocity)
    {
        if (!Carried) return;

        Carried = false;
        GlobalPosition = at;
        CollisionLayer = _layer;
        CollisionMask = _mask;
        Freeze = false;
        LinearVelocity = velocity;
        AngularVelocity = Vector3.Zero;
    }
}
