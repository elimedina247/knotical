using Godot;

namespace Knotical.Player;

[GlobalClass]
public partial class DangleArm : Node3D
{
    [Export(PropertyHint.Range, "0.1,3,0.01")] public float Length { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,60,0.5")] public float Gravity { get; set; } = 16f;

    [Export(PropertyHint.Range, "0.01,1,0.01")] public float Damping { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0.1,1,0.01")] public float Stiffness { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "1,2,0.01")] public float MaxStretch { get; set; } = 1.25f;

    [Export(PropertyHint.Range, "0,2,0.01")] public float SpineLowY { get; set; } = 0.62f;

    [Export(PropertyHint.Range, "0,2,0.01")] public float SpineHighY { get; set; } = 0.98f;

    [Export(PropertyHint.Range, "0,1,0.005")] public float Clearance { get; set; } = 0.25f;

    private readonly LimbChain _chain = new();
    private MeshInstance3D[] _links = System.Array.Empty<MeshInstance3D>();
    private MeshInstance3D[] _joints = System.Array.Empty<MeshInstance3D>();
    private float[] _rest = System.Array.Empty<float>();
    private Node3D _torso;
    private bool _pinned;
    private Vector3 _target;

    public bool IsGripping => _pinned;

    public Vector3 Tip => _chain.Tip;

    public void Grip(Vector3 globalTarget)
    {
        _pinned = true;
        _target = globalTarget;
    }

    public void Release() => _pinned = false;

    public override void _Ready()
    {
        _torso = GetParentOrNull<Node3D>();
        Collect();
        _chain.Build(_links.Length, Length, GlobalPosition, Vector3.Down);
    }

    private void Collect()
    {
        var links = new System.Collections.Generic.List<MeshInstance3D>();
        var joints = new System.Collections.Generic.List<MeshInstance3D>();

        foreach (Node child in GetChildren())
        {
            if (child is not MeshInstance3D mesh) continue;
            if (mesh.Mesh is SphereMesh) joints.Add(mesh);
            else links.Add(mesh);
        }

        _links = links.ToArray();
        _joints = joints.ToArray();
        _rest = new float[_links.Length];

        for (int i = 0; i < _links.Length; i++)
        {
            float height = _links[i].GetAabb().Size.Y;
            _rest[i] = height > 1e-4f ? height : 1f;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_links.Length == 0) return;

        _chain.Gravity = Gravity;
        _chain.Damping = Damping;
        _chain.Stiffness = Stiffness;
        _chain.MaxStretch = MaxStretch;

        _chain.Step((float)delta, GlobalPosition, _pinned, _target);

        if (_torso != null)
        {
            Transform3D body = _torso.GlobalTransform;
            _chain.PushOutside(
                body * new Vector3(0f, SpineLowY, 0f),
                body * new Vector3(0f, SpineHighY, 0f),
                Clearance);
        }

        for (int i = 0; i < _links.Length && i < _chain.Links; i++)
        {
            Vector3 a = _chain.Points[i];
            Vector3 b = _chain.Points[i + 1];
            Vector3 span = b - a;
            float length = span.Length();
            if (length < 1e-5f) continue;

            Basis aim = Aim(span);
            var stretched = new Basis(aim.X, aim.Y * (length / _rest[i]), aim.Z);
            _links[i].GlobalTransform = new Transform3D(stretched, (a + b) * 0.5f);
        }

        for (int i = 0; i < _joints.Length && i < _chain.Links - 1; i++)
        {
            _joints[i].GlobalPosition = _chain.Points[i + 1];
        }
    }

    private static Basis Aim(Vector3 direction)
    {
        Vector3 y = direction.Normalized();
        Vector3 reference = Mathf.Abs(y.Dot(Vector3.Forward)) > 0.99f ? Vector3.Right : Vector3.Forward;
        Vector3 x = reference.Cross(y).Normalized();
        return new Basis(x, y, x.Cross(y));
    }
}
