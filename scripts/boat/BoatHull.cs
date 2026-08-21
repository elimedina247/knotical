using System.Collections.Generic;
using Godot;
using Knotical.Weather;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Boat;

[Tool]
[GlobalClass]
public partial class BoatHull : RigidBody3D
{
    private const float Gravity = 9.81f;

    public static readonly List<BoatHull> Active = new();

    private readonly HullForm _form = new();
    private SailingRig _rig;
    private Vector3[] _probeLocal = System.Array.Empty<Vector3>();
    private float[] _probeVolume = System.Array.Empty<float>();
    private float[] _probeSpan = System.Array.Empty<float>();
    private float[] _probeReserve = System.Array.Empty<float>();
    private float[] _probeArea = System.Array.Empty<float>();
    private Vector3[] _probeWorld = System.Array.Empty<Vector3>();
    private float[] _probeWater = System.Array.Empty<float>();
    private float[] _probeWet = System.Array.Empty<float>();
    private Vector3[] _probeForce = System.Array.Empty<Vector3>();
    private float _lateralArea;
    private float _frontalArea;
    private OceanField _ocean;
    private Vector3 _com;
    private Vector3 _linear;
    private Vector3 _angular;
    private Vector3 _force;
    private Vector3 _torque;
    private Vector3 _drag;
    private float _designVolume = 1f;

    [Export(PropertyHint.Range, "4,300,0.05")]
    public float HullLength { get => _form.Length; set { _form.Length = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,120,0.05")]
    public float Beam { get => _form.Beam; set { _form.Beam = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,50,0.05")]
    public float Draft { get => _form.Draft; set { _form.Draft = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.2,50,0.05")]
    public float Freeboard { get => _form.Freeboard; set { _form.Freeboard = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,30,0.05")]
    public float BowSheerRise { get => _form.BowSheerRise; set { _form.BowSheerRise = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,30,0.05")]
    public float SternSheerRise { get => _form.SternSheerRise; set { _form.SternSheerRise = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,6,0.05")]
    public float SheerPower { get => _form.SheerPower; set { _form.SheerPower = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,25,0.05")]
    public float Rocker { get => _form.Rocker; set { _form.Rocker = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,6,0.05")]
    public float RockerPower { get => _form.RockerPower; set { _form.RockerPower = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,6,0.05")]
    public float BowSharpness { get => _form.BowSharpness; set { _form.BowSharpness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.5,6,0.05")]
    public float SternSharpness { get => _form.SternSharpness; set { _form.SternSharpness = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float TransomWidth { get => _form.TransomWidth; set { _form.TransomWidth = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-10,15,0.05")]
    public float TransomRake { get => _form.TransomRake; set { _form.TransomRake = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,20,0.05")]
    public float StemRake { get => _form.StemRake; set { _form.StemRake = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,6,0.05")]
    public float StemPower { get => _form.StemPower; set { _form.StemPower = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0.15,1.5,0.01")]
    public float BilgeFullness { get => _form.BilgeFullness; set { _form.BilgeFullness = value; Rebuild(); } }

    private int _solidStations = 17;
    private int _solidRings = 7;
    private int _collisionSlices = 7;

    [Export(PropertyHint.Range, "5,49,2")]
    public int SolidStations { get => _solidStations; set { _solidStations = value; Rebuild(); } }

    [Export(PropertyHint.Range, "3,17,1")]
    public int SolidRings { get => _solidRings; set { _solidRings = value; Rebuild(); } }

    [Export(PropertyHint.Range, "1,16,1")]
    public int CollisionSlices { get => _collisionSlices; set { _collisionSlices = value; Rebuild(); } }

    private float _deckHeight = 2.7f;

    [Export(PropertyHint.Range, "0,20,0.05")]
    public float DeckHeight { get => _deckHeight; set { _deckHeight = value; Rebuild(); } }

    private float _bulwarkThickness = 0.54f;

    [Export(PropertyHint.Range, "0.05,2,0.01")]
    public float BulwarkThickness { get => _bulwarkThickness; set { _bulwarkThickness = value; Rebuild(); } }

    private float _surfaceOffset = 0.075f;

    [Export(PropertyHint.Range, "0,2.5,0.005")]
    public float SurfaceOffset { get => _surfaceOffset; set { _surfaceOffset = value; Rebuild(); } }

    private bool _hollowHull;

    [Export] public bool HollowHull { get => _hollowHull; set { _hollowHull = value; Rebuild(); } }

    private float _hullThickness = 0.12f;

    [Export(PropertyHint.Range, "0.02,2,0.01")]
    public float HullThickness { get => _hullThickness; set { _hullThickness = value; Rebuild(); } }

    private float _holdFloor;

    [Export(PropertyHint.Range, "-20,20,0.05")]
    public float HoldFloor { get => _holdFloor; set { _holdFloor = value; Rebuild(); } }

    private float _deckThickness = 0.14f;

    [Export(PropertyHint.Range, "0.02,1,0.01")]
    public float DeckThickness { get => _deckThickness; set { _deckThickness = value; Rebuild(); } }

    private float _hatchFrom;

    [Export(PropertyHint.Range, "-150,150,0.05")]
    public float HatchFrom { get => _hatchFrom; set { _hatchFrom = value; Rebuild(); } }

    private float _hatchTo;

    [Export(PropertyHint.Range, "-150,150,0.05")]
    public float HatchTo { get => _hatchTo; set { _hatchTo = value; Rebuild(); } }

    private float _hatchWidth;

    [Export(PropertyHint.Range, "0,30,0.05")]
    public float HatchWidth { get => _hatchWidth; set { _hatchWidth = value; Rebuild(); } }

    private float _comHeight = -0.8f;
    private float _comLength = 0.13f;

    [Export(PropertyHint.Range, "-20,15,0.05")]
    public float CenterOfMassHeight { get => _comHeight; set { _comHeight = value; Rebuild(); } }

    [Export(PropertyHint.Range, "-30,30,0.05")]
    public float CenterOfMassLength { get => _comLength; set { _comLength = value; Rebuild(); } }

    [Export] public float WaterDensity { get; set; } = 1025f;

    private float _displacement = 1f;

    [Export(PropertyHint.Range, "0.2,1.4,0.005")]
    public float Displacement { get => _displacement; set { _displacement = value; Rebuild(); } }

    private int _probeStations = 5;

    [Export(PropertyHint.Range, "3,9,1")]
    public int ProbeStations { get => _probeStations; set { _probeStations = value; Rebuild(); } }

    private float _probeHeightFraction;

    [Export(PropertyHint.Range, "0,0.8,0.01")]
    public float ProbeHeightFraction { get => _probeHeightFraction; set { _probeHeightFraction = value; Rebuild(); } }

    [Export(PropertyHint.Range, "0,4,0.05")]
    public float HeaveDamping { get; set; } = 1.5f;

    [Export(PropertyHint.Range, "0,6,0.05")]
    public float SlamDrag { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float ForwardDrag { get; set; } = 0.3f;

    [Export(PropertyHint.Range, "0,0.2,0.005")]
    public float ForwardDragLinear { get; set; } = 0.02f;

    [Export(PropertyHint.Range, "0,20,0.1")]
    public float LateralDrag { get; set; } = 4f;

    [Export(PropertyHint.Range, "0,2,0.01")]
    public float LateralDragLinear { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SlopePush { get; set; } = 0.25f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float TiltResponse { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0,3,0.05")]
    public float ShortWaveFilter { get; set; } = 1f;

    [Export(PropertyHint.Range, "1,10,0.1")]
    public float RudderAuthority { get; set; } = 3f;

    [Export(PropertyHint.Range, "0,3,0.05")]
    public float WaveDragGain { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0.5,1,0.01")]
    public float WaveWallOnset { get; set; } = 0.85f;

    [Export(PropertyHint.Range, "0,3,0.01")]
    public float RollDamping { get; set; } = 0.6f;

    [Export(PropertyHint.Range, "0,3,0.01")]
    public float PitchDamping { get; set; } = 0.65f;

    [Export(PropertyHint.Range, "0,3,0.01")]
    public float YawDamping { get; set; } = 0.9f;

    [Export(PropertyHint.Range, "0,40,0.5")]
    public float HeelLever { get; set; } = 8f;

    [Export(PropertyHint.Range, "5,45,1")]
    public float MaxHeelDegrees { get; set; } = 18f;

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float KeelLever { get; set; } = 3f;

    [Export(PropertyHint.Range, "0,60,1")]
    public float DriveNoGoDeg { get; set; } = 35f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float NoGoDrive { get; set; } = 0.05f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float CloseHauledDrive { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ReachDrive { get; set; } = 0.8f;

    [Export(PropertyHint.Range, "5,200,1")]
    public float MaxAcceleration { get; set; } = 25f;

    [Export(PropertyHint.Range, "0,4,0.05")]
    public float SurfaceMaskMargin { get; set; } = 1.2f;

    [Export(PropertyHint.Range, "0,2,0.05")]
    public float SurfaceMaskDepth { get; set; } = 0.4f;

    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public float SurfaceMaskFeather { get; set; } = 0.35f;

    [Export] public bool RebuildNow { get => false; set { if (value) Rebuild(); } }

    [Export] public bool Trace { get; set; }

    private float _steerTorque;
    private float _tick;
    private float _clock;
    private float _knock;
    private float _startRoll;
    private float _bowPeak;
    private Vector3 _wasLinear;
    private Vector3 _wasAngular;

    public float SubmergedVolume { get; private set; }

    public Vector3 DeckAcceleration { get; private set; }

    public Vector3 RigForce { get; private set; }

    public Vector3 HullForce { get; private set; }

    public int FoilCount => _rig?.FoilCount ?? 0;

    public SailingRig Rig => _rig;

    public int ProbeCount => _probeLocal.Length;

    public Vector3 BowWorld => GlobalTransform * new Vector3(0f, 0f, _form.ZAt(1f));

    public float BeamWidth => _form.Beam;

    public void GetProbe(int i, out Vector3 world, out float wet, out Vector3 force, out float span)
    {
        world = _probeWorld[i];
        wet = _probeWet[i];
        force = _probeForce[i];
        span = _probeSpan[i];
    }

    [Signal] public delegate void FlippedEventHandler(bool flipped);

    [Export(PropertyHint.Range, "30,180,1")]
    public float FlipAngle { get; set; } = 130f;

    [Export(PropertyHint.Range, "5,90,1")]
    public float RightingAngle { get; set; } = 70f;

    public float Tilt { get; private set; }

    public bool IsFlipped { get; private set; }

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float SwampedRighting { get; set; }

    public void Right()
    {
        if (!IsFlipped) return;

        IsFlipped = false;
        EmitSignal(SignalName.Flipped, false);
        GD.Print($"{Name}: salvaged");
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
        CanSleep = false;
        Sleeping = false;
        LinearDampMode = DampMode.Replace;
        LinearDamp = 0f;
        AngularDampMode = DampMode.Replace;
        AngularDamp = 0f;

        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            string[] pair = arg.TrimPrefix("--").Split('=');
            if (pair.Length != 2 || !float.TryParse(pair[1], out float value)) continue;

            switch (pair[0])
            {
                case "pitchdamp": PitchDamping = value; break;
                case "rolldamp": RollDamping = value; break;
                case "yawdamp": YawDamping = value; break;
                case "heavedamp": HeaveDamping = value; break;
                case "slamdrag": SlamDrag = value; break;
                case "fwddrag": ForwardDrag = value; break;
                case "latdrag": LateralDrag = value; break;
                case "slopepush": SlopePush = value; break;
                case "tiltresponse": TiltResponse = value; break;
                case "wavefilter": ShortWaveFilter = value; break;
                case "heellever": HeelLever = value; break;
                case "maxheel": MaxHeelDegrees = value; break;
                case "keellever": KeelLever = value; break;
                case "nogo": DriveNoGoDeg = value; break;
                case "knockdown": _knock = value; break;
                case "rocker": Rocker = value; break;
                case "flipangle": FlipAngle = value; break;
                case "swamped": SwampedRighting = value; break;
                case "roll": _startRoll = Mathf.DegToRad(value); break;
                case "trace": Trace = value > 0f; break;
            }
        }

        GD.Print($"hull tuning: HeaveDamping={HeaveDamping} SlamDrag={SlamDrag} " +
                 $"ForwardDrag={ForwardDrag} LateralDrag={LateralDrag} SlopePush={SlopePush} " +
                 $"probes={_probeLocal.Length}");

        Rebuild();

        if (!Engine.IsEditorHint())
        {
            _rig = GetNodeOrNull<SailingRig>("SailingRig");
            if (_rig == null)
            {
                _rig = new SailingRig { Name = "SailingRig" };
                AddChild(_rig);
            }
        }

        RefreshRig();

        if (_startRoll != 0f) Rotation = new Vector3(Rotation.X, Rotation.Y, _startRoll);

        if (Trace) ReportCollision();
    }

    private void ReportCollision()
    {
        int slices = 0;
        float lowest = 0f;

        foreach (Node child in GetChildren())
        {
            if (child is not CollisionShape3D shape) continue;
            if (!shape.Name.ToString().StartsWith("HullSlice")) continue;
            if (shape.Shape is not ConvexPolygonShape3D convex) continue;

            slices++;
            foreach (Vector3 point in convex.Points) lowest = Mathf.Min(lowest, point.Y);
        }

        GD.Print($"collision: {slices} generated hull slices, deepest point {lowest:0.00} m " +
                 $"(keel at bow {_form.KeelY(1f):0.00} m, amidships {_form.KeelY(0.5f):0.00} m)");
    }

    public void RefreshRig() => _rig?.Refresh();

    private void Rebuild()
    {
        BuildProbes();

        CenterOfMassMode = CenterOfMassModeEnum.Custom;
        CenterOfMass = new Vector3(0f, CenterOfMassHeight, CenterOfMassLength);
        GravityScale = 0f;
        _designVolume = _form.VolumeBelow(0f, _surfaceOffset, 192, 192);
        Mass = Mathf.Max(1f, WaterDensity * _displacement * _designVolume);

        if (IsInsideTree()) BuildCollision();
    }

    private void BuildProbes()
    {
        int stations = Mathf.Clamp(_probeStations, 3, 9);
        int count = stations * 2 + 2;

        var locals = new List<Vector3>(count);
        var weights = new List<float>(count);

        for (int k = 0; k < stations; k++)
        {
            float t = (k + 0.5f) / stations;
            float keel = _form.KeelY(t);
            float depth = Mathf.Max(-keel, 0.2f);
            float width = _form.Beam * 0.5f * _form.PlanFactor(t);
            float x = Mathf.Max(width * 0.85f, 0.1f);
            float z = _form.ZAt(t);
            float weight = Mathf.Max(_form.PlanFactor(t) * depth, 0.01f);

            float y = keel * (1f - _probeHeightFraction);
            locals.Add(new Vector3(x, y, z));
            weights.Add(weight);
            locals.Add(new Vector3(-x, y, z));
            weights.Add(weight);
        }

        foreach (float t in new[] { 0.02f, 0.98f })
        {
            float keel = _form.KeelY(t);
            locals.Add(new Vector3(0f, keel * (1f - _probeHeightFraction), _form.ZAt(t)));
            weights.Add(Mathf.Max(_form.PlanFactor(t) * Mathf.Max(-keel, 0.2f), 0.01f) * 0.6f);
        }

        float total = 0f;
        foreach (float w in weights) total += w;

        float volume = Mathf.Max(_form.VolumeBelow(0f, _surfaceOffset, 96, 96), 0.1f);

        _probeLocal = locals.ToArray();
        _probeVolume = new float[count];
        _probeSpan = new float[count];
        _probeReserve = new float[count];
        _probeArea = new float[count];
        _probeWorld = new Vector3[count];
        _probeWater = new float[count];
        _probeWet = new float[count];
        _probeForce = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            _probeVolume[i] = volume * weights[i] / total;
        }

        BalanceProbes(volume);

        for (int i = 0; i < count; i++)
        {
            _probeSpan[i] = Mathf.Max(-_probeLocal[i].Y, 0.3f);
            _probeReserve[i] = (_probeSpan[i] + _form.Freeboard * 0.8f) / _probeSpan[i];
            _probeArea[i] = _probeVolume[i] / _probeSpan[i];
        }

        _lateralArea = _form.Length * _form.Draft / count;
        _frontalArea = _form.Beam * _form.Draft / count;
    }

    private void BalanceProbes(float volume)
    {
        float mean = 0f;
        foreach (float v in _probeVolume) mean += v;
        if (mean <= 0f) return;

        float centroid = 0f;
        for (int i = 0; i < _probeVolume.Length; i++)
        {
            centroid += _probeVolume[i] * _probeLocal[i].Z;
        }
        centroid /= mean;

        float spread = 0f;
        for (int i = 0; i < _probeVolume.Length; i++)
        {
            float dz = _probeLocal[i].Z - centroid;
            spread += _probeVolume[i] * dz * dz;
        }
        if (spread <= 0.0001f) return;

        float shift = (CenterOfMassLength - centroid) / (spread / mean);

        float rescale = 0f;
        for (int i = 0; i < _probeVolume.Length; i++)
        {
            float factor = 1f + shift * (_probeLocal[i].Z - centroid);
            _probeVolume[i] *= Mathf.Max(factor, 0.1f);
            rescale += _probeVolume[i];
        }

        for (int i = 0; i < _probeVolume.Length; i++)
        {
            _probeVolume[i] *= volume / rescale;
        }
    }

    private void BuildCollision()
    {
        foreach (Node child in GetChildren())
        {
            if (child is CollisionShape3D existing && existing.Name.ToString().StartsWith("HullSlice"))
            {
                RemoveChild(existing);
                existing.QueueFree();
            }
        }

        int stations = Mathf.Max(3, _solidStations);
        int levels = Mathf.Max(2, _solidRings);
        int slices = Mathf.Clamp(_collisionSlices, 1, stations - 1);
        int span = Mathf.CeilToInt((float)(stations - 1) / slices);
        float solidTop = SolidTop;

        var points = new List<Vector3>();

        for (int s = 0; s < slices; s++)
        {
            int start = s * span;
            int end = Mathf.Min(stations - 1, start + span);
            if (start >= end) break;

            points.Clear();
            for (int j = start; j <= end; j++)
            {
                float t = (float)j / (stations - 1);
                float top = _form.VAtHeight(t, solidTop);
                for (int r = 0; r < levels; r++)
                {
                    Vector3 p = _form.Shell(t, top * r / (levels - 1), _surfaceOffset, 1f);
                    points.Add(p);
                    points.Add(new Vector3(-p.X, p.Y, p.Z));
                }
            }
            AddSlice($"HullSlice{s}", points);

            if (_hollowHull && DeckHeight > solidTop)
            {
                AddSideBand("HullSliceWall", s, start, end, stations, solidTop, DeckHeight, HullThickness);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                points.Clear();
                for (int j = start; j <= end; j++)
                {
                    float t = (float)j / (stations - 1);
                    float low = _form.VAtHeight(t, DeckHeight);
                    for (int r = 0; r < 2; r++)
                    {
                        float v = Mathf.Lerp(low, 1f, r);
                        points.Add(_form.Shell(t, v, _surfaceOffset, side));
                        points.Add(_form.Shell(t, v, _surfaceOffset - BulwarkThickness, side));
                    }
                }
                AddSlice($"HullSliceRail{(side < 0 ? "P" : "S")}{s}", points);
            }
        }

        if (_hollowHull) BuildDecks(stations);

        AddEndWall("HullSliceTransom", 0f);
        AddEndWall("HullSliceStem", 1f);
    }

    private float SolidTop => _hollowHull ? Mathf.Min(HoldFloor, DeckHeight) : DeckHeight;

    private void AddSideBand(string name, int s, int start, int end, int stations,
        float from, float to, float thickness)
    {
        var points = new List<Vector3>();

        for (int side = -1; side <= 1; side += 2)
        {
            points.Clear();
            for (int j = start; j <= end; j++)
            {
                float t = (float)j / (stations - 1);
                for (int r = 0; r < 2; r++)
                {
                    float v = _form.VAtHeight(t, Mathf.Lerp(from, to, r));
                    points.Add(_form.Shell(t, v, _surfaceOffset, side));
                    points.Add(_form.Shell(t, v, _surfaceOffset - thickness, side));
                }
            }
            AddSlice($"{name}{(side < 0 ? "P" : "S")}{s}", points);
        }
    }

    private void BuildDecks(int stations)
    {
        float low = DeckHeight - DeckThickness;
        float open = HatchWidth * 0.5f;
        float front = Mathf.Min(HatchFrom, HatchTo);
        float back = Mathf.Max(HatchFrom, HatchTo);
        var points = new List<Vector3>();

        for (int j = 0; j < stations - 1; j++)
        {
            float t0 = (float)j / (stations - 1);
            float t1 = (float)(j + 1) / (stations - 1);
            float z = (_form.ZAt(t0) + _form.ZAt(t1)) * 0.5f;
            float inner = HatchWidth > 0f && z >= front && z <= back ? open : 0f;

            for (int side = -1; side <= 1; side += 2)
            {
                points.Clear();
                for (int k = 0; k < 4; k++)
                {
                    float t = k < 2 ? t0 : t1;
                    float h = (k & 1) == 0 ? low : DeckHeight;
                    Vector3 edge = _form.Shell(t, _form.VAtHeight(t, h), _surfaceOffset - HullThickness, side);
                    if (Mathf.Abs(edge.X) <= inner + 0.02f) continue;
                    points.Add(edge);
                    points.Add(new Vector3(inner * side, edge.Y, edge.Z));
                }

                if (points.Count >= 8) AddSlice($"HullSliceDeck{(side < 0 ? "P" : "S")}{j}", points);
            }
        }
    }

    private void AddEndWall(string name, float t)
    {
        float low = _form.VAtHeight(t, SolidTop);
        float inward = t < 0.5f ? -BulwarkThickness : BulwarkThickness;
        var points = new List<Vector3>();

        for (int r = 0; r < 2; r++)
        {
            Vector3 edge = _form.Shell(t, Mathf.Lerp(low, 1f, r), _surfaceOffset, 1f);
            points.Add(edge);
            points.Add(new Vector3(-edge.X, edge.Y, edge.Z));
            points.Add(new Vector3(edge.X, edge.Y, edge.Z + inward));
            points.Add(new Vector3(-edge.X, edge.Y, edge.Z + inward));
        }

        AddSlice(name, points);
    }

    private void AddSlice(string name, List<Vector3> points)
    {
        AddChild(new CollisionShape3D
        {
            Name = name,
            Shape = new ConvexPolygonShape3D { Points = points.ToArray() }
        });
    }

    public void GetSurfaceMask(out Vector4 frame, out Vector4 extents)
    {
        Transform3D xform = GlobalTransform;
        Basis level = xform.Basis.Orthonormalized();
        var fwd = new Vector2(-level.Z.X, -level.Z.Z);
        fwd = fwd.LengthSquared() > 0.0001f ? fwd.Normalized() : Vector2.Right;

        frame = new Vector4(xform.Origin.X, xform.Origin.Z, fwd.X, fwd.Y);

        float keelBow = (xform * new Vector3(0f, _form.KeelY(1f), _form.ZAt(1f))).Y;
        float keelMid = (xform * new Vector3(0f, _form.KeelY(0.5f), _form.ZAt(0.5f))).Y;
        float keelStern = (xform * new Vector3(0f, _form.KeelY(0f), _form.ZAt(0f))).Y;
        float sink = Mathf.Min(keelMid, Mathf.Min(keelBow, keelStern)) - SurfaceMaskDepth;

        extents = new Vector4(
            _form.Length * 0.5f + SurfaceMaskMargin,
            _form.Beam * 0.5f + SurfaceMaskMargin,
            sink,
            SurfaceMaskFeather);
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        Transform3D xform = state.Transform;
        Basis level = xform.Basis.Orthonormalized();

        _ocean = OceanField.Instance;
        _com = xform * state.CenterOfMassLocal;
        _linear = state.LinearVelocity;
        _angular = state.AngularVelocity;
        _force = Vector3.Zero;
        _torque = Vector3.Zero;
        _drag = Vector3.Zero;
        SubmergedVolume = 0f;

        if (_ocean != null)
        {
            float cutoff = ShortWaveFilter * _form.Length;
            float mean = 0f;

            for (int i = 0; i < _probeLocal.Length; i++)
            {
                Vector3 p = xform * _probeLocal[i];
                _probeWorld[i] = p;
                _probeWater[i] = _ocean.GetHeight(new Vector2(p.X, p.Z), cutoff);
                mean += _probeWater[i];
            }

            mean /= _probeLocal.Length;

            for (int i = 0; i < _probeLocal.Length; i++)
            {
                _probeWater[i] = mean + (_probeWater[i] - mean) * TiltResponse;
                Probe(i, level, cutoff);
            }
        }

        HullForce = _drag;

        Vector3 rig = Vector3.Zero;
        if (_rig != null)
        {
            _rig.Accumulate(level, _com, _linear, _angular, ref _force, ref _torque,
                out rig, out _steerTorque);
        }

        RigForce = rig;

        var ahead = new Vector3(-level.Z.X, 0f, -level.Z.Z);
        if (ahead.LengthSquared() > 0.0001f) ahead = ahead.Normalized();

        if (WaveDragGain > 0f && ahead.LengthSquared() > 0.0001f)
        {
            float headway = _linear.Dot(ahead);
            float over = headway - WaveWallOnset * 1.25f * Mathf.Sqrt(_form.Length);
            if (over > 0f)
            {
                _force -= ahead * (Mass * WaveDragGain * over * over);
            }
        }

        if (KeelLever > 0f)
        {
            float roll = -Mathf.Asin(Mathf.Clamp(level.X.Y, -1f, 1f));
            _torque += level.Z * (Mass * Gravity * KeelLever * roll);
        }

        _force.Y -= Mass * Gravity;

        Damp(state, level);

        float maxForce = Mass * MaxAcceleration;
        if (_force.LengthSquared() > maxForce * maxForce) _force = _force.Normalized() * maxForce;

        float maxTorque = maxForce * _form.Length * 0.5f;
        if (_torque.LengthSquared() > maxTorque * maxTorque) _torque = _torque.Normalized() * maxTorque;

        if (_knock > 0f)
        {
            _knock -= (float)state.Step;
            _torque += level.Z * (Mass * 40f);
        }

        Capsize(level);

        DeckAcceleration = new Vector3(_force.X, 0f, _force.Z) / Mass;

        state.ApplyCentralForce(_force);
        state.ApplyTorque(_torque);

        if (Trace) Log(state, xform, rig);
    }

    private void Probe(int i, Basis level, float cutoff)
    {
        Vector3 p = _probeWorld[i];
        float wasWet = _probeWet[i];
        _probeWet[i] = 0f;
        _probeForce[i] = Vector3.Zero;

        var flat = new Vector2(p.X, p.Z);
        float depth = _probeWater[i] - p.Y;
        float s = Mathf.Clamp(depth / _probeSpan[i], 0f, _probeReserve[i]);
        if (s <= 0f) return;

        float wet = Mathf.Min(s, 1f);
        _probeWet[i] = wet;
        SubmergedVolume += _probeVolume[i] * wet;

        Vector3 arm = p - _com;
        Vector3 rel = _linear + _angular.Cross(arm) - _ocean.GetFlow(p, cutoff);

        if (wasWet <= 0f && rel.Y < -1.5f)
        {
            Knotical.Vfx.SplashEmitter.Request(
                new Vector3(p.X, depth + p.Y, p.Z), -rel.Y, Mathf.Sqrt(_probeArea[i]));
        }

        Vector3 lift = SlopePush > 0f
            ? Vector3.Up.Lerp(_ocean.GetNormal(flat, cutoff), SlopePush).Normalized()
            : Vector3.Up;

        Vector3 f = lift * (WaterDensity * Gravity * _probeVolume[i] * s);

        float share = Mass / _probeLocal.Length;
        Vector3 ahead = -level.Z;
        Vector3 side = level.X;

        float relY = rel.Y;
        float relF = rel.Dot(ahead);
        float relS = rel.Dot(side);

        Vector3 drag =
            Vector3.Up * (-(HeaveDamping * share) * relY - SlamDrag * WaterDensity * _probeArea[i] * relY * Mathf.Abs(relY))
            + ahead * (-(ForwardDragLinear * share) * relF - ForwardDrag * WaterDensity * _frontalArea * relF * Mathf.Abs(relF))
            + side * (-(LateralDragLinear * share) * relS - LateralDrag * WaterDensity * _lateralArea * relS * Mathf.Abs(relS));

        drag *= wet;

        _drag += drag;
        f += drag;
        _probeForce[i] = f;
        _force += f;
        _torque += arm.Cross(f);
    }

    private void Capsize(Basis basis)
    {
        Tilt = Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(basis.Y.Y, -1f, 1f)));

        if (!IsFlipped && Tilt > FlipAngle)
        {
            IsFlipped = true;
            EmitSignal(SignalName.Flipped, true);
            GD.Print($"{Name}: CAPSIZED at {Tilt:0}° tilt");
        }
        else if (IsFlipped && Tilt < RightingAngle)
        {
            Right();
        }

        if (!IsFlipped || SwampedRighting >= 1f) return;

        Vector3 axis = basis.Z;
        _torque -= axis * (_torque.Dot(axis) * (1f - SwampedRighting));
    }

    private void Log(PhysicsDirectBodyState3D state, Transform3D xform, Vector3 rig)
    {
        float step = (float)state.Step;
        _clock += step;
        _tick += step;

        Vector3 velocity = state.LinearVelocity;
        Vector3 measured = step > 0f ? (velocity - _wasLinear) / step : Vector3.Zero;
        _wasLinear = velocity;

        Vector3 spin = state.AngularVelocity;
        Vector3 alpha = step > 0f ? (spin - _wasAngular) / step : Vector3.Zero;
        _wasAngular = spin;

        Vector3 bowArm = state.Transform.Basis * new Vector3(0f, 0f, -_form.Length * 0.5f);
        Vector3 bowAcc = measured + alpha.Cross(bowArm) + spin.Cross(spin.Cross(bowArm));
        _bowPeak = Mathf.Max(_bowPeak, -bowAcc.Y);

        if (_tick < 0.5f) return;
        _tick = 0f;

        Basis level = xform.Basis.Orthonormalized();
        Vector3 fwd = -level.Z;
        float speed = new Vector2(velocity.X, velocity.Z).Length();
        float lateral = velocity.Dot(level.X);

        float offWind = 0f;
        if (Wind.Instance != null && Wind.Instance.Velocity.LengthSquared() > 0.01f)
        {
            var heading = new Vector2(fwd.X, fwd.Z).Normalized();
            offWind = Mathf.RadToDeg(Mathf.Acos(
                Mathf.Clamp(heading.Dot(-Wind.Instance.Velocity.Normalized()), -1f, 1f)));
        }

        float trim = Mathf.RadToDeg(Mathf.Asin(Mathf.Clamp(fwd.Y, -1f, 1f)));
        Vector3 stem = xform * new Vector3(0f, _form.KeelY(1f), _form.ZAt(1f));
        float clear = _ocean != null ? stem.Y - _ocean.GetHeight(new Vector2(stem.X, stem.Z)) : 0f;

        GD.Print(
            $"{Name} t={_clock,6:0.0} spd={speed,5:0.00} lat={lateral,5:0.00} off={offWind,4:0}° polar={(_rig?.Polar(level) ?? 1f),4:0.00} yawrate={spin.Y,6:0.000} " +
            $"rig={rig.Dot(fwd) / 1e6f,6:0.00} drag={_drag.Dot(fwd) / 1e6f,6:0.00} " +
            $"applied={_force.Dot(fwd) / 1e6f,6:0.00} MN | " +
            $"a_want={_force.Dot(fwd) / Mass,6:0.000} a_real={measured.Dot(fwd),6:0.000} m/s2 | " +
            $"mass={Mass / 1e6f,5:0.00}M vol={SubmergedVolume,6:0} " +
            $"tilt={Tilt,5:0}° roll={Mathf.RadToDeg(-Mathf.Asin(Mathf.Clamp(level.X.Y, -1f, 1f))),6:0.0}° " +
            $"bowg={_bowPeak / 9.81f,5:0.00} vY={velocity.Y,5:0.00} " +
            $"trim={trim,5:0.0}° bow={clear,5:0.0}m{(clear < 0f ? " UNDER" : "")} " +
            $"steer={_steerTorque / 1e6f,6:0.00} totYaw={_torque.Dot(level.Y) / 1e6f,6:0.00}MNm");
    }

    private void Damp(PhysicsDirectBodyState3D state, Basis basis)
    {
        float step = (float)state.Step;
        if (step <= 0f || _designVolume <= 0f) return;

        float wet = Mathf.Clamp(SubmergedVolume / _designVolume, 0f, 1f);
        if (wet <= 0f) return;

        Basis inverseInertia = state.InverseInertiaTensor;
        if (inverseInertia.Determinant() == 0f) return;

        Vector3 local = basis.Inverse() * _angular;
        var shed = new Vector3(
            local.X * Mathf.Min(PitchDamping * wet * step, 1f),
            local.Y * Mathf.Min(YawDamping * wet * step, 1f),
            local.Z * Mathf.Min(RollDamping * wet * step, 1f));

        _torque -= inverseInertia.Inverse() * (basis * shed) / step;
    }
}
