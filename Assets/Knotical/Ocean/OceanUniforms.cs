using UnityEngine;

namespace Knotical
{
    public static class OceanUniforms
    {
        public static readonly int WaveCountId = Shader.PropertyToID("_OceanWaveCount");
        public static readonly int TimeId = Shader.PropertyToID("_OceanTime");
        public static readonly int SeaLevelId = Shader.PropertyToID("_OceanSeaLevel");
        public static readonly int SignificantHeightId = Shader.PropertyToID("_OceanSignificantHeight");
        public static readonly int FadeStartId = Shader.PropertyToID("_OceanFadeStart");
        public static readonly int FadeEndId = Shader.PropertyToID("_OceanFadeEnd");
        public static readonly int WaveAId = Shader.PropertyToID("_OceanWaveA");
        public static readonly int WaveBId = Shader.PropertyToID("_OceanWaveB");

        private static readonly Vector4[] WaveA = new Vector4[OceanSettings.MaxWaves];
        private static readonly Vector4[] WaveB = new Vector4[OceanSettings.MaxWaves];
        private static int packedVersion = -1;

        public static void Push()
        {
            Pack();
            Shader.SetGlobalVectorArray(WaveAId, WaveA);
            Shader.SetGlobalVectorArray(WaveBId, WaveB);
            Shader.SetGlobalInt(WaveCountId, Ocean.WaveSet.Count);
            Shader.SetGlobalFloat(TimeId, (float)Ocean.Time);
            Shader.SetGlobalFloat(SeaLevelId, Ocean.SeaLevel);
            Shader.SetGlobalFloat(SignificantHeightId, Ocean.SignificantHeight);
        }

        public static void SetFade(float startWavelengths, float endWavelengths)
        {
            Shader.SetGlobalFloat(FadeStartId, startWavelengths);
            Shader.SetGlobalFloat(FadeEndId, endWavelengths);
        }

        public static void Apply(ComputeShader shader)
        {
            Pack();
            shader.SetVectorArray(WaveAId, WaveA);
            shader.SetVectorArray(WaveBId, WaveB);
            shader.SetInt(WaveCountId, Ocean.WaveSet.Count);
            shader.SetFloat(TimeId, (float)Ocean.Time);
            shader.SetFloat(SeaLevelId, Ocean.SeaLevel);
            shader.SetFloat(SignificantHeightId, Ocean.SignificantHeight);
        }

        private static void Pack()
        {
            if (packedVersion == Ocean.Version) return;

            Wave[] waves = Ocean.WaveSet.Waves;
            for (int i = 0; i < OceanSettings.MaxWaves; i++)
            {
                if (i < waves.Length)
                {
                    Wave w = waves[i];
                    WaveA[i] = new Vector4(w.Direction.x, w.Direction.y, w.Amplitude, w.Wavelength);
                    WaveB[i] = new Vector4(w.Steepness, w.Phase, w.DepthResponse, w.AngularFrequency);
                }
                else
                {
                    WaveA[i] = new Vector4(1f, 0f, 0f, 1f);
                    WaveB[i] = Vector4.zero;
                }
            }

            packedVersion = Ocean.Version;
        }
    }
}
