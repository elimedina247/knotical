using UnityEngine;

namespace Knotical
{
    public static class GridSample
    {
        public static float Bilinear(float[] data, int width, int height, Rect bounds, Vector2 xz)
        {
            float tx = (xz.x - bounds.xMin) * (1f / bounds.width) * width - 0.5f;
            float tz = (xz.y - bounds.yMin) * (1f / bounds.height) * height - 0.5f;
            tx = Mathf.Clamp(tx, 0f, width - 1);
            tz = Mathf.Clamp(tz, 0f, height - 1);

            float ix = Mathf.Floor(tx);
            float iz = Mathf.Floor(tz);
            float fx = tx - ix;
            float fz = tz - iz;

            int x0 = (int)ix;
            int z0 = (int)iz;
            int x1 = Mathf.Min(x0 + 1, width - 1);
            int z1 = Mathf.Min(z0 + 1, height - 1);

            float a = data[z0 * width + x0];
            float b = data[z0 * width + x1];
            float c = data[z1 * width + x0];
            float d = data[z1 * width + x1];

            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }
    }
}
