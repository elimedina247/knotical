using Godot;
using Knotical.Weather;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class Sail : MeshInstance3D
{
    private const float AirDensity = 1.225f;

    private float _headWidth = 20f;
    private float _footWidth = 22f;
    private float _drop = 12f;
    private int _panelsAcross = 12;
    private int _panelsDown = 16;
    private float _camber = 0.16f;
    private float _furlThickness = 0.55f;
    private float _luffAmplitude = 0.35f;
    private float _deployment = 1f;

    private ShaderMaterial _material;
    private float _pressure = 1f;

    [Export(PropertyHint.Range, "0.5,60,0.1")]
    public float HeadWidth { get => _headWidth; set { _headWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,60,0.1")]
    public float FootWidth { get => _footWidth; set { _footWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,40,0.1")]
    public float Drop { get => _drop; set { _drop = value; Rebuild(); } }

    [Export(PropertyHint.Range, "2,48,1")]
    public int PanelsAcross { get => _panelsAcross; set { _panelsAcross = value; Rebuild(); } }

    [Export(PropertyHint.Range, "2,64,1")]
    public int PanelsDown { get => _panelsDown; set { _panelsDown = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,0.8,0.005")]
    public float Camber { get => _camber; set { _camber = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,3,0.01")]
    public float FurlThickness { get => _furlThickness; set { _furlThickness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float LuffAmplitude { get => _luffAmplitude; set { _luffAmplitude = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,20,0.1")]
    public float LuffSpeed { get; set; } = 5f;

    [Export(PropertyHint.Range, "0,1,0.001")]
    public float Deployment
    {
        get => _deployment;
        set { _deployment = Mathf.Clamp(value, 0f, 1f); PushUniforms(); }
    }

    [Export(PropertyHint.Range, "0,1,0.001")]
    public float TargetDeployment { get; set; } = 1f;

    [Export(PropertyHint.Range, "0,2,0.005")]
    public float HaulRate { get; set; } = 0.12f;

    [Export(PropertyHint.Range, "0,200,0.5")]
    public float DriveGain { get; set; } = 35f;

    [Export(PropertyHint.Range, "0,3,0.01")]
    public float NormalCoefficient { get; set; } = 1.2f;

    [Export] public Color CanvasColor { get; set; } = new(0.855f, 0.816f, 0.718f);

    [Export] public Color SeamColor { get; set; } = new(0.702f, 0.655f, 0.549f);

    [Export(PropertyHint.Range, "0,32,1")]
    public int Seams { get; set; } = 9;

    [Export(PropertyHint.Range, "0,40,0.5")]
    public float PreviewWindSpeed { get; set; } = 9f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float PreviewWindDeg { get; set; } = 35f;

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public float DeployedFootWidth => Mathf.Lerp(_headWidth, _footWidth, _deployment);

    public float Area => _drop * _deployment * (_headWidth + DeployedFootWidth) * 0.5f;

    public Vector3 LocalCentreOfEffort
    {
        get
        {
            float a = _headWidth;
            float b = DeployedFootWidth;
            float sum = a + b;
            float s = sum > 0.0001f ? (a + 2f * b) / (3f * sum) : 0.5f;
            return new Vector3(0f, -s * _drop * _deployment, 0f);
        }
    }

    public Vector3 GlobalCentreOfEffort => GlobalTransform * LocalCentreOfEffort;

    public Vector3 Normal => GlobalBasis.Z.Normalized();

    public float Pressure => _pressure;

    public override void _Ready()
    {
        if (!Engine.IsEditorHint()) TargetDeployment = _deployment;
        Rebuild();
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint() && HaulRate > 0f)
        {
            _deployment = Mathf.MoveToward(_deployment, Mathf.Clamp(TargetDeployment, 0f, 1f),
                HaulRate * (float)delta);
        }

        UpdatePressure();
        PushUniforms();
    }

    public Vector3 ComputeForce(Vector3 pointVelocity)
    {
        if (Area <= 0.0001f) return Vector3.Zero;

        Vector3 apparent = SampleWind() - pointVelocity;
        Vector3 n = Normal;
        float along = apparent.Dot(n);

        return n * (0.5f * AirDensity * NormalCoefficient * Area * along * Mathf.Abs(along) * DriveGain);
    }

    public Vector3 SampleWind()
    {
        if (!Engine.IsEditorHint() && Wind.Instance != null)
        {
            Vector3 p = GlobalPosition;
            Vector2 w = Wind.Instance.GetVelocity(new Vector2(p.X, p.Z));
            return new Vector3(w.X, 0f, w.Y);
        }

        float a = Mathf.DegToRad(PreviewWindDeg);
        return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * PreviewWindSpeed;
    }

    private void UpdatePressure()
    {
        Vector3 wind = SampleWind();
        float speed = wind.Length();
        _pressure = speed > 0.01f ? Mathf.Clamp(wind.Dot(Normal) / speed, -1f, 1f) : 0f;
    }

    private void Rebuild()
    {
        int nu = Mathf.Max(2, _panelsAcross);
        int nv = Mathf.Max(2, _panelsDown);
        int cols = nu + 1;
        int rows = nv + 1;

        var verts = new Vector3[cols * rows];
        var uvs = new Vector2[verts.Length];
        var normals = new Vector3[verts.Length];
        var indices = new int[nu * nv * 6];

        for (int j = 0; j < rows; j++)
        {
            float v = (float)j / nv;
            float w = Mathf.Lerp(_headWidth, _footWidth, v);

            for (int i = 0; i < cols; i++)
            {
                float u = (float)i / nu;
                int k = j * cols + i;
                verts[k] = new Vector3((u - 0.5f) * w, -v * _drop, 0f);
                uvs[k] = new Vector2(u, v);
                normals[k] = Vector3.Back;
            }
        }

        int n = 0;
        for (int j = 0; j < nv; j++)
        {
            for (int i = 0; i < nu; i++)
            {
                int a = j * cols + i;
                int b = a + 1;
                int c = a + cols + 1;
                int d = a + cols;

                indices[n++] = a;
                indices[n++] = d;
                indices[n++] = c;
                indices[n++] = a;
                indices[n++] = c;
                indices[n++] = b;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Mesh = mesh;

        float halfWidth = Mathf.Max(_headWidth, _footWidth) * 0.5f + _furlThickness + 1f;
        float reach = _camber * _drop + _luffAmplitude + _furlThickness + 1f;
        CustomAabb = new Aabb(
            new Vector3(-halfWidth, -_drop - 1f, -reach),
            new Vector3(halfWidth * 2f, _drop + 2f, reach * 2f));

        PushUniforms();
    }

    private void PushUniforms()
    {
        if (_material == null)
        {
            _material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/sail.gdshader") };
        }

        if (MaterialOverride != _material) MaterialOverride = _material;

        _material.SetShaderParameter("deployment", _deployment);
        _material.SetShaderParameter("head_width", _headWidth);
        _material.SetShaderParameter("foot_width", _footWidth);
        _material.SetShaderParameter("drop", _drop);
        _material.SetShaderParameter("camber", _camber);
        _material.SetShaderParameter("pressure", _pressure);
        _material.SetShaderParameter("furl_thickness", _furlThickness);
        _material.SetShaderParameter("luff_amplitude", _luffAmplitude);
        _material.SetShaderParameter("luff_speed", LuffSpeed);
        _material.SetShaderParameter("canvas_color", CanvasColor);
        _material.SetShaderParameter("seam_color", SeamColor);
        _material.SetShaderParameter("seams", (float)Seams);
    }
}
