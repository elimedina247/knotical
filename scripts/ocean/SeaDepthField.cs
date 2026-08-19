using Godot;

namespace Knotical.Ocean;

/// <summary>
/// Bakes the Terrain3D seabed into the sea-state field the ocean samples: shallow water
/// calms the sea, baseline depth is exactly the authored settings, sculpted deeps grow
/// it. The finished multiplier — not raw depth — is stored in both a CPU array and a
/// texture, and both sides sample it bilinearly, which is what keeps physics and
/// rendering identical. Bakes deterministically from the terrain, so every client in a
/// multiplayer session derives the same field with nothing to sync.
///
/// Sculpting guide: seabed at CalmDepth below the waterline or shallower reads as
/// protected water; around BaselineDepth is the standard sea; dig toward DeepDepth
/// (below terrain height 0, which sits at BaselineDepth) for the big-water zones.
/// </summary>
[GlobalClass]
public partial class SeaDepthField : Node
{
    [Export(PropertyHint.Range, "64,1024,32")]
    public int Resolution { get; set; } = 256;

    [Export(PropertyHint.Range, "0,20,0.5")]
    public float CalmDepth { get; set; } = 5f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float CalmValue { get; set; } = 0.15f;

    [Export(PropertyHint.Range, "10,60,1")]
    public float BaselineDepth { get; set; } = 30f;

    [Export(PropertyHint.Range, "30,120,1")]
    public float DeepDepth { get; set; } = 55f;

    [Export(PropertyHint.Range, "1,3,0.05")]
    public float DeepValue { get; set; } = 1.6f;

    [Export] public bool BakeNow { get => false; set { if (value) Bake(); } }

    public bool Baked { get; private set; }

    public ImageTexture Texture { get; private set; }

    public float HalfExtent => Ocean.WorldHalfExtent;

    private float[] _field = System.Array.Empty<float>();
    private int _size;

    public override void _Ready()
    {
        CallDeferred(MethodName.Bake);
    }

    public override void _ExitTree()
    {
        if (Ocean.Instance?.DepthField == this) Ocean.Instance.SetDepthField(null);
    }

    public float Sample(Vector2 worldXZ)
    {
        if (!Baked) return 1f;

        float u = worldXZ.X / (HalfExtent * 2f) + 0.5f;
        float v = worldXZ.Y / (HalfExtent * 2f) + 0.5f;

        float px = Mathf.Clamp(u * _size - 0.5f, 0f, _size - 1f);
        float py = Mathf.Clamp(v * _size - 0.5f, 0f, _size - 1f);

        int x0 = (int)px;
        int y0 = (int)py;
        int x1 = Mathf.Min(x0 + 1, _size - 1);
        int y1 = Mathf.Min(y0 + 1, _size - 1);
        float fx = px - x0;
        float fy = py - y0;

        float a = Mathf.Lerp(_field[y0 * _size + x0], _field[y0 * _size + x1], fx);
        float b = Mathf.Lerp(_field[y1 * _size + x0], _field[y1 * _size + x1], fx);

        return Mathf.Lerp(a, b, fy);
    }

    private void Bake()
    {
        Node terrain = FindTerrain(GetTree().Root);

        if (terrain == null)
        {
            GD.Print("SeaDepthField: no Terrain3D found, sea is uniform");
            Baked = false;
            Ocean.Instance?.SetDepthField(null);
            return;
        }

        GodotObject data = terrain.Get("data").AsGodotObject();

        if (data == null)
        {
            GD.Print("SeaDepthField: Terrain3D has no data object, sea is uniform");
            Baked = false;
            Ocean.Instance?.SetDepthField(null);
            return;
        }

        ulong startTicks = Godot.Time.GetTicksMsec();
        _size = Resolution;
        _field = new float[_size * _size];

        var image = Image.CreateEmpty(_size, _size, false, Image.Format.Rf);
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < _size; y++)
        {
            float wz = ((y + 0.5f) / _size - 0.5f) * HalfExtent * 2f;

            for (int x = 0; x < _size; x++)
            {
                float wx = ((x + 0.5f) / _size - 0.5f) * HalfExtent * 2f;

                float h = (float)data.Call("get_height", new Vector3(wx, 0f, wz)).AsDouble();
                if (float.IsNaN(h)) h = 0f;

                float value = FieldFromDepth(Ocean.SeaLevel - h);
                _field[y * _size + x] = value;
                image.SetPixel(x, y, new Color(value, 0f, 0f));

                if (value < min) min = value;
                if (value > max) max = value;
            }
        }

        Texture = ImageTexture.CreateFromImage(image);
        Baked = true;
        Ocean.Instance?.SetDepthField(this);

        GD.Print($"SeaDepthField: baked {_size}x{_size} in {Godot.Time.GetTicksMsec() - startTicks} ms, " +
                 $"field {min:0.00}..{max:0.00}");
    }

    private float FieldFromDepth(float depth)
    {
        if (depth <= CalmDepth) return CalmValue;

        if (depth <= BaselineDepth)
        {
            return Mathf.Lerp(CalmValue, 1f,
                Mathf.SmoothStep(0f, 1f, (depth - CalmDepth) / Mathf.Max(BaselineDepth - CalmDepth, 0.1f)));
        }

        return Mathf.Lerp(1f, DeepValue,
            Mathf.SmoothStep(0f, 1f, (depth - BaselineDepth) / Mathf.Max(DeepDepth - BaselineDepth, 0.1f)));
    }

    private static Node FindTerrain(Node node)
    {
        if (node.GetClass() == "Terrain3D") return node;

        foreach (Node child in node.GetChildren())
        {
            Node found = FindTerrain(child);
            if (found != null) return found;
        }

        return null;
    }
}
