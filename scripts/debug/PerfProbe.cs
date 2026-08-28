using Godot;
using Knotical.Rigging;

namespace Knotical.Debug;

[GlobalClass]
public partial class PerfProbe : Node
{
    private const float Window = 4f;

    private float _clock;
    private int _stage;
    private int _samples;
    private double _physics;
    private double _process;
    private double _objects;
    private double _pairs;

    public override void _PhysicsProcess(double delta)
    {
        _clock += (float)delta;
        RopeProfile.Enabled = true;
        if (_clock < 1.5f) return;

        _physics += Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess);
        _process += Performance.GetMonitor(Performance.Monitor.TimeProcess);
        _objects += Performance.GetMonitor(Performance.Monitor.Physics3DActiveObjects);
        _pairs += Performance.GetMonitor(Performance.Monitor.Physics3DCollisionPairs);
        _samples++;

        if (_clock < 1.5f + Window * (_stage + 1)) return;

        RopeProfile.Report(Window);
        Report(_stage switch
        {
            0 => "everything",
            1 => "ropes off",
            _ => "ropes+chains off",
        });

        _stage++;
        _samples = 0;
        _physics = 0;
        _process = 0;
        _objects = 0;
        _pairs = 0;

        if (_stage == 1) Silence<Rope>();
        if (_stage == 2) SilenceChains();
        if (_stage > 2) GetTree().Quit();
    }

    private void Report(string label)
    {
        if (_samples == 0) return;

        GD.Print($"perfprobe: {label,-18} physics={_physics / _samples * 1000f,6:0.00}ms " +
                 $"process={_process / _samples * 1000f,6:0.00}ms " +
                 $"bodies={_objects / _samples,6:0} pairs={_pairs / _samples,6:0}");
    }

    private void Silence<T>() where T : Node
    {
        int hit = 0;
        foreach (Node node in Walk(GetTree().Root))
        {
            if (node is not T) continue;
            node.SetPhysicsProcess(false);
            node.SetProcess(false);
            hit++;
        }

        GD.Print($"perfprobe: silenced {hit} {typeof(T).Name}");
    }

    private void SilenceChains()
    {
        int hit = 0;
        foreach (Node node in Walk(GetTree().Root))
        {
            if (node is not RigidBody3D body || !body.Name.ToString().StartsWith("Link")) continue;
            body.Freeze = true;
            body.SetPhysicsProcess(false);
            hit++;
        }

        GD.Print($"perfprobe: froze {hit} chain links");
    }

    private static System.Collections.Generic.IEnumerable<Node> Walk(Node from)
    {
        foreach (Node child in from.GetChildren())
        {
            yield return child;
            foreach (Node deep in Walk(child)) yield return deep;
        }
    }
}
