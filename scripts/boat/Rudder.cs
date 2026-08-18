using Godot;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class Rudder : MeshInstance3D, IFoil
{
    private const float WaterDensity = 1025f;

    private float _span = 4.7f;
    private float _headChord = 2.4f;
    private float _footChord = 2.8f;
    private float _thickness = 0.24f;
    private float _rake = 0.35f;
    private float _headRise;
    private float _steering;

    [Export(PropertyHint.Range, "0.2,20,0.05")]
    public float Span { get => _span; set { _span = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,12,0.05")]
    public float HeadChord { get => _headChord; set { _headChord = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,12,0.05")]
    public float FootChord { get => _footChord; set { _footChord = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.02,1,0.01")]
    public float Thickness { get => _thickness; set { _thickness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-4,4,0.05")]
    public float Rake { get => _rake; set { _rake = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,4,0.01")]
    public float HeadRise { get => _headRise; set { _headRise = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,60,0.5")]
    public float MaxAngleDegrees { get; set; } = 30f;

    [Export(PropertyHint.Range, "-1,1,0.005")]
    public float Steering
    {
        get => _steering;
        set { _steering = Mathf.Clamp(value, -1f, 1f); Swing(); }
    }

    [Export(PropertyHint.Range, "0,3,0.01")]
    public float NormalCoefficient { get; set; } = 1.6f;

    [Export(PropertyHint.Range, "0,200,0.5")]
    public float Gain { get; set; } = 12f;

    [Export(PropertyHint.Range, "0,20,0.1")]
    public float LowSpeedBite { get; set; } = 4f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float CentreOfPressure { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float BrakeRelief { get; set; } = 0.7f;

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public float Area => _span * (_headChord + _footChord) * 0.5f;

    public float Angle => _steering * Mathf.DegToRad(MaxAngleDegrees);

    public float Flow { get; private set; }

    public float StockTorque { get; private set; }

    public float Immersion { get; private set; } = 1f;

    public Vector3 Normal => GlobalBasis.X.Normalized();

    public Vector3 LocalCentreOfEffort
    {
        get
        {
            float sum = _headChord + _footChord;
            float s = sum > 0.0001f ? (_headChord + 2f * _footChord) / (3f * sum) : 0.5f;
            float chord = Mathf.Lerp(_headChord, _footChord, s);
            return new Vector3(0f, -s * _span, _rake * s + chord * CentreOfPressure);
        }
    }

    public Vector3 GlobalCentreOfEffort => GlobalTransform * LocalCentreOfEffort;

    public override void _Ready()
    {
        Rebuild();
        Swing();
    }

    public Vector3 ComputeForce(Vector3 pointVelocity)
    {
        Immersion = Submersion();

        float area = Area * Immersion;
        if (area <= 0.0001f)
        {
            Flow = 0f;
            StockTorque = 0f;
            return Vector3.Zero;
        }

        Vector3 stream = WaterVelocity() - pointVelocity;
        Flow = stream.Length();

        if (Flow <= 0.0001f)
        {
            StockTorque = 0f;
            return Vector3.Zero;
        }

        Vector3 n = Normal;
        float sin = Mathf.Clamp(stream.Dot(n) / Flow, -1f, 1f);
        float cos = Mathf.Sqrt(Mathf.Max(1f - sin * sin, 0f));

        float push = WaterDensity * NormalCoefficient * area * Flow * (Flow + LowSpeedBite) * sin * cos * Gain;

        StockTorque = LocalCentreOfEffort.Z * push;

        Vector3 f = n * push;

        if (BrakeRelief > 0f)
        {
            Vector3 downstream = stream / Flow;
            f -= downstream * (f.Dot(downstream) * BrakeRelief);
        }

        return f;
    }

    private Vector3 WaterVelocity()
    {
        OceanField ocean = OceanField.Instance;
        if (ocean == null) return Vector3.Zero;

        Vector3 at = GlobalCentreOfEffort;
        return new Vector3(0f, ocean.GetVerticalVelocity(new Vector2(at.X, at.Z)), 0f);
    }

    private float Submersion()
    {
        OceanField ocean = OceanField.Instance;
        if (ocean == null) return 1f;

        Vector3 head = GlobalPosition;
        Vector3 foot = GlobalTransform * new Vector3(0f, -_span, _rake);
        float surface = ocean.GetHeight(new Vector2(foot.X, foot.Z));

        return Mathf.Clamp((surface - foot.Y) / Mathf.Max(head.Y - foot.Y, 0.001f), 0f, 1f);
    }

    private void Swing()
    {
        Rotation = new Vector3(0f, Angle, 0f);
    }

    private void Rebuild()
    {
        float half = Mathf.Max(0.01f, _thickness) * 0.5f;
        float span = Mathf.Max(0.05f, _span);
        float head = Mathf.Max(0.05f, _headChord);
        float foot = Mathf.Max(0.05f, _footChord);

        Vector3 a = new(0f, 0f, 0f);
        Vector3 b = new(0f, _headRise, head);
        Vector3 c = new(0f, -span, _rake + foot);
        Vector3 d = new(0f, -span, _rake);

        var tool = new SurfaceTool();
        tool.Begin(Godot.Mesh.PrimitiveType.Triangles);

        Face(tool, Side(a, half), Side(b, half), Side(c, half), Side(d, half));
        Face(tool, Side(a, -half), Side(d, -half), Side(c, -half), Side(b, -half));
        Face(tool, Side(a, -half), Side(a, half), Side(d, half), Side(d, -half));
        Face(tool, Side(b, half), Side(b, -half), Side(c, -half), Side(c, half));
        Face(tool, Side(a, -half), Side(b, -half), Side(b, half), Side(a, half));
        Face(tool, Side(d, -half), Side(d, half), Side(c, half), Side(c, -half));

        var mesh = new ArrayMesh();
        tool.Commit(mesh);
        Mesh = mesh;
    }

    private static Vector3 Side(Vector3 point, float x) => new(x, point.Y, point.Z);

    private static void Face(SurfaceTool tool, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
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
}
