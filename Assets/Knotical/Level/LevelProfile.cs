using System.Collections.Generic;
using UnityEngine;

namespace Knotical
{
    [CreateAssetMenu(menuName = "Knotical/Level Profile", fileName = "LevelProfile")]
    public class LevelProfile : ScriptableObject
    {
        [Header("Sea")]
        public OceanProfile Ocean;

        [Header("Grid")]
        public FloatRange Extent = new FloatRange(1000f);

        [Header("Islands")]
        public FloatRange IslandScale = new FloatRange(380f);
        public IntRange Octaves = new IntRange(4);
        public FloatRange Lacunarity = new FloatRange(2f);
        public FloatRange Gain = new FloatRange(0.5f);
        public FloatRange WarpStrength = new FloatRange(0.35f);
        public FloatRange LandThreshold = new FloatRange(0.55f);
        public FloatRange LandHeight = new FloatRange(18f);
        public FloatRange LandRange = new FloatRange(0.15f);
        public FloatRange IslandHeightVariation = new FloatRange(0f);
        public FloatRange ShoreRise = new FloatRange(40f);
        public FloatRange ReliefHeight = new FloatRange(0f);
        public FloatRange ReliefScale = new FloatRange(90f);
        public FloatRange BorderStart = new FloatRange(0.75f);
        public FloatRange SpawnClearRadius = new FloatRange(70f);

        [Header("Seabed")]
        public FloatRange ShelfDepth = new FloatRange(1.5f);
        public FloatRange RampSlope = new FloatRange(0.08f);
        public FloatRange MaxDepth = new FloatRange(40f);
        public FloatRange DepthNoise = new FloatRange(0.25f);
        public FloatRange CanyonScale = new FloatRange(260f);
        public FloatRange CanyonDepth = new FloatRange(0f);
        public FloatRange ShoalDepth = new FloatRange(15f);

        [Header("Cliff blocks")]
        public FloatRange BlockSpacing = new FloatRange(10f);
        public FloatRange BlockJitter = new FloatRange(0.5f);
        public FloatRange BlockWidthMin = new FloatRange(1.3f);
        public FloatRange BlockWidthMax = new FloatRange(1.9f);
        public FloatRange BlockTilt = new FloatRange(4f);
        public FloatRange BlockSink = new FloatRange(3f);
        public FloatRange BlockRise = new FloatRange(1f);
        public FloatRange BlockMinHeight = new FloatRange(0.5f);

        [Header("Colours")]
        public FloatRange RockHeight = new FloatRange(10f);

        public static IReadOnlyList<ProfilePair> Pairs => ProfileRoller.PairsFor(typeof(LevelProfile), typeof(LevelSettings));

        public LevelSettings Roll(LevelSettings baseSettings, int seed) => ProfileRoller.Roll(this, baseSettings, seed);

        public void FixTo(LevelSettings settings) => ProfileRoller.FixTo(this, settings);
    }
}
