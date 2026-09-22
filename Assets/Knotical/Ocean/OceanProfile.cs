using System.Collections.Generic;
using UnityEngine;

namespace Knotical
{
    [CreateAssetMenu(menuName = "Knotical/Ocean Profile", fileName = "OceanProfile")]
    public class OceanProfile : ScriptableObject
    {
        [Header("Waves")]
        public IntRange Count = new IntRange(10);
        public FloatRange Speed = new FloatRange(1f);
        public FloatRange DirectionDeg = new FloatRange(0f, 360f);
        public FloatRange SpreadDeg = new FloatRange(45f);
        public FloatRange Distribution = new FloatRange(0.6f);
        public FloatRange MinWavelength = new FloatRange(8f);
        public FloatRange MaxWavelength = new FloatRange(140f);
        public FloatRange MinAmplitude = new FloatRange(0.06f);
        public FloatRange MaxAmplitude = new FloatRange(1.1f);
        public FloatRange MinSteepness = new FloatRange(0.85f);
        public FloatRange MaxSteepness = new FloatRange(0.4f);
        public FloatRange ShallowResponse = new FloatRange(0.6f);

        public static IReadOnlyList<ProfilePair> Pairs => ProfileRoller.PairsFor(typeof(OceanProfile), typeof(OceanSettings));

        public OceanSettings Roll(OceanSettings baseSettings, int seed) => ProfileRoller.Roll(this, baseSettings, seed);

        public void FixTo(OceanSettings settings) => ProfileRoller.FixTo(this, settings);
    }
}
