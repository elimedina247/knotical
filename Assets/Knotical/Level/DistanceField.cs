using UnityEngine;

namespace Knotical
{
    public static class DistanceField
    {
        private const float Far = 1e20f;

        public static float[] SquaredToNearest(bool[] inside, int width, int height)
        {
            var f = new float[width * height];
            for (int i = 0; i < f.Length; i++) f[i] = inside[i] ? 0f : Far;

            int n = Mathf.Max(width, height);
            var column = new float[n];
            var result = new float[n];
            var v = new int[n];
            var z = new float[n + 1];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++) column[y] = f[y * width + x];
                Transform1D(column, result, height, v, z);
                for (int y = 0; y < height; y++) f[y * width + x] = result[y];
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++) column[x] = f[y * width + x];
                Transform1D(column, result, width, v, z);
                for (int x = 0; x < width; x++) f[y * width + x] = result[x];
            }

            return f;
        }

        private static void Transform1D(float[] f, float[] d, int n, int[] v, float[] z)
        {
            int k = 0;
            v[0] = 0;
            z[0] = -Far;
            z[1] = Far;

            for (int q = 1; q < n; q++)
            {
                float s = Intersect(f, q, v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = Intersect(f, q, v[k]);
                }
                k++;
                v[k] = q;
                z[k] = s;
                z[k + 1] = Far;
            }

            k = 0;
            for (int q = 0; q < n; q++)
            {
                while (z[k + 1] < q) k++;
                int dq = q - v[k];
                d[q] = dq * dq + f[v[k]];
            }
        }

        private static float Intersect(float[] f, int q, int p) =>
            ((f[q] + (float)q * q) - (f[p] + (float)p * p)) / (2f * q - 2f * p);
    }
}
