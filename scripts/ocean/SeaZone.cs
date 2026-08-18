using System.Collections.Generic;
using Godot;

namespace Knotical.Ocean;

[GlobalClass]
public partial class SeaZone : Node3D
{
    public static readonly List<SeaZone> Active = new();

    [Export(PropertyHint.Range, "10,4000,5")]
    public float Radius { get; set; } = 250f;

    [Export(PropertyHint.Range, "10,4000,5")]
    public float Falloff { get; set; } = 200f;

    [Export(PropertyHint.Range, "0,4,0.01")]
    public float Intensity { get; set; } = 0.3f;

    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint()) Active.Add(this);
    }

    public override void _ExitTree()
    {
        Active.Remove(this);
    }
}
