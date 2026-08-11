using Godot;
using Knotical.Weather;

namespace Knotical.Sky;

[GlobalClass]
public partial class SkyController : Node
{
    [Export] public DirectionalLight3D Light { get; set; }
    [Export] public WorldEnvironment EnvironmentNode { get; set; }
    [Export] public DayCycleSettings Settings { get; set; }
    [Export] public float CloudDriftScale { get; set; } = 0.007f;

    private ShaderMaterial _sky;

    public override void _Ready()
    {
        DayCycle.Instance?.SetSettings(Settings);
    }

    public override void _Process(double delta)
    {
        DayCycle cycle = DayCycle.Instance;
        if (cycle == null) return;

        Light ??= FindLight();
        _sky ??= ResolveSkyMaterial();

        if (Light != null)
        {
            Light.LightEnergy = cycle.LightEnergy;
            Light.LightColor = cycle.LightColor;

            Vector3 dir = cycle.LightDirection;
            if (dir.Y > 0.001f)
            {
                Vector3 up = Mathf.Abs(dir.Y) > 0.99f ? Vector3.Back : Vector3.Up;
                Light.GlobalTransform = new Transform3D(Basis.LookingAt(-dir, up), Light.GlobalPosition);
            }
        }

        if (_sky == null) return;

        _sky.SetShaderParameter("sun_direction", cycle.SunDirection);
        _sky.SetShaderParameter("moon_direction", cycle.MoonDirection);

        Wind wind = Wind.Instance;
        if (wind != null)
        {
            _sky.SetShaderParameter("cloud_offset", wind.AccumulatedDrift * CloudDriftScale);
        }
    }

    private DirectionalLight3D FindLight()
    {
        Node scene = GetTree().CurrentScene;
        if (scene == null) return null;

        foreach (Node node in scene.FindChildren("*", "DirectionalLight3D", true, false))
        {
            return node as DirectionalLight3D;
        }

        return null;
    }

    private ShaderMaterial ResolveSkyMaterial()
    {
        Godot.Environment env = EnvironmentNode?.Environment ?? GetViewport().World3D?.Environment;
        return env?.Sky?.SkyMaterial as ShaderMaterial;
    }
}
