using NUnit.Framework;
using UnityEngine;

namespace Knotical.Tests
{
    public class OceanProfileTests
    {
        [Test]
        public void EveryRangeMatchesASettingsField()
        {
            foreach (ProfilePair pair in OceanProfile.Pairs)
            {
                Assert.IsNotNull(pair.Setting, $"OceanSettings has no field {pair.Range.Name}");
                System.Type expected = pair.Range.FieldType == typeof(IntRange) ? typeof(int) : typeof(float);
                Assert.AreEqual(expected, pair.Setting.FieldType, pair.Range.Name);
            }
        }

        [Test]
        public void RollSeedsTheWavesAndStaysInsideRange()
        {
            var settings = ScriptableObject.CreateInstance<OceanSettings>();
            settings.DeepColor = Color.magenta;
            var profile = ScriptableObject.CreateInstance<OceanProfile>();
            profile.MaxAmplitude = new FloatRange(0.5f, 2f);
            profile.Count = new IntRange(3, 6);

            bool differs = false;
            for (int seed = 1; seed <= 30; seed++)
            {
                OceanSettings a = profile.Roll(settings, seed);
                OceanSettings b = profile.Roll(settings, seed);
                Assert.AreEqual(a.MaxAmplitude, b.MaxAmplitude);
                Assert.AreEqual(seed, a.Seed);
                Assert.That(a.MaxAmplitude, Is.InRange(0.5f, 2f));
                Assert.That(a.Count, Is.InRange(3, 6));
                Assert.AreEqual(Color.magenta, a.DeepColor);
                Assert.AreEqual(a.Build().Count, b.Build().Count);
                if (!Mathf.Approximately(a.MaxAmplitude, profile.Roll(settings, seed + 1).MaxAmplitude)) differs = true;
            }
            Assert.IsTrue(differs);
        }

        [Test]
        public void FixedProfileReproducesSettings()
        {
            var settings = ScriptableObject.CreateInstance<OceanSettings>();
            settings.MinWavelength = 12f;
            settings.Count = 7;
            var profile = ScriptableObject.CreateInstance<OceanProfile>();
            profile.FixTo(settings);

            OceanSettings rolled = profile.Roll(settings, 5);
            Assert.AreEqual(12f, rolled.MinWavelength);
            Assert.AreEqual(7, rolled.Count);
        }
    }
}
