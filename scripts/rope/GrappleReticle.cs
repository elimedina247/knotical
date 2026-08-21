using Godot;

namespace Knotical.Rigging;

[GlobalClass]
public partial class GrappleReticle : Control
{
    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        GetViewport().SizeChanged += QueueRedraw;
    }

    public override void _Draw()
    {
        Vector2 centre = GetViewportRect().Size * 0.5f;
        var outline = new Color(0f, 0f, 0f, 0.7f);
        var color = new Color(1f, 1f, 1f, 0.95f);

        Dash(centre, new Vector2(-14f, 0f), new Vector2(-6f, 0f), outline, color);
        Dash(centre, new Vector2(6f, 0f), new Vector2(14f, 0f), outline, color);
        Dash(centre, new Vector2(0f, 6f), new Vector2(0f, 14f), outline, color);
    }

    private void Dash(Vector2 centre, Vector2 from, Vector2 to, Color outline, Color color)
    {
        DrawLine(centre + from, centre + to, outline, 4f);
        DrawLine(centre + from, centre + to, color, 2f);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }
}
