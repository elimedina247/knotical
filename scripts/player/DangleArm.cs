using Godot;

namespace Knotical.Player;

[GlobalClass]
public partial class DangleArm : Node3D, IHand
{
    [Export(PropertyHint.Range, "0.1,3,0.01")] public float Length { get; set; } = 0.5f;

    [Export(PropertyHint.Range, "0,60,0.5")] public float Gravity { get; set; } = 16f;

    [Export(PropertyHint.Range, "0.01,1,0.01")] public float Damping { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0.1,1,0.01")] public float Stiffness { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "0,2,0.01")] public float SpineLowY { get; set; } = 0.62f;

    [Export(PropertyHint.Range, "0,2,0.01")] public float SpineHighY { get; set; } = 0.98f;

    [Export(PropertyHint.Range, "0,1,0.005")] public float Clearance { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0.02,0.6,0.01")] public float ReachTime { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0.02,0.6,0.01")] public float RelaxTime { get; set; } = 0.2f;

    [Export(PropertyHint.Range, "0.01,1,0.01")] public float RigidDamping { get; set; } = 0.04f;

    [Export(PropertyHint.Range, "0,2,0.01")] public float ElbowOut { get; set; } = 0.6f;

    private readonly LimbChain _chain = new();
    private MeshInstance3D[] _links = System.Array.Empty<MeshInstance3D>();
    private MeshInstance3D[] _joints = System.Array.Empty<MeshInstance3D>();
    private float[] _rest = System.Array.Empty<float>();
    private Node3D _torso;
    private float _side = 1f;
    private float _rigid;
    private bool _gripping;
    private Vector3 _target;

    public bool IsGripping => _gripping;

    public Vector3 Tip => _chain.Tip;

    public Vector3 Root => GlobalPosition;

    public float Rigidity => _rigid;

    public void Grip(Vector3 globalTarget)
    {
        _gripping = true;
        _target = globalTarget;
    }

    public void Release()
    {
        _gripping = false;
    }

    public void Resize(float length)
    {
        Length = length;
        if (_links.Length > 0) _chain.Build(_links.Length, Length, GlobalPosition, Vector3.Down);
    }

    public override void _Ready()
    {
        _torso = GetParentOrNull<Node3D>();
        _side = Position.X < 0f ? -1f : 1f;
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

        float dt = (float)delta;
        _rigid = _gripping
            ? Mathf.Clamp(_rigid + dt / Mathf.Max(ReachTime, 1e-3f), 0f, 1f)
            : Mathf.Clamp(_rigid - dt / Mathf.Max(RelaxTime, 1e-3f), 0f, 1f);

        _chain.Gravity = Gravity * (1f - _rigid);
        _chain.Damping = Mathf.Lerp(Damping, RigidDamping, _rigid);
        _chain.Stiffness = Mathf.Lerp(Stiffness, 1f, _rigid);

        _chain.Straighten(_rigid);

        if (_torso != null)
        {
            Transform3D body = _torso.GlobalTransform;
            _chain.PushOutside(
                body * new Vector3(0f, SpineLowY, 0f),
                body * new Vector3(0f, SpineHighY, 0f),
                Clearance * (1f - _rigid));
        }

        _chain.Step(dt, GlobalPosition, _gripping, _target);

        if (_rigid > 0f && _chain.Points.Length == 3)
        {
            _chain.Points[1] = _chain.Points[1].Lerp(Elbow(_chain.Points[0], _chain.Points[2]), _rigid);
            _chain.Previous[1] = _chain.Points[1];
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

    private Vector3 Elbow(Vector3 root, Vector3 tip)
    {
        Vector3 span = tip - root;
        float distance = span.Length();
        float half = Length * 0.5f;
        Vector3 pole = Pole();

        if (distance < 1e-4f) return root + pole * half;

        Vector3 direction = span / distance;
        if (distance >= Length) return root + direction * (distance * 0.5f);

        float along = distance * 0.5f;
        float outward = Mathf.Sqrt(Mathf.Max(half * half - along * along, 0f));

        pole -= direction * pole.Dot(direction);
        if (pole.LengthSquared() < 1e-6f) pole = direction.Cross(Vector3.Up);
        if (pole.LengthSquared() < 1e-6f) pole = Vector3.Down;

        return root + direction * along + pole.Normalized() * outward;
    }

    private Vector3 Pole()
    {
        Basis body = _torso?.GlobalBasis ?? Basis.Identity;
        return (body * new Vector3(_side * ElbowOut, -1f, 0f)).Normalized();
    }

    private static Basis Aim(Vector3 direction)
    {
        Vector3 y = direction.Normalized();
        Vector3 reference = Mathf.Abs(y.Dot(Vector3.Forward)) > 0.99f ? Vector3.Right : Vector3.Forward;
        Vector3 x = reference.Cross(y).Normalized();
        return new Basis(x, y, x.Cross(y));
    }
}
