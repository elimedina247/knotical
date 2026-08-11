using Godot;

namespace Knotical.Sky;

[GlobalClass]
public partial class DayCycleSettings : Resource
{
    [Export(PropertyHint.Range, "1,120,0.5")]
    public float DayLengthMinutes { get; set; } = 20f;

    [Export(PropertyHint.Range, "0.1,0.9,0.01")]
    public float DayFraction { get; set; } = 0.7f;

    [Export(PropertyHint.Range, "0,60,1")]
    public float PathTiltDeg { get; set; } = 28f;

    [Export(PropertyHint.Range, "0,0.3,0.005")]
    public float MoonEnergy { get; set; } = 0.08f;

    [Export(PropertyHint.Range, "0.1,50,0.1")]
    public float TimeScale { get; set; } = 1f;
}
