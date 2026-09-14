using System.Collections.Generic;
using UnityEngine;

namespace Knotical
{
    [CreateAssetMenu(menuName = "Knotical/Ocean Settings", fileName = "OceanSettings")]
    public class OceanSettings : ScriptableObject
    {
        public const int MaxWaves = 24;

        [Range(0, 9999)] public int Seed = 7;

        [Header("Waves")]
        [Range(1, MaxWaves)] public int Count = 10;
        [Range(0f, 3f)] public float Speed = 1f;
        [Range(0f, 360f)] public float DirectionDeg = 20f;
        [Range(0f, 180f)] public float SpreadDeg = 45f;
        [Range(0f, 1f)] public float Distribution = 0.6f;
        [Range(1f, 900f)] public float MinWavelength = 8f;
        [Range(1f, 900f)] public float MaxWavelength = 140f;
        [Range(0f, 8f)] public float MinAmplitude = 0.06f;
        [Range(0f, 8f)] public float MaxAmplitude = 1.1f;
        [Range(0f, 1f)] public float MinSteepness = 0.85f;
        [Range(0f, 1f)] public float MaxSteepness = 0.4f;
        [Range(0f, 1f)] public float ShallowResponse = 0.6f;

        [Header("Colour")]
        [Range(0.5f, 120f)] public float ShallowDepth = 12f;
        public Color ShallowColor = new Color(0.592f, 0.875f, 0.941f);
        public Color DeepColor = new Color(0.07f, 0.29f, 0.62f);
        public Color CrestColor = new Color(0.957f, 0.992f, 1f);
        public Color SssColor = new Color(0.25f, 0.85f, 0.75f);

        public WaveSet Build()
        {
            int count = Mathf.Clamp(Count, 1, MaxWaves);
            var waves = new List<Wave>(count);
            var rng = new System.Random(Seed);

            float centre = DirectionDeg * Mathf.Deg2Rad;
            float halfSpread = SpreadDeg * Mathf.Deg2Rad * 0.5f;
            float shape = Mathf.Lerp(1f, 4f, Distribution);

            for (int j = 0; j < count; j++)
            {
                float t = Mathf.Pow((j + Next(rng)) / count, shape);

                float wavelength = Mathf.Lerp(MinWavelength, MaxWavelength, t);
                float amplitude = Mathf.Lerp(MinAmplitude, MaxAmplitude, t);
                float steepness = Mathf.Lerp(MinSteepness, MaxSteepness, t);
                float angle = centre + Range(rng, -halfSpread, halfSpread);
                float phase = Range(rng, 0f, 2f * Mathf.PI);

                waves.Add(new Wave(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                    amplitude, wavelength, steepness, phase, ShallowResponse, Speed));
            }

            return new WaveSet(waves.ToArray());
        }

        private void OnValidate()
        {
            if (Ocean.Settings == this) Ocean.Rebuild();
        }

        private static float Next(System.Random rng) => (float)rng.NextDouble();

        private static float Range(System.Random rng, float min, float max) => min + (max - min) * Next(rng);
    }
}
