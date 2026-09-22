using NUnit.Framework;
using UnityEngine;

namespace Knotical.Tests
{
    public class DayNightTests
    {
        [Test]
        public void NoonIsHighestAndMidnightIsBelowHorizon()
        {
            float noon = DayNightCycle.SunDirection(12f, 40f, 0f).y;
            Assert.Greater(noon, DayNightCycle.SunDirection(9f, 40f, 0f).y);
            Assert.Greater(noon, DayNightCycle.SunDirection(15f, 40f, 0f).y);
            Assert.AreEqual(Mathf.Cos(40f * Mathf.Deg2Rad), noon, 1e-4f);
            Assert.Less(DayNightCycle.SunDirection(0f, 40f, 0f).y, -0.5f);
            Assert.AreEqual(0f, DayNightCycle.SunDirection(6f, 40f, 0f).y, 1e-4f);
            Assert.AreEqual(0f, DayNightCycle.SunDirection(18f, 40f, 0f).y, 1e-4f);
        }

        [Test]
        public void MoonIsOppositeTheSun()
        {
            for (float hour = 0f; hour < 24f; hour += 1.5f)
            {
                Vector3 sun = DayNightCycle.SunDirection(hour, 40f, 30f);
                Vector3 moon = DayNightCycle.SunDirection(hour + 12f, 40f, 30f);
                Assert.AreEqual(-1f, Vector3.Dot(sun, moon), 1e-4f, $"hour {hour}");
                Assert.AreEqual(1f, sun.magnitude, 1e-4f);
            }
        }
    }
}
