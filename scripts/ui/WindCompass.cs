using Godot;
using Knotical.Weather;

namespace Knotical.UI;

[GlobalClass]
public partial class WindCompass : CanvasLayer
{
    [Export] public float Radius { get; set; } = 40f;

    [Export] public float Margin { get; set; } = 24f;

    [Export] public Color FaceColor { get; set; } = new("#0a1a1fa8");

    [Export] public Color RingColor { get; set; } = new("#c3f8f7cc");

    [Export] public Color TickColor { get; set; } = new("#c3f8f766");

    [Export] public Color NeedleColor { get; set; } = new("#5ad4c6");

    private static readonly float NorthHeading = -Mathf.Pi / 2f;

    private Control _dial;

    public override void _Ready()
    {
        float size = (Radius + 6f) * 2f;

        _dial = new Control
        {
            AnchorLeft = 1f,
            AnchorRight = 1f,
            OffsetLeft = -(size + Margin),
            OffsetTop = Margin,
            OffsetRight = -Margin,
            OffsetBottom = Margin + size,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };

        _dial.Draw += DrawDial;
        AddChild(_dial);
    }

    public override void _Process(double delta)
    {
        _dial?.QueueRedraw();
    }

    private void DrawDial()
    {
        Vector2 centre = _dial.Size * 0.5f;
        float camAngle = CameraAngle();

        _dial.DrawCircle(centre, Radius + 5f, FaceColor);
        _dial.DrawArc(centre, Radius, 0f, Mathf.Tau, 64, RingColor, 2f, true);

        for (int i = 0; i < 4; i++)
        {
            bool isNorth = i == 0;
            Vector2 dir = Screen(NorthHeading + i * Mathf.Pi * 0.5f, camAngle);
            float inner = Radius - (isNorth ? 0.30f : 0.18f) * Radius;

            _dial.DrawLine(
                centre + dir * inner,
                centre + dir * Radius,
                isNorth ? RingColor : TickColor,
                isNorth ? 2f : 1.5f,
                true);
        }

        Font font = ThemeDB.FallbackFont;
        int fontSize = Mathf.RoundToInt(Radius * 0.3f);
        Vector2 letterPos = centre + Screen(NorthHeading, camAngle) * (Radius * 0.48f);
        Vector2 letterSize = font.GetStringSize("N", HorizontalAlignment.Left, -1f, fontSize);

        _dial.DrawString(
            font,
            letterPos + new Vector2(-letterSize.X * 0.5f, letterSize.Y * 0.32f),
            "N",
            HorizontalAlignment.Left,
            -1f,
            fontSize,
            RingColor);

        Wind wind = Wind.Instance;
        if (wind == null) return;

        Vector2 along = Screen(wind.DirectionRad, camAngle);
        Vector2 across = new(-along.Y, along.X);

        Vector2[] needle =
        {
            centre + along * (Radius * 0.78f),
            centre + across * (Radius * 0.30f) - along * (Radius * 0.34f),
            centre - along * (Radius * 0.14f),
            centre - across * (Radius * 0.30f) - along * (Radius * 0.34f)
        };

        _dial.DrawColoredPolygon(needle, NeedleColor);
    }

    private float CameraAngle()
    {
        Camera3D camera = _dial.GetViewport()?.GetCamera3D();
        if (camera == null) return NorthHeading;

        Basis basis = camera.GlobalBasis;
        Vector2 flat = new(-basis.Z.X, -basis.Z.Z);

        if (flat.LengthSquared() < 1e-6f) flat = new Vector2(basis.Y.X, basis.Y.Z);
        if (flat.LengthSquared() < 1e-6f) return NorthHeading;

        return Mathf.Atan2(flat.Y, flat.X);
    }

    private static Vector2 Screen(float heading, float camAngle)
    {
        float rel = heading - camAngle;
        return new Vector2(Mathf.Sin(rel), -Mathf.Cos(rel));
    }
}
