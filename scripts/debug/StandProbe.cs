using Godot;

namespace Knotical.Debug;

[GlobalClass]
public partial class StandProbe : Node3D
{
    private readonly float[] _stations = { -4f, -2f, 0f, 2.6f, 4f };
    private RigidBody3D[] _pucks;
    private RigidBody3D _boat;
    private RigidBody3D _player;
    private float _clock;
    private float _tick;
    private readonly float[] _minRel = { 99f, 99f, 99f, 99f, 99f };

    public override void _Ready()
    {
        _boat = FindBoat(GetTree().Root, "BoatSloop");
        if (_boat == null) { GD.Print("standprobe: no BoatSloop"); GetTree().Quit(); return; }

        var scene = GD.Load<PackedScene>("res://character.tscn");
        _player = scene.Instantiate<RigidBody3D>();
        AddChild(_player);
        _player.GlobalPosition = _boat.ToGlobal(new Vector3(0.6f, 2.2f, -1f));

        _pucks = new RigidBody3D[_stations.Length];
        for (int i = 0; i < _stations.Length; i++)
        {
            var puck = new RigidBody3D { Mass = 70f, CanSleep = false, ContactMonitor = true, MaxContactsReported = 4 };
            puck.AddChild(new CollisionShape3D
            {
                Shape = new CapsuleShape3D { Radius = 0.21f, Height = 1.19f },
                Position = new Vector3(0f, 0.595f, 0f)
            });
            AddChild(puck);
            puck.GlobalPosition = _boat.ToGlobal(new Vector3(0f, 2.2f, _stations[i]));
            _pucks[i] = puck;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_boat == null) return;
        _clock += (float)delta;
        _tick += (float)delta;

        for (int i = 0; i < _pucks.Length; i++)
        {
            float rel = _boat.ToLocal(_pucks[i].GlobalPosition).Y;
            if (_clock > 1.5f && rel < _minRel[i]) _minRel[i] = rel;
        }

        if (_tick >= 1f)
        {
            _tick = 0f;
            string row = "";
            for (int i = 0; i < _pucks.Length; i++)
            {
                Vector3 local = _boat.ToLocal(_pucks[i].GlobalPosition);
                row += $" z{_stations[i],4:0.0}:y={local.Y,5:0.00} drift={new Vector2(local.X, local.Z - _stations[i]).Length():0.00} c={_pucks[i].GetContactCount()}";
            }
            Vector3 pl = _boat.ToLocal(_player.GlobalPosition);
            GD.Print($"standprobe t={_clock,4:0.0}{row} | player y={pl.Y,5:0.00} x={pl.X,5:0.00} z={pl.Z,5:0.00} c={_player.GetContactCount()}");
        }

        if (_clock < 12f) return;

        string summary = "";
        for (int i = 0; i < _pucks.Length; i++) summary += $" z{_stations[i],4:0.0}:min={_minRel[i],5:0.00}";
        GD.Print($"standprobe SUMMARY (deck=1.05, hold floor=-0.25, fell-through < -0.4):{summary}");
        GetTree().Quit();
    }

    private static RigidBody3D FindBoat(Node node, string name)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is RigidBody3D body && child.Name == name) return body;
            RigidBody3D found = FindBoat(child, name);
            if (found != null) return found;
        }

        return null;
    }
}
