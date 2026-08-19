using Godot;
using Knotical.Boat;
using Knotical.Weather;
using OceanSystem = Knotical.Ocean.Ocean;

namespace Knotical.Debug;

[GlobalClass]
public partial class MapOverlay : CanvasLayer
{
    private enum MapMode { Waves, Wind }

    private const float MinHalfExtent = 100f;
    private const float MaxHalfExtent = 6000f;
    private const float ZoomStep = 1.25f;
    private const int ArrowCells = 7;

    private static readonly Color[] WaveRamp =
    {
        new(0.04f, 0.16f, 0.32f),
        new(0.08f, 0.32f, 0.56f),
        new(0.18f, 0.53f, 0.73f),
        new(0.50f, 0.77f, 0.87f),
        new(0.91f, 0.96f, 0.98f)
    };

    private static readonly Color[] WindRamp =
    {
        new(0.62f, 0.71f, 0.77f),
        new(0.47f, 0.79f, 0.62f),
        new(0.85f, 0.88f, 0.48f),
        new(0.94f, 0.75f, 0.35f),
        new(0.93f, 0.56f, 0.31f),
        new(0.85f, 0.31f, 0.25f)
    };

    private static readonly float[] WindRampSpeeds = { 0f, 4f, 8f, 12f, 18f, 25f };

    private MapMode _mode = MapMode.Waves;
    private float _halfExtent = 600f;
    private Vector2 _center;
    private Rect2 _panel;

    private ColorRect _field;
    private ShaderMaterial _material;
    private MarkerSurface _markers;

    private readonly Godot.Collections.Array _packedWaves = new();
    private readonly Godot.Collections.Array _packedPhases = new();
    private readonly Godot.Collections.Array _packedResponse = new();
    private readonly System.Collections.Generic.List<(Vector2 Pos, float Radius)> _islands = new();
    private bool _islandsGathered;
    private string _shotPath;
    private int _shotCountdown = -1;

    public override void _Ready()
    {
        Layer = 10;
        Visible = false;

        _material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>("res://shaders/map_overlay.gdshader")
        };

        _packedWaves.Resize(Knotical.Ocean.OceanSettings.MaxWaves);
        _packedPhases.Resize(Knotical.Ocean.OceanSettings.MaxWaves);
        _packedResponse.Resize(Knotical.Ocean.OceanSettings.MaxWaves);

        var waveRamp = new Godot.Collections.Array();
        foreach (Color c in WaveRamp) waveRamp.Add(new Vector3(c.R, c.G, c.B));

        var windRamp = new Godot.Collections.Array();
        foreach (Color c in WindRamp) windRamp.Add(new Vector3(c.R, c.G, c.B));

        var windSpeeds = new Godot.Collections.Array();
        foreach (float s in WindRampSpeeds) windSpeeds.Add(s);

        _material.SetShaderParameter("wave_ramp", waveRamp);
        _material.SetShaderParameter("wind_ramp", windRamp);
        _material.SetShaderParameter("wind_ramp_speeds", windSpeeds);

        _field = new ColorRect
        {
            Material = _material,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(_field);

        _markers = new MarkerSurface
        {
            Overlay = this,
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        AddChild(_markers);

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--map") Open();
            if (arg == "--map-wind")
            {
                Open();
                _mode = MapMode.Wind;
            }

            if (arg.StartsWith("--map-shot="))
            {
                _shotPath = arg["--map-shot=".Length..];
                _shotCountdown = 180;
            }
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            if (key.PhysicalKeycode == Key.M)
            {
                if (Visible) Visible = false;
                else Open();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (!Visible) return;

            if (key.PhysicalKeycode == Key.Tab)
            {
                _mode = _mode == MapMode.Waves ? MapMode.Wind : MapMode.Waves;
                GetViewport().SetInputAsHandled();
            }
        }
        else if (Visible && @event is InputEventMouseButton { Pressed: true } wheel)
        {
            if (wheel.ButtonIndex == MouseButton.WheelUp)
            {
                Zoom(1f / ZoomStep);
                GetViewport().SetInputAsHandled();
            }
            else if (wheel.ButtonIndex == MouseButton.WheelDown)
            {
                Zoom(ZoomStep);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;

        GatherIslands();

        Vector2 view = GetViewport().GetVisibleRect().Size;
        float size = Mathf.Min(view.X, view.Y) * 0.8f;
        _panel = new Rect2((view - new Vector2(size, size)) * 0.5f, new Vector2(size, size));

        _field.Position = _panel.Position;
        _field.Size = _panel.Size;
        _markers.Position = _panel.Position;
        _markers.Size = _panel.Size;

        Camera3D cam = GetViewport().GetCamera3D();
        if (cam != null) _center = new Vector2(cam.GlobalPosition.X, cam.GlobalPosition.Z);

        PushUniforms();
        _markers.QueueRedraw();

        if (_shotCountdown > 0 && --_shotCountdown == 0)
        {
            GetViewport().GetTexture().GetImage().SavePng(_shotPath);
            GD.Print($"map shot saved to {_shotPath}");
        }
    }

    private void Open() => Visible = true;

    private void Zoom(float factor) =>
        _halfExtent = Mathf.Clamp(_halfExtent * factor, MinHalfExtent, MaxHalfExtent);

    private void PushUniforms()
    {
        OceanSystem ocean = OceanSystem.Instance;
        Wind wind = Wind.Instance;

        if (ocean != null)
        {
            for (int i = 0; i < Knotical.Ocean.OceanSettings.MaxWaves; i++)
            {
                _packedWaves[i] = i < ocean.Waves.Length ? ocean.Waves[i] : Vector4.Zero;
                _packedPhases[i] = i < ocean.Phases.Length ? ocean.Phases[i] : 0f;
                _packedResponse[i] = i < ocean.DepthResponse.Length ? ocean.DepthResponse[i] : 0f;
            }

            _material.SetShaderParameter("waves", _packedWaves);
            _material.SetShaderParameter("wave_phase", _packedPhases);
            _material.SetShaderParameter("wave_response", _packedResponse);
            _material.SetShaderParameter("wave_count", ocean.Waves.Length);
            _material.SetShaderParameter("wave_time", (float)ocean.Time);
            _material.SetShaderParameter("significant_height", ocean.SignificantHeight);

            Knotical.Ocean.SeaDepthField depth = ocean.DepthField;
            bool baked = depth != null && depth.Baked;
            _material.SetShaderParameter("sea_field_enabled", baked ? 1f : 0f);

            if (baked)
            {
                _material.SetShaderParameter("sea_field", depth.Texture);
                _material.SetShaderParameter("sea_field_extent", depth.HalfExtent);
            }
        }

        if (wind != null)
        {
            _material.SetShaderParameter("wind_dir", wind.Direction);
            _material.SetShaderParameter("wind_speed", wind.Speed);
            _material.SetShaderParameter("wind_drift", wind.AccumulatedDrift);
        }

        _material.SetShaderParameter("mode", (int)_mode);
        _material.SetShaderParameter("map_center", _center);
        _material.SetShaderParameter("map_half_extent", _halfExtent);
        _material.SetShaderParameter("grid_step", NiceStep(_halfExtent * 0.4f));
        _material.SetShaderParameter("px_per_metre", _panel.Size.X * 0.5f / _halfExtent);
        _material.SetShaderParameter("drift_scale", Mathf.Max(_halfExtent / 150f, 1f));
    }

    private void DrawMarkers(MarkerSurface c)
    {
        float size = _panel.Size.X;
        float scale = size * 0.5f / Mathf.Max(_halfExtent, 0.001f);
        Font font = c.GetThemeDefaultFont();

        Wind wind = Wind.Instance;
        OceanSystem ocean = OceanSystem.Instance;

        DrawWindArrows(c, size, wind);
        DrawIslands(c, scale, size);
        DrawBoats(c, size);
        DrawCameraMarker(c, size);
        DrawLegend(c, font, size, ocean);
        DrawRose(c, font, size, wind, ocean);
        DrawScaleBar(c, font, size, scale);
        DrawTitle(c, font, wind, ocean);

        DrawText(c, font, new Vector2(size * 0.5f - 5f, 24f), "N", 14);
        c.DrawRect(new Rect2(Vector2.Zero, _panel.Size), new Color(0.08f, 0.10f, 0.13f), false, 2f);
    }

    private Vector2 MapToLocal(Vector2 worldXZ, float size)
    {
        Vector2 d = (worldXZ - _center) / _halfExtent * 0.5f;
        return new Vector2((d.X + 0.5f) * size, (d.Y + 0.5f) * size);
    }

    private Vector2 LocalToMapWorld(Vector2 local, float size) =>
        _center + (local / size - new Vector2(0.5f, 0.5f)) * 2f * _halfExtent;

    private void DrawWindArrows(MarkerSurface c, float size, Wind wind)
    {
        if (wind == null) return;

        Color color = _mode == MapMode.Wind
            ? new Color(0.06f, 0.09f, 0.12f, 0.6f)
            : new Color(1f, 1f, 1f, 0.42f);

        float cell = size / ArrowCells;

        for (int gx = 0; gx < ArrowCells; gx++)
        {
            for (int gz = 0; gz < ArrowCells; gz++)
            {
                var local = new Vector2((gx + 0.5f) * cell, (gz + 0.5f) * cell);
                Vector2 v = wind.GetVelocity(LocalToMapWorld(local, size));
                float speed = v.Length();
                if (speed < 0.05f) continue;

                Vector2 dir = v / speed;
                float len = cell * 0.42f * Mathf.Clamp(speed / WindRampSpeeds[^1], 0.15f, 1f);
                Vector2 to = local + dir * len * 0.5f;

                c.DrawLine(local - dir * len * 0.5f, to, color, 1.6f, true);
                c.DrawLine(to, to + dir.Rotated(Mathf.Pi * 0.82f) * len * 0.32f, color, 1.6f, true);
                c.DrawLine(to, to + dir.Rotated(-Mathf.Pi * 0.82f) * len * 0.32f, color, 1.6f, true);
            }
        }
    }

    private void DrawIslands(MarkerSurface c, float scale, float size)
    {
        var fill = new Color(0.87f, 0.79f, 0.60f);
        var edge = new Color(0.45f, 0.38f, 0.25f);

        foreach ((Vector2 pos, float radius) in _islands)
        {
            Vector2 local = MapToLocal(pos, size);
            float r = Mathf.Max(radius * scale, 3f);
            if (local.X < -r || local.Y < -r || local.X > size + r || local.Y > size + r) continue;

            c.DrawCircle(local, r, fill);
            c.DrawArc(local, r, 0f, Mathf.Tau, 40, edge, 1.5f, true);
        }
    }

    private void DrawBoats(MarkerSurface c, float size)
    {
        var bounds = new Rect2(-20f, -20f, size + 40f, size + 40f);

        foreach (BoatHull hull in BoatHull.Active)
        {
            if (!IsInstanceValid(hull) || !hull.IsInsideTree()) continue;

            Vector3 pos = hull.GlobalPosition;
            Vector2 local = MapToLocal(new Vector2(pos.X, pos.Z), size);
            if (!bounds.HasPoint(local)) continue;

            Vector3 forward = -hull.GlobalBasis.Z;
            var dir = new Vector2(forward.X, forward.Z);
            dir = dir.LengthSquared() > 0.0001f ? dir.Normalized() : Vector2.Down;
            var perp = new Vector2(-dir.Y, dir.X);

            var points = new[]
            {
                local + dir * 10f,
                local - dir * 6f + perp * 5f,
                local - dir * 6f - perp * 5f
            };

            c.DrawColoredPolygon(points, new Color(0.55f, 0.23f, 0.16f));
            c.DrawPolyline(new[] { points[0], points[1], points[2], points[0] },
                new Color(1f, 1f, 1f, 0.8f), 1.2f, true);
        }
    }

    private void DrawCameraMarker(MarkerSurface c, float size)
    {
        Camera3D cam = GetViewport().GetCamera3D();
        if (cam == null) return;

        Vector2 local = MapToLocal(new Vector2(cam.GlobalPosition.X, cam.GlobalPosition.Z), size);
        Vector3 forward = -cam.GlobalBasis.Z;
        var dir = new Vector2(forward.X, forward.Z);

        if (dir.LengthSquared() > 0.0001f)
        {
            dir = dir.Normalized();
            var wedge = new Color(1f, 1f, 1f, 0.35f);
            c.DrawLine(local, local + dir.Rotated(0.45f) * 16f, wedge, 1.2f, true);
            c.DrawLine(local, local + dir.Rotated(-0.45f) * 16f, wedge, 1.2f, true);
        }

        c.DrawCircle(local, 3.5f, Colors.White);
    }

    private void DrawLegend(MarkerSurface c, Font font, float size, OceanSystem ocean)
    {
        float barH = size * 0.45f;
        const float barW = 10f;
        var origin = new Vector2(size - 30f, (size - barH) * 0.5f);

        const int slices = 40;
        for (int i = 0; i < slices; i++)
        {
            float t = 1f - (i + 0.5f) / slices;
            Color col = _mode == MapMode.Waves ? WaveColor(t) : WindColor(t * WindRampSpeeds[^1]);
            c.DrawRect(new Rect2(origin.X, origin.Y + barH * i / slices, barW, barH / slices + 1f), col);
        }

        c.DrawRect(new Rect2(origin, new Vector2(barW, barH)), new Color(0f, 0f, 0f, 0.6f), false, 1f);

        string top;
        string bottom;
        if (_mode == MapMode.Waves)
        {
            float span = 0.6f * (ocean?.SignificantHeight ?? 1f);
            top = $"+{span:0.0} m";
            bottom = $"-{span:0.0} m";
        }
        else
        {
            top = $"{WindRampSpeeds[^1]:0} m/s";
            bottom = "0";

            float speed = Wind.Instance?.Speed ?? 0f;
            float t = Mathf.Clamp(speed / WindRampSpeeds[^1], 0f, 1f);
            float y = origin.Y + (1f - t) * barH;
            c.DrawLine(new Vector2(origin.X - 5f, y), new Vector2(origin.X + barW + 5f, y), Colors.White, 2f, true);
        }

        DrawText(c, font, new Vector2(origin.X - 36f, origin.Y - 8f), top, 12);
        DrawText(c, font, new Vector2(origin.X - 36f, origin.Y + barH + 16f), bottom, 12);
    }

    private void DrawRose(MarkerSurface c, Font font, float size, Wind wind, OceanSystem ocean)
    {
        var centre = new Vector2(size - 74f, size - 74f);
        const float r = 34f;

        c.DrawCircle(centre, r + 9f, new Color(0f, 0f, 0f, 0.28f));
        c.DrawArc(centre, r, 0f, Mathf.Tau, 48, new Color(1f, 1f, 1f, 0.5f), 1f, true);

        var swellColor = new Color(0.45f, 0.85f, 1f);

        if (wind != null) DrawRoseArrow(c, centre, wind.Direction, r, Colors.White);

        Vector2 swell = SwellMeanDirection(ocean);
        if (swell != Vector2.Zero) DrawRoseArrow(c, centre, swell, r, swellColor);

        var label = new Vector2(centre.X - r - 52f, centre.Y - 6f);
        DrawText(c, font, label, "wind", 10);
        DrawText(c, font, label + new Vector2(0f, 14f), "swell", 10, swellColor);
    }

    private static void DrawRoseArrow(MarkerSurface c, Vector2 centre, Vector2 dir, float r, Color color)
    {
        Vector2 tip = centre + dir * (r - 3f);
        c.DrawLine(centre - dir * (r - 10f), tip, color, 2f, true);
        c.DrawLine(tip, tip + dir.Rotated(Mathf.Pi * 0.82f) * 8f, color, 2f, true);
        c.DrawLine(tip, tip + dir.Rotated(-Mathf.Pi * 0.82f) * 8f, color, 2f, true);
    }

    private static Vector2 SwellMeanDirection(OceanSystem ocean)
    {
        if (ocean == null) return Vector2.Zero;

        Vector2 sum = Vector2.Zero;

        for (int i = 0; i < ocean.Waves.Length; i++)
        {
            Vector4 w = ocean.Waves[i];
            sum += new Vector2(w.X, w.Y) * w.Z;
        }

        return sum.LengthSquared() > 1e-8f ? sum.Normalized() : Vector2.Zero;
    }

    private void DrawScaleBar(MarkerSurface c, Font font, float size, float scale)
    {
        float step = NiceStep(_halfExtent * 0.4f);
        float px = step * scale;
        var from = new Vector2(18f, size - 22f);
        Vector2 to = from + new Vector2(px, 0f);

        c.DrawLine(from, to, Colors.White, 2f, true);
        c.DrawLine(from + new Vector2(0f, -4f), from + new Vector2(0f, 4f), Colors.White, 2f, true);
        c.DrawLine(to + new Vector2(0f, -4f), to + new Vector2(0f, 4f), Colors.White, 2f, true);
        DrawText(c, font, from + new Vector2(px * 0.5f - 18f, -8f), $"{step:0} m", 12);
    }

    private void DrawTitle(MarkerSurface c, Font font, Wind wind, OceanSystem ocean)
    {
        string line1;
        string line2;

        if (_mode == MapMode.Waves)
        {
            line1 = "WAVES — surface height (physics bands)";
            line2 = ocean == null
                ? "ocean autoload missing"
                : $"H {ocean.SignificantHeight:0.00} m   peak wavelength {ocean.PeakWavelength:0} m";
        }
        else
        {
            float heading = wind != null ? Mathf.PosMod(Mathf.RadToDeg(wind.DirectionRad), 360f) : 0f;
            line1 = "WIND — velocity field";
            line2 = wind == null
                ? "wind autoload missing"
                : $"{wind.Speed:0.0} m/s   Force {wind.BeaufortForce} — {wind.BeaufortName}   blowing toward {heading:0}°";
        }

        DrawText(c, font, new Vector2(16f, 26f), line1, 16);
        DrawText(c, font, new Vector2(16f, 46f), line2, 13);
        DrawText(c, font, new Vector2(16f, 64f), "[M] close   [Tab] waves/wind   [scroll] zoom", 11,
            new Color(1f, 1f, 1f, 0.7f));
    }

    private static void DrawText(MarkerSurface c, Font font, Vector2 pos, string text, int fontSize, Color? color = null)
    {
        c.DrawStringOutline(font, pos, text, HorizontalAlignment.Left, -1f, fontSize, 4, new Color(0f, 0f, 0f, 0.75f));
        c.DrawString(font, pos, text, HorizontalAlignment.Left, -1f, fontSize, color ?? Colors.White);
    }

    private static Color WaveColor(float t)
    {
        float q = Mathf.Clamp(t, 0f, 0.999f) * (WaveRamp.Length - 1);
        int band = (int)q;
        return WaveRamp[band].Lerp(WaveRamp[band + 1], q - band);
    }

    private static Color WindColor(float speed)
    {
        Color col = WindRamp[0];
        for (int i = 0; i < WindRamp.Length - 1; i++)
        {
            col = col.Lerp(WindRamp[i + 1], Mathf.SmoothStep(WindRampSpeeds[i], WindRampSpeeds[i + 1], speed));
        }

        return col;
    }

    private static float NiceStep(float target)
    {
        float pow = Mathf.Pow(10f, Mathf.Floor(Mathf.Log(target) / Mathf.Log(10f)));
        float m = target / pow;
        float step = m < 1.5f ? 1f : m < 3.5f ? 2f : m < 7.5f ? 5f : 10f;
        return step * pow;
    }

    private void GatherIslands()
    {
        if (_islandsGathered) return;
        _islandsGathered = true;

        Node scene = GetTree().CurrentScene;
        if (scene != null) Gather(scene);
    }

    private void Gather(Node node)
    {
        if (node is MeshInstance3D mesh && node.Name.ToString().Contains("Island")
            && mesh.Mesh is CylinderMesh cylinder)
        {
            Vector3 pos = mesh.GlobalPosition;
            _islands.Add((new Vector2(pos.X, pos.Z), cylinder.TopRadius));
        }

        foreach (Node child in node.GetChildren()) Gather(child);
    }

    private partial class MarkerSurface : Control
    {
        public MapOverlay Overlay { get; set; }

        public override void _Draw() => Overlay?.DrawMarkers(this);
    }
}
