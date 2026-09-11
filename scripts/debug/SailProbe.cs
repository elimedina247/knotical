using Godot;
using Knotical.Boat;
using Knotical.Weather;

namespace Knotical.Debug;

public partial class SailProbe : Node
{
    [Export(PropertyHint.Range, "1,60,1")]
    public float SettleSeconds { get; set; } = 14f;

    [Export(PropertyHint.Range, "5,120,1")]
    public float SampleSeconds { get; set; } = 14f;

    private SailController _boat;
    private double _clock;
    private double _speedSum;
    private double _heelSum;
    private int _samples;
    private bool _done;

    public override void _Ready()
    {
        float yaw = float.NaN;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--yaw=") && float.TryParse(arg["--yaw=".Length..], out float y))
            {
                yaw = y;
            }
        }

        foreach (Node child in GetParent().GetChildren())
        {
            if (child is SailController boat)
            {
                _boat = boat;
                break;
            }
        }

        if (_boat == null)
        {
            GD.Print("sailprobe FAIL: no SailController sibling");
            return;
        }

        _boat.Trace = true;
        foreach (Sail sail in FindSails(_boat))
        {
            sail.Deployment = 1f;
            sail.TargetDeployment = 1f;
        }
        if (!float.IsNaN(yaw))
        {
            _boat.Rotation = new Vector3(0f, Mathf.DegToRad(yaw), 0f);
        }

        var wind = Wind.Instance;
        if (wind != null && wind.Settings != null)
        {
            wind.Settings.Gustiness = 0f;
            wind.Settings.VeerDeg = 0f;
        }
    }

    private static System.Collections.Generic.IEnumerable<Sail> FindSails(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Sail sail) yield return sail;
            foreach (Sail nested in FindSails(child)) yield return nested;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_boat == null || _done) return;

        _clock += delta;
        if (_clock > SettleSeconds && _clock <= SettleSeconds + SampleSeconds)
        {
            Basis level = _boat.GlobalBasis.Orthonormalized();
            var ahead = new Vector3(-level.Z.X, 0f, -level.Z.Z);
            if (ahead.LengthSquared() > 0.0001f)
            {
                _speedSum += _boat.LinearVelocity.Dot(ahead.Normalized());
            }
            _heelSum += Mathf.Abs(Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(level.X.Y, -1f, 1f))));
            _samples++;
        }
        else if (_clock > SettleSeconds + SampleSeconds)
        {
            _done = true;
            Basis level = _boat.GlobalBasis.Orthonormalized();
            float off = _boat.OffWindDeg(level);
            float spd = _samples > 0 ? (float)(_speedSum / _samples) : 0f;
            float heel = _samples > 0 ? (float)(_heelSum / _samples) : 0f;
            GD.Print($"sailprobe off={off:0} hdwy={spd:0.00} heel={heel:0.0} " +
                $"y={_boat.GlobalPosition.Y:0.00} pol={_boat.Polar(level):0.00}");
            GetTree().Quit();
        }
    }
}
