Shader "Knotical/FoamDecay"
{
    Properties
    {
        _MainTex ("Previous", 2D) = "black" {}
        _Shift ("Shift", Vector) = (0, 0, 0, 0)
        _Decay ("Decay", Float) = 0.99
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            Texture2D<float4> _MainTex;
            SamplerState sampler_MainTex;
            float4 _Shift;
            float _Decay;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv + _Shift.xy;
                if (any(uv < 0.0) || any(uv > 1.0)) return 0.0;
                float v = _MainTex.Sample(sampler_MainTex, uv).r * _Decay;
                return float4(v, v, v, v);
            }
            ENDHLSL
        }
    }
}
