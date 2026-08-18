using System.Collections.Generic;
using Godot;

namespace Knotical.Vfx;

/// <summary>
/// World-fixed foam accumulation texture the ocean shader samples. Splashes stamp white
/// blobs; a translucent black wash each frame fades everything on a frame-rate
/// independent clock.
///
/// The canvas covers the whole playable world at one fixed mapping, which is what lets
/// the render target persist between frames without content shifting — a camera-chasing
/// region would need a copy pass on every recentre. Roughly 6 m per texel: patches read
/// as soft lingering foam, and the crisp short-lived trail stays BoatWake's job.
/// </summary>
[GlobalClass]
public partial class FoamCapture : Node
{
    private const int Size = 2048;

    public static FoamCapture Instance { get; private set; }

    [Export(PropertyHint.Range, "500,8000,100")]
    public float HalfExtent { get; set; } = Knotical.Ocean.Ocean.WorldHalfExtent;

    /// <summary>Seconds for a stamp to fade to ~37%.</summary>
    [Export(PropertyHint.Range, "1,60,0.5")]
    public float FoamLife { get; set; } = 9f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Strength { get; set; } = 0.55f;

    private SubViewport _viewport;
    private FoamCanvas _canvas;

    public Texture2D Texture => _viewport?.GetTexture();

    public static void Stamp(Vector2 worldXZ, float radius, float strength)
    {
        Instance?._canvas?.Push(worldXZ, radius, strength);
    }

    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint()) Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Ready()
    {
        _viewport = new SubViewport
        {
            Size = new Vector2I(Size, Size),
            Disable3D = true,
            TransparentBg = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            RenderTargetClearMode = SubViewport.ClearMode.Once
        };

        _canvas = new FoamCanvas
        {
            Capture = this,
            CustomMinimumSize = new Vector2(Size, Size)
        };

        _viewport.AddChild(_canvas);
        AddChild(_viewport);
    }

    public Vector2 ToPixels(Vector2 worldXZ)
    {
        return (worldXZ / (HalfExtent * 2f) + new Vector2(0.5f, 0.5f)) * Size;
    }

    public float PixelsPerMetre => Size / (HalfExtent * 2f);

    private sealed partial class FoamCanvas : Control
    {
        public FoamCapture Capture;

        private readonly List<(Vector2 At, float Radius, float Strength)> _stamps = new();
        private float _fade;

        public void Push(Vector2 worldXZ, float radius, float strength)
        {
            if (_stamps.Count > 64) return;
            _stamps.Add((worldXZ, radius, strength));
        }

        public override void _Process(double delta)
        {
            _fade = 1f - Mathf.Exp(-(float)delta / Mathf.Max(Capture.FoamLife, 0.5f));
            QueueRedraw();
        }

        public override void _Draw()
        {
            DrawRect(new Rect2(0f, 0f, FoamCapture.Size, FoamCapture.Size), new Color(0f, 0f, 0f, _fade));

            foreach ((Vector2 at, float radius, float strength) in _stamps)
            {
                Vector2 px = Capture.ToPixels(at);
                float r = Mathf.Max(radius * Capture.PixelsPerMetre, 1.5f);

                DrawCircle(px, r, new Color(1f, 1f, 1f, strength * 0.7f));
                DrawCircle(px, r * 0.55f, new Color(1f, 1f, 1f, strength));
            }

            _stamps.Clear();
        }
    }
}
