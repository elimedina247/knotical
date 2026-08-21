using Godot;
using Knotical.Rigging;

namespace Knotical.Debugging;

[GlobalClass]
public partial class RopeRig : Node3D
{
    [Export] public NodePath AnchorPath { get; set; }

    [Export(PropertyHint.Range, "1,8,1")]
    public int Count { get; set; } = 3;

    [Export(PropertyHint.Range, "0.2,4,0.1")]
    public float Spacing { get; set; } = 2f;

    [Export(PropertyHint.Range, "0.5,12,0.1")]
    public float Length { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "-2,2,0.1")]
    public float LengthStep { get; set; } = 0.5f;

    [Export] public Vector3 Axis { get; set; } = Vector3.Back;

    public override void _Ready()
    {
        var anchor = AnchorPath != null ? GetNodeOrNull<Node3D>(AnchorPath) : null;
        if (anchor == null) return;

        Vector3 axis = Axis.LengthSquared() > 1e-6f ? Axis.Normalized() : Vector3.Back;
        float centre = (Count - 1) * 0.5f;

        for (int i = 0; i < Count; i++)
        {
            Vector3 point = GlobalPosition + axis * ((i - centre) * Spacing);

            var rope = new Rope { Name = $"Rope{i}" };
            AddChild(rope);
            rope.WorkingLength = Length + LengthStep * i;
            rope.BindStart(anchor, point);
        }
    }
}
