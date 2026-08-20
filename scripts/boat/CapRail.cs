using System.Collections.Generic;
using Godot;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class CapRail : MeshInstance3D
{
    private readonly HullForm _form = new();
    private readonly List<CollisionShape3D> _mounted = new();
    private CollisionObject3D _body;

    private float _thickness = 0.1f;
    private float _overhang = 0.22f;
    private float _proud = 0.03f;
    private float _inset = 0.12f;
    private int _stations = 40;
    private int _collisionSegments = 14;
    private Material _material;

    [Export(PropertyHint.Range, "0.02,0.6,0.005")]
    public float Thickness { get => _thickness; set { _thickness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,1.5,0.01")]
    public float Overhang { get => _overhang; set { _overhang = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,0.4,0.005")]
    public float Proud { get => _proud; set { _proud = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,1.5,0.01")]
    public float HullThickness { get => _inset; set { _inset = value; Rebuild(); } }

    [Export(PropertyHint.Range, "8,80,1")]
    public int Stations { get => _stations; set { _stations = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,40,1")]
    public int CollisionSegments { get => _collisionSegments; set { _collisionSegments = value; Rebuild(); } }

    [Export] public Material RailMaterial { get => _material; set { _material = value; Rebuild(); } }

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public override void _Ready() => Rebuild();

    private void SyncForm()
    {
        BoatHull hull = GetParentOrNull<BoatHull>();
        if (hull == null) return;

        _form.Length = hull.HullLength;
        _form.Beam = hull.Beam;
        _form.Draft = hull.Draft;
        _form.Freeboard = hull.Freeboard;
        _form.BowSheerRise = hull.BowSheerRise;
        _form.SternSheerRise = hull.SternSheerRise;
        _form.SheerPower = hull.SheerPower;
        _form.Rocker = hull.Rocker;
        _form.RockerPower = hull.RockerPower;
        _form.BowSharpness = hull.BowSharpness;
        _form.SternSharpness = hull.SternSharpness;
        _form.TransomWidth = hull.TransomWidth;
        _form.TransomRake = hull.TransomRake;
        _form.StemRake = hull.StemRake;
        _form.StemPower = hull.StemPower;
        _form.BilgeFullness = hull.BilgeFullness;
    }

    private void Rebuild()
    {
        if (!IsInsideTree()) return;

        SyncForm();

        Vector3[] outer = _form.Ring(1f, _proud, _stations);
        Vector3[] inner = _form.Ring(1f, -(_inset + _overhang), _stations);
        int loop = outer.Length;

        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < loop; i++)
        {
            int j = (i + 1) % loop;

            Vector3 a = outer[i];
            Vector3 b = outer[j];
            Vector3 c = inner[j];
            Vector3 d = inner[i];

            Vector3 down = Vector3.Down * _thickness;

            Quad(tool, a, b, c, d);
            Quad(tool, d + down, c + down, b + down, a + down);
            Quad(tool, d, c, c + down, d + down);
            Quad(tool, b, a, a + down, b + down);
        }

        tool.GenerateNormals();
        if (_material != null) tool.SetMaterial(_material);

        var mesh = new ArrayMesh();
        tool.Commit(mesh);
        Mesh = mesh;

        Mount(outer, inner, loop);
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

    private void Mount(Vector3[] outer, Vector3[] inner, int loop)
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

        if (_collisionSegments <= 0) return;

        Transform3D local = _body.GlobalTransform.AffineInverse() * GlobalTransform;
        int span = Mathf.Max(1, Mathf.CeilToInt((float)loop / _collisionSegments));
        Vector3 down = Vector3.Down * _thickness;

        for (int start = 0; start < loop; start += span)
        {
            var points = new List<Vector3>();

            for (int k = 0; k <= span; k++)
            {
                int i = (start + k) % loop;
                points.Add(outer[i]);
                points.Add(inner[i]);
                points.Add(outer[i] + down);
                points.Add(inner[i] + down);
            }

            var shape = new CollisionShape3D
            {
                Name = $"{Name}Col{start}",
                Shape = new ConvexPolygonShape3D { Points = points.ToArray() },
                Transform = local
            };

            _body.CallDeferred(Node.MethodName.AddChild, shape);
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
