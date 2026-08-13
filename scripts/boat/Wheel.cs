using Godot;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class Wheel : MeshInstance3D
{
    private float _radius = 0.45f;
    private float _rimRadius = 0.045f;
    private float _hubRadius = 0.1f;
    private float _hubDepth = 0.18f;
    private float _spokeRadius = 0.032f;
    private float _handleLength = 0.16f;
    private int _spokes = 8;
    private int _sides = 8;
    private int _arc = 40;

    [Export(PropertyHint.Range, "0.1,2,0.005")]
    public float Radius { get => _radius; set { _radius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.005,0.3,0.001")]
    public float RimRadius { get => _rimRadius; set { _rimRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.02,0.5,0.005")]
    public float HubRadius { get => _hubRadius; set { _hubRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.02,1,0.005")]
    public float HubDepth { get => _hubDepth; set { _hubDepth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.005,0.2,0.001")]
    public float SpokeRadius { get => _spokeRadius; set { _spokeRadius = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,1,0.005")]
    public float HandleLength { get => _handleLength; set { _handleLength = value; Rebuild(); } }

    [Export(PropertyHint.Range, "3,16,1")]
    public int Spokes { get => _spokes; set { _spokes = value; Rebuild(); } }

    [Export(PropertyHint.Range, "3,24,1")]
    public int Sides { get => _sides; set { _sides = value; Rebuild(); } }

    [Export(PropertyHint.Range, "8,96,1")]
    public int Arc { get => _arc; set { _arc = value; Rebuild(); } }

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public override void _Ready()
    {
        Rebuild();
    }

    private void Rebuild()
    {
        int spokes = Mathf.Max(3, _spokes);
        int sides = Mathf.Max(3, _sides);
        float major = Mathf.Max(0.02f, _radius);
        float minor = Mathf.Max(0.002f, _rimRadius);
        float hub = Mathf.Max(0.005f, _hubRadius);

        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);

        Rim(tool, major, minor, Mathf.Max(8, _arc), sides);
        Tube(tool, new Vector3(0f, 0f, -_hubDepth * 0.5f), new Vector3(0f, 0f, _hubDepth * 0.5f), hub, sides * 2);

        for (int i = 0; i < spokes; i++)
        {
            float a = Mathf.Tau * i / spokes;
            var direction = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            Tube(tool, direction * (hub * 0.5f), direction * (major + _handleLength), _spokeRadius, sides);
        }

        var mesh = new ArrayMesh();
        tool.Commit(mesh);
        Mesh = mesh;

        float extent = major + _handleLength + _spokeRadius;
        float depth = Mathf.Max(_hubDepth * 0.5f, minor) + 0.02f;
        CustomAabb = new Aabb(
            new Vector3(-extent, -extent, -depth),
            new Vector3(extent * 2f, extent * 2f, depth * 2f));
    }

    private static void Rim(SurfaceTool tool, float major, float minor, int arc, int sides)
    {
        for (int i = 0; i < arc; i++)
        {
            float a0 = Mathf.Tau * i / arc;
            float a1 = Mathf.Tau * (i + 1) / arc;

            for (int j = 0; j < sides; j++)
            {
                float b0 = Mathf.Tau * j / sides;
                float b1 = Mathf.Tau * (j + 1) / sides;

                Vertex(tool, major, minor, a0, b0);
                Vertex(tool, major, minor, a1, b0);
                Vertex(tool, major, minor, a1, b1);

                Vertex(tool, major, minor, a0, b0);
                Vertex(tool, major, minor, a1, b1);
                Vertex(tool, major, minor, a0, b1);
            }
        }
    }

    private static void Vertex(SurfaceTool tool, float major, float minor, float a, float b)
    {
        var normal = new Vector3(Mathf.Cos(b) * Mathf.Cos(a), Mathf.Cos(b) * Mathf.Sin(a), Mathf.Sin(b));
        float ring = major + minor * Mathf.Cos(b);

        tool.SetNormal(normal);
        tool.AddVertex(new Vector3(ring * Mathf.Cos(a), ring * Mathf.Sin(a), minor * Mathf.Sin(b)));
    }

    private static void Tube(SurfaceTool tool, Vector3 from, Vector3 to, float radius, int sides)
    {
        Vector3 span = to - from;
        float length = span.Length();
        if (length < 1e-5f) return;

        Vector3 axis = span / length;
        Vector3 reference = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
        Vector3 side = reference.Cross(axis).Normalized();
        Vector3 other = axis.Cross(side);

        var low = new Vector3[sides];
        var high = new Vector3[sides];
        var radial = new Vector3[sides];

        for (int i = 0; i < sides; i++)
        {
            float a = Mathf.Tau * i / sides;
            radial[i] = side * Mathf.Cos(a) + other * Mathf.Sin(a);
            low[i] = from + radial[i] * radius;
            high[i] = to + radial[i] * radius;
        }

        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;

            tool.SetNormal(radial[i]);
            tool.AddVertex(low[i]);
            tool.SetNormal(radial[j]);
            tool.AddVertex(low[j]);
            tool.AddVertex(high[j]);

            tool.SetNormal(radial[i]);
            tool.AddVertex(low[i]);
            tool.SetNormal(radial[j]);
            tool.AddVertex(high[j]);
            tool.SetNormal(radial[i]);
            tool.AddVertex(high[i]);
        }

        for (int i = 1; i < sides - 1; i++)
        {
            tool.SetNormal(axis);
            tool.AddVertex(high[0]);
            tool.AddVertex(high[i]);
            tool.AddVertex(high[i + 1]);

            tool.SetNormal(-axis);
            tool.AddVertex(low[0]);
            tool.AddVertex(low[i + 1]);
            tool.AddVertex(low[i]);
        }
    }
}
