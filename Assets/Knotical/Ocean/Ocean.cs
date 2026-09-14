using System;
using UnityEngine;

namespace Knotical
{
    public static class Ocean
    {
        public const float WorldHalfExtent = 8192f;
        public const float SeaLevel = 0f;

        private const int UndisplaceIterations = 4;

        public static OceanSettings Settings { get; private set; }
        public static WaveSet WaveSet { get; private set; } = WaveSet.Empty;
        public static double Time { get; private set; }
        public static int Version { get; private set; }
        public static Func<Vector2, float> DepthField { get; set; }

        public static float SignificantHeight => WaveSet.SignificantHeight;
        public static float PeakWavelength => WaveSet.PeakWavelength;

        public static void Configure(OceanSettings settings)
        {
            Settings = settings;
            WaveSet = settings != null ? settings.Build() : WaveSet.Empty;
            Version++;
        }

        public static void Rebuild() => Configure(Settings);

        public static void Advance(double delta) => Time += delta;

        public static void SetTime(double time) => Time = time;

        public static float Field(Vector2 worldXZ) => DepthField?.Invoke(worldXZ) ?? 1f;

        public static float GetHeight(Vector2 worldXZ, float minWavelength = 0f)
        {
            float field = Field(worldXZ);
            Vector2 p = Undisplace(worldXZ, field, minWavelength);
            return SeaLevel + HeightAtParameter(p, field, minWavelength);
        }

        public static float GetHeight(Vector3 worldPos, float minWavelength = 0f) =>
            GetHeight(new Vector2(worldPos.x, worldPos.z), minWavelength);

        public static Vector3 GetSurfacePoint(Vector2 parameterXZ, float minWavelength = 0f)
        {
            float field = Field(parameterXZ);
            Vector2 pinch = Pinch(parameterXZ, field, minWavelength);
            float y = HeightAtParameter(parameterXZ, field, minWavelength);
            return new Vector3(parameterXZ.x + pinch.x, SeaLevel + y, parameterXZ.y + pinch.y);
        }

        public static Vector3 GetNormal(Vector2 parameterXZ, float minWavelength = 0f)
        {
            float dx = 0f;
            float dz = 0f;
            float jxx = 0f;
            float jxz = 0f;
            float jzz = 0f;
            float t = (float)Time;
            float field = Field(parameterXZ);
            Wave[] waves = WaveSet.Waves;

            for (int i = 0; i < waves.Length; i++)
            {
                Wave w = waves[i];
                float weight = Cutoff(w.Wavelength, minWavelength);
                if (weight <= 0f) continue;

                float scale = WaveScale(field, w);
                float phase = w.WaveNumber * Vector2.Dot(w.Direction, parameterXZ) - w.AngularFrequency * t + w.Phase;

                float s = Mathf.Sin(phase);
                float c = Mathf.Cos(phase) * w.Amplitude * scale * w.WaveNumber * weight;

                dx += c * w.Direction.x;
                dz += c * w.Direction.y;

                float qak = PinchAmplitude(w, scale) * w.WaveNumber * s * weight;
                jxx += qak * w.Direction.x * w.Direction.x;
                jxz += qak * w.Direction.x * w.Direction.y;
                jzz += qak * w.Direction.y * w.Direction.y;
            }

            var tangentX = new Vector3(1f - jxx, dx, -jxz);
            var tangentZ = new Vector3(-jxz, dz, 1f - jzz);

            return Vector3.Cross(tangentZ, tangentX).normalized;
        }

        public static Vector3 GetNormal(Vector3 worldPos, float minWavelength = 0f) =>
            GetNormal(new Vector2(worldPos.x, worldPos.z), minWavelength);

        public static float GetVerticalVelocity(Vector2 worldXZ, float minWavelength = 0f)
        {
            float v = 0f;
            float t = (float)Time;
            float field = Field(worldXZ);
            Wave[] waves = WaveSet.Waves;

            for (int i = 0; i < waves.Length; i++)
            {
                Wave w = waves[i];
                float weight = Cutoff(w.Wavelength, minWavelength);
                if (weight <= 0f) continue;

                float phase = w.WaveNumber * Vector2.Dot(w.Direction, worldXZ) - w.AngularFrequency * t + w.Phase;
                v += -w.Amplitude * weight * WaveScale(field, w) * w.AngularFrequency * Mathf.Cos(phase);
            }

            return v;
        }

        public static Vector3 GetFlow(Vector3 worldPos, float minWavelength = 0f)
        {
            var flat = new Vector2(worldPos.x, worldPos.z);
            var flow = Vector3.zero;
            float t = (float)Time;
            float field = Field(flat);
            float depth = Mathf.Min(worldPos.y - SeaLevel, 0f);
            Wave[] waves = WaveSet.Waves;

            for (int i = 0; i < waves.Length; i++)
            {
                Wave w = waves[i];
                float weight = Cutoff(w.Wavelength, minWavelength);
                if (weight <= 0f) continue;

                float phase = w.WaveNumber * Vector2.Dot(w.Direction, flat) - w.AngularFrequency * t + w.Phase;
                float amplitude = w.Amplitude * weight * WaveScale(field, w) * w.AngularFrequency
                    * Mathf.Exp(w.WaveNumber * depth);
                float swing = Mathf.Sin(phase);

                flow.x += w.Direction.x * amplitude * swing;
                flow.z += w.Direction.y * amplitude * swing;
                flow.y -= amplitude * Mathf.Cos(phase);
            }

            return flow;
        }

        private static float HeightAtParameter(Vector2 p, float field, float minWavelength)
        {
            float y = 0f;
            float t = (float)Time;
            Wave[] waves = WaveSet.Waves;

            for (int i = 0; i < waves.Length; i++)
            {
                Wave w = waves[i];
                float weight = Cutoff(w.Wavelength, minWavelength);
                if (weight <= 0f) continue;

                float phase = w.WaveNumber * Vector2.Dot(w.Direction, p) - w.AngularFrequency * t + w.Phase;
                y += w.Amplitude * weight * WaveScale(field, w) * Mathf.Sin(phase);
            }

            return y;
        }

        private static Vector2 Pinch(Vector2 p, float field, float minWavelength)
        {
            Vector2 pinch = Vector2.zero;
            float t = (float)Time;
            Wave[] waves = WaveSet.Waves;

            for (int i = 0; i < waves.Length; i++)
            {
                Wave w = waves[i];
                float weight = Cutoff(w.Wavelength, minWavelength);
                if (weight <= 0f) continue;

                float phase = w.WaveNumber * Vector2.Dot(w.Direction, p) - w.AngularFrequency * t + w.Phase;
                pinch += w.Direction * (PinchAmplitude(w, WaveScale(field, w)) * weight * Mathf.Cos(phase));
            }

            return pinch;
        }

        private static Vector2 Undisplace(Vector2 worldXZ, float field, float minWavelength)
        {
            Vector2 p = worldXZ;

            for (int iter = 0; iter < UndisplaceIterations; iter++)
            {
                p = worldXZ - Pinch(p, field, minWavelength);
            }

            return p;
        }

        private static float WaveScale(float field, Wave w) =>
            field == 1f ? 1f : Mathf.Pow(field, w.DepthResponse);

        private static float PinchAmplitude(Wave w, float scale) =>
            w.Steepness / (w.WaveNumber * WaveSet.Count) * Mathf.Min(scale, 1f);

        private static float Cutoff(float wavelength, float minWavelength) =>
            minWavelength <= 0f ? 1f : SmoothStep01(minWavelength * 0.5f, minWavelength, wavelength);

        public static float SmoothStep01(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }
    }
}
