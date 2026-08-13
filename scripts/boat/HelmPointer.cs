using Godot;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class HelmPointer : MeshInstance3D
{
    private float _width = 0.075f;
    private float _height = 0.1f;
    private float _stemWidth = 0.026f;
    private float _headHeight = 0.055f;
    private float _thickness = 0.014f;

    private Vector3 _home;
    private bool _homed;

    [Export(PropertyHint.Range, "0.01,0.5,0.001")]
    public float Width { get => _width; set { _width = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.01,0.5,0.001")]
    public float Height { get => _height; set { _height = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.005,0.3,0.001")]
    public float StemWidth { get => _stemWidth; set { _stemWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.005,0.4,0.001")]
    public float HeadHeight { get => _headHeight; set { _headHeight = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.002,0.1,0.001")]
    public float Thickness { get => _thickness; set { _thickness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,1,0.005")]
    public float Travel { get; set; } = 0.13f;

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public override void _Ready()
    {
        Home();
        Rebuild();
    }

    public void Show(float steering)
    {
        Home();
        Position = _home + new Vector3(Mathf.Clamp(steering, -1f, 1f) * Travel, 0f, 0f);
    }

    private void Home()
    {
        if (_homed) return;

        _home = Position;
        _homed = true;
    }

    private void Rebuild()
    {
        float half = Mathf.Max(0.001f, _thickness) * 0.5f;
        float wing = Mathf.Max(0.005f, _width) * 0.5f;
        float stem = Mathf.Min(Mathf.Max(0.002f, _stemWidth) * 0.5f, wing);
        float head = Mathf.Min(Mathf.Max(0.002f, _headHeight), _height);
        float neck = _height - head;

        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);

        if (neck > 0.0005f)
        {
            Prism(tool, half, new Vector2(-stem, 0f), new Vector2(stem, 0f), new Vector2(stem, neck), new Vector2(-stem, neck));
        }

        Prism(tool, half, new Vector2(-wing, neck), new Vector2(wing, neck), new Vector2(0f, _height));

        var mesh = new ArrayMesh();
        tool.Commit(mesh);
        Mesh = mesh;
    }

    private static void Prism(SurfaceTool tool, float half, params Vector2[] outline)
    {
        int n = outline.Length;

        for (int i = 1; i < n - 1; i++)
        {
            Face(tool, Vector3.Back,
                Flat(outline[0], half), Flat(outline[i], half), Flat(outline[i + 1], half));

            Face(tool, Vector3.Forward,
                Flat(outline[0], -half), Flat(outline[i + 1], -half), Flat(outline[i], -half));
        }

        for (int i = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            Vector2 edge = outline[j] - outline[i];
            var normal = new Vector3(edge.Y, -edge.X, 0f).Normalized();

            tool.SetNormal(normal);
            tool.AddVertex(Flat(outline[j], half));
            tool.AddVertex(Flat(outline[i], half));
            tool.AddVertex(Flat(outline[i], -half));

            tool.SetNormal(normal);
            tool.AddVertex(Flat(outline[j], half));
            tool.AddVertex(Flat(outline[i], -half));
            tool.AddVertex(Flat(outline[j], -half));
        }
    }

    private static void Face(SurfaceTool tool, Vector3 normal, Vector3 p0, Vector3 p1, Vector3 p2)
    {
        tool.SetNormal(normal);
        tool.AddVertex(p0);
        tool.AddVertex(p1);
        tool.AddVertex(p2);
    }

    private static Vector3 Flat(Vector2 point, float z) => new(point.X, point.Y, z);
}
