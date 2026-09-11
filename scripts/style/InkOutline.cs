using Godot;

namespace Knotical.Style;

[GlobalClass]
public partial class InkOutline : Node3D
{
    [Export] public Color Ink { get; set; } = new(0.08f, 0.06f, 0.12f, 1f);

    [Export(PropertyHint.Range, "0.5,4,0.1")]
    public float Thickness { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "0.001,0.2,0.001")]
    public float DepthSensitivity { get; set; } = 0.02f;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float NormalSensitivity { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "10,5000,10")]
    public float FadeStart { get; set; } = 250f;

    [Export(PropertyHint.Range, "10,8000,10")]
    public float FadeEnd { get; set; } = 900f;

    private ShaderMaterial _material;

    public override void _Ready()
    {
        _material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://shaders/ink_outline.gdshader"),
            RenderPriority = -8
        };

        var quad = new MeshInstance3D
        {
            Name = "InkQuad",
            Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            CustomAabb = new Aabb(new Vector3(-1e6f, -1e6f, -1e6f), new Vector3(2e6f, 2e6f, 2e6f))
        };

        AddChild(quad);
        Push();
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) Push();
    }

    private void Push()
    {
        if (_material == null) return;
        _material.SetShaderParameter("ink", Ink);
        _material.SetShaderParameter("thickness", Thickness);
        _material.SetShaderParameter("depth_sensitivity", DepthSensitivity);
        _material.SetShaderParameter("normal_sensitivity", NormalSensitivity);
        _material.SetShaderParameter("fade_start", FadeStart);
        _material.SetShaderParameter("fade_end", FadeEnd);
    }
}
