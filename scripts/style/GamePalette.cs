using Godot;

namespace Knotical.Style;

[GlobalClass]
public partial class GamePalette : Resource
{
    [Export] public Color[] WaterRamp { get; set; } =
    {
        new("#3F9889"),
        new("#46AA9A"),
        new("#50C0B2"),
        new("#5AD4C6"),
        new("#62ECE2"),
        new("#9DECDB"),
    };

    [Export] public Color Foam { get; set; } = new("#C3F8F7");

    [Export] public Color CalmRing { get; set; } = new("#ADE4DD");

    [Export] public Color Sand { get; set; } = new("#E2D0A2");

    [Export] public Color SeabedDark { get; set; } = new("#6E7A50");
}
