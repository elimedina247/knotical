using System.Collections.Generic;
using Godot;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class Ratlines : MeshInstance3D
{
    private readonly List<CollisionShape3D> _mounted = new();
    private CollisionObject3D _body;

    private Vector3 _head = new(0f, 10f, 0f);
    private float _footSpread = 2f;
    private float _footFore = 0.6f;
    private float _footAft = -0.9f;
    private float _rungSpacing = 0.38f;
    private float _ropeRadius = 0.035f;
    private float _rungRadius = 0.025f;
    private float _topFraction = 0.92f;
    private float _grip = 0.3f;
    private int _sides = 5;
    private Material _material;

    [Export] public Vector3 Head { get => _head; set { _head = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,12,0.05")]
    public float FootSpread { get => _footSpread; set { _footSpread = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-20,20,0.05")]
    public float FootFore { get => _footFore; set { _footFore = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-20,20,0.05")]
    public float FootAft { get => _footAft; set { _footAft = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.15,1.2,0.01")]
    public float RungSpacing { get => _rungSpacing; set { _rungSpacing = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.01,0.2,0.005")]
    public float RopeRadius { get => _ropeRadius; set { _ropeRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.01,0.2,0.005")]
    public float RungRadius { get => _rungRadius; set { _rungRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.3,1,0.01")]
    public float TopFraction { get => _topFraction; set { _topFraction = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.1,1,0.01")]
    public float GripDepth { get => _grip; set { _grip = value; Rebuild(); } }

    [Export(PropertyHint.Range, "3,12,1")]
    public int Sides { get => _sides; set { _sides = value; Rebuild(); } }

    [Export] public Material RopeMaterial { get => _material; set { _material = value; Rebuild(); } }

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public override void _Ready() => Rebuild();

    public override void _EnterTree() => Rebuild();

    private Vector3 Foot(float side) => new(_footSpread, 0f, side > 0f ? _footFore : _footAft);

    private void Rebuild()
    {
        if (!IsInsideTree()) return;

        Vector3 fore = Foot(1f);
        Vector3 aft = Foot(-1f);

        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);

        Rope(tool, fore, _head);
        Rope(tool, aft, _head);

        int rungs = Mathf.Max(1, Mathf.FloorToInt(_head.Y * _topFraction / Mathf.Max(_rungSpacing, 0.05f)));
        for (int i = 1; i <= rungs; i++)
        {
            float t = i * _rungSpacing / Mathf.Max(_head.Y, 0.01f);
            if (t > _topFraction) break;
            Rope(tool, fore.Lerp(_head, t), aft.Lerp(_head, t), _rungRadius);
        }

        tool.GenerateNormals();
        if (_material != null) tool.SetMaterial(_material);

        var mesh = new ArrayMesh();
        tool.Commit(mesh);
        Mesh = mesh;

        Mount(fore, aft);
    }

    private void Rope(SurfaceTool tool, Vector3 from, Vector3 to) => Rope(tool, from, to, _ropeRadius);

    private static void Rope(SurfaceTool tool, Vector3 from, Vector3 to, float radius, int sides = 5)
    {
        Vector3 axis = to - from;
        float length = axis.Length();
        if (length < 0.001f) return;

        axis /= length;
        Vector3 guide = Mathf.Abs(axis.Y) > 0.9f ? Vector3.Forward : Vector3.Up;
        Vector3 u = axis.Cross(guide).Normalized() * radius;
        Vector3 v = axis.Cross(u).Normalized() * radius;

        for (int i = 0; i < sides; i++)
        {
            float a = Mathf.Tau * i / sides;
            float b = Mathf.Tau * (i + 1) / sides;
            Vector3 ra = u * Mathf.Cos(a) + v * Mathf.Sin(a);
            Vector3 rb = u * Mathf.Cos(b) + v * Mathf.Sin(b);

            Tri(tool, from + ra, to + ra, to + rb);
            Tri(tool, from + ra, to + rb, from + rb);
        }
    }

    private static void Tri(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c)
    {
        tool.AddVertex(a);
        tool.AddVertex(b);
        tool.AddVertex(c);
    }

    private void Mount(Vector3 fore, Vector3 aft)
    {
        if (_body == null || !IsInstanceValid(_body)) _body = FindBody();
        if (_body == null) return;

        foreach (CollisionShape3D old in _mounted)
        {
            if (!IsInstanceValid(old)) continue;
            _body.RemoveChild(old);
            old.QueueFree();
        }

        _mounted.Clear();

        Transform3D local = _body.GlobalTransform.AffineInverse() * GlobalTransform;
        Vector3 across = aft - fore;
        float width = across.Length();
        if (width < 0.01f) return;

        int steps = Mathf.Max(2, Mathf.CeilToInt(_head.Y * _topFraction / 2f));

        for (int i = 0; i < steps; i++)
        {
            float t0 = _topFraction * i / steps;
            float t1 = _topFraction * (i + 1) / steps;

            Vector3 a = fore.Lerp(_head, t0);
            Vector3 b = aft.Lerp(_head, t0);
            Vector3 c = fore.Lerp(_head, t1);
            Vector3 d = aft.Lerp(_head, t1);

            var points = new Vector3[8];
            Vector3 depth = (b - a).Cross(c - a).Normalized() * (_grip * 0.5f);

            points[0] = a + depth; points[1] = b + depth;
            points[2] = c + depth; points[3] = d + depth;
            points[4] = a - depth; points[5] = b - depth;
            points[6] = c - depth; points[7] = d - depth;

            var shape = new CollisionShape3D
            {
                Name = $"{Name}Col{i}",
                Shape = new ConvexPolygonShape3D { Points = points },
                Transform = local
            };

            _body.AddChild(shape);
            _mounted.Add(shape);
        }
    }

    private CollisionObject3D FindBody()
    {
        Node node = GetParent();

        while (node != null)
        {
            if (node is CollisionObject3D body) return body;
            node = node.GetParent();
        }

        return null;
    }
}
