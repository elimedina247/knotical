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
        _DuskColor ("Dusk", Color) = (1, 0.45, 0.2, 1)
        _NightZenith ("Night Zenith", Color) = (0.02, 0.04, 0.10, 1)
        _NightHorizon ("Night Horizon", Color) = (0.08, 0.10, 0.18, 1)
        _NightGround ("Night Below Horizon", Color) = (0.04, 0.05, 0.08, 1)
        _MoonColor ("Moon", Color) = (0.85, 0.9, 1, 1)
        _MoonSize ("Moon Size (radians)", Range(0.005, 0.2)) = 0.022
        _MoonIntensity ("Moon Intensity", Range(0, 10)) = 2
        _StarIntensity ("Stars", Range(0, 3)) = 1
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
            float4 _KnoticalMoonDir;
            float _KnoticalNight;
            float _KnoticalDusk;

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
                float4 _DuskColor;
                float4 _NightZenith;
                float4 _NightHorizon;
                float4 _NightGround;
                float4 _MoonColor;
                float _MoonSize;
                float _MoonIntensity;
                float _StarIntensity;
            CBUFFER_END

            float StarField(float3 d, float up)
            {
                float3 scaled = d * 140.0;
                float3 cell = floor(scaled);
                float h = frac(sin(dot(cell, float3(12.9898, 78.233, 37.719))) * 43758.5453);
                float3 local = frac(scaled) - 0.5;
                float spot = 1.0 - smoothstep(0.06, 0.16, length(local));
                return spot * step(0.991, h) * saturate(up * 4.0);
            }

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
                float night = saturate(_KnoticalNight);
                float3 zenith = lerp(_ZenithColor.rgb, _NightZenith.rgb, night);
                float3 horizon = lerp(_HorizonColor.rgb, _NightHorizon.rgb, night);
                float3 below = lerp(_GroundColor.rgb, _NightGround.rgb, night);
                float3 sky = lerp(horizon, zenith, pow(up, _HorizonPower));
                float3 ground = lerp(horizon, below, saturate(-d.y * 6.0));
                float3 col = d.y >= 0.0 ? sky : ground;

                float3 s = normalize(_KnoticalSunDir.xyz);
                float3 flatSun = normalize(float3(s.x, 0.0, s.z) + 1e-4);
                float3 flatDir = normalize(float3(d.x, 0.0, d.z) + 1e-4);
                float duskMask = pow(saturate(dot(flatDir, flatSun) * 0.5 + 0.5), 3.0) * (1.0 - saturate(abs(d.y) * 1.6));
                col += _DuskColor.rgb * (duskMask * _KnoticalDusk * 1.1);

                col += StarField(d, up) * _StarIntensity * night;

                float sunVisible = smoothstep(-0.08, 0.0, s.y);
                float cosA = dot(d, s);
                float ang = acos(clamp(cosA, -1.0, 1.0));
                float disc = 1.0 - smoothstep(_SunSize, _SunSize + _SunEdge, ang);
                float glow = pow(saturate(cosA), 40.0) * 0.6 + pow(saturate(cosA), 6.0) * 0.25;
                col += _SunColor.rgb * (disc * _SunIntensity + glow * _SunGlow) * sunVisible;

                float3 m = normalize(_KnoticalMoonDir.xyz);
                float cosM = dot(d, m);
                float angM = acos(clamp(cosM, -1.0, 1.0));
                float discM = 1.0 - smoothstep(_MoonSize, _MoonSize + _SunEdge, angM);
                float glowM = pow(saturate(cosM), 60.0) * 0.35;
                col += _MoonColor.rgb * (discM * _MoonIntensity + glowM) * night * smoothstep(-0.05, 0.02, m.y);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}
