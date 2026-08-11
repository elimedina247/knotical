using Godot;

namespace Knotical.Ocean;

/// <summary>
/// Renders the sea as a set of nested grids that follow the camera.
///
/// Each level is a uniform grid snapped to its own cell size. Snapping is what stops
/// vertices sliding through the wave field as you sail — without it the whole surface
/// crawls. Levels overlap rather than tiling edge-to-edge: because displacement is a
/// pure function of world XZ, two grids agree wherever they overlap, so there are no
/// seams to stitch. Each successive level sits a few centimetres lower purely to stop
/// z-fighting in the overlap.
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
    public float[] LevelExtents { get; set; } = { 256f, 1024f, 4096f };

    [Export] public Shader OceanShader { get; set; }

    /// <summary>
    /// Authored wave spectrum. Handed to the Ocean autoload on ready, so the inspector
    /// on this node is the one place the sea gets tuned. Leave null for defaults.
    /// </summary>
    [Export] public OceanSettings Settings { get; set; }

    private readonly System.Collections.Generic.List<MeshInstance3D> _levels = new();
    private ShaderMaterial _material;
    private Camera3D _camera;
    private bool _uniformsPushed;

    public override void _Ready()
    {
        Ocean.Instance?.SetSettings(Settings);

        _material = new ShaderMaterial
        {
            Shader = OceanShader ?? GD.Load<Shader>("res://shaders/ocean.gdshader")
        };

        for (int i = 0; i < LevelExtents.Length; i++)
        {
            var instance = new MeshInstance3D
            {
                Name = $"OceanLevel{i}",
                Mesh = BuildGrid(LevelExtents[i], CellsPerLevel),
                MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0f, -0.05f * i, 0f)
            };

            AddChild(instance);
            _levels.Add(instance);
        }

        PushWaveUniforms();
    }

    public override void _Process(double delta)
    {
        // Ocean is an autoload so it is normally ready first, but retry rather than
        // silently rendering a flat plane if scene order ever changes.
        if (!_uniformsPushed) PushWaveUniforms();

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
    }

    /// <summary>Call after editing OceanSettings so the shader picks up the new spectrum.</summary>
    public void PushWaveUniforms()
    {
        Ocean ocean = Ocean.Instance;
        if (ocean == null || _material == null) return;

        // The shader declares fixed-size arrays, so always send a full set with the
        // unused tail zeroed rather than a short array.
        // Untyped arrays: Godot maps these onto fixed-size shader array uniforms the
        // same way a GDScript array literal does.
        var packed = new Godot.Collections.Array();
        var phases = new Godot.Collections.Array();
        for (int i = 0; i < OceanSettings.MaxWaves; i++)
        {
            packed.Add(i < ocean.Waves.Length ? ocean.Waves[i] : Vector4.Zero);
            phases.Add(i < ocean.Phases.Length ? ocean.Phases[i] : 0f);
        }

        _material.SetShaderParameter("waves", packed);
        _material.SetShaderParameter("wave_phase", phases);
        _material.SetShaderParameter("wave_count", ocean.Waves.Length);
        _material.SetShaderParameter("steepness", ocean.Settings.Steepness);
        _uniformsPushed = true;
    }

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
