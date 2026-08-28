using Godot;
using Knotical.Rigging;
using Knotical.Debug;

namespace Knotical.Debug;

[GlobalClass]
public partial class RopeWrapProbe : Node3D
{
    private const float OrbitRadius = 2.5f;
    private const float Turns = 1.25f;
    private const float Sweep = 10f;
    private const float Hold = 1f;

    private StaticBody3D _column;
    private RigidBody3D _holder;
    private Rope _rope;
    private float _clock;
    private float _tick;
    private float _maxPen;
    private int _maxWraps;
    private bool _wrappedHalf;

    public override void _Ready()
    {
        _column = new StaticBody3D { Name = "Column" };
        _column.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.3f, 4f, 0.3f) } });
        AddChild(_column);
        _column.GlobalPosition = new Vector3(0f, 2f, 0f);

        _holder = new RigidBody3D { Name = "Hand", Freeze = true, Mass = 70f };
        _holder.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.1f } });
        AddChild(_holder);
        _holder.GlobalPosition = Hand(0f);

        _rope = new Rope { Name = "ProbeRope", Breaks = false, WorkingLength = 3.2f, Beads = 48 };
        AddChild(_rope);
        _rope.BindStart(_column, new Vector3(-0.16f, 2f, 0f));
        _rope.Hold(_holder.GlobalPosition, _holder);

        GD.Print("wrapprobe: start");
    }

    private static Vector3 Hand(float clock)
    {
        float angle;
        if (clock < Sweep) angle = clock / Sweep * Turns * Mathf.Tau;
        else if (clock < Sweep + Hold) angle = Turns * Mathf.Tau;
        else if (clock < Sweep * 2f + Hold) angle = (1f - (clock - Sweep - Hold) / Sweep) * Turns * Mathf.Tau;
        else angle = 0f;

        float theta = Mathf.Pi + angle;
        return new Vector3(Mathf.Cos(theta) * OrbitRadius, 2f, Mathf.Sin(theta) * OrbitRadius);
    }

    public override void _PhysicsProcess(double delta)
    {
        _clock += (float)delta;
        _tick += (float)delta;
        RopeProfile.Enabled = true;
        if (_tick >= 0.25f) RopeProfile.Report(0.25f);

        _holder.GlobalPosition = Hand(_clock);
        _rope.Hold(_holder.GlobalPosition, _holder);

        float pen = Penetration();
        if (_clock > 0.5f) _maxPen = Mathf.Max(_maxPen, pen);
        _maxWraps = Mathf.Max(_maxWraps, _rope.WrapCount);
        if (_clock > Sweep * 0.35f && _clock < Sweep && _rope.WrapCount > 0) _wrappedHalf = true;

        if (_tick >= 0.25f)
        {
            _tick = 0f;
            string wraps = "";
            for (int i = 0; i < _rope.WrapCount; i++)
            {
                Vector3 w = _rope.WrapAt(i);
                wraps += $" ({w.X:0.00},{w.Z:0.00})";
            }
            GD.Print($"t={_clock,5:0.00} wraps={_rope.WrapCount} pen={pen:0.000} taut={(_rope.Taut ? 1 : 0)}{wraps}");
        }

        if (_clock >= Sweep * 2f + Hold * 2f + 0.5f)
        {
            bool unwound = _rope.WrapCount <= 1;
            GD.Print($"wrapprobe: done maxPen={_maxPen:0.000} maxWraps={_maxWraps} wrappedHalf={_wrappedHalf} endWraps={_rope.WrapCount}");
            GD.Print($"wrapprobe: {(_maxPen < 0.05f && _wrappedHalf && unwound ? "PASS" : "FAIL")}");
            GetTree().Quit();
        }
    }

    private float Penetration()
    {
        float worst = 0f;

        for (int i = 0; i + 1 < _rope.BeadCount; i++)
            for (int s = 0; s <= 4; s++)
                worst = Mathf.Max(worst, Inside(_rope.BeadAt(i).Lerp(_rope.BeadAt(i + 1), s / 4f)));

        return worst;
    }

    private static float Inside(Vector3 p)
    {
        if (p.Y < 0f || p.Y > 4f) return 0f;

        float dx = 0.15f - Mathf.Abs(p.X);
        float dz = 0.15f - Mathf.Abs(p.Z);
        return dx > 0f && dz > 0f ? Mathf.Min(dx, dz) : 0f;
    }
}
