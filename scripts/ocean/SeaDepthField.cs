using Godot;

namespace Knotical.Ocean;

/// <summary>
/// Bakes the sculpted seabed into the sea-state field: shallow water is calm, deep water
/// is open sea.
///
/// The bake stores the finished multiplier rather than raw depth, so the CPU array and
/// the GPU texture are the same numbers sampled the same way. Both read it bilinearly,
/// which is what keeps buoyancy matching what is drawn near a coastline.
///
/// Terrain3D is a GDExtension, so its data is reached through Call rather than a typed
/// binding. With no terrain present the field reports open sea everywhere and everything
/// downstream behaves as if it were absent.
/// </summary>
[GlobalClass]
public partial class SeaDepthField : Node
{
    [Export] public NodePath Terrain { get; set; }

    [Export(PropertyHint.Range, "64,1024,64")]
    public int Resolution { get; set; } = 512;

    [Export(PropertyHint.Range, "500,8000,100")]
    public float HalfExtent { get; set; } = Ocean.WorldHalfExtent;

    /// <summary>Water this shallow is fully calm.</summary>
    [Export(PropertyHint.Range, "0,30,0.5")]
    public float CalmDepth { get; set; } = 3f;

    /// <summary>Water this deep is fully open sea.</summary>
    [Export(PropertyHint.Range, "1,120,1")]
    public float OpenDepth { get; set; } = 30f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ShallowCalm { get; set; } = 0.15f;

    [Export] public bool BakeNow { get => false; set { if (value) Bake(); } }

    private float[] _field = System.Array.Empty<float>();
    private int _size;
    private ImageTexture _texture;

    public ImageTexture Texture => _texture;

    public bool Baked => _size > 0;

    public override void _Ready()
    {
        Bake();
        Ocean.Instance?.SetDepthField(this);
    }

    public override void _ExitTree()
    {
        if (Ocean.Instance?.DepthField == this) Ocean.Instance.SetDepthField(null);
    }

    public void Bake()
    {
        GodotObject data = ResolveTerrainData(out string trouble);
        if (data == null)
        {
            _size = 0;
            _field = System.Array.Empty<float>();
            _texture = null;
            GD.Print($"sea depth: {trouble}, sea is uniform");
            return;
        }

        ulong started = Godot.Time.GetTicksMsec();

        int size = Mathf.Clamp(Resolution, 64, 1024);
        var field = new float[size * size];
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rf);

        float step = HalfExtent * 2f / (size - 1);

        for (int z = 0; z < size; z++)
        {
            float worldZ = -HalfExtent + z * step;

            for (int x = 0; x < size; x++)
            {
                float worldX = -HalfExtent + x * step;

                Variant sampled = data.Call("get_height", new Vector3(worldX, 0f, worldZ));
                float height = sampled.AsSingle();
                if (float.IsNaN(height)) height = 0f;

                float scale = ScaleForDepth(Ocean.SeaLevel - height);

                field[z * size + x] = scale;
                image.SetPixel(x, z, new Color(scale, 0f, 0f));
            }
        }

        _field = field;
        _size = size;
        _texture = ImageTexture.CreateFromImage(image);

        GD.Print($"sea depth: baked {size}x{size} over {HalfExtent * 2f:0} m " +
                 $"in {Godot.Time.GetTicksMsec() - started} ms");
    }

    private float ScaleForDepth(float depth)
    {
        if (depth <= 0f) return 0f;

        float open = Mathf.Max(OpenDepth, CalmDepth + 0.1f);
        return Mathf.Lerp(ShallowCalm, 1f, Mathf.SmoothStep(CalmDepth, open, depth));
    }

    private GodotObject ResolveTerrainData(out string trouble)
    {
        trouble = "no Terrain3D found";

        Node node = Terrain != null && !Terrain.IsEmpty ? GetNodeOrNull(Terrain) : null;
        node ??= FindTerrain(GetTree()?.CurrentScene) ?? FindTerrain(GetTree()?.Root);
        if (node == null) return null;

        foreach (string property in new[] { "data", "storage" })
        {
            Variant value = node.Get(property);
            if (value.VariantType != Variant.Type.Object) continue;

            GodotObject data = value.AsGodotObject();
            if (data != null && data.HasMethod("get_height")) return data;
        }

        trouble = "Terrain3D has no readable height data yet";
        return null;
    }

    private static Node FindTerrain(Node node)
    {
        if (node == null) return null;
        if (node.GetClass() == "Terrain3D") return node;

        foreach (Node child in node.GetChildren())
        {
            Node found = FindTerrain(child);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>
    /// Bilinear sample matching the GPU's texture() on the same data, so CPU physics and
    /// GPU rendering agree at coastlines.
    /// </summary>
    public float Sample(Vector2 worldXZ)
    {
        if (_size <= 0) return 1f;

        float u = (worldXZ.X / (HalfExtent * 2f) + 0.5f) * _size - 0.5f;
        float v = (worldXZ.Y / (HalfExtent * 2f) + 0.5f) * _size - 0.5f;

        int x0 = Mathf.FloorToInt(u);
        int z0 = Mathf.FloorToInt(v);
        float fx = u - x0;
        float fz = v - z0;

        float a = Texel(x0, z0);
        float b = Texel(x0 + 1, z0);
        float c = Texel(x0, z0 + 1);
        float d = Texel(x0 + 1, z0 + 1);

        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
    }

    private float Texel(int x, int z)
    {
        x = Mathf.Clamp(x, 0, _size - 1);
        z = Mathf.Clamp(z, 0, _size - 1);
        return _field[z * _size + x];
    }
}
