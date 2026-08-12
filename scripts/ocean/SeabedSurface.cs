using Godot;
using Knotical.Style;

namespace Knotical.Ocean;

/// <summary>
/// Placeholder sea floor. Draws the same procedural pattern that ocean.gdshader
/// paints onto the water from above, so the bottom does not change appearance as
/// the camera crosses the waterline.
/// </summary>
[GlobalClass]
public partial class SeabedSurface : MeshInstance3D
{
    [Export] public GamePalette Palette { get; set; }

    [Export] public Shader SeabedShader { get; set; }

    private ShaderMaterial _material;

    public override void _Ready()
    {
        Palette ??= new GamePalette();

        _material = new ShaderMaterial
        {
            Shader = SeabedShader ?? GD.Load<Shader>("res://shaders/seabed.gdshader")
        };

        MaterialOverride = _material;

        Color[] ramp = Palette.WaterRamp;
        _material.SetShaderParameter("sand_color", Palette.Sand);
        _material.SetShaderParameter("seabed_dark_color", Palette.SeabedDark);

        if (ramp != null && ramp.Length > 0)
        {
            _material.SetShaderParameter("murk_color", ramp[0]);
        }
    }

    public override void _Process(double delta)
    {
        if (_material == null) return;

        Ocean ocean = Ocean.Instance;
        if (ocean != null)
        {
            _material.SetShaderParameter("wave_time", (float)ocean.Time);
        }

        Knotical.Sky.DayCycle cycle = Knotical.Sky.DayCycle.Instance;
        if (cycle != null)
        {
            _material.SetShaderParameter("light_direction", cycle.LightDirection);
        }
    }
}
