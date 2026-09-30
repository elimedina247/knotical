using UnityEngine;

namespace Knotical
{
    [CreateAssetMenu(menuName = "Knotical/Level Settings", fileName = "LevelSettings")]
    public class LevelSettings : ScriptableObject
    {
        [Range(0, 9999)] public int Seed = 1;

        [Header("Grid")]
        [Range(200f, 4000f)] public float Extent = 1000f;
        [Range(32, 512)] public int Resolution = 256;

        [Header("Islands")]
        [Range(50f, 2000f)] public float IslandScale = 380f;
        [Range(1, 8)] public int Octaves = 4;
        [Range(1.5f, 3f)] public float Lacunarity = 2f;
        [Range(0.2f, 0.8f)] public float Gain = 0.5f;
        [Range(0f, 1f)] public float WarpStrength = 0.35f;
        [Range(0f, 1f)] public float LandThreshold = 0.55f;
        [Range(0f, 80f)] public float LandHeight = 18f;
        [Range(0.02f, 0.5f)] public float LandRange = 0.15f;
        [Range(0f, 1f)] public float IslandHeightVariation = 0f;
        [Range(0f, 200f)] public float ShoreRise = 40f;
        [Range(0f, 60f)] public float ReliefHeight = 0f;
        [Range(20f, 400f)] public float ReliefScale = 90f;
        [Range(0f, 1f)] public float BorderStart = 0.75f;
        [Range(0f, 300f)] public float SpawnClearRadius = 70f;

        [Header("Seabed")]
        [Range(0f, 10f)] public float ShelfDepth = 1.5f;
        [Range(0f, 1f)] public float RampSlope = 0.08f;
        [Range(5f, 200f)] public float MaxDepth = 40f;
        [Range(0f, 0.6f)] public float DepthNoise = 0.25f;
        [Range(50f, 1000f)] public float CanyonScale = 260f;
        [Range(0f, 100f)] public float CanyonDepth = 0f;
        [Range(1f, 60f)] public float ShoalDepth = 15f;

        [Header("Cliff blocks")]
        [Range(3f, 40f)] public float BlockSpacing = 10f;
        [Range(0f, 1f)] public float BlockJitter = 0.5f;
        [Range(0.5f, 3f)] public float BlockWidthMin = 1.3f;
        [Range(0.5f, 3f)] public float BlockWidthMax = 1.9f;
        [Range(0f, 15f)] public float BlockTilt = 4f;
        [Range(0f, 10f)] public float BlockSink = 3f;
        [Range(0f, 5f)] public float BlockRise = 1f;
        [Range(0f, 5f)] public float BlockMinHeight = 0.5f;

        [Header("Scatter")]
        [Range(0f, 10f)] public float TreesPerCliff = 1.5f;
        [Range(0f, 10f)] public float BushesPerCliff = 2f;
        [Range(0f, 0.6f)] public float ScatterMargin = 0.3f;
        [Range(0f, 0.6f)] public float ScatterScaleJitter = 0.25f;
        [Range(0.5f, 1f)] public float ScatterMinSlope = 0.85f;

        [Header("Colours")]
        public Color Sand = new Color(0.87f, 0.80f, 0.58f);
        public Color Grass = new Color(0.42f, 0.62f, 0.30f);
        public Color Rock = new Color(0.50f, 0.47f, 0.42f);
        public Color SeaFloor = new Color(0.16f, 0.24f, 0.30f);
        [Range(0f, 80f)] public float RockHeight = 10f;
    }
}
