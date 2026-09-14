Shader "Knotical/Sky"
{
    Properties
    {
        _ZenithColor ("Zenith", Color) = (0.25, 0.52, 0.82, 1)
        _HorizonColor ("Horizon", Color) = (0.74, 0.89, 0.95, 1)
        _GroundColor ("Below Horizon", Color) = (0.55, 0.70, 0.80, 1)
        _HorizonPower ("Horizon Spread", Range(0.2, 4)) = 0.6
        _SunColor ("Sun", Color) = (1, 0.95, 0.78, 1)
        _SunSize ("Sun Size (radians)", Range(0.005, 0.2)) = 0.035
        _SunEdge ("Sun Edge", Range(0.001, 0.05)) = 0.004
        _SunGlow ("Sun Glow", Range(0, 2)) = 0.8
        _SunIntensity ("Sun Intensity", Range(0, 10)) = 6
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _KnoticalSunDir;

            CBUFFER_START(UnityPerMaterial)
                float4 _ZenithColor;
                float4 _HorizonColor;
                float4 _GroundColor;
                float _HorizonPower;
                float4 _SunColor;
                float _SunSize;
                float _SunEdge;
                float _SunGlow;
                float _SunIntensity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.dir = IN.positionOS.xyz;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float3 d = normalize(IN.dir);
                float up = saturate(d.y);
                float3 sky = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(up, _HorizonPower));
                float3 ground = lerp(_HorizonColor.rgb, _GroundColor.rgb, saturate(-d.y * 6.0));
                float3 col = d.y >= 0.0 ? sky : ground;

                float3 s = normalize(_KnoticalSunDir.xyz);
                float cosA = dot(d, s);
                float ang = acos(clamp(cosA, -1.0, 1.0));
                float disc = 1.0 - smoothstep(_SunSize, _SunSize + _SunEdge, ang);
                float glow = pow(saturate(cosA), 40.0) * 0.6 + pow(saturate(cosA), 6.0) * 0.25;
                col += _SunColor.rgb * (disc * _SunIntensity + glow * _SunGlow);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
