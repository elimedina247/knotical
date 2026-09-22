using UnityEngine;

namespace Knotical
{
    [CreateAssetMenu(menuName = "Knotical/Day Night Settings", fileName = "DayNightSettings")]
    public class DayNightSettings : ScriptableObject
    {
        [Header("Clock")]
        [Range(0.5f, 120f)] public float DayLengthMinutes = 20f;
        [Range(0f, 24f)] public float StartHour = 10f;

        [Header("Sun")]
        [Range(0f, 80f)] public float SunPathTilt = 40f;
        [Range(0f, 360f)] public float SunAzimuth = 0f;
        [Range(0f, 4f)] public float SunIntensity = 1f;
        public Color SunNoon = new Color(1f, 0.957f, 0.839f);
        public Color SunHorizon = new Color(1f, 0.62f, 0.38f);

        [Header("Moon")]
        [Range(0f, 1f)] public float MoonIntensity = 0.18f;
        public Color MoonColor = new Color(0.62f, 0.72f, 0.95f);

        [Header("Fog")]
        public Color DayFog = new Color(0.74f, 0.85f, 0.94f);
        public Color NightFog = new Color(0.05f, 0.07f, 0.12f);
    }
}
