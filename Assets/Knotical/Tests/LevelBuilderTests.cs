using NUnit.Framework;
using UnityEngine;

namespace Knotical.Tests
{
    public class LevelBuilderTests
    {
        private static LevelSettings Settings(int seed = 1)
        {
            var s = ScriptableObject.CreateInstance<LevelSettings>();
            s.Seed = seed;
            s.Resolution = 128;
            return s;
        }

        [Test]
        public void SameSeedGivesSameHeights()
        {
            float[] a = LevelBuilder.Build(Settings(3)).Heightmap.Heights;
            float[] b = LevelBuilder.Build(Settings(3)).Heightmap.Heights;
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void DifferentSeedsDiffer()
        {
            float[] a = LevelBuilder.Build(Settings(3)).Heightmap.Heights;
            float[] b = LevelBuilder.Build(Settings(4)).Heightmap.Heights;
            CollectionAssert.AreNotEqual(a, b);
        }

        [Test]
        public void HasBothLandAndSea()
        {
            LevelData data = LevelBuilder.Build(Settings());
            float fraction = data.LandCells / (float)data.Land.Length;
            Debug.Log($"land fraction {fraction:F3}");
            Assert.Greater(fraction, 0.03f);
            Assert.Less(fraction, 0.5f);
        }

        [Test]
        public void SpawnAreaAndBorderAreSea()
        {
            LevelSettings s = Settings();
            LevelData data = LevelBuilder.Build(s);
            Heightmap map = data.Heightmap;

            for (int z = 0; z < map.Size; z++)
            {
                for (int x = 0; x < map.Size; x++)
                {
                    Vector2 p = map.CellCentre(x, z);
                    float border = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)) / s.Extent;
                    if (p.magnitude < s.SpawnClearRadius * 0.5f) Assert.Less(map[x, z], 0f, $"spawn cell {p}");
                    if (border > 0.97f) Assert.Less(map[x, z], 0f, $"border cell {p}");
                }
            }
        }

        [Test]
        public void SeabedDeepensAwayFromCoast()
        {
            LevelData data = LevelBuilder.Build(Settings());
            float[] bins = { 25f, 80f, 200f };
            var sum = new float[bins.Length];
            var count = new int[bins.Length];

            for (int i = 0; i < data.Land.Length; i++)
            {
                if (data.Land[i]) continue;
                float d = data.CoastDistance[i];
                for (int b = 0; b < bins.Length; b++)
                {
                    if (d < bins[b])
                    {
                        sum[b] += -data.Heightmap.Heights[i];
                        count[b]++;
                        break;
                    }
                }
            }

            float near = sum[0] / Mathf.Max(count[0], 1);
            float mid = sum[1] / Mathf.Max(count[1], 1);
            float far = sum[2] / Mathf.Max(count[2], 1);
            Debug.Log($"mean depth near {near:F1} mid {mid:F1} far {far:F1}");
            Assert.Greater(mid, near);
            Assert.Greater(far, mid);
        }

        [Test]
        public void DepthFieldFollowsHeights()
        {
            LevelSettings s = Settings();
            Heightmap map = LevelBuilder.Build(s).Heightmap;
            DepthMap field = map.ToDepthMap(s.ShoalDepth);

            for (int z = 0; z < map.Size; z += 7)
            {
                for (int x = 0; x < map.Size; x += 5)
                {
                    Vector2 p = map.CellCentre(x, z);
                    Assert.AreEqual(map[x, z], map.Sample(p), 1e-4f);
                    Assert.AreEqual(Mathf.Clamp01(-map[x, z] / s.ShoalDepth), field.Sample(p), 1e-5f);
                }
            }
        }

        [Test]
        public void LabelsSeparateIslands()
        {
            const int n = 6;
            bool[] land =
            {
                true, true, false, false, false, true,
                true, false, false, false, false, true,
                false, false, false, true, false, false,
                false, false, false, true, true, false,
                false, false, false, false, false, false,
                true, false, false, false, false, false,
            };
            int[] id = LevelBuilder.LabelIslands(land, n, out int count);
            Assert.AreEqual(4, count);
            Assert.AreEqual(id[0], id[6]);
            Assert.AreEqual(id[5], id[11]);
            Assert.AreEqual(id[15], id[22]);
            Assert.AreNotEqual(id[0], id[5]);
            Assert.AreEqual(-1, id[2]);
        }

        [Test]
        public void IslandsVaryInHeightWhenAsked()
        {
            LevelSettings s = Settings();
            s.IslandHeightVariation = 0.6f;
            LevelData data = LevelBuilder.Build(s);
            var peak = new float[data.IslandCount];
            for (int i = 0; i < data.Land.Length; i++)
            {
                if (data.Land[i]) peak[data.IslandId[i]] = Mathf.Max(peak[data.IslandId[i]], data.Heightmap.Heights[i]);
            }
            float min = float.MaxValue, max = 0f;
            foreach (float p in peak) { min = Mathf.Min(min, p); max = Mathf.Max(max, p); }
            Debug.Log($"{data.IslandCount} islands, peaks {min:F1} .. {max:F1}");
            Assert.Greater(data.IslandCount, 1);
            Assert.Greater(max, min * 1.5f);
        }

        [Test]
        public void LandClimbsInlandFromTheShore()
        {
            LevelSettings s = Settings();
            s.ShoreRise = 60f;
            LevelData data = LevelBuilder.Build(s);
            float shoreSum = 0f, inlandSum = 0f;
            int shoreCount = 0, inlandCount = 0;
            for (int i = 0; i < data.Land.Length; i++)
            {
                if (!data.Land[i]) continue;
                float d = data.InlandDistance[i];
                if (d < 15f) { shoreSum += data.Heightmap.Heights[i]; shoreCount++; }
                else if (d > 60f) { inlandSum += data.Heightmap.Heights[i]; inlandCount++; }
            }
            Assert.Greater(shoreCount, 0);
            Assert.Greater(inlandCount, 0);
            float shore = shoreSum / shoreCount;
            float inland = inlandSum / inlandCount;
            Debug.Log($"mean land height shore {shore:F1} inland {inland:F1}");
            Assert.Less(shore, 3f);
            Assert.Greater(inland, shore * 3f);
        }

        [Test]
        public void DistanceFieldMatchesBruteForce()
        {
            const int n = 24;
            var inside = new bool[n * n];
            var rng = new System.Random(5);
            for (int i = 0; i < inside.Length; i++) inside[i] = rng.NextDouble() < 0.08;

            float[] fast = DistanceField.SquaredToNearest(inside, n, n);

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    float best = float.MaxValue;
                    for (int j = 0; j < inside.Length; j++)
                    {
                        if (!inside[j]) continue;
                        int dx = x - j % n;
                        int dz = z - j / n;
                        best = Mathf.Min(best, dx * dx + dz * dz);
                    }
                    Assert.AreEqual(best, fast[z * n + x], 1e-3f, $"cell {x},{z}");
                }
            }
        }
    }
}
