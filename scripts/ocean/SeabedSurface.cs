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

    private ShaderMaterial _material;

    public override void _Ready()
    {
        Palette ??= new GamePalette();
        _material = MaterialOverride as ShaderMaterial ?? GetSurfaceOverrideMaterial(0) as ShaderMaterial;
        if (_material == null) return;

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
        _material.SetShaderParameter("wave_time", (float)(Ocean.Instance?.Time ?? 0.0));
    }
}
