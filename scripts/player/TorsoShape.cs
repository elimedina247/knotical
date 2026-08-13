using Godot;

namespace Knotical.Player;

[GlobalClass]
public partial class TorsoShape : Node3D
{
    private float _radius = 0.21f;
    private float _standHeight = 1.19f;
    private float _torsoBottom = 0.409f;
    private float _torsoTop = 1.191f;
    private float _shoulderHeight = 0.9f;
    private float _shoulderOffset = 0.23f;
    private float _armLength = 0.5f;
    private float _armRadius = 0.04f;

    [Export(PropertyHint.Range, "0.08,0.6,0.005")]
    public float Radius { get => _radius; set { _radius = value; Apply(); } }

    [Export(PropertyHint.Range, "0.6,2.4,0.01")]
    public float StandHeight { get => _standHeight; set { _standHeight = value; Apply(); } }

    [Export(PropertyHint.Range, "0,1.5,0.005")]
    public float TorsoBottom { get => _torsoBottom; set { _torsoBottom = value; Apply(); } }

    [Export(PropertyHint.Range, "0.1,2.4,0.005")]
    public float TorsoTop { get => _torsoTop; set { _torsoTop = value; Apply(); } }

    [Export(PropertyHint.Range, "0.1,2.4,0.005")]
    public float ShoulderHeight { get => _shoulderHeight; set { _shoulderHeight = value; Apply(); } }

    [Export(PropertyHint.Range, "0,1,0.005")]
    public float ShoulderOffset { get => _shoulderOffset; set { _shoulderOffset = value; Apply(); } }

    [Export(PropertyHint.Range, "0.1,2,0.01")]
    public float ArmLength { get => _armLength; set { _armLength = value; Apply(); } }

    [Export(PropertyHint.Range, "0.01,0.2,0.002")]
    public float ArmRadius { get => _armRadius; set { _armRadius = value; Apply(); } }

    [Export] public CollisionShape3D Collider { get; set; }

    [Export] public MeshInstance3D Torso { get; set; }

    [Export] public DangleArm ArmLeft { get; set; }

    [Export] public DangleArm ArmRight { get; set; }

    [Export] public bool ApplyNow { get => false; set { if (value) Apply(); } }

    private bool _unshared;

    public Vector3 Shoulder(bool right) =>
        new(right ? _shoulderOffset : -_shoulderOffset, _shoulderHeight, 0f);

    public float SpineLow => _torsoBottom + _radius;

    public float SpineHigh => Mathf.Max(SpineLow, _torsoTop - _radius);

    public override void _Ready() => Apply();

    private void Apply()
    {
        if (!IsInsideTree()) return;

        Unshare();
        ShapeCollider();
        ShapeTorso();
        ShapeArm(ArmLeft, false);
        ShapeArm(ArmRight, true);
    }

    private void Unshare()
    {
        if (_unshared) return;
        _unshared = true;

        if (Collider?.Shape != null) Collider.Shape = (Shape3D)Collider.Shape.Duplicate();
        if (Torso?.Mesh != null) Torso.Mesh = (Mesh)Torso.Mesh.Duplicate();
    }

    private void ShapeCollider()
    {
        if (Collider?.Shape is not CapsuleShape3D capsule) return;

        capsule.Radius = _radius;
        capsule.Height = Mathf.Max(_standHeight, _radius * 2f);
        Collider.Position = new Vector3(0f, capsule.Height * 0.5f, 0f);
    }

    private void ShapeTorso()
    {
        if (Torso?.Mesh is not CapsuleMesh capsule) return;

        float span = Mathf.Max(_torsoTop - _torsoBottom, _radius * 2f);
        capsule.Radius = _radius;
        capsule.Height = span;
        Torso.Position = new Vector3(0f, (_torsoTop + _torsoBottom) * 0.5f, 0f);
    }

    private void ShapeArm(DangleArm arm, bool right)
    {
        if (arm == null) return;

        arm.Position = Shoulder(right);
        arm.SpineLowY = SpineLow;
        arm.SpineHighY = SpineHigh;
        arm.Clearance = _radius + _armRadius;
        arm.Resize(_armLength);
    }
}
