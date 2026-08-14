using Godot;
using Knotical.Weather;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class Sail : MeshInstance3D, IFoil
{
    private const float AirDensity = 1.225f;

    private float _headWidth = 20f;
    private float _footWidth = 22f;
    private float _drop = 12f;
    private int _panelsAcross = 24;
    private int _panelsDown = 24;
    private float _camber = 0.30f;
    private float _furlThickness = 0.55f;
    private float _luffAmplitude = 0.55f;
    private float _deployment = 1f;

    private ShaderMaterial _material;
    private float _pressure = 1f;
    private float _windStrength = 1f;
    private float _brace;

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

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float LuffKnee { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float LeechFlutter { get; set; } = 0.55f;

    [Export(PropertyHint.Range, "1,40,0.5")]
    public float FullWindSpeed { get; set; } = 12f;

    [Export(PropertyHint.Range, "0.05,2,0.01")]
    public float FillEase { get; set; } = 0.55f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float MinFill { get; set; } = 0.55f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float GustDepth { get; set; } = 0.22f;

    [Export(PropertyHint.Range, "0.2,1.5,0.01")]
    public float BellyRoundness { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0.15,0.85,0.01")]
    public float DraftPosition { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0.1,0.9,0.01")]
    public float DraftHeight { get; set; } = 0.55f;

    [Export(PropertyHint.Range, "0.1,4,0.01")]
    public float FootFreedom { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "0,0.9,0.01")]
    public float FootPinch { get; set; } = 0.45f;

    [Export(PropertyHint.Range, "0,1.5,0.01")]
    public float FootScallop { get; set; } = 0.55f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ClewTension { get; set; } = 0.85f;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float WrinkleDepth { get; set; } = 0.75f;

    [Export(PropertyHint.Range, "1,40,0.5")]
    public float WrinkleScale { get; set; } = 7f;

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
    public float DriveGain { get; set; } = 30f;

    [Export(PropertyHint.Range, "0,4,0.01")]
    public float LiftCoefficient { get; set; } = 1.6f;

    [Export(PropertyHint.Range, "0,4,0.01")]
    public float DragCoefficient { get; set; } = 1.3f;

    [Export(PropertyHint.Range, "0,1,0.005")]
    public float DragBase { get; set; } = 0.08f;

    [Export] public Node3D Boom { get; set; }

    [Export] public bool AutoTrim { get; set; } = true;

    [Export(PropertyHint.Range, "0,90,1")]
    public float MaxBraceDegrees { get; set; } = 80f;

    [Export(PropertyHint.Range, "0.05,6,0.05")]
    public float TrimRate { get; set; } = 0.7f;

    [Export] public Color CanvasColor { get; set; } = new(0.855f, 0.816f, 0.718f);

    [Export] public Color SeamColor { get; set; } = new(0.702f, 0.655f, 0.549f);

    [Export(PropertyHint.Range, "0,32,1")]
    public int Seams { get; set; } = 9;

    [Export(PropertyHint.Range, "0,40,0.5")]
    public float PreviewWindSpeed { get; set; } = 9f;

    [Export(PropertyHint.Range, "0,360,1")]
    public float PreviewWindDeg { get; set; } = 35f;

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    private float Phase
    {
        get
        {
            uint h = 2166136261u;
            foreach (char c in Name.ToString())
            {
                h = (h ^ c) * 16777619u;
            }

            return (h % 977u) * 0.0213f;
        }
    }

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

    public float Exposure { get; private set; } = 1f;

    public float Brace => _brace;

    public override void _Ready()
    {
        if (!Engine.IsEditorHint()) TargetDeployment = _deployment;

        Boom ??= GetParentOrNull<Node3D>();
        _brace = Boom?.Rotation.Y ?? 0f;

        Rebuild();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint() || !AutoTrim || Boom == null) return;
        if (_deployment <= 0.01f) return;

        Node3D rig = Boom.GetParentOrNull<Node3D>();
        if (rig == null) return;

        Vector3 wind = SampleWind();
        if (rig is RigidBody3D body) wind -= body.LinearVelocity;
        wind.Y = 0f;

        Vector3 local = rig.GlobalBasis.Inverse() * wind;
        if (local.LengthSquared() < 0.01f) return;

        float bearing = Mathf.Atan2(local.X, -local.Z);
        float off = Mathf.Pi - Mathf.Abs(bearing);
        float attack = 0.5f * Mathf.Atan2(
            2f * LiftCoefficient * Mathf.Sin(off),
            Mathf.Max(DragCoefficient, 0.01f) * Mathf.Cos(off));

        float limit = Mathf.DegToRad(MaxBraceDegrees);
        float want = Mathf.Clamp(
            Mathf.Sign(bearing) * (Mathf.Pi * 0.5f - attack) - bearing, -limit, limit);

        float swing = Mathf.Max(TrimRate, 0f) * (float)delta;
        _brace += Mathf.Clamp(Mathf.AngleDifference(_brace, want), -swing, swing);

        Vector3 rotation = Boom.Rotation;
        Boom.Rotation = new Vector3(rotation.X, _brace, rotation.Z);
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
        Exposure = Windage();

        float area = Area * Exposure;
        if (area <= 0.0001f) return Vector3.Zero;

        Vector3 n = Normal;
        n.Y = 0f;
        if (n.LengthSquared() < 1e-6f) return Vector3.Zero;
        n = n.Normalized();

        Vector3 apparent = SampleWind() - pointVelocity;
        apparent.Y = 0f;

        float speed = apparent.Length();
        if (speed < 0.01f) return Vector3.Zero;

        Vector3 w = apparent / speed;
        float attack = Mathf.Clamp(w.Dot(n), -1f, 1f);

        float force = 0.5f * AirDensity * area * speed * speed * DriveGain;
        Vector3 lift = (n - w * attack) * (2f * LiftCoefficient * attack);
        Vector3 drag = w * (DragBase + DragCoefficient * attack * attack);

        return (lift + drag) * force;
    }

    private float Windage()
    {
        if (Area <= 0.0001f) return 0f;

        OceanField ocean = OceanField.Instance;
        if (ocean == null) return 1f;

        Vector3 head = GlobalPosition;
        Vector3 foot = GlobalTransform * new Vector3(0f, -_drop * _deployment, 0f);

        float top = Mathf.Max(head.Y, foot.Y);
        float bottom = Mathf.Min(head.Y, foot.Y);
        float surface = ocean.GetHeight(new Vector2(foot.X, foot.Z));

        return Mathf.Clamp((top - surface) / Mathf.Max(top - bottom, 0.001f), 0f, 1f);
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
        float full = Mathf.Max(0.5f, FullWindSpeed);

        _pressure = Mathf.Clamp(wind.Dot(Normal) / full, -1f, 1f);
        _windStrength = Mathf.Clamp(wind.Length() / full, 0f, 1f);
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
        var tangents = new float[verts.Length * 4];
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
                tangents[k * 4] = 1f;
                tangents[k * 4 + 1] = 0f;
                tangents[k * 4 + 2] = 0f;
                tangents[k * 4 + 3] = 1f;
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
        arrays[(int)Mesh.ArrayType.Tangent] = tangents;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        Mesh = mesh;

        float chord = (_headWidth + _footWidth) * 0.5f;
        float halfWidth = Mathf.Max(_headWidth, _footWidth) * 0.5f + _furlThickness + 1f;
        float reach = _camber * chord * 1.8f + _luffAmplitude * chord * 0.15f + _furlThickness + 1f;
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
        _material.SetShaderParameter("wind_strength", _windStrength);
        _material.SetShaderParameter("fill_ease", FillEase);
        _material.SetShaderParameter("min_fill", MinFill);
        _material.SetShaderParameter("gust_depth", GustDepth);
        _material.SetShaderParameter("belly_roundness", BellyRoundness);
        _material.SetShaderParameter("draft_position", DraftPosition);
        _material.SetShaderParameter("draft_height", DraftHeight);
        _material.SetShaderParameter("foot_freedom", FootFreedom);
        _material.SetShaderParameter("foot_pinch", FootPinch);
        _material.SetShaderParameter("foot_scallop", FootScallop);
        _material.SetShaderParameter("clew_tension", ClewTension);
        _material.SetShaderParameter("furl_thickness", _furlThickness);
        _material.SetShaderParameter("luff_amplitude", _luffAmplitude);
        _material.SetShaderParameter("luff_speed", LuffSpeed);
        _material.SetShaderParameter("luff_knee", LuffKnee);
        _material.SetShaderParameter("leech_flutter", LeechFlutter);
        _material.SetShaderParameter("wrinkle_depth", WrinkleDepth);
        _material.SetShaderParameter("wrinkle_scale", WrinkleScale);
        _material.SetShaderParameter("phase", Phase);
        _material.SetShaderParameter("canvas_color", CanvasColor);
        _material.SetShaderParameter("seam_color", SeamColor);
        _material.SetShaderParameter("seams", (float)Seams);
    }
}
