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
            Shape = new BoxShape3D { Size = new Vector3(0.14f, 0.14f, 0.22f) },
        });

        AddChild(BuildGrapnel());

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
        anchor.AddChild(BuildGrapnel());
        host.AddChild(anchor);
        anchor.GlobalTransform = new Transform3D(GlobalBasis, Point);

        if (lifetime != null && IsInstanceValid(lifetime))
            lifetime.TreeExiting += () => { if (IsInstanceValid(anchor)) anchor.QueueFree(); };

        return anchor;
    }

    private static StandardMaterial3D BuildMetal() => new()
    {
        AlbedoColor = new Color(0.35f, 0.33f, 0.3f),
        Roughness = 0.7f,
        Metallic = 0.3f,
    };

    private static MeshInstance3D Segment(Vector3 center, Vector3 dir, float length, float thick, Material mat)
    {
        Vector3 y = dir.Normalized();
        Vector3 reference = Mathf.Abs(y.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
        Vector3 x = reference.Cross(y).Normalized();
        var basis = new Basis(x, y, x.Cross(y)).Orthonormalized();

        return new MeshInstance3D
        {
            Transform = new Transform3D(basis, center),
            Mesh = new BoxMesh { Size = new Vector3(thick, length, thick) },
            MaterialOverride = mat,
        };
    }

    internal static Node3D BuildGrapnel()
    {
        Material metal = BuildMetal();
        var root = new Node3D { Name = "Grapnel" };

        var shaft = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.016f, BottomRadius = 0.024f, Height = 0.18f },
            MaterialOverride = metal,
        };
        shaft.RotationDegrees = new Vector3(90f, 0f, 0f);
        root.AddChild(shaft);

        var collar = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.032f, BottomRadius = 0.032f, Height = 0.05f },
            MaterialOverride = metal,
            Position = new Vector3(0f, 0f, -0.045f),
        };
        collar.RotationDegrees = new Vector3(90f, 0f, 0f);
        root.AddChild(collar);

        var eye = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.01f, OuterRadius = 0.026f },
            MaterialOverride = metal,
            Position = new Vector3(0f, 0f, 0.085f),
        };
        eye.RotationDegrees = new Vector3(90f, 0f, 0f);
        root.AddChild(eye);

        for (int k = 0; k < 4; k++)
        {
            float phi = Mathf.Tau * k / 4f + Mathf.Pi * 0.25f;
            Vector3 r = new(Mathf.Cos(phi), Mathf.Sin(phi), 0f);
            Vector3 fwd = -Vector3.Back;

            Vector3 d1 = (r * 0.5f + fwd * 0.866f).Normalized();
            Vector3 p0 = new(0f, 0f, -0.06f);
            float l1 = 0.09f;
            Vector3 p1 = p0 + d1 * l1;

            Vector3 d2 = (r * 0.94f + fwd * 0.34f).Normalized();
            float l2 = 0.08f;
            Vector3 p2 = p1 + d2 * l2;

            root.AddChild(Segment((p0 + p1) * 0.5f, d1, l1, 0.028f, metal));
            root.AddChild(Segment((p1 + p2) * 0.5f, d2, l2, 0.017f, metal));
        }

        return root;
    }

    private void OnBodyEntered(Node body)
    {
        if (Landed || body is not Node3D node) return;

        Landed = true;
        Target = node;
        Point = GlobalPosition;
        SetDeferred(PropertyName.Freeze, true);
    }
}
