using Godot;

namespace Knotical.Style;

[GlobalClass]
public partial class GamePalette : Resource
{
    [Export] public Color[] WaterRamp { get; set; } =
    {
        new("#1C5CB0"),
        new("#2672C6"),
        new("#348FD8"),
        new("#4DAEE5"),
        new("#6ECAEC"),
        new("#97DFF0"),
    };

    [Export] public Color Foam { get; set; } = new("#F4FDFF");

    [Export] public Color CalmRing { get; set; } = new("#ADE4DD");

    [Export] public Color Sand { get; set; } = new("#E2D0A2");

    [Export] public Color SeabedDark { get; set; } = new("#6E7A50");
}
