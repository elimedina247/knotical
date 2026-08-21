using Godot;

namespace Knotical.Debug;

[GlobalClass]
public partial class DeckProbe : Node3D
{
    [Export] public NodePath Target { get; set; }

    [Export] public float From { get; set; } = 6f;

    [Export] public float To { get; set; } = -3f;

    private int _tick;

    private static Node3D FindBody(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is RigidBody3D body) return body;
            Node3D found = FindBody(child);
            if (found != null) return found;
        }

        return null;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (++_tick != 4) return;

        Node3D body = GetNodeOrNull<Node3D>(Target) ?? FindBody(GetTree().Root);
        if (body == null) { GD.Print("probe: no target"); return; }

        var space = GetWorld3D().DirectSpaceState;
        GD.Print("probe: deck height by station (rows aft->fwd, cols port->stbd)");

        for (float z = 5.0f; z >= -5.01f; z -= 0.5f)
        {
            string row = "";
            for (float x = -1.75f; x <= 1.76f; x += 0.5f)
            {
                Vector3 top = body.GlobalPosition + new Vector3(x, From, z);
                var q = PhysicsRayQueryParameters3D.Create(top, top + Vector3.Down * (From - To));
                q.CollideWithAreas = false;
                var hit = space.IntersectRay(q);
                row += hit.Count == 0 ? "   --" : $"{((Vector3)hit["position"]).Y - body.GlobalPosition.Y,5:0.00}";
            }
            GD.Print($"  z={z,5:0.0} {row}");
        }

        GD.Print("probe: second surface below the deck (drop from just under deck top)");
        for (float z = 5.0f; z >= -5.01f; z -= 0.5f)
        {
            Vector3 top = body.GlobalPosition + new Vector3(0f, 0.9f, z);
            var q = PhysicsRayQueryParameters3D.Create(top, top + Vector3.Down * 3f);
            var hit = space.IntersectRay(q);
            GD.Print($"  z={z,5:0.0} floor={(hit.Count == 0 ? "none" : $"{((Vector3)hit["position"]).Y - body.GlobalPosition.Y:0.00}")}");
        }

        GetTree().Quit();
    }
}
