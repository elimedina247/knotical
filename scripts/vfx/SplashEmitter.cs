using System.Collections.Generic;
using Godot;
using Knotical.Boat;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Vfx;

/// <summary>
/// Scene-level splash service. Hulls and floating cargo report water entries through
/// <see cref="Request"/>; each splash fires a few large textured spray plumes plus a
/// burst of droplet sprites, and stamps the foam capture so it leaves a fading patch.
///
/// The plume texture is a 2x2 sheet of spray variants (Girardot's splash format) —
/// a handful of big sprites carrying the detail instead of many small blank quads.
/// Replace res://assets/textures/spray_sheet_2x2.png to reskin every splash at once.
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
    private GpuParticles3D[] _plumes = System.Array.Empty<GpuParticles3D>();
    private GpuParticles3D[] _bursts = System.Array.Empty<GpuParticles3D>();
    private GpuParticles3D[] _sprays = System.Array.Empty<GpuParticles3D>();
    private int _next;

    private static Texture2D _sheet;
    private static Texture2D _droplet;

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
        _sheet ??= LoadOptional("res://assets/textures/spray_sheet_2x2.png");
        _droplet ??= LoadOptional("res://assets/textures/spray_droplet.png");

        _plumes = new GpuParticles3D[Pool];
        _bursts = new GpuParticles3D[Pool];

        for (int i = 0; i < Pool; i++)
        {
            _plumes[i] = BuildPlume();
            _bursts[i] = BuildBurst();
            AddChild(_plumes[i]);
            AddChild(_bursts[i]);
        }

        _sprays = new GpuParticles3D[SprayPool];

        for (int i = 0; i < SprayPool; i++)
        {
            _sprays[i] = BuildSpray();
            AddChild(_sprays[i]);
        }
    }

    private static Texture2D LoadOptional(string path) =>
        ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

    private static StandardMaterial3D SheetMaterial(float alpha)
    {
        return new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            AlbedoColor = new Color(1f, 1f, 1f, alpha),
            AlbedoTexture = _sheet,
            ParticlesAnimHFrames = 2,
            ParticlesAnimVFrames = 2,
            ParticlesAnimLoop = false
        };
    }

    private static GpuParticles3D BuildPlume()
    {
        var ramp = new Gradient();
        ramp.AddPoint(0.18f, Colors.White);
        ramp.SetColor(0, new Color(1f, 1f, 1f, 0.35f));
        ramp.SetColor(1, new Color(1f, 1f, 1f, 0.95f));
        ramp.SetColor(2, new Color(0.94f, 0.98f, 1f, 0f));

        var grow = new Curve();
        grow.AddPoint(new Vector2(0f, 0.35f));
        grow.AddPoint(new Vector2(0.35f, 0.8f));
        grow.AddPoint(new Vector2(1f, 1.15f));

        var process = new ParticleProcessMaterial
        {
            Direction = new Vector3(0f, 1f, 0f),
            Spread = 22f,
            InitialVelocityMin = 1.2f,
            InitialVelocityMax = 2.6f,
            Gravity = new Vector3(0f, -2.4f, 0f),
            ScaleMin = 0.8f,
            ScaleMax = 1.5f,
            ScaleCurve = new CurveTexture { Curve = grow },
            AngleMin = -18f,
            AngleMax = 18f,
            AnimOffsetMin = 0f,
            AnimOffsetMax = 1f,
            ColorRamp = new GradientTexture1D { Gradient = ramp },
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.4f
        };

        return new GpuParticles3D
        {
            Emitting = false,
            OneShot = true,
            Explosiveness = 1f,
            Lifetime = 1.5,
            Amount = 4,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(1f, 1f), Material = SheetMaterial(0.9f) },
            VisibilityAabb = new Aabb(new Vector3(-14f, -14f, -14f), new Vector3(28f, 28f, 28f))
        };
    }

    private static GpuParticles3D BuildBurst()
    {
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 1f, 1f, 0.95f));
        ramp.SetColor(1, new Color(0.92f, 0.97f, 1f, 0f));

        var process = new ParticleProcessMaterial
        {
            Direction = new Vector3(0f, 1f, 0f),
            Spread = 55f,
            InitialVelocityMin = 3f,
            InitialVelocityMax = 7f,
            Gravity = new Vector3(0f, -18f, 0f),
            ScaleMin = 0.4f,
            ScaleMax = 1f,
            ColorRamp = new GradientTexture1D { Gradient = ramp },
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.5f
        };

        var sprite = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            AlbedoColor = new Color(1f, 1f, 1f, 0.9f),
            AlbedoTexture = _droplet
        };

        return new GpuParticles3D
        {
            Emitting = false,
            OneShot = true,
            Explosiveness = 0.95f,
            Lifetime = 0.85,
            Amount = 42,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.16f, 0.16f), Material = sprite },
            VisibilityAabb = new Aabb(new Vector3(-12f, -12f, -12f), new Vector3(24f, 24f, 24f))
        };
    }

    private static GpuParticles3D BuildSpray()
    {
        var ramp = new Gradient();
        ramp.SetColor(0, new Color(1f, 1f, 1f, 0.55f));
        ramp.SetColor(1, new Color(0.9f, 0.96f, 1f, 0f));

        var process = new ParticleProcessMaterial
        {
            Direction = new Vector3(0f, 0.7f, -0.7f),
            Spread = 40f,
            InitialVelocityMin = 3f,
            InitialVelocityMax = 6f,
            Gravity = new Vector3(0f, -9f, 0f),
            ScaleMin = 0.5f,
            ScaleMax = 1.1f,
            AnimOffsetMin = 0f,
            AnimOffsetMax = 1f,
            ColorRamp = new GradientTexture1D { Gradient = ramp },
            EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(1.2f, 0.2f, 0.6f)
        };

        return new GpuParticles3D
        {
            Emitting = false,
            OneShot = false,
            Lifetime = 0.9,
            Amount = 90,
            AmountRatio = 0f,
            ProcessMaterial = process,
            DrawPass1 = new QuadMesh { Size = new Vector2(0.8f, 0.8f), Material = SheetMaterial(0.6f) },
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
                used = SprayBow(hull, hull.BowWorld, hull.BeamWidth, ocean, used);
            }

            foreach (SailController boat in SailController.Active)
            {
                if (used >= SprayPool) break;
                if (!IsInstanceValid(boat) || !boat.IsInsideTree()) continue;
                used = SprayBow(boat, boat.BowWorld, boat.BeamWidth, ocean, used);
            }
        }

        for (; used < SprayPool; used++)
        {
            _sprays[used].Emitting = false;
            _sprays[used].AmountRatio = 0f;
        }
    }

    private int SprayBow(RigidBody3D body, Vector3 bow, float beamWidth, OceanField ocean, int used)
    {
        Basis level = body.GlobalBasis.Orthonormalized();
        Vector3 forward = -level.Z;
        float headway = body.LinearVelocity.Dot(forward);

        float water = ocean.GetHeight(new Vector2(bow.X, bow.Z));
        float clearance = bow.Y - water;

        float ratio = Mathf.Clamp(
            (headway - SprayStartSpeed) / Mathf.Max(SprayFullSpeed - SprayStartSpeed, 0.1f), 0f, 1f);

        if (clearance is > 2.5f or < -2f) ratio = 0f;

        GpuParticles3D spray = _sprays[used++];
        spray.AmountRatio = ratio;
        spray.Emitting = ratio > 0.01f;

        if (ratio <= 0.01f) return used;

        spray.GlobalPosition = new Vector3(bow.X, water + 0.2f, bow.Z);
        spray.GlobalBasis = level;

        if (spray.ProcessMaterial is ParticleProcessMaterial process)
        {
            process.InitialVelocityMin = 2f + headway * 0.5f;
            process.InitialVelocityMax = 3.5f + headway * 0.9f;
            process.EmissionBoxExtents = new Vector3(beamWidth * 0.35f, 0.2f, 0.6f);
        }

        FoamCapture.Stamp(new Vector2(bow.X, bow.Z), beamWidth * 0.6f, 0.12f * ratio);
        return used;
    }

    public override void _Process(double delta)
    {
        UpdateSprays();

        while (_requests.Count > 0)
        {
            (Vector3 pos, float speed, float size) = _requests.Dequeue();

            GpuParticles3D plume = _plumes[_next];
            GpuParticles3D burst = _bursts[_next];
            _next = (_next + 1) % Pool;

            float punch = Mathf.Clamp(speed / 6f, 0.3f, 2f) * Loudness;

            plume.GlobalPosition = pos;
            plume.Amount = 3 + (int)Mathf.Clamp(punch * 2f, 0f, 3f);

            if (plume.ProcessMaterial is ParticleProcessMaterial plumeProcess)
            {
                plumeProcess.InitialVelocityMin = 1f * punch;
                plumeProcess.InitialVelocityMax = 2.4f * punch;
                plumeProcess.EmissionSphereRadius = 0.5f * size;
                plumeProcess.ScaleMin = 1.6f * size * Mathf.Max(punch, 0.6f);
                plumeProcess.ScaleMax = 3f * size * Mathf.Max(punch, 0.6f);
            }

            plume.Restart();

            burst.GlobalPosition = pos;
            burst.Amount = (int)Mathf.Clamp(16f + 30f * punch * size, 12f, 72f);

            if (burst.ProcessMaterial is ParticleProcessMaterial process)
            {
                process.InitialVelocityMin = 2f * punch;
                process.InitialVelocityMax = 6.5f * punch;
                process.EmissionSphereRadius = 0.4f * size;
            }

            burst.Restart();

            FoamCapture.Stamp(new Vector2(pos.X, pos.Z), 3f * size * punch, Mathf.Clamp(punch, 0.3f, 1f));

            if (Trace) GD.Print($"splash at ({pos.X:0},{pos.Z:0}) speed {speed:0.0} size {size:0.0}");
        }
    }
}
