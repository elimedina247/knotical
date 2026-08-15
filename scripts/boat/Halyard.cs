using Godot;
using Knotical.Player;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class Halyard : MeshInstance3D, IGrabbable
{
    private float _drop = 2.4f;
    private float _radius = 0.035f;
    private int _sides = 6;

    [Export] public NodePath SailPath { get; set; }

    public Sail Sail { get; set; }

    [Export(PropertyHint.Range, "0.3,8,0.05")]
    public float Drop { get => _drop; set { _drop = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.01,0.15,0.005")]
    public float Radius { get => _radius; set { _radius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "3,16,1")]
    public int Sides { get => _sides; set { _sides = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float GripRadius { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "0.02,1,0.01")]
    public float HaulSpeed { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,0.8,0.01")]
    public float DeadZone { get; set; } = 0.25f;

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    private AnimatableBody3D _grip;
    private bool _held;

    public bool Anchors => false;

    public bool IsManned => _held;

    public float Setting => Sail?.TargetDeployment ?? 1f;

    public override void _Ready()
    {
        Sail ??= GetNodeOrNull<Sail>(SailPath);

        Rebuild();
        if (!Engine.IsEditorHint()) BuildGrip();

        if (Sail == null) GD.PushWarning($"{Name}: halyard has no sail to haul.");
        else GD.Print($"{Name}: hauls {Sail.Name}");
    }

    public Vector3 Attach(Vector3 globalPoint)
    {
        _held = true;
        return globalPoint;
    }

    public Vector3 Track(GrabHold hold, float dt)
    {
        if (Sail == null) return hold.Point;

        float lean = Mathf.Clamp(hold.Aim.Y, -1f, 1f);
        float past = Mathf.Abs(lean) - DeadZone;

        if (past > 0f)
        {
            float pull = Mathf.Sign(lean) * past / Mathf.Max(1f - DeadZone, 0.01f);
            Sail.TargetDeployment = Mathf.Clamp(Sail.TargetDeployment + pull * HaulSpeed * dt, 0f, 1f);
        }

        return hold.Point;
    }

    public void Detach()
    {
        _held = false;
    }

    private void BuildGrip()
    {
        _grip = new AnimatableBody3D
        {
            Name = "Grip",
            CollisionLayer = 2,
            CollisionMask = 0,
            SyncToPhysics = false
        };

        _grip.AddChild(new CollisionShape3D
        {
            Name = "Shape",
            Position = new Vector3(0f, -_drop * 0.5f, 0f),
            Shape = new CapsuleShape3D { Radius = GripRadius, Height = _drop + GripRadius }
        });

        AddChild(_grip);
    }

    private void Rebuild()
    {
        int sides = Mathf.Max(3, _sides);
        float radius = Mathf.Max(0.002f, _radius);
        float drop = Mathf.Max(0.05f, _drop);

        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < sides; i++)
        {
            float a = Mathf.Tau * i / sides;
            float b = Mathf.Tau * (i + 1) / sides;

            var na = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var nb = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));

            Vector3 topA = na * radius;
            Vector3 topB = nb * radius;
            Vector3 lowA = topA - Vector3.Up * drop;
            Vector3 lowB = topB - Vector3.Up * drop;

            tool.SetNormal(na);
            tool.AddVertex(topA);
            tool.SetNormal(nb);
            tool.AddVertex(topB);
            tool.AddVertex(lowB);

            tool.SetNormal(na);
            tool.AddVertex(topA);
            tool.SetNormal(nb);
            tool.AddVertex(lowB);
            tool.SetNormal(na);
            tool.AddVertex(lowA);
        }

        var mesh = new ArrayMesh();
        tool.Commit(mesh);
        Mesh = mesh;

        CustomAabb = new Aabb(
            new Vector3(-radius, -drop, -radius),
            new Vector3(radius * 2f, drop, radius * 2f));
    }
}
