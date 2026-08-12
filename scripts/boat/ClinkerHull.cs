using Godot;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class ClinkerHull : MeshInstance3D
{
    private float _length = 17f;
    private float _beam = 6.2f;
    private float _draft = 2.1f;
    private float _freeboard = 2.3f;
    private float _deckHeight = 1.35f;
    private float _bowSheerRise = 1.6f;
    private float _sternSheerRise = 1.2f;
    private float _sheerPower = 2.4f;
    private float _rocker = 1.75f;
    private float _rockerPower = 2.6f;
    private float _bowSharpness = 2.2f;
    private float _sternSharpness = 3f;
    private float _bilgeFullness = 0.42f;
    private float _thickness = 0.16f;
    private float _strakeProud = 0.15f;
    private int _strakeCount = 24;
    private int _stations = 40;
    private bool _evenPlankWidth = true;
    private Material _hullMaterial;
    private Material _deckMaterial;

    [Export(PropertyHint.Range, "4,60,0.05")]
    public float Length { get => _length; set { _length = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,25,0.05")]
    public float Beam { get => _beam; set { _beam = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,10,0.05")]
    public float Draft { get => _draft; set { _draft = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,10,0.05")]
    public float Freeboard { get => _freeboard; set { _freeboard = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-2,8,0.05")]
    public float DeckHeight { get => _deckHeight; set { _deckHeight = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,6,0.05")]
    public float BowSheerRise { get => _bowSheerRise; set { _bowSheerRise = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,6,0.05")]
    public float SternSheerRise { get => _sternSheerRise; set { _sternSheerRise = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,6,0.05")]
    public float SheerPower { get => _sheerPower; set { _sheerPower = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,5,0.05")]
    public float Rocker { get => _rocker; set { _rocker = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,6,0.05")]
    public float RockerPower { get => _rockerPower; set { _rockerPower = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,6,0.05")]
    public float BowSharpness { get => _bowSharpness; set { _bowSharpness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,6,0.05")]
    public float SternSharpness { get => _sternSharpness; set { _sternSharpness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.15,1.5,0.01")]
    public float BilgeFullness { get => _bilgeFullness; set { _bilgeFullness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.02,1,0.01")]
    public float Thickness { get => _thickness; set { _thickness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,0.5,0.005")]
    public float StrakeProud { get => _strakeProud; set { _strakeProud = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,48,1")]
    public int StrakeCount { get => _strakeCount; set { _strakeCount = value; Rebuild(); } }

    [Export(PropertyHint.Range, "8,160,2")]
    public int Stations { get => _stations; set { _stations = value; Rebuild(); } }

    [Export] public bool EvenPlankWidth { get => _evenPlankWidth; set { _evenPlankWidth = value; Rebuild(); } }

    [Export] public Material HullMaterial { get => _hullMaterial; set { _hullMaterial = value; Rebuild(); } }

    [Export] public Material DeckMaterial { get => _deckMaterial; set { _deckMaterial = value; Rebuild(); } }

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    public override void _Ready() => Rebuild();

    public override void _EnterTree() => Rebuild();

    private float PlanFactor(float t)
    {
        float m = Mathf.Abs(t * 2f - 1f);
        float sharpness = t > 0.5f ? _bowSharpness : _sternSharpness;
        return Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Pow(m, sharpness)), 0.65f);
    }

    private float SectionFactor(float v) => Mathf.Pow(Mathf.Clamp(v, 0f, 1f), _bilgeFullness);

    private float KeelY(float t)
    {
        float m = Mathf.Abs(t * 2f - 1f);
        return -_draft + _rocker * Mathf.Pow(m, _rockerPower);
    }

    private float SheerY(float t)
    {
        float m = Mathf.Abs(t * 2f - 1f);
        float rise = t > 0.5f ? _bowSheerRise : _sternSheerRise;
        return _freeboard + rise * Mathf.Pow(m, _sheerPower);
    }

    private float ZAt(float t) => Mathf.Lerp(_length * 0.5f, -_length * 0.5f, t);

    private float VAtHeight(float t, float y)
    {
        float keel = KeelY(t);
        float sheer = SheerY(t);
        if (sheer - keel < 0.001f) return 0f;
        return Mathf.Clamp((y - keel) / (sheer - keel), 0f, 1f);
    }

    private Vector3 Shell(float t, float v, float offset, float side)
    {
        float y = Mathf.Lerp(KeelY(t), SheerY(t), v);
        float half = Mathf.Max(0.02f, _beam * 0.5f * PlanFactor(t) * SectionFactor(v) + offset);
        return new Vector3(half * side, y, ZAt(t));
    }

    private Vector3[] Ring(float v, float offset)
    {
        int count = Mathf.Max(3, _stations);
        var points = new Vector3[count * 2];
        for (int j = 0; j < count; j++)
        {
            float t = (float)j / (count - 1);
            Vector3 starboard = Shell(t, v, offset, 1f);
            points[j] = starboard;
            points[count * 2 - 1 - j] = new Vector3(-starboard.X, starboard.Y, starboard.Z);
        }
        return points;
    }

    private Vector3[] RingAtHeight(float y, float offset)
    {
        int count = Mathf.Max(3, _stations);
        var points = new Vector3[count * 2];
        for (int j = 0; j < count; j++)
        {
            float t = (float)j / (count - 1);
            Vector3 starboard = Shell(t, VAtHeight(t, y), offset, 1f);
            points[j] = new Vector3(starboard.X, y, starboard.Z);
            points[count * 2 - 1 - j] = new Vector3(-starboard.X, y, starboard.Z);
        }
        return points;
    }

    private float[] StrakeSeams(int strakes)
    {
        var seams = new float[strakes + 1];
        seams[strakes] = 1f;

        if (!_evenPlankWidth)
        {
            for (int i = 1; i < strakes; i++) seams[i] = (float)i / strakes;
            return seams;
        }

        const int samples = 256;
        var girth = new float[samples + 1];
        float keel = KeelY(0.5f);
        float sheer = SheerY(0.5f);
        float widest = _beam * 0.5f * PlanFactor(0.5f);
        float previousWidth = 0f;
        float previousY = keel;

        for (int i = 1; i <= samples; i++)
        {
            float v = (float)i / samples;
            float width = widest * SectionFactor(v);
            float y = Mathf.Lerp(keel, sheer, v);
            float dw = width - previousWidth;
            float dy = y - previousY;
            girth[i] = girth[i - 1] + Mathf.Sqrt(dw * dw + dy * dy);
            previousWidth = width;
            previousY = y;
        }

        if (girth[samples] < 0.0001f)
        {
            for (int i = 1; i < strakes; i++) seams[i] = (float)i / strakes;
            return seams;
        }

        int cursor = 0;
        for (int s = 1; s < strakes; s++)
        {
            float target = girth[samples] * s / strakes;
            while (cursor < samples - 1 && girth[cursor + 1] < target) cursor++;
            float span = girth[cursor + 1] - girth[cursor];
            float fraction = span > 0.0001f ? (target - girth[cursor]) / span : 0f;
            seams[s] = (cursor + fraction) / samples;
        }

        return seams;
    }

    private static void Tri(SurfaceTool tool, Vector3 a, Vector3 b, Vector3 c)
    {
        tool.AddVertex(a);
        tool.AddVertex(b);
        tool.AddVertex(c);
    }

    private static void Band(SurfaceTool tool, Vector3[] lower, Vector3[] upper)
    {
        for (int k = 0; k < lower.Length; k++)
        {
            int next = (k + 1) % lower.Length;
            Tri(tool, lower[k], lower[next], upper[next]);
            Tri(tool, lower[k], upper[next], upper[k]);
        }
    }

    private static void BandInward(SurfaceTool tool, Vector3[] lower, Vector3[] upper)
    {
        for (int k = 0; k < lower.Length; k++)
        {
            int next = (k + 1) % lower.Length;
            Tri(tool, lower[k], upper[next], lower[next]);
            Tri(tool, lower[k], upper[k], upper[next]);
        }
    }

    private static void Cap(SurfaceTool tool, Vector3[] ring, bool faceUp)
    {
        int half = ring.Length / 2;
        for (int j = 0; j < half - 1; j++)
        {
            Vector3 s0 = ring[j];
            Vector3 s1 = ring[j + 1];
            Vector3 p0 = ring[ring.Length - 1 - j];
            Vector3 p1 = ring[ring.Length - 2 - j];

            if (faceUp)
            {
                Tri(tool, s0, p1, p0);
                Tri(tool, s0, s1, p1);
            }
            else
            {
                Tri(tool, s0, p0, p1);
                Tri(tool, s0, p1, s1);
            }
        }
    }

    private void Rebuild()
    {
        int strakes = Mathf.Max(1, _strakeCount);
        float[] seams = StrakeSeams(strakes);

        var hull = new SurfaceTool();
        hull.Begin(Godot.Mesh.PrimitiveType.Triangles);

        Vector3[] previous = Ring(0f, _strakeProud);
        Cap(hull, previous, false);

        for (int i = 0; i < strakes; i++)
        {
            float top = seams[i + 1];
            Vector3[] plankTop = Ring(top, 0f);
            Band(hull, previous, plankTop);

            if (i < strakes - 1)
            {
                Vector3[] nextPlankBottom = Ring(top, _strakeProud);
                Band(hull, plankTop, nextPlankBottom);
                previous = nextPlankBottom;
            }
            else
            {
                previous = plankTop;
            }
        }

        Vector3[] sheerInner = Ring(1f, -_thickness);
        Band(hull, previous, sheerInner);

        Vector3[] deckEdge = RingAtHeight(_deckHeight, -_thickness);
        BandInward(hull, deckEdge, sheerInner);

        hull.GenerateNormals();
        if (_hullMaterial != null) hull.SetMaterial(_hullMaterial);

        var deck = new SurfaceTool();
        deck.Begin(Godot.Mesh.PrimitiveType.Triangles);
        Cap(deck, deckEdge, true);
        deck.GenerateNormals();
        if (_deckMaterial != null) deck.SetMaterial(_deckMaterial);

        var mesh = new ArrayMesh();
        hull.Commit(mesh);
        deck.Commit(mesh);
        Mesh = mesh;
    }
}
