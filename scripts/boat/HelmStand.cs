using System.Collections.Generic;
using Godot;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class HelmStand : MeshInstance3D
{
    private float _height = 0.98f;
    private float _footWidth = 0.44f;
    private float _headWidth = 0.17f;
    private float _thickness = 0.13f;
    private float _flare = 2.2f;
    private float _lean = 10f;
    private float _plinthWidth = 0.66f;
    private float _plinthDepth = 0.44f;
    private float _plinthHeight = 0.07f;
    private int _steps = 14;

    [Export(PropertyHint.Range, "0.2,3,0.005")]
    public float Height { get => _height; set { _height = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.05,2,0.005")]
    public float FootWidth { get => _footWidth; set { _footWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.03,2,0.005")]
    public float HeadWidth { get => _headWidth; set { _headWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.02,1,0.005")]
    public float Thickness { get => _thickness; set { _thickness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,5,0.05")]
    public float Flare { get => _flare; set { _flare = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-30,30,0.5")]
    public float Lean { get => _lean; set { _lean = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.05,3,0.005")]
    public float PlinthWidth { get => _plinthWidth; set { _plinthWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.05,3,0.005")]
    public float PlinthDepth { get => _plinthDepth; set { _plinthDepth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.01,0.5,0.005")]
    public float PlinthHeight { get => _plinthHeight; set { _plinthHeight = value; Rebuild(); } }

    [Export(PropertyHint.Range, "2,48,1")]
    public int Steps { get => _steps; set { _steps = value; Rebuild(); } }

    [Export] public bool Collides { get; set; } = true;

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public override void _Ready()
    {
        Rebuild();
    }

    private float HalfWidth(float t)
    {
        float head = Mathf.Max(0.015f, _headWidth) * 0.5f;
        float foot = Mathf.Max(0.015f, _footWidth) * 0.5f;

        return head + (foot - head) * Mathf.Pow(1f - t, Mathf.Max(_flare, 0.05f));
    }

    private void Rebuild()
    {
        int steps = Mathf.Max(2, _steps);
        float half = Mathf.Max(0.01f, _thickness) * 0.5f;
        float plinth = Mathf.Max(0.005f, _plinthHeight);
        float rise = Mathf.Max(0.02f, _height - plinth);
        float rake = Mathf.Tan(Mathf.DegToRad(_lean));

        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);

        Box(tool,
            new Vector3(-_plinthWidth * 0.5f, 0f, -_plinthDepth * 0.5f),
            new Vector3(_plinthWidth * 0.5f, plinth, _plinthDepth * 0.5f));

        var hull = new List<Vector3>();
        Vector3[] lower = Ring(0f, plinth, rise, rake, half, hull);

        for (int i = 1; i <= steps; i++)
        {
            Vector3[] upper = Ring((float)i / steps, plinth, rise, rake, half, hull);

            Quad(tool, lower[0], lower[1], upper[1], upper[0]);
            Quad(tool, lower[1], lower[2], upper[2], upper[1]);
            Quad(tool, lower[2], lower[3], upper[3], upper[2]);
            Quad(tool, lower[3], lower[0], upper[0], upper[3]);

            lower = upper;
        }

        Quad(tool, lower[0], lower[1], lower[2], lower[3]);

        var mesh = new ArrayMesh();
        tool.Commit(mesh);
        Mesh = mesh;

        hull.Add(new Vector3(-_plinthWidth * 0.5f, 0f, -_plinthDepth * 0.5f));
        hull.Add(new Vector3(_plinthWidth * 0.5f, 0f, -_plinthDepth * 0.5f));
        hull.Add(new Vector3(_plinthWidth * 0.5f, 0f, _plinthDepth * 0.5f));
        hull.Add(new Vector3(-_plinthWidth * 0.5f, 0f, _plinthDepth * 0.5f));

        if (IsInsideTree() && Collides) BuildCollision(hull);
    }

    private Vector3[] Ring(float t, float plinth, float rise, float rake, float half, List<Vector3> hull)
    {
        float w = HalfWidth(t);
        float y = plinth + rise * t;
        float z = -rise * t * rake;

        var ring = new[]
        {
            new Vector3(-w, y, z + half),
            new Vector3(w, y, z + half),
            new Vector3(w, y, z - half),
            new Vector3(-w, y, z - half),
        };

        hull.AddRange(ring);
        return ring;
    }

    private static void Quad(SurfaceTool tool, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 normal = (p1 - p0).Cross(p2 - p0).Normalized();
        tool.SetNormal(normal);

        tool.AddVertex(p0);
        tool.AddVertex(p1);
        tool.AddVertex(p2);

        tool.AddVertex(p0);
        tool.AddVertex(p2);
        tool.AddVertex(p3);
    }

    private static void Box(SurfaceTool tool, Vector3 min, Vector3 max)
    {
        Vector3 a = new(min.X, min.Y, max.Z);
        Vector3 b = new(max.X, min.Y, max.Z);
        Vector3 c = new(max.X, max.Y, max.Z);
        Vector3 d = new(min.X, max.Y, max.Z);
        Vector3 e = new(min.X, min.Y, min.Z);
        Vector3 f = new(max.X, min.Y, min.Z);
        Vector3 g = new(max.X, max.Y, min.Z);
        Vector3 h = new(min.X, max.Y, min.Z);

        Quad(tool, a, b, c, d);
        Quad(tool, f, e, h, g);
        Quad(tool, b, f, g, c);
        Quad(tool, e, a, d, h);
        Quad(tool, d, c, g, h);
        Quad(tool, e, f, b, a);
    }

    private void BuildCollision(List<Vector3> points)
    {
        CollisionObject3D body = FindBody();
        if (body == null) return;

        var shape = new CollisionShape3D
        {
            Name = $"{Name}Shape",
            Transform = body.GlobalTransform.AffineInverse() * GlobalTransform,
            Shape = new ConvexPolygonShape3D { Points = points.ToArray() }
        };

        Callable.From(() => Attach(body, shape)).CallDeferred();
    }

    private static void Attach(CollisionObject3D body, CollisionShape3D shape)
    {
        if (!IsInstanceValid(body))
        {
            shape.QueueFree();
            return;
        }

        Node existing = body.GetNodeOrNull(shape.Name.ToString());

        if (existing != null)
        {
            body.RemoveChild(existing);
            existing.QueueFree();
        }

        body.AddChild(shape);
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
