using Godot;
using Knotical.Player;

namespace Knotical.Debug;

[GlobalClass]
public partial class GunProbe : Node3D
{
    private Node3D _character;
    private Node3D _pivot;
    private float _clock;
    private float _tick;
    private int _shot;
    private bool _toggled;
    private bool _untoggled;

    public override void _Ready()
    {
        var floor = new StaticBody3D { Name = "Floor" };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(40f, 1f, 40f) } });
        floor.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(40f, 1f, 40f) } });
        AddChild(floor);

        var sun = new DirectionalLight3D { Name = "Sun", ShadowEnabled = true };
        AddChild(sun);
        sun.RotationDegrees = new Vector3(-50f, 30f, 0f);

        var env = new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Color,
                BackgroundColor = new Color(0.4f, 0.6f, 0.8f),
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = new Color(1f, 1f, 1f),
                AmbientLightEnergy = 0.6f,
            },
        };
        AddChild(env);

        floor.GlobalPosition = new Vector3(0f, 99.5f, 0f);

        var scene = GD.Load<PackedScene>("res://character.tscn");
        if (scene == null)
        {
            GD.Print("gunprobe: character failed to load");
            return;
        }

        _character = scene.Instantiate<Node3D>();
        AddChild(_character);
        _character.Position = new Vector3(0f, 101.2f, 0f);
        _pivot = _character.GetNodeOrNull<Node3D>("CameraPivot");
        GD.Print($"gunprobe: ready pivot={_pivot != null}");
    }

    public override void _PhysicsProcess(double delta)
    {
        _clock += (float)delta;
        _tick += (float)delta;

        if (!_toggled && _clock > 0.5f)
        {
            _toggled = true;
            Input.ActionPress("tool_toggle");
            GD.Print($"gunprobe: toggle pressed t={_clock:F2}");
        }

        if (_toggled && !_untoggled && _clock > 0.7f)
        {
            _untoggled = true;
            Input.ActionRelease("tool_toggle");
        }

        if (_tick < 0.25f) return;
        _tick = 0f;

        if (_pivot == null || _character == null) return;

        var gun = _pivot.GetNodeOrNull<Node3D>("GunModel");
        Camera3D camera = GetViewport().GetCamera3D();

        if (gun == null)
        {
            GD.Print($"gunprobe: t={_clock:F2} no GunModel under pivot");
            return;
        }

        string meshes = "";
        int index = 0;
        foreach (Node child in gun.GetChildren())
        {
            if (child is not MeshInstance3D mesh) continue;
            meshes += $" m{index}:vis={mesh.IsVisibleInTree()} pos={Fmt(mesh.GlobalPosition)} scr={Screen(camera, mesh.GlobalPosition)}";
            index++;
        }

        GD.Print($"gunprobe: t={_clock:F2} gun vis={gun.Visible} vistree={gun.IsVisibleInTree()} pos={Fmt(gun.GlobalPosition)} scr={Screen(camera, gun.GlobalPosition)} cam={Fmt(camera?.GlobalPosition ?? Vector3.Zero)}{meshes}");

        if (_character.GetNodeOrNull<Node3D>("ArmRight") is DangleArm arm)
            GD.Print($"gunprobe: t={_clock:F2} shoulder={Fmt(arm.Root)} tip={Fmt(arm.Tip)}");

        if (_toggled && _clock > 1.2f + _shot * 1.0f && _shot < 3)
        {
            _shot++;
            var img = GetViewport().GetTexture().GetImage();
            string path = $"C:/Users/elime/AppData/Local/Temp/claude/C--Users-elime-source-repos-knotical/dc7cf43a-44cd-4f3e-a530-c0af13b33c9f/scratchpad/gunprobe_{_shot}.png";
            img.SavePng(path);
            GD.Print($"gunprobe: shot {_shot} saved");
        }
    }

    private string Screen(Camera3D camera, Vector3 world)
    {
        if (camera == null) return "nocam";
        Vector3 local = camera.GlobalTransform.AffineInverse() * world;
        if (local.Z > -0.01f) return $"behind(z={local.Z:F2})";
        Vector2 uv = camera.UnprojectPosition(world) / GetViewport().GetVisibleRect().Size;
        return $"{uv.X:F2},{uv.Y:F2}";
    }

    private static string Fmt(Vector3 v) => $"({v.X:F2},{v.Y:F2},{v.Z:F2})";
}
