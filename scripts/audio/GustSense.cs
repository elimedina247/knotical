using Godot;

namespace Knotical.Audio;

public sealed class GustSense
{
    public float MeanTime { get; set; } = 9f;

    public float Excess { get; set; } = 0.5f;

    private float _mean;
    private float _over;
    private bool _primed;

    public float Mean => _mean;

    public bool Rising(float speed, float dt)
    {
        if (!_primed)
        {
            _mean = speed;
            _primed = true;
        }

        _mean = Mathf.Lerp(_mean, speed, 1f - Mathf.Exp(-dt / Mathf.Max(MeanTime, 1e-3f)));

        float over = speed - _mean;
        bool rising = over > Excess && _over <= Excess;
        _over = over;

        return rising;
    }
}
