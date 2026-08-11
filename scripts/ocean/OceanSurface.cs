using Godot;

namespace Knotical.Ocean;

/// <summary>
/// Renders the sea as a set of nested grids that follow the camera.
///
/// Each level is a uniform grid snapped to its own cell size. Snapping is what stops
/// vertices sliding through the wave field as you sail — without it the whole surface
/// crawls. Levels overlap rather than tiling edge-to-edge: because displacement is a
/// pure function of world XZ, two grids agree wherever they overlap, so there are no
/// seams to stitch. Each outer level discards fragments over the region its finer
/// neighbour covers, so a coarse grid is simply never drawn where a fine one exists;
/// the small vertical stagger only prevents z-fighting in the thin hand-over ring.
/// </summary>
[GlobalClass]
public partial class OceanSurface : Node3D
{
    /// <summary>Cells per side, per level. Same for every level; only the extent changes.</summary>
    [Export(PropertyHint.Range, "16,512,16")]
    public int CellsPerLevel { get; set; } = 256;

    /// <summary>
    /// Half-extent of each level in metres, innermost first. Cell size is
    /// 2 * extent / CellsPerLevel — so 256 m over 256 cells gives 2 m cells.
    /// </summary>
    [Export]
    public float[] LevelExtents { get; set; } = { 256f, 1024f, 4096f, 8192f };

    [Export] public Shader OceanShader { get; set; }

    /// <summary>
    /// Authored wave spectrum. Handed to the Ocean autoload on ready, so the inspector
    /// on this node is the one place the sea gets tuned. Leave null for defaults.
    /// </summary>
    [Export] public OceanSettings Settings { get; set; }

    [Export] public Knotical.Style.GamePalette Palette { get; set; }

    [Export] public Node3D Seabed { get; set; }

    private readonly System.Collections.Generic.List<MeshInstance3D> _levels = new();
    private ShaderMaterial _material;
    private Camera3D _camera;
    private bool _skeletonPushed;

    // Reused every frame. Godot maps untyped arrays onto fixed-size shader array uniforms
    // the same way a GDScript array literal does, and rebuilding them each frame would
    // allocate 48 boxed values per frame for nothing.
    private readonly Godot.Collections.Array _packedWaves = new();
    private readonly Godot.Collections.Array _packedPhases = new();

    public override void _Ready()
    {
        Ocean.Instance?.SetSettings(Settings);
        Palette ??= new Knotical.Style.GamePalette();

        _material = new ShaderMaterial
        {
            Shader = OceanShader ?? GD.Load<Shader>("res://shaders/ocean.gdshader")
        };

        // The shader declares fixed-size arrays, so always send a full set with the
        // unused tail zeroed rather than a short array.
        _packedWaves.Resize(OceanSettings.MaxWaves);
        _packedPhases.Resize(OceanSettings.MaxWaves);

        for (int i = 0; i < LevelExtents.Length; i++)
        {
            var instance = new MeshInstance3D
            {
                Name = $"OceanLevel{i}",
                Mesh = BuildGrid(LevelExtents[i], CellsPerLevel),
                MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };

            AddChild(instance);
            instance.SetInstanceShaderParameter("inner_extent", i == 0 ? 0f : LevelExtents[i - 1] * 0.95f);
            _levels.Add(instance);
        }

        PushWaveUniforms();
    }

    public override void _Process(double delta)
    {
        // Amplitudes are re-solved from the wind every frame, so this is no longer a
        // one-shot. Ocean is an autoload and so normally ready first, but PushWaveUniforms
        // retries the skeleton rather than silently rendering a flat plane if scene order
        // ever changes.
        PushWaveUniforms();

        _camera ??= GetViewport().GetCamera3D();
        if (_camera == null) return;

        Vector3 eye = _camera.GlobalPosition;

        // Snap each level independently to its own cell size.
        for (int i = 0; i < _levels.Count; i++)
        {
            float cell = LevelExtents[i] * 2f / CellsPerLevel;
            float x = Mathf.Floor(eye.X / cell) * cell;
            float z = Mathf.Floor(eye.Z / cell) * cell;

            _levels[i].GlobalPosition = new Vector3(x, -0.05f * i, z);
        }

        _material.SetShaderParameter("wave_time", (float)(Ocean.Instance?.Time ?? 0.0));

        float submerged = 0f;
        if (Ocean.Instance != null && eye.Y < Ocean.Instance.GetHeight(new Vector2(eye.X, eye.Z)))
        {
            submerged = 1f;
        }

        _material.SetShaderParameter("camera_submerged", submerged);
    }

    /// <summary>
    /// Ships the live spectrum to the shader. Safe to call every frame — the unchanging
    /// half (phases, count) is only pushed once, and the rest is 24 vectors.
    /// </summary>
    public void PushWaveUniforms()
    {
        Ocean ocean = Ocean.Instance;
        if (ocean == null || _material == null) return;

        if (!_skeletonPushed)
        {
            for (int i = 0; i < OceanSettings.MaxWaves; i++)
            {
                _packedPhases[i] = i < ocean.Phases.Length ? ocean.Phases[i] : 0f;
            }

            _material.SetShaderParameter("wave_phase", _packedPhases);
            _material.SetShaderParameter("wave_count", ocean.Waves.Length);
            _skeletonPushed = true;
        }

        for (int i = 0; i < OceanSettings.MaxWaves; i++)
        {
            _packedWaves[i] = i < ocean.Waves.Length ? ocean.Waves[i] : Vector4.Zero;
        }

        _material.SetShaderParameter("waves", _packedWaves);
        _material.SetShaderParameter("ka_sum", ocean.SteepnessNormaliser);
        _material.SetShaderParameter("steepness", ocean.Settings.Steepness);
        _material.SetShaderParameter("significant_height", ocean.SignificantHeight);
        PushPalette();

        Seabed ??= GetTree().CurrentScene?.FindChild("Seabed", true, false) as Node3D;
        if (Seabed != null)
        {
            _material.SetShaderParameter("seabed_height", Seabed.GlobalPosition.Y);
        }

        if (Knotical.Sky.DayCycle.Instance != null)
        {
            _material.SetShaderParameter("sky_color", Knotical.Sky.DayCycle.Instance.WaterHorizonColor);
        }
    }

    private void PushPalette()
    {
        Color[] ramp = Palette.WaterRamp;
        if (ramp == null || ramp.Length == 0) return;

        int count = Mathf.Min(ramp.Length, 8);
        var packed = new Godot.Collections.Array();
        for (int i = 0; i < 8; i++)
        {
            Color c = ramp[Mathf.Min(i, count - 1)].SrgbToLinear();
            packed.Add(new Vector3(c.R, c.G, c.B));
        }

        Color ring = Palette.CalmRing.SrgbToLinear();

        _material.SetShaderParameter("water_ramp", packed);
        _material.SetShaderParameter("water_ramp_size", count);
        _material.SetShaderParameter("foam_color", Palette.Foam);
        _material.SetShaderParameter("sand_color", Palette.Sand);
        _material.SetShaderParameter("ring_color", new Vector3(ring.R, ring.G, ring.B));
    }

    /// <summary>Call after rebuilding the skeleton so phases and count are re-sent.</summary>
    public void InvalidateSkeleton() => _skeletonPushed = false;

    /// <summary>
    /// Flat grid centred on the origin. The vertex shader supplies all displacement,
    /// so the custom AABB has to be inflated by hand or Godot culls the mesh the moment
    /// its flat bounds leave the frustum.
    /// </summary>
    private static ArrayMesh BuildGrid(float halfExtent, int cells)
    {
        int verts = cells + 1;
        float step = halfExtent * 2f / cells;

        var positions = new Vector3[verts * verts];
        var normals = new Vector3[verts * verts];
        var indices = new int[cells * cells * 6];

        for (int z = 0; z < verts; z++)
        {
            for (int x = 0; x < verts; x++)
            {
                int i = z * verts + x;
                positions[i] = new Vector3(-halfExtent + x * step, 0f, -halfExtent + z * step);
                normals[i] = Vector3.Up;
            }
        }

        int t = 0;
        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                int tl = z * verts + x;
                int tr = tl + 1;
                int bl = tl + verts;
                int br = bl + 1;

                indices[t++] = tl; indices[t++] = bl; indices[t++] = tr;
                indices[t++] = tr; indices[t++] = bl; indices[t++] = br;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        mesh.CustomAabb = new Aabb(
            new Vector3(-halfExtent, -60f, -halfExtent),
            new Vector3(halfExtent * 2f, 120f, halfExtent * 2f));

        return mesh;
    }
}
