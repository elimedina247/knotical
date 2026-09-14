using UnityEngine;

namespace Knotical
{
    public readonly struct Wave
    {
        public const float Gravity = 9.81f;

        public readonly Vector2 Direction;
        public readonly float Amplitude;
        public readonly float Wavelength;
        public readonly float Steepness;
        public readonly float Phase;
        public readonly float DepthResponse;
        public readonly float WaveNumber;
        public readonly float AngularFrequency;

        public Wave(Vector2 direction, float amplitude, float wavelength, float steepness, float phase, float depthResponse, float speed = 1f)
        {
            Direction = direction;
            Amplitude = amplitude;
            Wavelength = wavelength;
            Steepness = steepness;
            Phase = phase;
            DepthResponse = depthResponse;
            WaveNumber = 2f * Mathf.PI / wavelength;
            AngularFrequency = speed * Mathf.Sqrt(Gravity * WaveNumber);
        }
    }

    public sealed class WaveSet
    {
        public static readonly WaveSet Empty = new WaveSet(new Wave[0]);

        public readonly Wave[] Waves;
        public readonly float SignificantHeight;
        public readonly float PeakWavelength;

        public int Count => Waves.Length;

        public WaveSet(Wave[] waves)
        {
            Waves = waves;

            float squareSum = 0f;
            float weighted = 0f;

            foreach (Wave w in waves)
            {
                float a2 = w.Amplitude * w.Amplitude;
                squareSum += a2;
                weighted += a2 * w.Wavelength;
            }

            SignificantHeight = 4f * Mathf.Sqrt(squareSum * 0.5f);
            PeakWavelength = squareSum > 0f ? weighted / squareSum : 0f;
        }
    }
}
