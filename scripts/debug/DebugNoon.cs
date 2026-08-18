using Godot;
using Knotical.Sky;

namespace Knotical.Debug;

/// <summary>
/// Pins the day cycle so the debug scene is always lit the same way.
///
/// The clock is forced every frame rather than paused once, because DebugWindHud rewrites
/// the day's time scale each frame for its fast-forward key. Forcing the time outranks
/// that without having to coordinate the two.
///
/// TimeOfDay runs sunrise to sunset, so 0.5 is the sun at its highest whatever the
/// configured day length or day fraction.
/// </summary>
[GlobalClass]
public partial class DebugNoon : Node
{
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float TimeOfDay { get; set; } = 0.5f;

    public override void _Ready() => Pin();

    public override void _Process(double delta) => Pin();

    private void Pin()
    {
        DayCycle cycle = DayCycle.Instance;
        if (cycle?.Settings == null) return;

        float phase = Mathf.Clamp(TimeOfDay, 0f, 1f) * cycle.Settings.DayFraction;
        cycle.SetTime(phase * cycle.Settings.DayLengthMinutes * 60.0);
    }
}
