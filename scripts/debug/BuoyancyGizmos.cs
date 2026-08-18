using Godot;
using Knotical.Boat;

namespace Knotical.Debug;

/// <summary>
/// Draws every buoyancy pontoon in the scene as a wireframe sphere with a force line,
/// plus a HUD list of buoyant bodies. Toggled with B.
///
/// One ImmediateMesh rebuilt per frame; a few hundred line segments, far below the cost
/// of caring. Depth test off so pontoons read through the hull and the water.
/// </summary>
[GlobalClass]
public partial class BuoyancyGizmos : Node3D
{
    [Export] public bool StartVisible { get; set; }

    private static readonly Color Dry = new(1f, 0.62f, 0.1f);
    private static readonly Color Wet = new(0.25f, 0.95f, 0.85f);
    private static readonly Color Force = new(1f, 0.9f, 0.2f);

    private ImmediateMesh _lines;
    private MeshInstance3D _mesh;
    private Label _label;
    private CanvasLayer _layer;
    private bool _shown;

    public override void _Ready()
    {
        _shown = StartVisible;

        _lines = new ImmediateMesh();
        _mesh = new MeshInstance3D
        {
            Mesh = _lines,
            TopLevel = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                VertexColorUseAsAlbedo = true,
                NoDepthTest = true,
                RenderPriority = 10
            }
        };
        AddChild(_mesh);

        _layer = new CanvasLayer { Layer = 12 };
        _label = new Label
        {
            Position = new Vector2(16f, 12f),
            Modulate = new Color(1f, 0.85f, 0.4f, 0.95f)
        };
        _label.AddThemeFontSizeOverride("font_size", 14);
        _label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.8f));
        _label.AddThemeConstantOverride("outline_size", 4);
        _layer.AddChild(_label);
        AddChild(_layer);

        Apply();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.PhysicalKeycode != Key.B) return;

        _shown = !_shown;
        Apply();
        GetViewport().SetInputAsHandled();
    }

    private void Apply()
    {
        _mesh.Visible = _shown;
        _layer.Visible = _shown;
    }

    public override void _Process(double delta)
    {
        if (!_shown) return;

        _lines.ClearSurfaces();
        _lines.SurfaceBegin(Mesh.PrimitiveType.Lines);

        int pontoons = 0;
        string list = "";

        foreach (BoatHull hull in BoatHull.Active)
        {
            if (!IsInstanceValid(hull) || !hull.IsInsideTree()) continue;

            for (int i = 0; i < hull.ProbeCount; i++)
            {
                hull.GetProbe(i, out Vector3 world, out float wet, out Vector3 force, out float span);
                Pontoon(world, Mathf.Clamp(span * 0.4f, 0.25f, 1.4f), wet, force, hull.Mass);
                pontoons++;
            }

            list += $"\n{hull.Name}";
        }

        foreach (Buoyancy floater in Buoyancy.Active)
        {
            if (!IsInstanceValid(floater) || !floater.IsInsideTree() || floater.Body == null) continue;

            for (int i = 0; i < floater.ProbeCount; i++)
            {
                floater.GetProbe(i, out Vector3 world, out float wet, out Vector3 force, out float span);
                Pontoon(world, Mathf.Clamp(span, 0.15f, 1.4f), wet, force, floater.Body.Mass);
                pontoons++;
            }

            list += $"\n{floater.Body.Name}";
        }

        _lines.SurfaceEnd();

        _label.Text = $"Buoyant Pontoons: {pontoons}{list}";
    }

    private void Pontoon(Vector3 at, float radius, float wet, Vector3 force, float mass)
    {
        Color color = Dry.Lerp(Wet, Mathf.Clamp(wet, 0f, 1f));

        Ring(at, radius, 0, color);
        Ring(at, radius, 1, color);
        Ring(at, radius, 2, color);

        float weight = Mathf.Max(mass * 9.81f, 1f);
        Vector3 tip = at + force / weight * (radius * 6f);

        _lines.SurfaceSetColor(Force);
        _lines.SurfaceAddVertex(at);
        _lines.SurfaceAddVertex(tip);
    }

    private void Ring(Vector3 at, float radius, int plane, Color color)
    {
        const int segments = 20;
        _lines.SurfaceSetColor(color);

        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Tau * i / segments;
            float b = Mathf.Tau * (i + 1) / segments;

            _lines.SurfaceAddVertex(at + Spoke(a, radius, plane));
            _lines.SurfaceAddVertex(at + Spoke(b, radius, plane));
        }
    }

    private static Vector3 Spoke(float angle, float radius, int plane)
    {
        float c = Mathf.Cos(angle) * radius;
        float s = Mathf.Sin(angle) * radius;

        return plane switch
        {
            0 => new Vector3(c, s, 0f),
            1 => new Vector3(c, 0f, s),
            _ => new Vector3(0f, c, s)
        };
    }
}
