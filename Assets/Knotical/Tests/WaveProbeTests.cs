using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Knotical.Tests
{
    public class WaveProbeTests
    {
        private const string ProbePath = "Assets/Knotical/Shaders/OceanProbe.compute";

        [Test]
        public void GpuSurfaceAndNormalMatchCpu()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders unavailable in this run");
            }

            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ProbePath);
            Assert.IsNotNull(shader, $"missing {ProbePath}");

            Ocean.DepthField = null;
            Ocean.Configure(ScriptableObject.CreateInstance<OceanSettings>());
            Ocean.SetTime(5.5);

            WaveProbe.Result r = WaveProbe.Run(shader, WaveProbe.DefaultPoints);

            Assert.Less(r.MaxPositionError, 0.005f, $"worst at {WaveProbe.DefaultPoints[r.WorstIndex]}");
            Assert.Less(r.MaxNormalAngle, 0.25f);
        }

        [Test]
        public void GpuMatchesCpuWithDepthMap()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                Assert.Ignore("Compute shaders unavailable in this run");
            }

            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>(ProbePath);
            Assert.IsNotNull(shader, $"missing {ProbePath}");

            const int n = 16;
            var field = new float[n * n];
            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++) field[z * n + x] = Mathf.Clamp01((x + 0.3f * z) / (n - 1f));
            }

            var map = new DepthMap(n, n, new Rect(-64f, -64f, 128f, 128f), field);
            Ocean.DepthMap = map;
            Ocean.Configure(ScriptableObject.CreateInstance<OceanSettings>());
            Ocean.SetTime(5.5);

            try
            {
                WaveProbe.Result r = WaveProbe.Run(shader, WaveProbe.DefaultPoints);
                Assert.Less(r.MaxPositionError, 0.005f, $"worst at {WaveProbe.DefaultPoints[r.WorstIndex]}");
                Assert.Less(r.MaxNormalAngle, 0.25f);
            }
            finally
            {
                Ocean.DepthMap = null;
                map.Dispose();
            }
        }
    }
}
