using Godot;

namespace Knotical.Style;

[Tool]
[GlobalClass]
public partial class ToonPaint : Node
{
    [Export] public NodePath Target { get; set; }

    [Export] public Material PaintMaterial { get; set; }

    [Export] public Material DeckMaterial { get; set; }

    [Export] public bool ApplyNow { get => false; set { if (value) Apply(); } }

    public override void _Ready()
    {
        Apply();
    }

    public void Apply()
    {
        Node root = Target.IsEmpty ? GetParent() : GetNodeOrNull(Target);
        if (root == null) return;
        Walk(root);
    }

    private void Walk(Node node)
    {
        if (node is MeshInstance3D instance && instance.Mesh != null)
        {
            for (int i = 0; i < instance.Mesh.GetSurfaceCount(); i++)
            {
                string name = instance.Mesh.SurfaceGetMaterial(i)?.ResourceName ?? "";
                Material pick = name.Contains("Deck") ? DeckMaterial : PaintMaterial;
                if (pick != null) instance.SetSurfaceOverrideMaterial(i, pick);
            }
        }

        foreach (Node child in node.GetChildren()) Walk(child);
    }
}
