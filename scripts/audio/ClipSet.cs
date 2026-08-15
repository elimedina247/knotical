using Godot;

namespace Knotical.Audio;

[GlobalClass]
public partial class ClipSet : Resource
{
    [Export] public AudioStream[] Clips { get; set; } = System.Array.Empty<AudioStream>();

    [Export(PropertyHint.Range, "-40,12,0.5")]
    public float VolumeDb { get; set; }

    [Export(PropertyHint.Range, "0,12,0.5")]
    public float VolumeJitterDb { get; set; } = 2.5f;

    [Export(PropertyHint.Range, "0,0.4,0.005")]
    public float PitchJitter { get; set; } = 0.1f;

    private int _last = -1;

    public bool IsEmpty => Clips == null || Clips.Length == 0;

    public static ClipSet Filled(params ClipSet[] options)
    {
        foreach (ClipSet option in options)
        {
            if (option != null && !option.IsEmpty) return option;
        }

        return null;
    }

    public AudioStream Pick(RandomNumberGenerator rng)
    {
        if (IsEmpty) return null;
        if (Clips.Length == 1) return Clips[0];

        if (_last < 0 || _last >= Clips.Length)
        {
            _last = rng.RandiRange(0, Clips.Length - 1);
            return Clips[_last];
        }

        int index = rng.RandiRange(0, Clips.Length - 2);
        if (index >= _last) index++;

        _last = index;
        return Clips[index];
    }

    public float Gain(RandomNumberGenerator rng) => VolumeDb - rng.Randf() * VolumeJitterDb;

    public float Pitch(RandomNumberGenerator rng) => 1f + rng.RandfRange(-PitchJitter, PitchJitter);
}
