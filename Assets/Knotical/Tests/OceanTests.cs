using NUnit.Framework;
using UnityEngine;

namespace Knotical.Tests
{
    public class OceanTests
    {
        private static readonly Vector2[] SamplePoints =
        {
            new Vector2(0f, 0f),
            new Vector2(3.2f, -7.9f),
            new Vector2(-14.6f, 11.1f),
            new Vector2(19.5f, 18.25f),
            new Vector2(-2.75f, -19.9f),
        };

        [SetUp]
        public void SetUp()
        {
            Ocean.DepthField = null;
            Ocean.SetTime(0.0);
        }

        [Test]
        public void SingleWaveHeightAtTimeZeroMatchesHandComputation()
        {
            Ocean.Configure(SingleWave(1.5f, 100f, 0f, 90f));
            Wave w = Ocean.WaveSet.Waves[0];

            var p = new Vector2(12.5f, -37f);
            float k = 2f * Mathf.PI / 100f;
            float expected = Ocean.SeaLevel + 1.5f * Mathf.Sin(k * p.y + w.Phase);

            Assert.AreEqual(expected, Ocean.GetHeight(p), 1e-4f);
        }

        [Test]
        public void SingleWaveHeightAdvancesWithTime()
        {
            Ocean.Configure(SingleWave(1f, 50f, 0f, 0f));
            Wave w = Ocean.WaveSet.Waves[0];

            var p = new Vector2(5f, 0f);
            double t = 2.5;
            Ocean.SetTime(t);
            float expected = Mathf.Sin(w.WaveNumber * p.x - w.AngularFrequency * (float)t + w.Phase);

            Assert.AreEqual(expected, Ocean.GetHeight(p), 1e-4f);
        }

        [Test]
        public void NormalMatchesFiniteDifferenceOfSurfacePoint()
        {
            Ocean.Configure(ScriptableObject.CreateInstance<OceanSettings>());
            Ocean.SetTime(3.7);

            const float h = 0.02f;

            foreach (Vector2 p in SamplePoints)
            {
                Vector3 tangentX = (Ocean.GetSurfacePoint(p + new Vector2(h, 0f)) - Ocean.GetSurfacePoint(p - new Vector2(h, 0f))) / (2f * h);
                Vector3 tangentZ = (Ocean.GetSurfacePoint(p + new Vector2(0f, h)) - Ocean.GetSurfacePoint(p - new Vector2(0f, h))) / (2f * h);
                Vector3 numeric = Vector3.Cross(tangentZ, tangentX).normalized;
                Vector3 analytic = Ocean.GetNormal(p);

                Assert.Less(Vector3.Angle(numeric, analytic), 0.5f, $"at {p}");
                Assert.Greater(analytic.y, 0f, $"at {p}");
            }
        }

        [Test]
        public void HeightAtDisplacedPositionMatchesSurfacePoint()
        {
            Ocean.Configure(ScriptableObject.CreateInstance<OceanSettings>());
            Ocean.SetTime(8.1);

            foreach (Vector2 p in SamplePoints)
            {
                Vector3 surface = Ocean.GetSurfacePoint(p);
                float sampled = Ocean.GetHeight(new Vector2(surface.x, surface.z));

                Assert.AreEqual(surface.y, sampled, 0.02f, $"at {p}");
            }
        }

        [Test]
        public void FlowVerticalAtSurfaceEqualsVerticalVelocity()
        {
            Ocean.Configure(ScriptableObject.CreateInstance<OceanSettings>());
            Ocean.SetTime(12.3);

            foreach (Vector2 p in SamplePoints)
            {
                float vertical = Ocean.GetVerticalVelocity(p);
                Vector3 flow = Ocean.GetFlow(new Vector3(p.x, Ocean.SeaLevel, p.y));

                Assert.AreEqual(vertical, flow.y, 1e-4f, $"at {p}");
            }
        }

        [Test]
        public void FlowDecaysWithDepth()
        {
            Ocean.Configure(ScriptableObject.CreateInstance<OceanSettings>());
            Ocean.SetTime(1.0);

            var p = new Vector2(4f, 9f);
            float atSurface = Ocean.GetFlow(new Vector3(p.x, Ocean.SeaLevel, p.y)).magnitude;
            float deep = Ocean.GetFlow(new Vector3(p.x, Ocean.SeaLevel - 60f, p.y)).magnitude;

            Assert.Less(deep, atSurface * 0.5f);
        }

        [Test]
        public void SameSeedBuildsSameWaves()
        {
            var a = ScriptableObject.CreateInstance<OceanSettings>().Build();
            var b = ScriptableObject.CreateInstance<OceanSettings>().Build();

            Assert.AreEqual(a.Count, b.Count);
            Assert.AreEqual(ScriptableObject.CreateInstance<OceanSettings>().Count, a.Count);

            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.Waves[i].Phase, b.Waves[i].Phase);
                Assert.AreEqual(a.Waves[i].Direction, b.Waves[i].Direction);
                Assert.AreEqual(a.Waves[i].Wavelength, b.Waves[i].Wavelength);
            }
        }

        [Test]
        public void SmoothStepIsGlslThreshold()
        {
            Assert.AreEqual(0f, Ocean.SmoothStep01(2f, 4f, 1f));
            Assert.AreEqual(0.5f, Ocean.SmoothStep01(2f, 4f, 3f), 1e-6f);
            Assert.AreEqual(1f, Ocean.SmoothStep01(2f, 4f, 9f));
        }

        private static OceanSettings SingleWave(float amplitude, float wavelength, float steepness, float directionDeg)
        {
            var s = ScriptableObject.CreateInstance<OceanSettings>();
            s.Count = 1;
            s.MinWavelength = wavelength;
            s.MaxWavelength = wavelength;
            s.MinAmplitude = amplitude;
            s.MaxAmplitude = amplitude;
            s.MinSteepness = steepness;
            s.MaxSteepness = steepness;
            s.DirectionDeg = directionDeg;
            s.SpreadDeg = 0f;
            return s;
        }
    }
}
