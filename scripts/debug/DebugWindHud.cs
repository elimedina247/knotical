using Godot;
using Knotical.Boat;
using Knotical.Sky;
using Knotical.Weather;
using OceanSystem = Knotical.Ocean.Ocean;

namespace Knotical.Debug;

/// <summary>
/// Throwaway readout for driving the weather by hand.
///
/// Exists to make the wind-to-water chain visible: change the wind, watch how long the sea
/// takes to answer, and watch which way the crests swing. The lag between the two numbers
/// is the interesting part — if wave height tracks wind speed instantly, the development
/// lag is broken.
///
/// Arrow keys steer, T fast-forwards the weather, 0 hands control back.
/// </summary>
[GlobalClass]
public partial class DebugWindHud : CanvasLayer
{
    /// <summary>Metres per second added or removed per second of held key.</summary>
    [Export] public float SpeedStepPerSecond { get; set; } = 6f;

    /// <summary>Degrees of heading bias applied per second of held key.</summary>
    [Export] public float VeerStepPerSecond { get; set; } = 45f;

    /// <summary>Weather time multiplier while T is held.</summary>
    [Export] public float FastForwardScale { get; set; } = 25f;

    /// <summary>Day-cycle time multiplier while Y is held.</summary>
    [Export] public float DayFastForwardScale { get; set; } = 60f;

    private static readonly string[] CompassPoints =
    {
        "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW",
        "W", "WNW", "NW", "NNW", "N", "NNE", "NE", "ENE"
    };

    [Export] public BoatHull Boat { get; set; }

    [Export] public Rudder Blade { get; set; }

    [Export] public Helm Wheel { get; set; }

    private Sail[] _canvas = System.Array.Empty<Sail>();

    private Label _label;

    public override void _Ready()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (!arg.StartsWith("--wind=")) continue;
            if (!float.TryParse(arg["--wind=".Length..], out float forced)) continue;
            if (Wind.Instance?.Settings == null) continue;

            Wind.Instance.Settings.BaseSpeed = forced;
            GD.Print($"wind forced to {forced} m/s");
        }

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (!arg.StartsWith("--seacap=")) continue;
            if (!float.TryParse(arg["--seacap=".Length..], out float cap)) continue;
            if (OceanSystem.Instance?.Settings == null) continue;

            Knotical.Ocean.OceanSettings sea = OceanSystem.Instance.Settings;
            sea.MaxAmplitude = Mathf.Min(sea.MaxAmplitude, cap);
            sea.MinAmplitude = Mathf.Min(sea.MinAmplitude, cap);
            OceanSystem.Instance.Rebuild();
            GD.Print($"sea capped at {cap} m");
        }

        Boat ??= FindBoat(GetTree().Root);

        var found = new System.Collections.Generic.List<Sail>();
        Gather(GetTree().Root, found);
        _canvas = found.ToArray();
        Blade ??= Find<Rudder>(GetTree().Root);
        Wheel ??= Find<Helm>(GetTree().Root);

        _label = new Label
        {
            Position = new Vector2(16f, 12f),
            Modulate = new Color(1f, 1f, 1f, 0.92f)
        };

        _label.AddThemeFontSizeOverride("font_size", 15);
        _label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
        _label.AddThemeConstantOverride("outline_size", 4);

        AddChild(_label);
    }

    public override void _Process(double delta)
    {
        Wind wind = Wind.Instance;
        OceanSystem ocean = OceanSystem.Instance;

        if (wind == null || ocean == null)
        {
            _label.Text = "Wind / Ocean autoload missing";
            return;
        }

        ReadInput(wind, (float)delta);

        float headingDeg = Mathf.PosMod(Mathf.RadToDeg(wind.DirectionRad), 360f);

        string clock = "";
        DayCycle cycle = DayCycle.Instance;
        if (cycle != null)
        {
            float hours = Mathf.PosMod(6f + cycle.Phase * 24f, 24f);
            int hh = (int)hours;
            int mm = (int)((hours - hh) * 60f);
            clock = $"TIME   {hh:00}:{mm:00}  ({(cycle.IsDay ? "day" : "night")})\n\n";
        }

        _label.Text =
            Sailing(wind, (float)delta) +
            Canvas() +
            $"WIND   {wind.Speed,5:0.0} m/s  ({wind.Speed * 1.944f,4:0.0} kn)   " +
            $"Force {wind.BeaufortForce} — {wind.BeaufortName}\n" +
            $"       {headingDeg,5:0}°  {Compass(headingDeg)}\n" +
            $"\n" +
            $"SEA    H  {ocean.SignificantHeight,5:0.00} m   peak {ocean.PeakWavelength,5:0} m   " +
            $"waves {ocean.Waves.Length}   seed {ocean.Settings.Seed}\n" +
            $"\n" +
            clock +
            $"[Up/Down] speed   [Left/Right] veer   [T] wind ff   [Y] day ff   [0] reset";
    }

    private void ReadInput(Wind wind, float delta)
    {
        DayCycle cycle = DayCycle.Instance;
        if (cycle != null)
        {
            cycle.DebugTimeScale = Input.IsPhysicalKeyPressed(Key.Y) ? DayFastForwardScale : 1f;
            if (Input.IsPhysicalKeyPressed(Key.Key0)) cycle.DebugTimeScale = 1f;
        }

        if (Input.IsPhysicalKeyPressed(Key.Up)) wind.SpeedBias += SpeedStepPerSecond * delta;
        if (Input.IsPhysicalKeyPressed(Key.Down)) wind.SpeedBias -= SpeedStepPerSecond * delta;
        if (Input.IsPhysicalKeyPressed(Key.Left)) wind.HeadingBiasDeg -= VeerStepPerSecond * delta;
        if (Input.IsPhysicalKeyPressed(Key.Right)) wind.HeadingBiasDeg += VeerStepPerSecond * delta;

        wind.DebugTimeScale = Input.IsPhysicalKeyPressed(Key.T) ? FastForwardScale : 1f;

        if (Input.IsPhysicalKeyPressed(Key.Key0)) wind.ResetOverrides();

        // Speed is clamped at zero on read, so let the bias go no further negative than it
        // needs to — otherwise winding it down parks it a long way below zero and the key
        // appears dead on the way back up.
        wind.SpeedBias = Mathf.Max(wind.SpeedBias, -wind.Settings.BaseSpeed * 2f);
    }

    private Vector3 _rigMean;
    private Vector3 _hullMean;

    private string Sailing(Wind wind, float delta)
    {
        if (Boat == null || !IsInstanceValid(Boat)) return "";

        float blend = 1f - Mathf.Exp(-delta / 2f);
        _rigMean = _rigMean.Lerp(Boat.RigForce, blend);
        _hullMean = _hullMean.Lerp(Boat.HullForce, blend);

        Vector3 velocity = Boat.LinearVelocity;
        float speed = new Vector2(velocity.X, velocity.Z).Length();

        Basis basis = Boat.GlobalBasis.Orthonormalized();
        Vector3 forward = -basis.Z;
        float heading = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(forward.X, -forward.Z)), 360f);

        Vector2 gust = wind.Velocity;
        Vector3 local = basis.Inverse() * new Vector3(gust.X, 0f, gust.Y);
        float off = 180f - Mathf.Abs(Mathf.RadToDeg(Mathf.Atan2(local.X, -local.Z)));

        string steering = Blade == null
            ? "no rudder found\n"
            : $"       helm {(Wheel != null ? Wheel.Steering : Blade.Steering),+5:0.00}   " +
              $"blade {Mathf.RadToDeg(Blade.Angle),4:0}°   " +
              $"flow {Blade.Flow,4:0.0} m/s   " +
              $"wet {Blade.Immersion,4:0.00}   " +
              $"yaw {Mathf.RadToDeg(Boat.AngularVelocity.Y),5:0.0}°/s" +
              (Wheel != null && Wheel.Rudder == null ? "   ** HELM NOT LINKED **" : "") + "\n";

        Vector3 rig = _rigMean;
        Vector3 hull = _hullMean;

        Vector3 spin = basis.Inverse() * Boat.AngularVelocity;

        string forces =
            $"       rig {rig.Dot(forward) / 1e6f,6:0.00} MN fwd   drag {hull.Dot(forward) / 1e6f,6:0.00} MN   " +
            $"foils {Boat.FoilCount}\n" +
            $"       pitch {Mathf.RadToDeg(spin.X),5:0.0}°/s   roll {Mathf.RadToDeg(spin.Z),5:0.0}°/s   " +
            $"heave {Boat.LinearVelocity.Y,5:0.0} m/s\n";

        return $"BOAT   {speed,5:0.0} m/s  ({speed * 1.944f,4:0.0} kn)   heading {heading,3:0}°\n" +
               $"       wind {off,5:0}° off the bow — {PointOfSail(off)}\n" +
               steering + forces + "\n";
    }

    private static string PointOfSail(float off) =>
        off < 25f ? "in irons, no drive"
        : off < 45f ? "close hauled, weak"
        : off < 70f ? "close hauled"
        : off < 110f ? "beam reach"
        : off < 150f ? "broad reach"
        : "running — fastest";

    private static void Gather(Node node, System.Collections.Generic.List<Sail> into)
    {
        if (node is Sail sail) into.Add(sail);
        foreach (Node child in node.GetChildren()) Gather(child, into);
    }

    private string Canvas()
    {
        if (_canvas.Length == 0) return "";

        float total = 0f;
        string each = "";

        foreach (Sail sail in _canvas)
        {
            total += sail.Deployment;
            each += $"  {sail.Name}{sail.Deployment * 100f,4:0}%";
        }

        return $"CANVAS {total / _canvas.Length * 100f,4:0}% set  {each}\n\n";
    }

    private static BoatHull FindBoat(Node node) => Find<BoatHull>(node);

    private static T Find<T>(Node node) where T : class
    {
        if (node is T match) return match;

        foreach (Node child in node.GetChildren())
        {
            T found = Find<T>(child);
            if (found != null) return found;
        }

        return null;
    }

    private static string Compass(float headingDeg)
    {
        int index = Mathf.RoundToInt(headingDeg / 22.5f) % CompassPoints.Length;
        return CompassPoints[index];
    }
}
