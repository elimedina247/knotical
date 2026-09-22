using UnityEngine;

namespace Knotical
{
    [ExecuteAlways]
    [DefaultExecutionOrder(300)]
    public class DayNightCycle : MonoBehaviour
    {
        private static readonly int SunDirId = Shader.PropertyToID("_KnoticalSunDir");
        private static readonly int MoonDirId = Shader.PropertyToID("_KnoticalMoonDir");
        private static readonly int NightId = Shader.PropertyToID("_KnoticalNight");
        private static readonly int DuskId = Shader.PropertyToID("_KnoticalDusk");
        private static readonly int AmbientId = Shader.PropertyToID("_KnoticalAmbient");

        [SerializeField] private DayNightSettings settings;
        [SerializeField] private Light sun;
        [SerializeField] private Light moon;
        [Range(0f, 24f)] public float Hour = 10f;
        public bool ClockDriven = true;

        public float Night { get; private set; }
        public float SunElevation { get; private set; }
        public DayNightSettings Settings => settings;

        private void Update()
        {
            if (!Application.isPlaying || !ClockDriven || settings == null) return;
            float hoursPerSecond = 24f / (Mathf.Max(settings.DayLengthMinutes, 0.01f) * 60f);
            Hour = Mathf.Repeat(settings.StartHour + (float)Ocean.Time * hoursPerSecond, 24f);
        }

        private void LateUpdate() => Apply();

        private void OnDisable()
        {
            Shader.SetGlobalFloat(NightId, 0f);
            Shader.SetGlobalFloat(DuskId, 0f);
            Shader.SetGlobalFloat(AmbientId, 1f);
        }

        public static Vector3 SunDirection(float hour, float tiltDeg, float azimuthDeg)
        {
            float phi = (hour - 6f) / 12f * Mathf.PI;
            var arc = new Vector3(Mathf.Cos(phi), Mathf.Sin(phi), 0f);
            return Quaternion.Euler(0f, azimuthDeg, 0f) * (Quaternion.Euler(tiltDeg, 0f, 0f) * arc);
        }

        private void Apply()
        {
            if (settings == null || sun == null) return;

            Vector3 sunDir = SunDirection(Hour, settings.SunPathTilt, settings.SunAzimuth);
            Vector3 moonDir = SunDirection(Hour + 12f, settings.SunPathTilt, settings.SunAzimuth);
            float elevation = sunDir.y;
            SunElevation = elevation;

            float daylight = Ocean.SmoothStep01(-0.12f, 0.1f, elevation);
            Night = 1f - Ocean.SmoothStep01(-0.22f, 0.05f, elevation);
            float dusk = Mathf.Exp(-(elevation * elevation) / (0.14f * 0.14f));

            sun.transform.rotation = Quaternion.LookRotation(-sunDir, Vector3.up);
            sun.color = Color.Lerp(settings.SunHorizon, settings.SunNoon, Ocean.SmoothStep01(0f, 0.35f, elevation));
            sun.intensity = settings.SunIntensity * daylight;
            sun.enabled = daylight > 0.001f;

            bool moonUp = false;
            if (moon != null)
            {
                moon.transform.rotation = Quaternion.LookRotation(-moonDir, Vector3.up);
                moon.color = settings.MoonColor;
                moon.intensity = settings.MoonIntensity * Night * Ocean.SmoothStep01(0f, 0.1f, moonDir.y);
                moonUp = moon.intensity > 0.001f;
                moon.enabled = moonUp;
            }

            RenderSettings.sun = sun.enabled || !moonUp ? sun : moon;
            RenderSettings.fogColor = Color.Lerp(settings.DayFog, settings.NightFog, Night);

            Shader.SetGlobalVector(SunDirId, sunDir);
            Shader.SetGlobalVector(MoonDirId, moonDir);
            Shader.SetGlobalFloat(NightId, Night);
            Shader.SetGlobalFloat(DuskId, dusk);
            Shader.SetGlobalFloat(AmbientId, Mathf.Lerp(1f, 0.12f, Night));
        }
    }
}
