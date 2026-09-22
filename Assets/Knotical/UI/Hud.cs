using UnityEngine;
using UnityEngine.UI;

namespace Knotical
{
    public class Hud : MonoBehaviour
    {
        private const float GaugeSize = 120f;
        private const float Margin = 28f;
        private const float Top = 22f;
        private const float RudderSweepDeg = 60f;

        private static readonly Color DiscGrey = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        private static readonly Color Ink = new Color(0.97f, 0.97f, 0.95f, 1f);

        private BoatMotor motor;
        private Camera view;
        private Text gearText;
        private Text windText;
        private RectTransform windArrow;
        private RectTransform rudderBlade;
        private int shownGear = int.MinValue;

        private void Start()
        {
            motor = FindAnyObjectByType<BoatMotor>();
            view = Camera.main;
            Canvas canvas = UiKit.Canvas("HudCanvas", 10);
            canvas.transform.SetParent(transform, false);

            Sprite disc = UiKit.DiscSprite(128, 0.35f, 0.06f);

            BuildWindGauge(canvas.transform, disc, new Vector2(Margin, Top));
            BuildRudderGauge(canvas.transform, disc, new Vector2(Margin + GaugeSize + 16f, Top));

            gearText = UiKit.Text(canvas.transform, "Gear", 40, Color.white);
            UiKit.TopLeft(gearText.rectTransform, new Vector2(Margin + 2f * (GaugeSize + 16f), Top + 30f), new Vector2(700f, 60f));
        }

        private void BuildWindGauge(Transform parent, Sprite disc, Vector2 offset)
        {
            Image face = UiKit.Image(parent, "WindDisc", disc, DiscGrey);
            UiKit.TopLeft(face.rectTransform, offset, new Vector2(GaugeSize, GaugeSize));

            Image tick = UiKit.Image(face.transform, "Forward", null, Ink);
            UiKit.Centre(tick.rectTransform, new Vector2(3f, 10f));
            tick.rectTransform.anchoredPosition = new Vector2(0f, GaugeSize * 0.5f - 7f);

            windArrow = UiKit.Node(face.transform, "Arrow");
            Image shaft = UiKit.Image(windArrow, "Shaft", null, Ink);
            UiKit.Centre(shaft.rectTransform, new Vector2(6f, 48f));
            shaft.rectTransform.anchoredPosition = new Vector2(0f, -8f);
            Image head = UiKit.Image(windArrow, "Head", UiKit.TriangleSprite(28, 24), Ink);
            UiKit.Centre(head.rectTransform, new Vector2(28f, 24f));
            head.rectTransform.anchoredPosition = new Vector2(0f, 26f);

            windText = UiKit.Text(parent, "WindSpeed", 22, Color.white);
            windText.alignment = TextAnchor.UpperCenter;
            UiKit.TopLeft(windText.rectTransform, offset + new Vector2(0f, GaugeSize + 4f), new Vector2(GaugeSize, 30f));
        }

        private void BuildRudderGauge(Transform parent, Sprite disc, Vector2 offset)
        {
            Image face = UiKit.Image(parent, "RudderDisc", disc, DiscGrey);
            UiKit.TopLeft(face.rectTransform, offset, new Vector2(GaugeSize, GaugeSize));

            Image tick = UiKit.Image(face.transform, "Neutral", null, Ink);
            UiKit.Centre(tick.rectTransform, new Vector2(3f, 10f));
            tick.rectTransform.anchoredPosition = new Vector2(0f, -(GaugeSize * 0.5f - 7f));

            Image hinge = UiKit.Image(face.transform, "Hinge", UiKit.DiscSprite(16, 1f, 0f), Ink);
            UiKit.Centre(hinge.rectTransform, new Vector2(10f, 10f));

            Image blade = UiKit.Image(face.transform, "Blade", UiKit.BladeSprite(20, 44), Ink);
            UiKit.Centre(blade.rectTransform, new Vector2(20f, 44f));
            blade.rectTransform.pivot = new Vector2(0.5f, 1f);
            rudderBlade = blade.rectTransform;
        }

        private void Update()
        {
            if (motor == null) return;

            if (motor.Gear != shownGear)
            {
                shownGear = motor.Gear;
                gearText.text = Describe(shownGear);
            }

            float swing = Mathf.Clamp(motor.RudderAngle, -1f, 1f) * RudderSweepDeg;
            rudderBlade.localRotation = Quaternion.Euler(0f, 0f, swing);

            Vector2 wind = Wind.Velocity;
            if (wind.sqrMagnitude > 0.0001f)
            {
                float windHeading = Mathf.Atan2(wind.x, wind.y) * Mathf.Rad2Deg;
                float cameraYaw = 0f;
                if (view != null)
                {
                    Vector3 f = view.transform.forward;
                    cameraYaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
                }
                windArrow.localRotation = Quaternion.Euler(0f, 0f, -(windHeading - cameraYaw));
            }
            windText.text = $"{Wind.Speed:F0} m/s";
        }

        public static string Describe(int gear) => gear switch
        {
            -1 => "GEAR R  reverse",
            0 => "GEAR N  neutral",
            1 => "GEAR 1  putter",
            2 => "GEAR 2  half sail",
            3 => "GEAR 3  full sail",
            _ => $"GEAR {gear}",
        };
    }
}
