using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Knotical
{
    public readonly struct ProfilePair
    {
        public readonly FieldInfo Range;
        public readonly FieldInfo Setting;

        public ProfilePair(FieldInfo range, FieldInfo setting)
        {
            Range = range;
            Setting = setting;
        }
    }

    public static class ProfileRoller
    {
        private const BindingFlags PublicFields = BindingFlags.Public | BindingFlags.Instance;

        private static readonly Dictionary<Type, ProfilePair[]> cache = new Dictionary<Type, ProfilePair[]>();

        public static IReadOnlyList<ProfilePair> PairsFor(Type profileType, Type settingsType)
        {
            if (cache.TryGetValue(profileType, out ProfilePair[] pairs)) return pairs;

            pairs = profileType
                .GetFields(PublicFields)
                .Where(f => f.FieldType == typeof(FloatRange) || f.FieldType == typeof(IntRange))
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .Select(f => new ProfilePair(f, settingsType.GetField(f.Name, PublicFields)))
                .ToArray();
            cache[profileType] = pairs;
            return pairs;
        }

        public static TSettings Roll<TSettings>(ScriptableObject profile, TSettings baseSettings, int seed)
            where TSettings : ScriptableObject
        {
            TSettings settings = UnityEngine.Object.Instantiate(baseSettings);
            settings.name = baseSettings.name + " (rolled " + seed + ")";
            settings.hideFlags = HideFlags.HideAndDontSave;
            typeof(TSettings).GetField("Seed", PublicFields)?.SetValue(settings, seed);

            foreach (ProfilePair pair in PairsFor(profile.GetType(), typeof(TSettings)))
            {
                if (pair.Setting == null) continue;
                var rng = new System.Random(unchecked(seed * 40503 + StableHash(pair.Range.Name)));
                object range = pair.Range.GetValue(profile);
                object value = range is FloatRange f ? f.Pick(rng) : (object)((IntRange)range).Pick(rng);
                pair.Setting.SetValue(settings, value);
            }

            return settings;
        }

        public static void FixTo(ScriptableObject profile, ScriptableObject settings)
        {
            foreach (ProfilePair pair in PairsFor(profile.GetType(), settings.GetType()))
            {
                if (pair.Setting == null) continue;
                object value = pair.Setting.GetValue(settings);
                object range = value is float f ? new FloatRange(f) : (object)new IntRange((int)value);
                pair.Range.SetValue(profile, range);
            }
        }

        private static int StableHash(string text)
        {
            uint hash = 2166136261;
            foreach (char c in text) hash = unchecked((hash ^ c) * 16777619);
            return unchecked((int)hash);
        }
    }
}
