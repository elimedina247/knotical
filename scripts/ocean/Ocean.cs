using Godot;

namespace Knotical.Ocean;

/// <summary>
/// The single source of truth for the sea surface.
///
/// The wave field is a pure function of world position and time, which is why it needs
/// almost no networking: sync <see cref="Time"/> and every client renders and simulates
/// an identical ocean. This class owns that function on the CPU; ocean.gdshader runs the
/// same maths on the GPU. If you change one, change the other.
///
/// Register as an autoload named "Ocean".
/// </summary>
public partial class Ocean : Node
{
    /// <summary>Half-extent of the playable world in metres. Coordinates run -6000..+6000.</summary>
    public const float WorldHalfExtent = 6000f;

    private const float Gravity = 9.81f;

    public static Ocean Instance { get; private set; }

    [Export] public OceanSettings Settings { get; set; }

    /// <summary>
    /// Seconds of wave time. Advanced locally, but authoritative on the host once
    /// networking lands — clients adopt the host's value rather than their own clock.
    /// </summary>
    public double Time { get; private set; }

    private Vector4[] _waves = System.Array.Empty<Vector4>();
    private float[] _phases = System.Array.Empty<float>();

    /// <summary>xy = direction, z = amplitude, w = wavelength. Read by OceanSurface.</summary>
    /// <remarks>
    /// The shader additionally fades short waves out with distance from the camera, to
    /// stop the far field aliasing on a coarse mesh. That fade is deliberately not
    /// mirrored here: it is a rendering concern, it is view-dependent, and everything
    /// that samples the CPU side is close enough to the camera for it to be inactive.
    /// </remarks>
    public Vector4[] Waves => _waves;

    /// <summary>Per-wave phase offset in radians, parallel to <see cref="Waves"/>.</summary>
    public float[] Phases => _phases;

    public override void _EnterTree()
    {
        Instance = this;
        Settings ??= new OceanSettings();
        Rebuild();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    public override void _Process(double delta)
    {
        Time += delta;
    }

    /// <summary>Rebuilds the wave table after editing <see cref="Settings"/>.</summary>
    public void Rebuild()
    {
        Settings.Build(out _waves, out _phases);
    }

    /// <summary>
    /// Adopts a settings resource and rebuilds. Autoload exports are awkward to edit in
    /// the inspector, so OceanSurface owns the authored resource and hands it over here.
    /// </summary>
    public void SetSettings(OceanSettings settings)
    {
        if (settings == null) return;
        Settings = settings;
        Rebuild();
    }

    /// <summary>Overrides wave time. Used by clients to adopt the host's clock.</summary>
    public void SetTime(double time) => Time = time;

    /// <summary>
    /// Surface height at a world XZ position.
    ///
    /// Note this samples the undisplaced height field — it ignores the Gerstner
    /// horizontal pinch the shader applies, so in choppy water the value is off by
    /// roughly the displacement magnitude. That error is well under the size of a hull
    /// and buoyancy probes average it out, so it is not worth inverting yet. Revisit if
    /// Steepness ever goes high enough that boats visibly float above the crests.
    /// </summary>
    public float GetHeight(Vector2 worldXZ)
    {
        float y = 0f;
        float t = (float)Time;

        for (int i = 0; i < _waves.Length; i++)
        {
            Vector4 w = _waves[i];
            var dir = new Vector2(w.X, w.Y);
            float k = Mathf.Tau / w.W;
            float omega = Mathf.Sqrt(Gravity * k);

            y += w.Z * Mathf.Sin(k * dir.Dot(worldXZ) - omega * t + _phases[i]);
        }

        return y;
    }

    public float GetHeight(Vector3 worldPos) => GetHeight(new Vector2(worldPos.X, worldPos.Z));

    /// <summary>
    /// Analytic surface normal. Derived from the wave derivatives rather than sampled
    /// neighbours, so it stays exact no matter how coarse the mesh is.
    /// </summary>
    public Vector3 GetNormal(Vector2 worldXZ)
    {
        float dx = 0f;
        float dz = 0f;
        float t = (float)Time;

        for (int i = 0; i < _waves.Length; i++)
        {
            Vector4 w = _waves[i];
            var dir = new Vector2(w.X, w.Y);
            float k = Mathf.Tau / w.W;
            float omega = Mathf.Sqrt(Gravity * k);

            float c = Mathf.Cos(k * dir.Dot(worldXZ) - omega * t + _phases[i]) * w.Z * k;
            dx += c * dir.X;
            dz += c * dir.Y;
        }

        return new Vector3(-dx, 1f, -dz).Normalized();
    }

    public Vector3 GetNormal(Vector3 worldPos) => GetNormal(new Vector2(worldPos.X, worldPos.Z));

    /// <summary>
    /// Vertical velocity of the surface. Useful for drag that should not fight a wave
    /// lifting a hull, and for spray thresholds.
    /// </summary>
    public float GetVerticalVelocity(Vector2 worldXZ)
    {
        float v = 0f;
        float t = (float)Time;

        for (int i = 0; i < _waves.Length; i++)
        {
            Vector4 w = _waves[i];
            var dir = new Vector2(w.X, w.Y);
            float k = Mathf.Tau / w.W;
            float omega = Mathf.Sqrt(Gravity * k);

            v += -w.Z * omega * Mathf.Cos(k * dir.Dot(worldXZ) - omega * t + _phases[i]);
        }

        return v;
    }
}
