using NUnit.Framework;
using UnityEngine;

namespace Knotical.Tests
{
    public class LevelProfileTests
    {
        [Test]
        public void EveryRangeMatchesASettingsField()
        {
            foreach (ProfilePair pair in LevelProfile.Pairs)
            {
                Assert.IsNotNull(pair.Setting, $"LevelSettings has no field {pair.Range.Name}");
                System.Type expected = pair.Range.FieldType == typeof(IntRange) ? typeof(int) : typeof(float);
                Assert.AreEqual(expected, pair.Setting.FieldType, pair.Range.Name);
            }
        }

        [Test]
        public void FixedProfileReproducesSettings()
        {
            var settings = ScriptableObject.CreateInstance<LevelSettings>();
            settings.LandHeight = 33f;
            settings.Octaves = 6;
            var profile = ScriptableObject.CreateInstance<LevelProfile>();
            profile.FixTo(settings);

            LevelSettings rolled = profile.Roll(settings, 9);
            Assert.AreEqual(33f, rolled.LandHeight);
            Assert.AreEqual(6, rolled.Octaves);
            Assert.AreEqual(9, rolled.Seed);
            Assert.AreEqual(settings.Sand, rolled.Sand);
        }

        [Test]
        public void RollStaysInsideRangeAndIsSeeded()
        {
            var settings = ScriptableObject.CreateInstance<LevelSettings>();
            var profile = ScriptableObject.CreateInstance<LevelProfile>();
            profile.LandHeight = new FloatRange(5f, 40f);
            profile.Octaves = new IntRange(2, 7);

            bool differs = false;
            for (int seed = 1; seed <= 30; seed++)
            {
                LevelSettings a = profile.Roll(settings, seed);
                LevelSettings b = profile.Roll(settings, seed);
                Assert.AreEqual(a.LandHeight, b.LandHeight);
                Assert.AreEqual(a.Octaves, b.Octaves);
                Assert.That(a.LandHeight, Is.InRange(5f, 40f));
                Assert.That(a.Octaves, Is.InRange(2, 7));
                Assert.AreEqual(settings.IslandScale, a.IslandScale);
                if (!Mathf.Approximately(a.LandHeight, profile.Roll(settings, seed + 1).LandHeight)) differs = true;
            }
            Assert.IsTrue(differs);
        }
    }
}
