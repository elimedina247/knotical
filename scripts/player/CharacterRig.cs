using Godot;

namespace Knotical.Player;

public enum Limb
{
    ArmLeft,
    ArmRight,
    LegLeft,
    LegRight,
}

[GlobalClass]
public partial class CharacterRig : Node3D
{
    private const int LimbCount = 4;

    private readonly CharacterForm _form = new();
    private readonly LimbChain[] _chains = { new(), new(), new(), new() };
    private readonly MeshInstance3D[][] _links = new MeshInstance3D[LimbCount][];
    private readonly MeshInstance3D[][] _joints = new MeshInstance3D[LimbCount][];
    private readonly Node3D[] _tips = new Node3D[LimbCount];
    private readonly bool[] _pinned = new bool[LimbCount];
    private readonly Vector3[] _targets = new Vector3[LimbCount];

    private Node3D _body;
    private Node3D _limbs;
    private StandardMaterial3D _skinMaterial;
    private StandardMaterial3D _clothMaterial;
    private StandardMaterial3D _shoeMaterial;
    private StandardMaterial3D _eyeMaterial;
    private StandardMaterial3D _pupilMaterial;
    private StandardMaterial3D _faceMaterial;
    private bool _built;

    [Export(PropertyHint.Range, "0.2,3,0.01")]
    public float LegLength { get => _form.LegLength; set { _form.LegLength = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,3,0.01")]
    public float ArmLength { get => _form.ArmLength; set { _form.ArmLength = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.1,2,0.01")]
    public float TorsoHeight { get => _form.TorsoHeight; set { _form.TorsoHeight = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.05,0.8,0.005")]
    public float TorsoRadius { get => _form.TorsoRadius; set { _form.TorsoRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,0.5,0.005")]
    public float NeckLength { get => _form.NeckLength; set { _form.NeckLength = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.05,0.8,0.005")]
    public float HeadRadius { get => _form.HeadRadius; set { _form.HeadRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.01,0.3,0.002")]
    public float LimbRadius { get => _form.LimbRadius; set { _form.LimbRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.02,0.4,0.002")]
    public float HandRadius { get => _form.HandRadius; set { _form.HandRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float ShoulderWidth { get => _form.ShoulderWidth; set { _form.ShoulderWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float HipWidth { get => _form.HipWidth; set { _form.HipWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,0.5,0.005")]
    public float ShoulderDrop { get => _form.ShoulderDrop; set { _form.ShoulderDrop = value; Rebuild(); } }

    [Export] public Vector3 FootSize { get => _form.FootSize; set { _form.FootSize = value; Rebuild(); } }

    [Export(PropertyHint.Range, "2,6,1")]
    public int LimbSegments { get => _form.Segments; set { _form.Segments = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,0.3,0.002")]
    public float EyeSpacing { get => _form.EyeSpacing; set { _form.EyeSpacing = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-0.2,0.2,0.002")]
    public float EyeHeight { get => _form.EyeHeight; set { _form.EyeHeight = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.005,0.2,0.002")]
    public float EyeRadius { get => _form.EyeRadius; set { _form.EyeRadius = value; Rebuild(); } }

    private Color _skin = new("#E8A33D");
    private Color _cloth = new("#3E6EA8");
    private Color _shoe = new("#2B2B31");

    [Export] public Color Skin { get => _skin; set { _skin = value; Rebuild(); } }
    [Export] public Color Cloth { get => _cloth; set { _cloth = value; Rebuild(); } }
    [Export] public Color Shoe { get => _shoe; set { _shoe = value; Rebuild(); } }

    private Texture2D _face;
    private PackedScene _hat;

    [Export] public Texture2D Face { get => _face; set { _face = value; Rebuild(); } }
    [Export] public PackedScene Hat { get => _hat; set { _hat = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,60,0.5")]
    public float DangleGravity { get; set; } = 20f;

    [Export(PropertyHint.Range, "0.01,1,0.01")]
    public float DangleDamping { get; set; } = 0.2f;

    [Export(PropertyHint.Range, "0.1,1,0.01")]
    public float DangleStiffness { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "1,2,0.01")]
    public float MaxStretch { get; set; } = 1.3f;

    [Export] public bool PlantFeet { get; set; } = true;

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public Node3D HatMount { get; private set; }

    public override void _Ready() => Rebuild();

    public void Grip(Limb limb, Vector3 globalTarget)
    {
        _pinned[(int)limb] = true;
        _targets[(int)limb] = globalTarget;
    }

    public void Release(Limb limb) => _pinned[(int)limb] = false;

    public bool IsGripping(Limb limb) => _pinned[(int)limb];

    public Vector3 TipPosition(Limb limb) => _chains[(int)limb].Tip;

    private void Rebuild()
    {
        if (!IsInsideTree()) return;

        Discard(ref _body);
        Discard(ref _limbs);

        BuildMaterials();
        BuildBody();
        BuildLimbs();

        _built = true;
        ResetPose();
    }

    private void Discard(ref Node3D node)
    {
        if (node == null) return;
        RemoveChild(node);
        node.QueueFree();
        node = null;
    }

    private void BuildMaterials()
    {
        _skinMaterial = new StandardMaterial3D { AlbedoColor = _skin, Roughness = 0.95f };
        _clothMaterial = new StandardMaterial3D { AlbedoColor = _cloth, Roughness = 0.95f };
        _shoeMaterial = new StandardMaterial3D { AlbedoColor = _shoe, Roughness = 0.8f };
        _eyeMaterial = new StandardMaterial3D { AlbedoColor = Colors.White, Roughness = 0.6f };
        _pupilMaterial = new StandardMaterial3D { AlbedoColor = new Color("#15161C"), Roughness = 0.5f };

        _faceMaterial = _face == null ? null : new StandardMaterial3D
        {
            AlbedoTexture = _face,
            Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor,
            AlphaScissorThreshold = 0.5f,
            Roughness = 1f,
        };
    }

    private void BuildBody()
    {
        _body = new Node3D { Name = "Body" };
        AddChild(_body);

        var torso = new MeshInstance3D
        {
            Name = "Torso",
            Mesh = new CapsuleMesh
            {
                Radius = _form.TorsoRadius,
                Height = Mathf.Max(_form.TorsoHeight, _form.TorsoRadius * 2f),
                RadialSegments = 14,
                Rings = 4,
            },
            MaterialOverride = _clothMaterial,
            Position = new Vector3(0f, _form.TorsoCenterY, 0f),
        };
        _body.AddChild(torso);

        var head = new Node3D { Name = "Head", Position = new Vector3(0f, _form.HeadCenterY, 0f) };
        _body.AddChild(head);

        var skull = new MeshInstance3D
        {
            Name = "Skull",
            Mesh = Ball(_form.HeadRadius, 16, 8),
            MaterialOverride = _skinMaterial,
        };
        head.AddChild(skull);

        if (_faceMaterial != null) BuildFacePlate(head);
        else BuildEyes(head);

        HatMount = new Node3D { Name = "HatMount", Position = new Vector3(0f, _form.HeadRadius * 0.82f, 0f) };
        head.AddChild(HatMount);

        if (_hat != null && _hat.Instantiate() is Node3D hat) HatMount.AddChild(hat);
    }

    private void BuildFacePlate(Node3D head)
    {
        float size = _form.HeadRadius * 1.7f;
        var plate = new MeshInstance3D
        {
            Name = "FacePlate",
            Mesh = new QuadMesh { Size = new Vector2(size, size) },
            MaterialOverride = _faceMaterial,
            Transform = new Transform3D(
                Basis.FromEuler(new Vector3(0f, Mathf.Pi, 0f)),
                new Vector3(0f, 0f, -_form.HeadRadius * 0.94f)),
        };
        head.AddChild(plate);
    }

    private void BuildEyes(Node3D head)
    {
        float z = -_form.HeadRadius * 0.82f;

        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;

            var white = new MeshInstance3D
            {
                Name = i == 0 ? "EyeLeft" : "EyeRight",
                Mesh = Ball(_form.EyeRadius, 12, 6),
                MaterialOverride = _eyeMaterial,
                Position = new Vector3(_form.EyeSpacing * side, _form.EyeHeight, z),
            };
            head.AddChild(white);

            var pupil = new MeshInstance3D
            {
                Name = "Pupil",
                Mesh = Ball(_form.EyeRadius * 0.52f, 10, 5),
                MaterialOverride = _pupilMaterial,
                Position = new Vector3(0f, 0f, -_form.EyeRadius * 0.62f),
            };
            white.AddChild(pupil);
        }
    }

    private void BuildLimbs()
    {
        _limbs = new Node3D { Name = "Limbs" };
        AddChild(_limbs);

        for (int limb = 0; limb < LimbCount; limb++)
        {
            var holder = new Node3D { Name = LimbName(limb) };
            _limbs.AddChild(holder);

            int segments = Mathf.Max(2, _form.Segments);
            _links[limb] = new MeshInstance3D[segments];
            _joints[limb] = new MeshInstance3D[segments];

            float span = _form.Reach(limb) / segments;

            for (int i = 0; i < segments; i++)
            {
                var link = new MeshInstance3D
                {
                    Name = $"Link{i}",
                    Mesh = new CapsuleMesh
                    {
                        Radius = _form.LimbRadius,
                        Height = Mathf.Max(span, _form.LimbRadius * 2f),
                        RadialSegments = 8,
                        Rings = 2,
                    },
                    MaterialOverride = _skinMaterial,
                };
                holder.AddChild(link);
                _links[limb][i] = link;

                var joint = new MeshInstance3D
                {
                    Name = $"Joint{i}",
                    Mesh = Ball(_form.LimbRadius * 1.08f, 8, 4),
                    MaterialOverride = _skinMaterial,
                };
                holder.AddChild(joint);
                _joints[limb][i] = joint;
            }

            _tips[limb] = _form.IsArm(limb) ? BuildHand() : BuildFoot();
            holder.AddChild(_tips[limb]);
        }
    }

    private Node3D BuildHand() => new MeshInstance3D
    {
        Name = "Hand",
        Mesh = Ball(_form.HandRadius, 10, 5),
        MaterialOverride = _skinMaterial,
    };

    private Node3D BuildFoot() => new MeshInstance3D
    {
        Name = "Foot",
        Mesh = new BoxMesh { Size = _form.FootSize },
        MaterialOverride = _shoeMaterial,
    };

    private void ResetPose()
    {
        Transform3D xform = GlobalTransform;

        for (int limb = 0; limb < LimbCount; limb++)
        {
            Vector3 root = xform * _form.Root(limb);
            _chains[limb].Build(Mathf.Max(2, _form.Segments), _form.Reach(limb), root, Vector3.Down);
            _pinned[limb] = false;
        }

        ApplyAll();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_built) return;

        float dt = (float)delta;
        Transform3D xform = GlobalTransform;
        Vector3 spineLow = xform * new Vector3(0f, _form.SpineLowY, 0f);
        Vector3 spineHigh = xform * new Vector3(0f, _form.SpineHighY, 0f);
        float clearance = _form.TorsoRadius + _form.LimbRadius * 0.5f;

        for (int limb = 0; limb < LimbCount; limb++)
        {
            LimbChain chain = _chains[limb];
            chain.Gravity = DangleGravity;
            chain.Damping = DangleDamping;
            chain.Stiffness = DangleStiffness;
            chain.MaxStretch = MaxStretch;

            bool pinned = _pinned[limb];
            Vector3 target = _targets[limb];

            if (!pinned && PlantFeet && !_form.IsArm(limb))
            {
                Vector3 hip = _form.Root(limb);
                pinned = true;
                target = xform * new Vector3(hip.X, _form.FootSize.Y * 0.5f, 0f);
            }

            chain.Step(dt, xform * _form.Root(limb), pinned, target);

            if (_form.IsArm(limb)) chain.PushOutside(spineLow, spineHigh, clearance);

            Apply(limb);
        }
    }

    private void ApplyAll()
    {
        for (int limb = 0; limb < LimbCount; limb++) Apply(limb);
    }

    private void Apply(int limb)
    {
        LimbChain chain = _chains[limb];
        MeshInstance3D[] links = _links[limb];
        MeshInstance3D[] joints = _joints[limb];
        if (links == null) return;

        int count = Mathf.Min(links.Length, chain.Links);

        for (int i = 0; i < count; i++)
        {
            Vector3 a = chain.Points[i];
            Vector3 b = chain.Points[i + 1];
            links[i].GlobalTransform = new Transform3D(Aim(b - a), (a + b) * 0.5f);
            joints[i].GlobalPosition = a;
        }

        Vector3 tip = chain.Tip;
        Vector3 last = chain.Points[chain.Points.Length - 2];

        if (_form.IsArm(limb))
        {
            _tips[limb].GlobalTransform = new Transform3D(Aim(tip - last), tip);
            return;
        }

        Basis facing = GlobalBasis.Orthonormalized();
        _tips[limb].GlobalTransform = new Transform3D(
            facing,
            tip + facing.Z * -_form.FootSize.Z * 0.28f);
    }

    private static Basis Aim(Vector3 direction)
    {
        if (direction.LengthSquared() < 1e-10f) return Basis.Identity;

        Vector3 y = direction.Normalized();
        Vector3 reference = Mathf.Abs(y.Dot(Vector3.Forward)) > 0.99f ? Vector3.Right : Vector3.Forward;
        Vector3 x = reference.Cross(y).Normalized();
        return new Basis(x, y, x.Cross(y));
    }

    private static SphereMesh Ball(float radius, int radial, int rings) => new()
    {
        Radius = radius,
        Height = radius * 2f,
        RadialSegments = radial,
        Rings = rings,
    };

    private static string LimbName(int limb) => limb switch
    {
        0 => "ArmLeft",
        1 => "ArmRight",
        2 => "LegLeft",
        _ => "LegRight",
    };
}
