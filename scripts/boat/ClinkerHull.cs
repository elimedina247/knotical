using Godot;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class ClinkerHull : MeshInstance3D
{
    private readonly HullForm _form = new();
    private float _deckHeight = 1.35f;
    private float _thickness = 0.16f;
    private float _strakeProud = 0.15f;
    private int _strakeCount = 24;
    private int _stations = 40;
    private bool _evenPlankWidth = true;
    private Material _hullMaterial;
    private Material _deckMaterial;

    [Export(PropertyHint.Range, "4,60,0.05")]
    public float Length { get => _form.Length; set { _form.Length = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,25,0.05")]
    public float Beam { get => _form.Beam; set { _form.Beam = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,10,0.05")]
    public float Draft { get => _form.Draft; set { _form.Draft = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,10,0.05")]
    public float Freeboard { get => _form.Freeboard; set { _form.Freeboard = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-2,8,0.05")]
    public float DeckHeight { get => _deckHeight; set { _deckHeight = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,6,0.05")]
    public float BowSheerRise { get => _form.BowSheerRise; set { _form.BowSheerRise = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,6,0.05")]
    public float SternSheerRise { get => _form.SternSheerRise; set { _form.SternSheerRise = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,6,0.05")]
    public float SheerPower { get => _form.SheerPower; set { _form.SheerPower = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,5,0.05")]
    public float Rocker { get => _form.Rocker; set { _form.Rocker = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,6,0.05")]
    public float RockerPower { get => _form.RockerPower; set { _form.RockerPower = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,6,0.05")]
    public float BowSharpness { get => _form.BowSharpness; set { _form.BowSharpness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,6,0.05")]
    public float SternSharpness { get => _form.SternSharpness; set { _form.SternSharpness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float TransomWidth { get => _form.TransomWidth; set { _form.TransomWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-2,3,0.05")]
    public float TransomRake { get => _form.TransomRake; set { _form.TransomRake = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,20,0.05")]
    public float StemRake { get => _form.StemRake; set { _form.StemRake = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,6,0.05")]
    public float StemPower { get => _form.StemPower; set { _form.StemPower = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.15,1.5,0.01")]
    public float BilgeFullness { get => _form.BilgeFullness; set { _form.BilgeFullness = value; Rebuild(); } }

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

    private Vector3[] Ring(float v, float offset) => _form.Ring(v, offset, _stations);

    private Vector3[] RingAtHeight(float y, float offset) => _form.RingAtHeight(y, offset, _stations);

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
        float keel = _form.KeelY(0.5f);
        float sheer = _form.SheerY(0.5f);
        float widest = _form.Beam * 0.5f * _form.PlanFactor(0.5f);
        float previousWidth = 0f;
        float previousY = keel;

        for (int i = 1; i <= samples; i++)
        {
            float v = (float)i / samples;
            float width = widest * _form.SectionFactor(v);
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
            Tri(tool, lower[k], upper[next], lower[next]);
            Tri(tool, lower[k], upper[k], upper[next]);
        }
    }

    private static void BandInward(SurfaceTool tool, Vector3[] lower, Vector3[] upper)
    {
        for (int k = 0; k < lower.Length; k++)
        {
            int next = (k + 1) % lower.Length;
            Tri(tool, lower[k], lower[next], upper[next]);
            Tri(tool, lower[k], upper[next], upper[k]);
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
                Tri(tool, s0, p0, p1);
                Tri(tool, s0, p1, s1);
            }
            else
            {
                Tri(tool, s0, p1, p0);
                Tri(tool, s0, s1, p1);
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
