using Godot;

namespace Knotical.Ocean;

/// <summary>
/// Debug marker that rides the surface using the CPU wave sampler.
///
/// This exists to prove the thing the whole architecture rests on: that Ocean.cs and
/// ocean.gdshader agree. Drop a few of these with a small sphere or box as a child. If
/// they sit exactly on the rendered waves, CPU and GPU are in sync and buoyancy can be
/// trusted. If they drift, sink, or hover, the two implementations have diverged and
/// nothing built on top of them will behave.
/// </summary>
[GlobalClass]
public partial class WaveProbe : Node3D
{
    /// <summary>Tilt the marker to match the surface slope. Off makes drift easier to spot.</summary>
    [Export] public bool AlignToNormal { get; set; } = true;

    /// <summary>Metres above the surface to sit, so a sphere marker is not half-buried.</summary>
    [Export] public float SurfaceOffset { get; set; }

    public override void _Process(double delta)
    {
        Ocean ocean = Ocean.Instance;
        if (ocean == null) return;

        var xz = new Vector2(GlobalPosition.X, GlobalPosition.Z);
        GlobalPosition = new Vector3(xz.X, ocean.GetHeight(xz) + SurfaceOffset, xz.Y);

        if (!AlignToNormal) return;

        Vector3 up = ocean.GetNormal(xz);
        Vector3 forward = up.Cross(Vector3.Right);

        if (forward.LengthSquared() < 0.0001f) return;

        forward = forward.Normalized();
        GlobalBasis = new Basis(forward.Cross(up).Normalized(), up, forward);
    }
}
