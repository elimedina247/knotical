using UnityEngine;

namespace Knotical
{
    public sealed class LevelData
    {
        public Heightmap Heightmap;
        public bool[] Land;
        public float[] CoastDistance;
        public int[] IslandId;
        public int IslandCount;
        public float[] InlandDistance;

        public int LandCells
        {
            get
            {
                int count = 0;
                foreach (bool l in Land) if (l) count++;
                return count;
            }
        }
    }

    public static class LevelBuilder
    {
        private const int IslandSlot = 0;
        private const int WarpSlot = 1;
        private const int DepthSlot = 3;
        private const int CanyonSlot = 4;
        private const int ReliefSlot = 5;

        public static LevelData Build(LevelSettings s)
        {
            int n = s.Resolution;
            float extent = s.Extent;
            float cell = 2f * extent / n;
            var noise = new LevelNoise(s.Seed);
            var heights = new float[n * n];
            var land = new bool[n * n];
            var landValue = new float[n * n];
            var map = new Heightmap(n, extent, heights);

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    Vector2 p = map.CellCentre(x, z);
                    Vector2 q = noise.Warp(p / s.IslandScale, s.WarpStrength, 3, WarpSlot);
                    float v = noise.Fbm(q, s.Octaves, s.Lacunarity, s.Gain, IslandSlot);

                    float border = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) / extent;
                    v -= Ocean.SmoothStep01(s.BorderStart, 1f, border);

                    if (s.SpawnClearRadius > 0f)
                    {
                        v -= 1f - Ocean.SmoothStep01(s.SpawnClearRadius * 0.5f, s.SpawnClearRadius, p.magnitude);
                    }

                    float lv = v - s.LandThreshold;
                    landValue[i] = lv;
                    land[i] = lv > 0f;
                }
            }

            int[] islandId = LabelIslands(land, n, out int islandCount);
            var sea = new bool[n * n];
            for (int i = 0; i < sea.Length; i++) sea[i] = !land[i];
            float[] inlandSquared = DistanceField.SquaredToNearest(sea, n, n);
            var inland = new float[n * n];
            for (int i = 0; i < inland.Length; i++) inland[i] = Mathf.Min(Mathf.Sqrt(inlandSquared[i]) * cell, extent * 4f);
            float[] islandScale = IslandScales(islandCount, s);
            float[] squared = DistanceField.SquaredToNearest(land, n, n);
            var coast = new float[n * n];

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    float dist = Mathf.Min(Mathf.Sqrt(squared[i]) * cell, extent * 4f);
                    coast[i] = dist;

                    Vector2 p = map.CellCentre(x, z);

                    if (land[i])
                    {
                        float interior = Mathf.Pow(Mathf.Clamp01(landValue[i] / s.LandRange), 0.7f);
                        float relief = noise.Fbm(p / s.ReliefScale, 3, 2f, 0.5f, ReliefSlot) * s.ReliefHeight;
                        float shore = Ocean.SmoothStep01(0f, Mathf.Max(s.ShoreRise, cell), inland[i]);
                        heights[i] = interior * shore * (s.LandHeight * islandScale[islandId[i]] + relief);
                        continue;
                    }
                    float ramp = Mathf.Min(s.ShelfDepth + dist * s.RampSlope, s.MaxDepth);
                    float wobble = noise.Fbm(p / s.IslandScale * 2.7f, 3, 2f, 0.5f, DepthSlot) * 2f - 1f;
                    ramp *= 1f + s.DepthNoise * wobble;

                    if (s.CanyonDepth > 0f)
                    {
                        float ridge = noise.Ridged(p / s.CanyonScale, 3, CanyonSlot);
                        float offshore = Ocean.SmoothStep01(cell, cell * 4f, dist);
                        ramp += s.CanyonDepth * ridge * ridge * ridge * offshore;
                    }

                    heights[i] = -ramp;
                }
            }

            return new LevelData { Heightmap = map, Land = land, CoastDistance = coast, IslandId = islandId, IslandCount = islandCount, InlandDistance = inland };
        }

        private static float[] IslandScales(int count, LevelSettings s)
        {
            var scales = new float[Mathf.Max(count, 1)];
            var rng = new System.Random(s.Seed * 6151 + 29);
            for (int i = 0; i < scales.Length; i++)
            {
                scales[i] = 1f + ((float)rng.NextDouble() * 2f - 1f) * s.IslandHeightVariation;
            }
            return scales;
        }

        public static int[] LabelIslands(bool[] land, int n, out int count)
        {
            var id = new int[land.Length];
            for (int i = 0; i < id.Length; i++) id[i] = -1;
            var stack = new System.Collections.Generic.Stack<int>();
            count = 0;

            for (int start = 0; start < land.Length; start++)
            {
                if (!land[start] || id[start] >= 0) continue;
                int current = count++;
                id[start] = current;
                stack.Push(start);

                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    int x = i % n;
                    int z = i / n;
                    Visit(x - 1, z);
                    Visit(x + 1, z);
                    Visit(x, z - 1);
                    Visit(x, z + 1);
                }

                void Visit(int x, int z)
                {
                    if (x < 0 || z < 0 || x >= n || z >= n) return;
                    int j = z * n + x;
                    if (!land[j] || id[j] >= 0) return;
                    id[j] = current;
                    stack.Push(j);
                }
            }

            return id;
        }
    }
}
