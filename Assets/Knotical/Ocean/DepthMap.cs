using UnityEngine;

namespace Knotical
{
    public sealed class DepthMap
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Rect Bounds;
        public readonly float[] Field;

        private Texture2D texture;

        public DepthMap(int width, int height, Rect bounds, float[] field)
        {
            Width = width;
            Height = height;
            Bounds = bounds;
            Field = field;
        }

        public Vector4 ShaderBounds => new Vector4(Bounds.xMin, Bounds.yMin, 1f / Bounds.width, 1f / Bounds.height);
        public Vector4 ShaderSize => new Vector4(Width, Height, 0f, 0f);

        public Texture2D Texture
        {
            get
            {
                if (texture != null) return texture;
                texture = new Texture2D(Width, Height, TextureFormat.RFloat, false, true)
                {
                    name = "OceanDepthField",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                texture.SetPixelData(Field, 0);
                texture.Apply(false, true);
                return texture;
            }
        }

        public float Sample(Vector2 xz) => GridSample.Bilinear(Field, Width, Height, Bounds, xz);

        public void Dispose()
        {
            if (texture == null) return;
            if (Application.isPlaying) Object.Destroy(texture);
            else Object.DestroyImmediate(texture);
            texture = null;
        }
    }
}
