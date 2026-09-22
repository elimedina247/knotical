using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Knotical
{
    public class MapView : MonoBehaviour
    {
        [Range(1, 4)] public int PixelsPerCell = 3;
        [Range(200f, 1080f)] public float ScreenSize = 940f;

        private Canvas canvas;
        private RawImage image;
        private RectTransform marker;
        private Texture2D texture;
        private Heightmap heightmap;

        public bool Visible => canvas != null && canvas.gameObject.activeSelf;
        public Texture2D Texture => texture;

        private void Start()
        {
            canvas = UiKit.Canvas("MapCanvas", 20);
            canvas.transform.SetParent(transform, false);

            var dim = new GameObject("Dim").AddComponent<Image>();
            dim.transform.SetParent(canvas.transform, false);
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            UiKit.Stretch(dim.rectTransform);

            var frame = new GameObject("Frame").AddComponent<Image>();
            frame.transform.SetParent(canvas.transform, false);
            frame.color = MapPainter.Ink;
            UiKit.Centre(frame.rectTransform, new Vector2(ScreenSize + 16f, ScreenSize + 16f));

            image = new GameObject("Map").AddComponent<RawImage>();
            image.transform.SetParent(canvas.transform, false);
            UiKit.Centre(image.rectTransform, new Vector2(ScreenSize, ScreenSize));

            Image boat = UiKit.Image(canvas.transform, "Boat", UiKit.TriangleSprite(24, 30), new Color(0.95f, 0.25f, 0.2f));
            marker = boat.rectTransform;
            marker.sizeDelta = new Vector2(14f, 18f);

            Refresh();
            canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k != null && k.mKey.wasPressedThisFrame) Toggle();
        }

        private void LateUpdate()
        {
            if (!Visible || heightmap == null) return;
            var boat = FindAnyObjectByType<BoatMotor>();
            if (boat == null) return;
            Vector3 p = boat.transform.position;
            float u = Mathf.InverseLerp(-heightmap.Extent, heightmap.Extent, p.x);
            float v = Mathf.InverseLerp(-heightmap.Extent, heightmap.Extent, p.z);
            marker.anchoredPosition = new Vector2((u - 0.5f) * ScreenSize, (v - 0.5f) * ScreenSize);
            float yaw = Vector3.SignedAngle(Vector3.forward, Vector3.ProjectOnPlane(boat.transform.forward, Vector3.up), Vector3.up);
            marker.localRotation = Quaternion.Euler(0f, 0f, -yaw);
        }

        public void Toggle()
        {
            if (canvas == null) return;
            canvas.gameObject.SetActive(!canvas.gameObject.activeSelf);
        }

        public void Refresh()
        {
            var generator = FindAnyObjectByType<LevelGenerator>();
            heightmap = generator != null ? generator.Heightmap : null;
            if (heightmap == null)
            {
                image.color = MapPainter.DefaultDeep;
                image.texture = null;
                return;
            }

            if (texture != null) Destroy(texture);
            texture = MapPainter.Paint(heightmap, PixelsPerCell, generator.Active, generator.transform.Find("Generated/Blocks"));
            image.color = Color.white;
            image.texture = texture;
        }

        private void OnDestroy()
        {
            if (texture != null) Destroy(texture);
        }
    }
}
