using UnityEngine;

namespace Knotical
{
    public static class Wind
    {
        private const float Tau = 2f * Mathf.PI;
        private const double PlasticConjugate = 0.7548776662466927;
        private const float DriftKneeSpeed = 12f;

        private static readonly float[] VeerPeriods = { 137f, 61f, 23.3f };
        private static readonly float[] VeerWeights = { 1f, 0.45f, 0.18f };
        private static readonly float[] GustPeriods = { 47f, 17.1f, 6.3f, 2.9f };
        private static readonly float[] GustWeights = { 1f, 0.5f, 0.28f, 0.14f };

        private static readonly string[] BeaufortNames =
        {
            "Calm", "Light air", "Light breeze", "Gentle breeze", "Moderate breeze",
            "Fresh breeze", "Strong breeze", "Near gale", "Gale", "Strong gale",
            "Storm", "Violent storm", "Hurricane"
        };

        private static readonly float[] BeaufortUpperBounds =
        {
            0.5f, 1.6f, 3.4f, 5.5f, 8.0f, 10.8f, 13.9f, 17.2f, 20.8f, 24.5f, 28.5f, 32.7f
        };

        public static WindSettings Settings { get; private set; }
        public static double Time { get; private set; }
        public static float DirectionRad { get; private set; }
        public static float Speed { get; private set; }
        public static Vector2 AccumulatedDrift { get; private set; }

        public static float SpeedBias { get; set; }
        public static float HeadingBiasDeg { get; set; }
        public static float DebugTimeScale { get; set; } = 1f;

        public static Vector2 Direction => new Vector2(Mathf.Cos(DirectionRad), Mathf.Sin(DirectionRad));
        public static Vector2 Velocity => Direction * Speed;

        public static int BeaufortForce => BeaufortForceFor(Speed);
        public static string BeaufortName => BeaufortNames[BeaufortForce];

        public static void Configure(WindSettings settings)
        {
            Settings = settings != null ? settings : ScriptableObject.CreateInstance<WindSettings>();
            Sample();
        }

        public static void Advance(double delta)
        {
            EnsureConfigured();
            double dt = delta * Settings.TimeScale * DebugTimeScale;
            Time += dt;
            Sample();

            float driftSpeed = Speed * DriftKneeSpeed / (DriftKneeSpeed + Speed);
            AccumulatedDrift += Direction * driftSpeed * (float)dt;
        }

        public static void SetTime(double time)
        {
            EnsureConfigured();
            Time = time;
            Sample();
        }

        public static void ResetOverrides()
        {
            SpeedBias = 0f;
            HeadingBiasDeg = 0f;
            DebugTimeScale = 1f;
        }

        public static int BeaufortForceFor(float speed)
        {
            for (int i = 0; i < BeaufortUpperBounds.Length; i++)
            {
                if (speed < BeaufortUpperBounds[i]) return i;
            }

            return BeaufortNames.Length - 1;
        }

        public static Vector2 GetVelocity(Vector2 worldXZ) => Velocity;

        private static void EnsureConfigured()
        {
            if (Settings == null) Configure(null);
        }

        private static void Sample()
        {
            float t = (float)Time;

            float veer = WeightedOscillator(t, VeerPeriods, VeerWeights, 0);
            float gust = WeightedOscillator(t, GustPeriods, GustWeights, VeerPeriods.Length);

            DirectionRad = (Settings.BaseDirectionDeg + HeadingBiasDeg) * Mathf.Deg2Rad
                         + Settings.VeerDeg * Mathf.Deg2Rad * veer;

            Speed = Mathf.Max(0f, Settings.BaseSpeed * (1f + Settings.Gustiness * gust) + SpeedBias);
        }

        private static float WeightedOscillator(float t, float[] periods, float[] weights, int phaseOffset)
        {
            float sum = 0f;
            float total = 0f;

            for (int i = 0; i < periods.Length; i++)
            {
                float phase = (float)(Tau * Frac((i + phaseOffset + 1) * PlasticConjugate));
                sum += weights[i] * Mathf.Sin(Tau * t / periods[i] + phase);
                total += weights[i];
            }

            return total > 0f ? sum / total : 0f;
        }

        private static double Frac(double v) => v - System.Math.Floor(v);
    }
}
