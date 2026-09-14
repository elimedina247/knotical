using NUnit.Framework;
using UnityEngine;

namespace Knotical.Tests
{
    public class WindTests
    {
        [SetUp]
        public void SetUp()
        {
            Wind.ResetOverrides();
        }

        [Test]
        public void NoGustsAndNoVeerGivesBaseConditions()
        {
            var s = ScriptableObject.CreateInstance<WindSettings>();
            s.BaseSpeed = 7f;
            s.BaseDirectionDeg = 120f;
            s.Gustiness = 0f;
            s.VeerDeg = 0f;

            Wind.Configure(s);
            Wind.SetTime(123.4);

            Assert.AreEqual(7f, Wind.Speed, 1e-5f);
            Assert.AreEqual(120f * Mathf.Deg2Rad, Wind.DirectionRad, 1e-5f);
            Assert.AreEqual(1f, Wind.Direction.magnitude, 1e-5f);
        }

        [Test]
        public void SpeedNeverGoesNegative()
        {
            var s = ScriptableObject.CreateInstance<WindSettings>();
            s.BaseSpeed = 5f;
            s.Gustiness = 1f;
            Wind.Configure(s);

            for (double t = 0; t < 600; t += 0.37)
            {
                Wind.SetTime(t);
                Assert.GreaterOrEqual(Wind.Speed, 0f, $"at t={t}");
            }
        }

        [Test]
        public void BeaufortScale()
        {
            Assert.AreEqual(0, Wind.BeaufortForceFor(0f));
            Assert.AreEqual(5, Wind.BeaufortForceFor(9f));
            Assert.AreEqual(12, Wind.BeaufortForceFor(40f));
        }
    }
}
