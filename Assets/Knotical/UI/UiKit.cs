using UnityEngine;
using UnityEngine.UI;

namespace Knotical
{
    public static class UiKit
    {
        public static Canvas Canvas(string name, int sortingOrder)
        {
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            return canvas;
        }

        public static Text Text(Transform parent, string name, int size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return Centre((RectTransform)go.transform, Vector2.zero);
        }

        public static RectTransform TopLeft(RectTransform rect, Vector2 offset, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(offset.x, -offset.y);
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Centre(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Sprite DiscSprite(int size, float fillAlpha, float rimFraction)
        {
            float radius = size * 0.5f;
            float rim = rimFraction * size;
            return Paint("Disc", size, size, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(radius, radius));
                float edge = Mathf.Clamp01(radius - d);
                float inRim = Mathf.Clamp01(d - (radius - rim) + 0.5f);
                return edge * Mathf.Lerp(fillAlpha, 1f, inRim);
            });
        }

        public static Sprite TriangleSprite(int width, int height)
        {
            return Paint("Triangle", width, height, (x, y) =>
            {
                float halfWidth = (1f - y / height) * width * 0.5f;
                return Mathf.Clamp01(halfWidth - Mathf.Abs(x - width * 0.5f) + 0.5f);
            });
        }

        public static Sprite BladeSprite(int width, int height)
        {
            return Paint("Blade", width, height, (x, y) =>
            {
                float v = y / height;
                float taper = Mathf.Lerp(0.2f, 1f, Mathf.Sqrt(v));
                float halfWidth = taper * width * 0.5f;
                float cap = Mathf.Clamp01(Mathf.Min(y, height - y) + 0.5f);
                return Mathf.Clamp01(halfWidth - Mathf.Abs(x - width * 0.5f) + 0.5f) * cap;
            });
        }

        private static Sprite Paint(string name, int width, int height, System.Func<float, float, float> alphaAt)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name };
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = new Color(1f, 1f, 1f, alphaAt(x + 0.5f, y + 0.5f));
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
