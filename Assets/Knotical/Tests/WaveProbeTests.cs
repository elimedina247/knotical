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
    }
}
