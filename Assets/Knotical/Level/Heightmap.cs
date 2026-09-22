using UnityEngine;

namespace Knotical
{
    public sealed class Heightmap
    {
        public readonly int Size;
        public readonly float Extent;
        public readonly float[] Heights;

        public Heightmap(int size, float extent, float[] heights)
        {
            Size = size;
            Extent = extent;
            Heights = heights;
        }

        public float CellSize => 2f * Extent / Size;
        public Rect Bounds => new Rect(-Extent, -Extent, 2f * Extent, 2f * Extent);

        public Vector2 CellCentre(int x, int z) =>
            new Vector2(-Extent + (x + 0.5f) * CellSize, -Extent + (z + 0.5f) * CellSize);

        public float this[int x, int z] => Heights[z * Size + x];

        public float Sample(Vector2 xz) => GridSample.Bilinear(Heights, Size, Size, Bounds, xz);

        public DepthMap ToDepthMap(float shoalDepth)
        {
            var field = new float[Heights.Length];
            for (int i = 0; i < field.Length; i++)
            {
                field[i] = Mathf.Clamp01(-Heights[i] / shoalDepth);
            }
            return new DepthMap(Size, Size, Bounds, field);
        }
    }
}
