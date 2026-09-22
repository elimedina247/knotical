using UnityEngine;

namespace Knotical
{
    public sealed class LevelNoise
    {
        private const int Slots = 8;

        private readonly Vector2[] offsets = new Vector2[Slots];

        public LevelNoise(int seed)
        {
            var rng = new System.Random(seed);
            for (int i = 0; i < Slots; i++)
            {
                offsets[i] = new Vector2((float)rng.NextDouble() * 2048f, (float)rng.NextDouble() * 2048f);
            }
        }

        public float Perlin(Vector2 p, int slot)
        {
            Vector2 o = offsets[slot % Slots];
            return Mathf.Clamp01(Mathf.PerlinNoise(p.x + o.x, p.y + o.y));
        }

        public float Fbm(Vector2 p, int octaves, float lacunarity, float gain, int slot)
        {
            float sum = 0f;
            float norm = 0f;
            float amplitude = 1f;
            float frequency = 1f;

            for (int o = 0; o < octaves; o++)
            {
                sum += amplitude * Perlin(p * frequency + new Vector2(o * 17.3f, o * 31.7f), slot);
                norm += amplitude;
                amplitude *= gain;
                frequency *= lacunarity;
            }

            return sum / norm;
        }

        public float Ridged(Vector2 p, int octaves, int slot)
        {
            float sum = 0f;
            float norm = 0f;
            float amplitude = 1f;
            float frequency = 1f;

            for (int o = 0; o < octaves; o++)
            {
                float n = 1f - Mathf.Abs(2f * Perlin(p * frequency + new Vector2(o * 11.1f, o * 23.9f), slot) - 1f);
                sum += amplitude * n * n;
                norm += amplitude;
                amplitude *= 0.5f;
                frequency *= 2f;
            }

            return sum / norm;
        }

        public Vector2 Warp(Vector2 p, float strength, int octaves, int slot)
        {
            if (strength <= 0f) return p;
            float dx = Fbm(p, octaves, 2f, 0.5f, slot) - 0.5f;
            float dz = Fbm(p, octaves, 2f, 0.5f, slot + 1) - 0.5f;
            return p + new Vector2(dx, dz) * (strength * 2f);
        }
    }
}
