using System.Collections.Generic;
using UnityEngine;

namespace Knotical
{
    public static class MapPainter
    {
        public static readonly Color Ink = new Color(0.16f, 0.13f, 0.10f);
        public static readonly Color Foam = new Color(0.97f, 0.99f, 1f);
        public static readonly Color DefaultShallow = new Color(0.592f, 0.875f, 0.941f);
        public static readonly Color DefaultDeep = new Color(0.07f, 0.29f, 0.62f);
        public static readonly Color DefaultCliff = new Color(0.55f, 0.5f, 0.45f);

        private static readonly Vector3 LightDir = new Vector3(-0.45f, 0.8f, 0.4f).normalized;
        private static readonly float[] DepthBands = { 0.5f, 1f, 2f };

        public static Texture2D Paint(Heightmap map, int pixelsPerCell, LevelSettings settings, Transform blocks = null)
        {
            int n = map.Size * pixelsPerCell;
            float step = map.CellSize / pixelsPerCell;
            Color shallow = Ocean.Settings != null ? Ocean.Settings.ShallowColor : DefaultShallow;
            Color deep = Ocean.Settings != null ? Ocean.Settings.DeepColor : DefaultDeep;
            float shoal = settings != null ? settings.ShoalDepth : 15f;

            var heights = new float[n * n];
            for (int py = 0; py < n; py++)
            {
                for (int px = 0; px < n; px++)
                {
                    heights[py * n + px] = map.Sample(Pixel(map, px, py, step));
                }
            }

            var pixels = new Color[n * n];
            for (int py = 0; py < n; py++)
            {
                for (int px = 0; px < n; px++)
                {
                    float h = heights[py * n + px];
                    Color c;
                    if (h > 0f)
                    {
                        Facet(map, Pixel(map, px, py, step), out Vector3 normal, out float facetHeight);
                        c = TerrainMeshBuilder.ColourFor(facetHeight, settings) * Shade(normal);
                    }
                    else
                    {
                        float depth = -h / shoal;
                        int band = 0;
                        foreach (float edge in DepthBands) if (depth > edge) band++;
                        c = Color.Lerp(shallow, deep, band / (float)DepthBands.Length);
                    }

                    if (Crosses(heights, n, px, py, 0f)) c = Color.Lerp(c, Foam, 0.7f);
                    c.a = 1f;
                    pixels[py * n + px] = c;
                }
            }

            if (blocks != null) PaintBlocks(pixels, n, map, step, blocks);

            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                name = "LevelMap",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
            };
            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void PaintBlocks(Color[] pixels, int n, Heightmap map, float step, Transform blocks)
        {
            var sorted = new List<Transform>(blocks.childCount);
            foreach (Transform block in blocks) sorted.Add(block);
            sorted.Sort((a, b) => (a.position.y + a.lossyScale.y * 0.5f).CompareTo(b.position.y + b.lossyScale.y * 0.5f));

            int index = 0;
            foreach (Transform block in sorted)
            {
                Color top = DefaultCliff;
                Color side = DefaultCliff;
                var renderer = block.GetComponent<MeshRenderer>();
                Material material = renderer != null ? renderer.sharedMaterial : null;
                if (material != null && material.HasProperty("_GrassColor")) top = material.GetColor("_GrassColor");
                if (material != null && material.HasProperty("_RockColor")) side = material.GetColor("_RockColor");
                else if (material != null && material.HasProperty("_Tint")) top = side = material.GetColor("_Tint");
                float vary = 0.9f + 0.2f * Hash(index++);
                Color colour = top * (Shade(block.up) * vary);
                Color rim = side * (Shade(block.up) * vary * 0.8f);
                colour.a = 1f;
                rim.a = 1f;

                Vector3 scale = block.lossyScale;
                float edgeX = Mathf.Min(0.45f, step * 0.9f / Mathf.Max(scale.x, 0.01f));
                float edgeZ = Mathf.Min(0.45f, step * 0.9f / Mathf.Max(scale.z, 0.01f));
                float radius = 0.5f * Mathf.Sqrt(scale.x * scale.x + scale.z * scale.z);
                Vector3 centre = block.position;
                Matrix4x4 toLocal = block.worldToLocalMatrix;

                int minX = Mathf.Max(0, Mathf.FloorToInt((centre.x - radius + map.Extent) / step));
                int maxX = Mathf.Min(n - 1, Mathf.CeilToInt((centre.x + radius + map.Extent) / step));
                int minY = Mathf.Max(0, Mathf.FloorToInt((centre.z - radius + map.Extent) / step));
                int maxY = Mathf.Min(n - 1, Mathf.CeilToInt((centre.z + radius + map.Extent) / step));

                for (int py = minY; py <= maxY; py++)
                {
                    for (int px = minX; px <= maxX; px++)
                    {
                        Vector2 p = Pixel(map, px, py, step);
                        Vector3 local = toLocal.MultiplyPoint3x4(new Vector3(p.x, centre.y, p.y));
                        if (Mathf.Abs(local.x) > 0.5f || Mathf.Abs(local.z) > 0.5f) continue;
                        bool edge = Mathf.Abs(local.x) > 0.5f - edgeX || Mathf.Abs(local.z) > 0.5f - edgeZ;
                        pixels[py * n + px] = edge ? rim : colour;
                    }
                }
            }
        }

        private static float Shade(Vector3 normal) => 0.55f + 0.45f * Mathf.Clamp01(Vector3.Dot(normal, LightDir));

        private static float Hash(int i)
        {
            uint h = (uint)i * 2654435761u;
            h ^= h >> 13;
            h *= 0x5bd1e995u;
            h ^= h >> 15;
            return (h & 0xffffu) / 65535f;
        }

        private static Vector2 Pixel(Heightmap map, int px, int py, float step) =>
            new Vector2(-map.Extent + (px + 0.5f) * step, -map.Extent + (py + 0.5f) * step);

        private static void Facet(Heightmap map, Vector2 p, out Vector3 normal, out float height)
        {
            float fx = (p.x + map.Extent) / map.CellSize - 0.5f;
            float fz = (p.y + map.Extent) / map.CellSize - 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(fx), 0, map.Size - 2);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(fz), 0, map.Size - 2);
            float tx = Mathf.Clamp01(fx - x0);
            float tz = Mathf.Clamp01(fz - z0);

            Vector3 a = Corner(map, x0, z0);
            Vector3 b = Corner(map, x0 + 1, z0);
            Vector3 c = Corner(map, x0, z0 + 1);
            Vector3 d = Corner(map, x0 + 1, z0 + 1);

            if (tx + tz < 1f)
            {
                normal = Vector3.Cross(c - a, b - a).normalized;
                height = (a.y + b.y + c.y) / 3f;
            }
            else
            {
                normal = Vector3.Cross(c - b, d - b).normalized;
                height = (b.y + c.y + d.y) / 3f;
            }
        }

        private static Vector3 Corner(Heightmap map, int x, int z)
        {
            Vector2 p = map.CellCentre(x, z);
            return new Vector3(p.x, map[x, z], p.y);
        }

        private static bool Crosses(float[] heights, int n, int px, int py, float level)
        {
            float h = heights[py * n + px];
            bool above = h > level;
            if (px + 1 < n && (heights[py * n + px + 1] > level) != above) return true;
            if (py + 1 < n && (heights[(py + 1) * n + px] > level) != above) return true;
            return false;
        }
    }
}
