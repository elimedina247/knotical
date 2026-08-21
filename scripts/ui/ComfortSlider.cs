using Godot;
using Knotical.Player;

namespace Knotical.UI;

[GlobalClass]
public partial class ComfortSlider : CanvasLayer
{
    [Export] public float Margin { get; set; } = 24f;

    [Export] public Color FaceColor { get; set; } = new("#0a1a1fa8");

    [Export] public Color TextColor { get; set; } = new("#c3f8f7cc");

    private PanelContainer _panel;
    private HSlider _slider;
    private Label _label;
    private PlayerBody _player;

    public override void _Ready()
    {
        var style = new StyleBoxFlat
        {
            BgColor = FaceColor,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 14f,
            ContentMarginRight = 14f,
            ContentMarginTop = 10f,
            ContentMarginBottom = 10f
        };

        _panel = new PanelContainer
        {
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = Margin,
            OffsetTop = -(Margin + 84f),
            OffsetBottom = -Margin,
            GrowVertical = Control.GrowDirection.Begin,
            Visible = false
        };
        _panel.AddThemeStyleboxOverride("panel", style);

        var column = new VBoxContainer();
        _panel.AddChild(column);

        _label = new Label();
        _label.AddThemeColorOverride("font_color", TextColor);
        column.AddChild(_label);

        _slider = new HSlider
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Step = 0.05,
            CustomMinimumSize = new Vector2(220f, 0f)
        };
        _slider.ValueChanged += OnChanged;
        column.AddChild(_slider);

        AddChild(_panel);
    }

    public override void _Process(double delta)
    {
        bool open = Input.MouseMode != Input.MouseModeEnum.Captured;
        _panel.Visible = open;
        if (!open) return;

        if (_player != null && IsInstanceValid(_player)) return;

        _player = GetTree().GetFirstNodeInGroup("Players") as PlayerBody;
        if (_player == null) return;

        _slider.SetValueNoSignal(1f - _player.BodyTiltFollow);
        Refresh();
    }

    private void OnChanged(double value)
    {
        if (_player != null && IsInstanceValid(_player))
        {
            _player.BodyTiltFollow = 1f - (float)value;
        }

        Refresh();
    }

    private void Refresh()
    {
        _label.Text = $"Camera stabilization {Mathf.RoundToInt((float)_slider.Value * 100f)}%";
    }
}
