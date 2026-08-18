using System.Collections.Generic;
using Godot;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class Spar : MeshInstance3D
{
    private float _halfLength = 13f;
    private float _endRadius = 0.18f;
    private float _midRadius = 0.42f;
    private int _sides = 8;
    private CollisionShape3D _shape;
    private CollisionObject3D _hull;

    [Export(PropertyHint.Range, "0.5,40,0.05")]
    public float HalfLength { get => _halfLength; set { _halfLength = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.02,4,0.005")]
    public float EndRadius { get => _endRadius; set { _endRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.02,4,0.005")]
    public float MidRadius { get => _midRadius; set { _midRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "3,24,1")]
    public int Sides { get => _sides; set { _sides = value; Rebuild(); } }

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public override void _Ready()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        int sides = Mathf.Max(3, _sides);
        float half = Mathf.Max(0.01f, _halfLength);
        float end = Mathf.Max(0.005f, _endRadius);
        float mid = Mathf.Max(0.005f, _midRadius);

        Vector3[] port = Ring(-half, end, sides);
        Vector3[] waist = Ring(0f, mid, sides);
        Vector3[] starboard = Ring(half, end, sides);

        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);
        Band(tool, port, waist, Slope(end, mid, half, sides));
        Band(tool, waist, starboard, Slope(mid, end, half, sides));
        Cap(tool, starboard, Vector3.Right);
        Cap(tool, port, Vector3.Left);

        var mesh = new ArrayMesh();
        tool.Commit(mesh);
        Mesh = mesh;

        if (IsInsideTree()) BuildCollision(port, waist, starboard);
    }

    private static Vector3[] Ring(float x, float radius, int sides)
    {
        var ring = new Vector3[sides];

        for (int i = 0; i < sides; i++)
        {
            float a = Mathf.Tau * i / sides;
            ring[i] = new Vector3(x, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }

        return ring;
    }

    private static Vector3[] Slope(float from, float to, float run, int sides)
    {
        var normals = new Vector3[sides];

        for (int i = 0; i < sides; i++)
        {
            float a = Mathf.Tau * i / sides;
            normals[i] = new Vector3(from - to, run * Mathf.Cos(a), run * Mathf.Sin(a)).Normalized();
        }

        return normals;
    }

    private static void Vertex(SurfaceTool tool, Vector3 point, Vector3 normal)
    {
        tool.SetNormal(normal);
        tool.AddVertex(point);
    }

    private static void Band(SurfaceTool tool, Vector3[] lower, Vector3[] upper, Vector3[] normals)
    {
        for (int i = 0; i < lower.Length; i++)
        {
            int j = (i + 1) % lower.Length;

            Vertex(tool, lower[i], normals[i]);
            Vertex(tool, lower[j], normals[j]);
            Vertex(tool, upper[j], normals[j]);

            Vertex(tool, lower[i], normals[i]);
            Vertex(tool, upper[j], normals[j]);
            Vertex(tool, upper[i], normals[i]);
        }
    }

    private static void Cap(SurfaceTool tool, Vector3[] ring, Vector3 normal)
    {
        for (int i = 1; i < ring.Length - 1; i++)
        {
            Vertex(tool, ring[0], normal);

            if (normal.X > 0f)
            {
                Vertex(tool, ring[i], normal);
                Vertex(tool, ring[i + 1], normal);
            }
            else
            {
                Vertex(tool, ring[i + 1], normal);
                Vertex(tool, ring[i], normal);
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint()) return;
        Sync();
    }

    private void BuildCollision(params Vector3[][] rings)
    {
        if (Engine.IsEditorHint()) return;

        var points = new List<Vector3>();
        foreach (Vector3[] ring in rings) points.AddRange(ring);

        var shape = new ConvexPolygonShape3D { Points = points.ToArray() };
        Callable.From(() => Attach(shape)).CallDeferred();
    }

    private void Attach(ConvexPolygonShape3D shape)
    {
        if (!IsInsideTree()) return;

        if (_hull == null || !IsInstanceValid(_hull)) _hull = FindBody();
        if (_hull == null) return;

        if (_shape == null || !IsInstanceValid(_shape))
        {
            string name = $"{Name}Shape";
            Node stale = _hull.GetNodeOrNull(name);

            if (stale != null)
            {
                _hull.RemoveChild(stale);
                stale.QueueFree();
            }

            _shape = new CollisionShape3D { Name = name };
            _hull.AddChild(_shape);
        }

        _shape.Shape = shape;
        Sync();
    }

    private void Sync()
    {
        if (_shape == null || !IsInstanceValid(_shape)) return;
        if (_hull == null || !IsInstanceValid(_hull)) return;

        Transform3D local = _hull.GlobalTransform.AffineInverse() * GlobalTransform;
        if (!_shape.Transform.IsEqualApprox(local)) _shape.Transform = local;
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
