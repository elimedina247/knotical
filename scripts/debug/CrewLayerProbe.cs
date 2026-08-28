using Godot;

namespace Knotical.Debug;

[GlobalClass]
public partial class CrewLayerProbe : Node3D
{
    private const float Deck = 40f;

    private RigidBody3D _character;
    private RigidBody3D _pusher;
    private RigidBody3D _walker;
    private RigidBody3D _faller;
    private RigidBody3D _standing;
    private RigidBody3D _crate;
    private Node3D _links;
    private float _clock;
    private float _crateStart;
    private float _linkSwing;

    public override void _Ready()
    {
        Ground(new Vector3(0f, Deck, 0f), new Vector3(60f, 0.5f, 60f));

        _character = GD.Load<PackedScene>("res://character.tscn").Instantiate<RigidBody3D>();
        AddChild(_character);
        _character.GlobalPosition = new Vector3(-6f, Deck + 1.5f, 0f);

        uint layer = _character.CollisionLayer;
        uint mask = _character.CollisionMask;

        _crate = GD.Load<PackedScene>("res://cargo_crate.tscn").Instantiate<RigidBody3D>();
        AddChild(_crate);
        _crate.GlobalPosition = new Vector3(4f, Deck + 0.3f, 0f);
        _crateStart = _crate.GlobalPosition.X;

        var chain = GD.Load<PackedScene>("res://scenes/dynamic_chain.tscn").Instantiate<Node3D>();
        AddChild(chain);
        chain.GlobalPosition = new Vector3(0f, Deck + 4f, 8f);
        _links = chain.GetNode<Node3D>("LinkContainer");

        _pusher = Crew("Pusher", new Vector3(0f, Deck + 0.9f, 0f), layer, mask);
        _walker = Crew("Walker", new Vector3(0f, Deck + 0.9f, 4f), layer, mask);
        _standing = Crew("Standing", new Vector3(-12f, Deck + 0.9f, 0f), layer, mask);
        _faller = Crew("Faller", new Vector3(-12f, Deck + 4f, 0f), layer, mask);

        GD.Print($"crewprobe: character layer={layer} mask={mask}");
    }

    private void Ground(Vector3 at, Vector3 size)
    {
        var body = new StaticBody3D { Name = "Deck" };
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        AddChild(body);
        body.GlobalPosition = at;
    }

    private RigidBody3D Crew(string name, Vector3 at, uint layer, uint mask)
    {
        var body = new RigidBody3D
        {
            Name = name,
            Mass = 70f,
            CanSleep = false,
            CollisionLayer = layer,
            CollisionMask = mask,
            AxisLockAngularX = true,
            AxisLockAngularY = true,
            AxisLockAngularZ = true,
        };

        body.AddChild(new CollisionShape3D
        {
            Shape = new CapsuleShape3D { Radius = 0.21f, Height = 1.19f },
            Position = new Vector3(0f, 0.595f, 0f),
        });

        AddChild(body);
        body.GlobalPosition = at;
        return body;
    }

    public override void _PhysicsProcess(double delta)
    {
        _clock += (float)delta;

        if (_clock > 1f && _clock < 5f)
        {
            _pusher.LinearVelocity = new Vector3(2.5f, _pusher.LinearVelocity.Y, 0f);
            _walker.LinearVelocity = new Vector3(0f, _walker.LinearVelocity.Y, 2.5f);
        }

        foreach (Node child in _links.GetChildren())
            if (child is Node3D link)
                _linkSwing = Mathf.Max(_linkSwing, Mathf.Abs(link.GlobalPosition.Z - 8f));

        if (_clock < 7f) return;

        bool characterStands = _character.GlobalPosition.Y > Deck;
        float gap = _crate.GlobalPosition.X - 0.5f - 0.21f - _pusher.GlobalPosition.X;
        bool crewMeetsCargo = Mathf.Abs(gap) < 0.05f || _crate.GlobalPosition.X - _crateStart > 0.05f;
        bool chainHit = _linkSwing > 0.15f;
        bool crewBlocked = _faller.GlobalPosition.Y > _standing.GlobalPosition.Y + 0.9f;
        bool linksSelfCollide = LinkGaps();

        GD.Print($"crewprobe: characterStands={characterStands} y={_character.GlobalPosition.Y:0.00}");
        GD.Print($"crewprobe: crewMeetsCargo={crewMeetsCargo} dx={_crate.GlobalPosition.X - _crateStart:0.00}" + $" pusherX={_pusher.GlobalPosition.X:0.00} pusherY={_pusher.GlobalPosition.Y:0.00}" + $" crateLayer={_crate.CollisionLayer} crateMask={_crate.CollisionMask} crateFrozen={_crate.Freeze} crateSleep={_crate.Sleeping}");
        GD.Print($"crewprobe: chainDeflected={chainHit} swing={_linkSwing:0.00}");
        GD.Print($"crewprobe: crewOnCrew={crewBlocked} faller={_faller.GlobalPosition.Y:0.00} standing={_standing.GlobalPosition.Y:0.00}");
        GD.Print($"crewprobe: linkSelfCollision={linksSelfCollide}");
        GD.Print($"crewprobe: {(characterStands && crewMeetsCargo && chainHit && crewBlocked ? "PASS" : "FAIL")}");
        GetTree().Quit();
    }

    private bool LinkGaps()
    {
        var seen = new Godot.Collections.Array<Vector3>();
        foreach (Node child in _links.GetChildren())
            if (child is Node3D link) seen.Add(link.GlobalPosition);

        for (int i = 0; i < seen.Count; i++)
            for (int j = i + 3; j < seen.Count; j++)
                if (seen[i].DistanceTo(seen[j]) < 0.09f) return false;

        return true;
    }
}
