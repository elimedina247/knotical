using System.Collections.Generic;
using Godot;
using Knotical.Boat;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Vfx;

/// <summary>
/// Scene-level splash service. Hulls and floating cargo report water entries through
/// <see cref="Request"/>; a small pool of one-shot particle bursts plays them and each
/// splash stamps the foam capture so it leaves a fading patch behind.
///
/// Droplets are billboarded sprites with plain ballistic motion. They do not yet die
/// against the wave surface — at these lifetimes they fall past it for a few frames at
/// most, which reads fine from an eagle camera. The analytic-surface kill from the plan
/// is the upgrade path if close-up shots ever matter.
/// </summary>
[GlobalClass]
public partial class SplashEmitter : Node3D
{
    private const int Pool = 8;

    public static readonly List<SplashEmitter> Active = new();

    [Export(PropertyHint.Range, "0.5,10,0.1")]
    public float MinEntrySpeed { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0,2,0.05")]
    public float Loudness { get; set; } = 1f;

    [Export] public bool Trace { get; set; }

    [Export(PropertyHint.Range, "0.5,10,0.1")]
    public float SprayStartSpeed { get; set; } = 2f;

    [Export(PropertyHint.Range, "2,20,0.1")]
    public float SprayFullSpeed { get; set; } = 7f;

    private const int SprayPool = 4;

    private readonly Queue<(Vector3 Pos, float Speed, float Size)> _requests = new();
    private GpuParticles3D[] _bursts = System.Array.Empty<GpuParticles3D>();
    private GpuParticles3D[] _sprays = System.Array.Empty<GpuParticles3D>();
    private int _next;

    public static void Request(Vector3 surfacePos, float entrySpeed, float size)
    {
        if (Active.Count == 0) return;

        SplashEmitter emitter = Active[0];
        if (entrySpeed < emitter.MinEntrySpeed) return;
        if (emitter._requests.Count >= Pool) return;

        emitter._requests.Enqueue((surfacePos, entrySpeed, size));
    }

    public override void _EnterTree()
    {
        if (!Engine.IsEditorHint()) Active.Add(this);
    }

    public override void _ExitTree()
    {
        Active.Remove(this);
    }

    public override void _Ready()
    {
        _bursts = new GpuParticles3D[Pool];

        for (int i = 0; i < Pool; i++)
        {
            _bursts[i] = BuildBurst();
            AddChild(_bursts[i]);
        }

        _sprays = new GpuParticles3D[SprayPool];

        for (int i = 0; i < SprayPool; i++)
        {
            _sprays[i] = BuildSpray();
            AddChild(_sprays[i]);
        }
    }

    private static GpuParticles3D BuildSpray()
    {
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 1f, 1f, 0.75f));
        ramp.SetColor(1, new Color(0.9f, 0.96f, 1f, 0f));

        var process = new ParticleProcessMaterial
        {
            Direction = new Vector3(0f, 0.7f, -0.7f),
            Spread = 40f,
            InitialVelocityMin = 3f,
            InitialVelocityMax = 6f,
            Gravity = new Vector3(0f, -13f, 0f),
            ScaleMin = 0.35f,
            ScaleMax = 0.9f,
            ColorRamp = new GradientTexture1D { Gradient = ramp },
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(1.2f, 0.2f, 0.6f)
        };

        var sprite = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            AlbedoColor = new Color(1f, 1f, 1f, 0.8f)
        };

        return new GpuParticles3D
        {
            Emitting = false,
            OneShot = false,
            Lifetime = 0.9,
            Amount = 180,
            AmountRatio = 0f,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.35f, 0.35f), Material = sprite },
            VisibilityAabb = new Aabb(new Vector3(-15f, -15f, -15f), new Vector3(30f, 30f, 30f))
        };
    }

    private void UpdateSprays()
    {
        OceanField ocean = OceanField.Instance;
        int used = 0;

        if (ocean != null)
        {
            foreach (BoatHull hull in BoatHull.Active)
            {
                if (used >= SprayPool) break;
                if (!IsInstanceValid(hull) || !hull.IsInsideTree()) continue;

                Basis level = hull.GlobalBasis.Orthonormalized();
                Vector3 forward = -level.Z;
                float headway = hull.LinearVelocity.Dot(forward);

                Vector3 bow = hull.BowWorld;
                float water = ocean.GetRenderedHeight(new Vector2(bow.X, bow.Z));
                float clearance = bow.Y - water;

                float ratio = Mathf.Clamp(
                    (headway - SprayStartSpeed) / Mathf.Max(SprayFullSpeed - SprayStartSpeed, 0.1f), 0f, 1f);

                if (clearance is > 2.5f or < -2f) ratio = 0f;

                GpuParticles3D spray = _sprays[used++];
                spray.AmountRatio = ratio;
                spray.Emitting = ratio > 0.01f;

                if (ratio <= 0.01f) continue;

                spray.GlobalPosition = new Vector3(bow.X, water + 0.2f, bow.Z);
                spray.GlobalBasis = level;

                if (spray.ProcessMaterial is ParticleProcessMaterial process)
                {
                    process.InitialVelocityMin = 2f + headway * 0.5f;
                    process.InitialVelocityMax = 3.5f + headway * 0.9f;
                    process.EmissionBoxExtents = new Vector3(hull.BeamWidth * 0.35f, 0.2f, 0.6f);
                }

                FoamCapture.Stamp(new Vector2(bow.X, bow.Z), hull.BeamWidth * 0.6f, 0.12f * ratio);
            }
        }

        for (; used < SprayPool; used++)
        {
            _sprays[used].Emitting = false;
            _sprays[used].AmountRatio = 0f;
        }
    }

    private static GpuParticles3D BuildBurst()
    {
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 1f, 1f, 0.9f));
        ramp.SetColor(1, new Color(0.92f, 0.97f, 1f, 0f));

        var process = new ParticleProcessMaterial
        {
            Direction = new Vector3(0f, 1f, 0f),
            Spread = 65f,
            InitialVelocityMin = 2f,
            InitialVelocityMax = 6f,
            Gravity = new Vector3(0f, -13f, 0f),
            ScaleMin = 0.5f,
            ScaleMax = 1.4f,
            ColorRamp = new GradientTexture1D { Gradient = ramp },
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.6f
        };

        var sprite = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            AlbedoColor = new Color(1f, 1f, 1f, 0.85f)
        };

        return new GpuParticles3D
        {
            Emitting = false,
            OneShot = true,
            Explosiveness = 0.9f,
            Lifetime = 1.1,
            Amount = 24,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.4f, 0.4f), Material = sprite },
            VisibilityAabb = new Aabb(new Vector3(-12f, -12f, -12f), new Vector3(24f, 24f, 24f))
        };
    }

    public override void _Process(double delta)
    {
        UpdateSprays();

        while (_requests.Count > 0)
        {
            (Vector3 pos, float speed, float size) = _requests.Dequeue();

            GpuParticles3D burst = _bursts[_next];
            _next = (_next + 1) % Pool;

            float punch = Mathf.Clamp(speed / 6f, 0.3f, 2f) * Loudness;

            burst.GlobalPosition = pos;
            burst.Amount = (int)Mathf.Clamp(10f + 22f * punch * size, 8f, 64f);

            if (burst.ProcessMaterial is ParticleProcessMaterial process)
            {
                process.InitialVelocityMin = 1.5f * punch;
                process.InitialVelocityMax = 5.5f * punch;
                process.EmissionSphereRadius = 0.4f * size;
                process.ScaleMin = 0.4f * size;
                process.ScaleMax = 1.2f * size;
            }

            burst.Restart();

            FoamCapture.Stamp(new Vector2(pos.X, pos.Z), 3f * size * punch, Mathf.Clamp(punch, 0.3f, 1f));

            if (Trace) GD.Print($"splash at ({pos.X:0},{pos.Z:0}) speed {speed:0.0} size {size:0.0}");
        }
    }
}
