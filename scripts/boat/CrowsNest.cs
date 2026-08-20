using System.Collections.Generic;
using Godot;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class CrowsNest : MeshInstance3D
{
    private readonly List<CollisionShape3D> _mounted = new();
    private CollisionObject3D _body;

    private float _floorRadius = 0.8f;
    private float _rimRadius = 0.95f;
    private float _wallHeight = 0.85f;
    private float _floorThickness = 0.12f;
    private float _wallThickness = 0.07f;
    private float _gapDegrees = 65f;
    private float _gapBearing = 180f;
    private int _sides = 20;
    private Material _material;

    [Export(PropertyHint.Range, "0.3,3,0.01")]
    public float FloorRadius { get => _floorRadius; set { _floorRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.3,3,0.01")]
    public float RimRadius { get => _rimRadius; set { _rimRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.1,2.5,0.01")]
    public float WallHeight { get => _wallHeight; set { _wallHeight = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.03,0.5,0.005")]
    public float FloorThickness { get => _floorThickness; set { _floorThickness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.02,0.3,0.005")]
    public float WallThickness { get => _wallThickness; set { _wallThickness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,180,1")]
    public float GapDegrees { get => _gapDegrees; set { _gapDegrees = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,360,1")]
    public float GapBearing { get => _gapBearing; set { _gapBearing = value; Rebuild(); } }

    [Export(PropertyHint.Range, "6,48,1")]
    public int Sides { get => _sides; set { _sides = value; Rebuild(); } }

    [Export] public Material NestMaterial { get => _material; set { _material = value; Rebuild(); } }

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public override void _Ready() => Rebuild();

    private bool InGap(float angle)
    {
        if (_gapDegrees <= 0f) return false;
        float delta = Mathf.Abs(Mathf.AngleDifference(angle, Mathf.DegToRad(_gapBearing)));
        return delta < Mathf.DegToRad(_gapDegrees) * 0.5f;
    }

    private static Vector3 At(float radius, float angle, float y) =>
        new(radius * Mathf.Sin(angle), y, radius * Mathf.Cos(angle));

    private void Rebuild()
    {
        if (!IsInsideTree()) return;

        int n = Mathf.Max(6, _sides);
        float floorTop = 0f;
        float floorLow = -_floorThickness;
        float rimTop = _wallHeight;

        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < n; i++)
        {
            float a0 = Mathf.Tau * i / n;
            float a1 = Mathf.Tau * (i + 1) / n;

            Quad(tool,
                At(_floorRadius, a1, floorTop), At(_floorRadius, a0, floorTop),
                Vector3.Up * floorTop, Vector3.Up * floorTop);

            Quad(tool,
                Vector3.Up * floorLow, Vector3.Up * floorLow,
                At(_floorRadius, a0, floorLow), At(_floorRadius, a1, floorLow));

            Quad(tool,
                At(_floorRadius, a0, floorTop), At(_floorRadius, a1, floorTop),
                At(_floorRadius, a1, floorLow), At(_floorRadius, a0, floorLow));

            float mid = (a0 + a1) * 0.5f;
            if (InGap(mid)) continue;

            float outer = _rimRadius;
            float inner = _rimRadius - _wallThickness;

            Quad(tool,
                At(_floorRadius, a0, floorTop), At(_floorRadius, a1, floorTop),
                At(outer, a1, rimTop), At(outer, a0, rimTop));

            Quad(tool,
                At(inner, a0, rimTop), At(inner, a1, rimTop),
                At(_floorRadius - _wallThickness, a1, floorTop), At(_floorRadius - _wallThickness, a0, floorTop));

            Quad(tool,
                At(inner, a0, rimTop), At(outer, a0, rimTop),
                At(outer, a1, rimTop), At(inner, a1, rimTop));
        }

        tool.GenerateNormals();
        if (_material != null) tool.SetMaterial(_material);

        var mesh = new ArrayMesh();
        tool.Commit(mesh);
        Mesh = mesh;

        Mount(n, floorTop, floorLow, rimTop);
    }

    private static void Quad(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        tool.AddVertex(a);
        tool.AddVertex(b);
        tool.AddVertex(c);
        tool.AddVertex(a);
        tool.AddVertex(c);
        tool.AddVertex(d);
    }

    private void Mount(int n, float floorTop, float floorLow, float rimTop)
    {
        if (_body == null || !IsInstanceValid(_body)) _body = FindBody();
        if (_body == null) return;

        foreach (CollisionShape3D old in _mounted)
        {
            if (!IsInstanceValid(old)) continue;
            if (old.GetParent() == _body) _body.RemoveChild(old);
            old.QueueFree();
        }

        _mounted.Clear();

        Transform3D local = _body.GlobalTransform.AffineInverse() * GlobalTransform;

        Add("Floor", new CylinderShape3D { Radius = _floorRadius, Height = _floorThickness },
            local * new Transform3D(Basis.Identity, new Vector3(0f, (floorTop + floorLow) * 0.5f, 0f)));

        for (int i = 0; i < n; i++)
        {
            float a0 = Mathf.Tau * i / n;
            float a1 = Mathf.Tau * (i + 1) / n;
            if (InGap((a0 + a1) * 0.5f)) continue;

            float outer = _rimRadius;
            float inner = _rimRadius - _wallThickness;

            var points = new Vector3[8];
            points[0] = At(outer, a0, rimTop);
            points[1] = At(outer, a1, rimTop);
            points[2] = At(inner, a0, rimTop);
            points[3] = At(inner, a1, rimTop);
            points[4] = At(_floorRadius, a0, floorTop);
            points[5] = At(_floorRadius, a1, floorTop);
            points[6] = At(_floorRadius - _wallThickness, a0, floorTop);
            points[7] = At(_floorRadius - _wallThickness, a1, floorTop);

            Add($"Wall{i}", new ConvexPolygonShape3D { Points = points }, local);
        }
    }

    private void Add(string name, Shape3D shape, Transform3D transform)
    {
        var node = new CollisionShape3D { Name = $"{Name}{name}", Shape = shape, Transform = transform };
        _body.CallDeferred(Node.MethodName.AddChild, node);
        _mounted.Add(node);
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
