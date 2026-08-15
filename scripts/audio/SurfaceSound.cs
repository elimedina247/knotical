using Godot;

namespace Knotical.Audio;

[GlobalClass]
public partial class SurfaceSound : Node
{
    [Export] public ClipSet Steps { get; set; }

    [Export] public ClipSet Landings { get; set; }

    [Export] public ClipSet Impacts { get; set; }

    public static SurfaceSound Of(Node body)
    {
        if (body == null) return null;

        foreach (Node child in body.GetChildren())
        {
            if (child is SurfaceSound surface) return surface;
        }

        return null;
    }
}
