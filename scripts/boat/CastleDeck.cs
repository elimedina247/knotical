using System.Collections.Generic;
using Godot;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class CastleDeck : MeshInstance3D
{
    public enum Edge { None, Fore, Aft }

    private readonly HullForm _form = new();
    private readonly List<CollisionShape3D> _mounted = new();
    private CollisionObject3D _body;

    private float _zFrom = 4f;
    private float _zTo = 12f;
    private float _platformHeight = 4.6f;
    private float _slabThickness = 0.25f;
    private float _bulwarkHeight = 0.95f;
    private float _wallThickness = 0.15f;
    private float _sideWallBottom = 2.1f;
    private float _inset = 0.12f;
    private int _stations = 8;
    private bool _wallFore;
    private bool _wallAft;
    private float _wallBottom = 2.1f;
    private float _doorWidth;
    private float _doorHeight = 2f;
    private Edge _stairEdge = Edge.None;
    private float _stairWidth = 1.2f;
    private float _stairRun = 2.4f;
    private float _stairBottom = 2.2f;
    private float _stairOffsetX;
    private Material _deckMaterial;
    private Material _wallMaterial;

    [Export(PropertyHint.Range, "-60,60,0.05")]
    public float ZFrom { get => _zFrom; set { _zFrom = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-60,60,0.05")]
    public float ZTo { get => _zTo; set { _zTo = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,20,0.05")]
    public float PlatformHeight { get => _platformHeight; set { _platformHeight = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float SlabThickness { get => _slabThickness; set { _slabThickness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,3,0.05")]
    public float BulwarkHeight { get => _bulwarkHeight; set { _bulwarkHeight = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.05,0.6,0.01")]
    public float WallThickness { get => _wallThickness; set { _wallThickness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,20,0.05")]
    public float SideWallBottom { get => _sideWallBottom; set { _sideWallBottom = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Inset { get => _inset; set { _inset = value; Rebuild(); } }

    [Export(PropertyHint.Range, "2,32,1")]
    public int Stations { get => _stations; set { _stations = value; Rebuild(); } }

    [Export] public bool WallFore { get => _wallFore; set { _wallFore = value; Rebuild(); } }

    [Export] public bool WallAft { get => _wallAft; set { _wallAft = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,20,0.05")]
    public float WallBottom { get => _wallBottom; set { _wallBottom = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,4,0.05")]
    public float DoorWidth { get => _doorWidth; set { _doorWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,4,0.05")]
    public float DoorHeight { get => _doorHeight; set { _doorHeight = value; Rebuild(); } }

    [Export] public Edge StairEdge { get => _stairEdge; set { _stairEdge = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,4,0.05")]
    public float StairWidth { get => _stairWidth; set { _stairWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.3,10,0.05")]
    public float StairRun { get => _stairRun; set { _stairRun = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,20,0.05")]
    public float StairBottom { get => _stairBottom; set { _stairBottom = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-10,10,0.05")]
    public float StairOffsetX { get => _stairOffsetX; set { _stairOffsetX = value; Rebuild(); } }

    [Export] public Material DeckMaterial { get => _deckMaterial; set { _deckMaterial = value; Rebuild(); } }

    [Export] public Material WallMaterial { get => _wallMaterial; set { _wallMaterial = value; Rebuild(); } }

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

    private Vector3 Station(float z)
    {
        float t = Mathf.Clamp(0.5f - z / Mathf.Max(_form.Length, 0.01f), 0f, 1f);
        float v = _form.VAtHeight(t, _platformHeight);
        Vector3 shell = _form.Shell(t, v, -_inset, 1f);
        return new Vector3(Mathf.Max(shell.X, 0.25f), _platformHeight, shell.Z);
    }

    private static Vector3 Mirror(Vector3 p) => new(-p.X, p.Y, p.Z);

    private static Vector3 At(Vector3 p, float y) => new(p.X, y, p.Z);

    private static void Tri(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c)
    {
        tool.AddVertex(a);
        tool.AddVertex(b);
        tool.AddVertex(c);
    }

    private static void Quad(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing)
    {
        if ((b - a).Cross(c - a).Dot(facing) < 0f) (b, d) = (d, b);
        Tri(tool, a, b, c);
        Tri(tool, a, c, d);
    }

    private static void Box(SurfaceTool tool, Vector3 min, Vector3 max, List<Vector3[]> hulls)
    {
        var a = min;
        var b = new Vector3(max.X, min.Y, min.Z);
        var c = new Vector3(max.X, min.Y, max.Z);
        var d = new Vector3(min.X, min.Y, max.Z);
        var e = new Vector3(min.X, max.Y, min.Z);
        var f = new Vector3(max.X, max.Y, min.Z);
        var g = max;
        var h = new Vector3(min.X, max.Y, max.Z);

        Quad(tool, a, b, c, d, Vector3.Down);
        Quad(tool, e, f, g, h, Vector3.Up);
        Quad(tool, a, b, f, e, Vector3.Forward);
        Quad(tool, d, c, g, h, Vector3.Back);
        Quad(tool, a, d, h, e, Vector3.Left);
        Quad(tool, b, c, g, f, Vector3.Right);

        hulls?.Add(new[] { a, b, c, d, e, f, g, h });
    }

    private void Rebuild()
    {
        SyncForm();

        int n = Mathf.Max(2, _stations);
        var rim = new Vector3[n + 1];
        for (int i = 0; i <= n; i++) rim[i] = Station(Mathf.Lerp(_zFrom, _zTo, (float)i / n));

        var deck = new SurfaceTool();
        deck.Begin(Godot.Mesh.PrimitiveType.Triangles);
        var wall = new SurfaceTool();
        wall.Begin(Godot.Mesh.PrimitiveType.Triangles);
        var hulls = new List<Vector3[]>();

        BuildSlab(deck, wall, rim, hulls);
        BuildSides(wall, rim, hulls);
        if (_wallFore) BuildBulkhead(wall, rim[0], -1f, hulls);
        if (_wallAft) BuildBulkhead(wall, rim[n], 1f, hulls);
        BuildStairs(wall, rim, hulls);

        deck.GenerateNormals();
        wall.GenerateNormals();
        if (_deckMaterial != null) deck.SetMaterial(_deckMaterial);
        if (_wallMaterial != null) wall.SetMaterial(_wallMaterial);

        var mesh = new ArrayMesh();
        deck.Commit(mesh);
        wall.Commit(mesh);
        Mesh = mesh;

        if (!Engine.IsEditorHint() && IsInsideTree())
        {
            Callable.From(() => Mount(hulls)).CallDeferred();
        }
    }

    private void BuildSlab(SurfaceTool deck, SurfaceTool wall, Vector3[] rim, List<Vector3[]> hulls)
    {
        float bottom = _platformHeight - _slabThickness;

        for (int i = 0; i < rim.Length - 1; i++)
        {
            Vector3 s0 = rim[i];
            Vector3 s1 = rim[i + 1];
            Vector3 p0 = Mirror(s0);
            Vector3 p1 = Mirror(s1);

            Quad(deck, s0, s1, p1, p0, Vector3.Up);
            Quad(wall, At(s0, bottom), At(s1, bottom), At(p1, bottom), At(p0, bottom), Vector3.Down);
            Quad(wall, s0, s1, At(s1, bottom), At(s0, bottom), Vector3.Right);
            Quad(wall, p0, p1, At(p1, bottom), At(p0, bottom), Vector3.Left);

            hulls.Add(new[]
            {
                s0, s1, p0, p1,
                At(s0, bottom), At(s1, bottom), At(p0, bottom), At(p1, bottom)
            });
        }

        Vector3 f0 = rim[0];
        Vector3 f1 = rim[^1];
        Quad(wall, f0, Mirror(f0), At(Mirror(f0), bottom), At(f0, bottom), Vector3.Forward);
        Quad(wall, f1, Mirror(f1), At(Mirror(f1), bottom), At(f1, bottom), Vector3.Back);
    }

    private void BuildSides(SurfaceTool wall, Vector3[] rim, List<Vector3[]> hulls)
    {
        float top = _platformHeight + _bulwarkHeight;
        float bottom = Mathf.Min(_sideWallBottom, _platformHeight - _slabThickness);

        for (int side = -1; side <= 1; side += 2)
        {
            var thick = new Vector3(_wallThickness * side, 0f, 0f);
            var outward = new Vector3(side, 0f, 0f);

            for (int i = 0; i < rim.Length - 1; i++)
            {
                Vector3 o0 = side < 0 ? Mirror(rim[i]) : rim[i];
                Vector3 o1 = side < 0 ? Mirror(rim[i + 1]) : rim[i + 1];
                Vector3 in0 = o0 - thick;
                Vector3 in1 = o1 - thick;

                Quad(wall, At(o0, bottom), At(o1, bottom), At(o1, top), At(o0, top), outward);
                Quad(wall, At(in0, bottom), At(in1, bottom), At(in1, top), At(in0, top), -outward);
                Quad(wall, At(o0, top), At(o1, top), At(in1, top), At(in0, top), Vector3.Up);

                hulls.Add(new[]
                {
                    At(o0, bottom), At(o1, bottom), At(o0, top), At(o1, top),
                    At(in0, bottom), At(in1, bottom), At(in0, top), At(in1, top)
                });
            }

            Vector3 e0 = side < 0 ? Mirror(rim[0]) : rim[0];
            Vector3 e1 = side < 0 ? Mirror(rim[^1]) : rim[^1];
            Quad(wall, At(e0, bottom), At(e0 - thick, bottom), At(e0 - thick, top), At(e0, top), Vector3.Forward);
            Quad(wall, At(e1, bottom), At(e1 - thick, bottom), At(e1 - thick, top), At(e1, top), Vector3.Back);
        }
    }

    private void BuildBulkhead(SurfaceTool wall, Vector3 rimAt, float sign, List<Vector3[]> hulls)
    {
        float half = Mathf.Max(rimAt.X - _wallThickness, 0.2f);
        float zNear = sign < 0f ? rimAt.Z : rimAt.Z - _wallThickness;
        float zFar = zNear + _wallThickness;
        float top = _platformHeight;
        float bottom = Mathf.Min(_wallBottom, top - 0.1f);

        if (_doorWidth > 0.01f && _doorWidth < half * 2f - 0.2f)
        {
            float d = _doorWidth * 0.5f;
            float lintel = Mathf.Min(bottom + _doorHeight, top);
            Box(wall, new Vector3(-half, bottom, zNear), new Vector3(-d, top, zFar), hulls);
            Box(wall, new Vector3(d, bottom, zNear), new Vector3(half, top, zFar), hulls);
            if (top - lintel > 0.02f)
            {
                Box(wall, new Vector3(-d, lintel, zNear), new Vector3(d, top, zFar), hulls);
            }
        }
        else
        {
            Box(wall, new Vector3(-half, bottom, zNear), new Vector3(half, top, zFar), hulls);
        }
    }

    private void BuildStairs(SurfaceTool wall, Vector3[] rim, List<Vector3[]> hulls)
    {
        if (_stairEdge == Edge.None || _stairWidth <= 0.01f) return;

        float rise = _platformHeight - _stairBottom;
        if (rise <= 0.05f || _stairRun <= 0.05f) return;

        float sign = _stairEdge == Edge.Fore ? -1f : 1f;
        float zEdge = (_stairEdge == Edge.Fore ? rim[0] : rim[^1]).Z;
        float x0 = _stairOffsetX - _stairWidth * 0.5f;
        float x1 = _stairOffsetX + _stairWidth * 0.5f;

        int steps = Mathf.Max(2, Mathf.CeilToInt(rise / 0.23f));
        float depth = _stairRun / steps;

        for (int k = 1; k <= steps; k++)
        {
            float top = _platformHeight - rise * k / steps;
            if (top - _stairBottom < 0.03f) continue;

            float zNear = zEdge + sign * depth * (k - 1);
            float zFar = zEdge + sign * depth * k;
            Box(wall,
                new Vector3(x0, _stairBottom, Mathf.Min(zNear, zFar)),
                new Vector3(x1, top, Mathf.Max(zNear, zFar)),
                null);
        }

        float zToe = zEdge + sign * _stairRun;
        hulls.Add(new[]
        {
            new Vector3(x0, _platformHeight, zEdge),
            new Vector3(x1, _platformHeight, zEdge),
            new Vector3(x0, _stairBottom, zEdge),
            new Vector3(x1, _stairBottom, zEdge),
            new Vector3(x0, _stairBottom, zToe),
            new Vector3(x1, _stairBottom, zToe)
        });
    }

    private void Mount(List<Vector3[]> hulls)
    {
        if (!IsInsideTree()) return;

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

        for (int i = 0; i < hulls.Count; i++)
        {
            var shape = new CollisionShape3D
            {
                Name = $"{Name}Col{i}",
                Shape = new ConvexPolygonShape3D { Points = hulls[i] },
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
