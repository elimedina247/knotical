using Godot;

namespace Knotical.Rigging;

[GlobalClass]
public partial class GrappleHook : RigidBody3D
{
    public bool Landed { get; private set; }
    public Node3D Target { get; private set; }
    public Vector3 Point { get; private set; }

    public override void _Ready()
    {
        Mass = 0.4f;
        GravityScale = 0.4f;
        CollisionLayer = 0;
        CollisionMask = 1 | 2 | 8;
        ContactMonitor = true;
        MaxContactsReported = 4;
        ContinuousCd = true;

        AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(0.08f, 0.08f, 0.16f) },
        });

        AddChild(BuildMesh());

        BodyEntered += OnBodyEntered;
    }

    public void Launch(Vector3 from, Vector3 velocity, PhysicsBody3D shooter)
    {
        GlobalPosition = from;
        if (velocity.LengthSquared() > 1e-6f)
            LookAt(from + velocity, Vector3.Up.Cross(velocity).LengthSquared() > 1e-6f ? Vector3.Up : Vector3.Right);
        LinearVelocity = velocity;
        if (shooter != null) AddCollisionExceptionWith(shooter);
    }

    public Node3D Plant(Node lifetime)
    {
        Node host = Target != null && IsInstanceValid(Target) ? Target : GetTree().CurrentScene;
        if (host == null) return null;

        var anchor = new Node3D { Name = "GrappleAnchor" };
        anchor.AddChild(BuildMesh());
        host.AddChild(anchor);
        anchor.GlobalTransform = new Transform3D(GlobalBasis, Point);

        if (lifetime != null && IsInstanceValid(lifetime))
            lifetime.TreeExiting += () => { if (IsInstanceValid(anchor)) anchor.QueueFree(); };

        return anchor;
    }

    private static MeshInstance3D BuildMesh() => new()
    {
        Mesh = new BoxMesh { Size = new Vector3(0.08f, 0.08f, 0.16f) },
        MaterialOverride = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.35f, 0.33f, 0.3f),
            Roughness = 0.7f,
        },
    };

    private void OnBodyEntered(Node body)
    {
        if (Landed || body is not Node3D node) return;

        Landed = true;
        Target = node;
        Point = GlobalPosition;
        SetDeferred(PropertyName.Freeze, true);
    }
}
