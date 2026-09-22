Shader "Knotical/Cliff"
{
    Properties
    {
        _RockColor ("Rock", Color) = (0.55, 0.5, 0.45, 1)
        _GrassColor ("Grass", Color) = (0.42, 0.62, 0.30, 1)
        _GrassFraction ("Grass Cap (fraction of height)", Range(0, 1)) = 0.1
        _ShadeDepth ("Shade Depth", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _KnoticalAmbient;

            CBUFFER_START(UnityPerMaterial)
                float4 _RockColor;
                float4 _GrassColor;
                float _GrassFraction;
                float _ShadeDepth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 height : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float height : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.height = IN.height.y;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                Light light = GetMainLight();
                float3 n = normalize(IN.normalWS);
                float ndl = saturate(dot(n, light.direction));
                float shade = (1.0 - _ShadeDepth) + _ShadeDepth * ndl;
                float3 ambient = SampleSH(n) * (0.35 * _KnoticalAmbient);
                float grass = step(1.0 - _GrassFraction, IN.height);
                float3 albedo = lerp(_RockColor.rgb, _GrassColor.rgb, grass);
                float3 col = albedo * (shade * light.color + ambient);
                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return half4(normalize(IN.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
