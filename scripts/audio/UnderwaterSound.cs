using Godot;
using OceanField = Knotical.Ocean.Ocean;

namespace Knotical.Audio;

[GlobalClass]
public partial class UnderwaterSound : Node3D
{
    [Export] public Node3D Head { get; set; }

    [Export] public AudioStreamPlayer Bed { get; set; }

    [Export] public SoundEmitter Emitter { get; set; }

    [Export] public ClipSet Submerges { get; set; }

    [Export] public ClipSet Emerges { get; set; }

    [Export(PropertyHint.Range, "-60,6,0.5")]
    public float BedPeakDb { get; set; } = -6f;

    [Export(PropertyHint.Range, "-80,-20,1")]
    public float BedFloorDb { get; set; } = -60f;

    [Export(PropertyHint.Range, "0.05,5,0.01")]
    public float FadeTime { get; set; } = 0.35f;

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Hysteresis { get; set; } = 0.06f;

    [Export] public bool Muffle { get; set; } = true;

    [Export] public string[] MuffledBuses { get; set; } = { "Ambience", "Sfx" };

    [Export(PropertyHint.Range, "100,4000,10")]
    public float MuffleHz { get; set; } = 480f;

    [Export(PropertyHint.Range, "5000,20500,100")]
    public float ClearHz { get; set; } = 20500f;

    [Export(PropertyHint.Range, "0.02,2,0.01")]
    public float MuffleTime { get; set; } = 0.18f;

    private readonly System.Collections.Generic.List<AudioEffectLowPassFilter> _filters = new();
    private Node3D _head;
    private AudioStreamPlayer _bed;
    private SoundEmitter _emitter;
    private float _weight;
    private float _muffle;
    private bool _under;
    private bool _primed;

    public bool IsUnder => _under;

    public override void _Ready()
    {
        _head = Head ?? GetParentOrNull<Node3D>();
        _bed = Bed ?? GetNodeOrNull<AudioStreamPlayer>("Bed");
        _emitter = Emitter ?? GetNodeOrNull<SoundEmitter>("SoundEmitter");

        if (Muffle)
        {
            foreach (string bus in MuffledBuses)
            {
                AudioEffectLowPassFilter filter = Mount(bus);
                if (filter != null) _filters.Add(filter);
            }
        }

        if (_bed == null) return;

        if (AudioServer.GetBusIndex(_bed.Bus) < 0) _bed.Bus = "Master";
        _bed.VolumeDb = BedFloorDb;

        if (_bed.Stream != null) _bed.Play();
    }

    public override void _ExitTree()
    {
        foreach (AudioEffectLowPassFilter filter in _filters) filter.CutoffHz = ClearHz;
    }

    private AudioEffectLowPassFilter Mount(string name)
    {
        int bus = AudioServer.GetBusIndex(name);
        if (bus < 0) return null;

        for (int i = 0; i < AudioServer.GetBusEffectCount(bus); i++)
        {
            if (AudioServer.GetBusEffect(bus, i) is AudioEffectLowPassFilter found) return found;
        }

        var filter = new AudioEffectLowPassFilter { CutoffHz = ClearHz };
        AudioServer.AddBusEffect(bus, filter);

        return filter;
    }

    public override void _Process(double delta)
    {
        OceanField ocean = OceanField.Instance;
        if (ocean == null || _head == null || !IsInstanceValid(_head)) return;

        Vector3 at = _head.GlobalPosition;
        float depth = ocean.GetHeight(at) - at.Y;
        bool under = depth > (_under ? -Hysteresis : Hysteresis);

        if (under != _under || !_primed)
        {
            if (_primed) _emitter?.Emit(under ? Submerges : Emerges);
            _under = under;
            _primed = true;
        }

        float dt = (float)delta;
        float target = _under ? 1f : 0f;

        _weight = Mathf.Lerp(_weight, target, 1f - Mathf.Exp(-dt / Mathf.Max(FadeTime, 1e-3f)));
        _muffle = Mathf.Lerp(_muffle, target, 1f - Mathf.Exp(-dt / Mathf.Max(MuffleTime, 1e-3f)));

        if (_bed != null)
        {
            _bed.VolumeDb = Mathf.Max(
                BedPeakDb + Mathf.LinearToDb(Mathf.Max(_weight, 1e-4f)), BedFloorDb);
        }

        if (_filters.Count == 0) return;

        float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(ClearHz), Mathf.Log(MuffleHz), _muffle));
        foreach (AudioEffectLowPassFilter filter in _filters) filter.CutoffHz = cutoff;
    }
}
