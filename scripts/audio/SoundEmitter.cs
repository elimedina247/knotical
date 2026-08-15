using Godot;

namespace Knotical.Audio;

[GlobalClass]
public partial class SoundEmitter : AudioStreamPlayer3D
{
    [Export] public string DefaultBus { get; set; } = "Sfx";

    private readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();

        if (MaxPolyphony < 4) MaxPolyphony = 4;
        if (Bus == "Master" && AudioServer.GetBusIndex(DefaultBus) >= 0) Bus = DefaultBus;
        if (AudioServer.GetBusIndex(Bus) < 0) Bus = "Master";
    }

    public void Emit(ClipSet set, float gainDb = 0f, float pitch = 1f)
    {
        if (set == null || set.IsEmpty) return;

        Stream = set.Pick(_rng);
        VolumeDb = set.Gain(_rng) + gainDb;
        PitchScale = Mathf.Clamp(set.Pitch(_rng) * pitch, 0.1f, 4f);
        Play();
    }
}
