Shader "Knotical/OceanSurface"
{
    Properties
    {
        [Enum(Off, 0, Grey, 1, Normals, 2, Height, 3, Fold, 4, Foam, 5, Depth, 6)] _DebugView ("Debug View (OceanSurface overrides)", Float) = 0

        [Header(Colour)]
        _ShallowColor ("Shallow Color", Color) = (0.592, 0.875, 0.941, 1)
        _DeepColor ("Deep Color", Color) = (0.07, 0.29, 0.62, 1)
        _SkyColor ("Sky Color", Color) = (0.74, 0.89, 0.95, 1)
        _FoamColor ("Foam Color", Color) = (0.97, 0.99, 1, 1)
        _DepthFade ("Shallow Depth (m)", Range(0.5, 120)) = 12
        _DepthContrast ("Height Contrast", Range(0, 1)) = 0.55
        _BandSoftness ("Band Softness", Range(0, 1)) = 0.08
        _SlopeRange ("Slope Range", Range(0.05, 0.6)) = 0.22
        _BandSlopeWeight ("Slope Weight", Range(0, 1)) = 0.65
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 4
        _FresnelMax ("Fresnel Max", Range(0, 1)) = 0.16

        [Header(Light)]
        _LitBand ("Lit Band", Range(0, 1)) = 0.2
        _ShadeLevel ("Shade Level", Range(0, 1)) = 0.82
        _GlintStrength ("Glint Strength", Range(0, 2)) = 0.35
        _GlintSize ("Glint Size", Range(10, 600)) = 300
        _SssColor ("Scatter Color", Color) = (0.25, 0.85, 0.75, 1)
        _SssPower ("Scatter Focus", Range(1, 16)) = 4
        _SssStrength ("Scatter Strength", Range(0, 2)) = 0.25

        [Header(Detail)]
        [NoScaleOffset] _DetailFlipbook ("FFT Normal Flipbook (8x8)", 2D) = "black" {}
        _DetailEnabled ("Flipbook Enabled", Float) = 0
        _DetailStrength ("Detail Strength", Range(0, 2)) = 0.35
        _DetailFar ("Detail Far (m)", Range(50, 2000)) = 600
        _DetailTile ("Detail Tile (m)", Range(4, 100)) = 22
        _DetailFps ("Detail FPS", Range(1, 60)) = 14

        [Header(Foam Sources)]
        _CrestHeight ("Crest Height (x significant)", Range(0, 1.5)) = 0.35
        _CrestHeightSoft ("Crest Height Softness", Range(0.01, 1)) = 0.2
        _CapThreshold ("Fold Threshold", Range(0.05, 0.6)) = 0.25
        _CapSoftness ("Fold Softness", Range(0.005, 0.3)) = 0.1
        _TrailSeconds ("Trail Seconds", Range(0.5, 8)) = 3
        _TrailOpacity ("Trail Strength", Range(0, 1)) = 0.6
        _FoamFar ("Foam Far (m)", Range(100, 4000)) = 1500

        [Header(Foam Pattern)]
        _FoamScale ("Pattern Scale", Float) = 0.045
        _FoamDrift ("Pattern Drift", Range(0, 0.3)) = 0.03
        _FoamWarp ("Pattern Warp", Range(0, 1.5)) = 0.4
        _FoamHoleSize ("Hole Size", Range(0.2, 3)) = 1.9
        _FoamEdge ("Hole Edge", Range(0.002, 0.3)) = 0.01
        _FoamBlotch ("Blotchiness", Range(0, 2)) = 1.4
        _FoamCut ("Cut", Range(0, 1)) = 0.55
        _FoamCutSoft ("Cut Softness", Range(0.005, 0.5)) = 0.04
        _SurfaceFoamOpacity ("Loose Foam Opacity", Range(0, 1)) = 0

        [Header(Shore Lines)]
        _ShoreWashDepth ("Wash Depth", Range(0.02, 2)) = 0.22
        _ShoreLineDepth ("Line Depth", Range(0.05, 4)) = 0.7
        _ShoreLineWidth ("Line Width", Range(0.02, 1)) = 0.14
        _ShoreSecondDepth ("Second Line Depth", Range(0.05, 6)) = 1.6
        _ShoreSecondWidth ("Second Line Width", Range(0.02, 1)) = 0.1
        _ShoreWobble ("Shore Wobble", Range(0, 1)) = 0.3
        _ShoreGap ("Shore Gap", Range(0, 1)) = 0.35

        _InnerExtent ("Inner Extent (set by OceanSurface)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "OceanWaves.hlsl"

            Texture2D<float4> _DetailFlipbook;
            TEXTURE2D(_FoamCapture);
            SAMPLER(sampler_FoamCapture);
            float4 _FoamCaptureParams;

            CBUFFER_START(UnityPerMaterial)
                float _DebugView;
                float4 _ShallowColor;
                float4 _DeepColor;
                float4 _SkyColor;
                float4 _FoamColor;
                float _DepthFade;
                float _DepthContrast;
                float _BandSoftness;
                float _SlopeRange;
                float _BandSlopeWeight;
                float _FresnelPower;
                float _FresnelMax;
                float _LitBand;
                float _ShadeLevel;
                float _GlintStrength;
                float _GlintSize;
                float4 _SssColor;
                float _SssPower;
                float _SssStrength;
                float _DetailEnabled;
                float _DetailStrength;
                float _DetailFar;
                float _DetailTile;
                float _DetailFps;
                float _CrestHeight;
                float _CrestHeightSoft;
                float _CapThreshold;
                float _CapSoftness;
                float _TrailSeconds;
                float _TrailOpacity;
                float _FoamFar;
                float _FoamScale;
                float _FoamDrift;
                float _FoamWarp;
                float _FoamHoleSize;
                float _FoamEdge;
                float _FoamBlotch;
                float _FoamCut;
                float _FoamCutSoft;
                float _SurfaceFoamOpacity;
                float _ShoreWashDepth;
                float _ShoreLineDepth;
                float _ShoreLineWidth;
                float _ShoreSecondDepth;
                float _ShoreSecondWidth;
                float _ShoreWobble;
                float _ShoreGap;
                float _InnerExtent;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 param : TEXCOORD1;
                float fadeDist : TEXCOORD2;
                float trail : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            float2 FlipSample(float2 uv, float frame)
            {
                float f = OceanMod(floor(frame), 64.0);
                int2 tileBase = int2((int)OceanMod(f, 8.0), (int)floor(f / 8.0)) * 512;
                float2 p = frac(uv) * 512.0 - 0.5;
                float2 i = floor(p);
                float2 fr = p - i;
                int2 i00 = int2(OceanMod(i.x, 512.0), OceanMod(i.y, 512.0));
                int2 i11 = int2(OceanMod(i.x + 1.0, 512.0), OceanMod(i.y + 1.0, 512.0));
                int2 t00 = tileBase + i00;
                int2 t10 = tileBase + int2(i11.x, i00.y);
                int2 t01 = tileBase + int2(i00.x, i11.y);
                int2 t11 = tileBase + i11;
                float2 h00 = _DetailFlipbook.Load(int3(t00.x, 4095 - t00.y, 0)).gb;
                float2 h10 = _DetailFlipbook.Load(int3(t10.x, 4095 - t10.y, 0)).gb;
                float2 h01 = _DetailFlipbook.Load(int3(t01.x, 4095 - t01.y, 0)).gb;
                float2 h11 = _DetailFlipbook.Load(int3(t11.x, 4095 - t11.y, 0)).gb;
                return lerp(lerp(h00, h10, fr.x), lerp(h01, h11, fr.x), fr.y);
            }

            float3 FlipGrad(float2 uv, float frame)
            {
                float2 s = lerp(FlipSample(uv, frame), FlipSample(uv, frame + 1.0), frac(frame));
                float2 hor = s * 2.0 - 1.0;
                return float3(hor.y, 0.0, hor.x);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 flatWS = TransformObjectToWorld(IN.positionOS.xyz);
                float2 p = flatWS.xz;
                float dist = distance(p, _WorldSpaceCameraPos.xz);
                float3 surface = OceanSurfacePoint(p, dist, _OceanFadeStart, _OceanFadeEnd);

                float3 taus = float3(1.0, 2.0, 3.0) * (_TrailSeconds / 3.0);
                float trail = OceanFoldAgo(p, dist, _OceanFadeStart, _OceanFadeEnd, taus.x) * 0.9;
                trail = max(trail, OceanFoldAgo(p, dist, _OceanFadeStart, _OceanFadeEnd, taus.y) * 0.7);
                trail = max(trail, OceanFoldAgo(p, dist, _OceanFadeStart, _OceanFadeEnd, taus.z) * 0.5);

                OUT.positionWS = surface;
                OUT.positionCS = TransformWorldToHClip(surface);
                OUT.param = p;
                OUT.fadeDist = dist;
                OUT.trail = trail;
                OUT.fogFactor = ComputeFogFactor(OUT.positionCS.z);
                return OUT;
            }

            half4 frag(Varyings IN, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                float2 relCam = IN.positionWS.xz - _WorldSpaceCameraPos.xz;
                if (_InnerExtent > 0.0 && max(abs(relCam.x), abs(relCam.y)) < _InnerExtent) discard;

                float3 n;
                float jacobian;
                OceanFrame(IN.param, IN.fadeDist, _OceanFadeStart, _OceanFadeEnd, n, jacobian);
                n *= IS_FRONT_VFACE(face, 1.0, -1.0);
                float fold = saturate(1.0 - jacobian);

                float3 view = normalize(_WorldSpaceCameraPos - IN.positionWS);
                float camDist = length(IN.positionWS - _WorldSpaceCameraPos);
                float detailFade = 1.0 - smoothstep(_DetailFar * 0.25, _DetailFar, camDist);
                float foamFade = 1.0 - smoothstep(_FoamFar * 0.5, _FoamFar, camDist);

                float3 grad;
                if (_DetailEnabled > 0.5)
                {
                    float frame = _OceanTime * _DetailFps;
                    grad = FlipGrad(IN.positionWS.xz / _DetailTile, frame);
                    grad += FlipGrad(IN.positionWS.xz / (_DetailTile * 0.313) + float2(37.7, 11.3), frame * 1.31 + 17.0) * 0.45;
                }
                else
                {
                    float2 duv = IN.positionWS.xz * 0.35;
                    float2 flow = float2(_OceanTime * 0.55, _OceanTime * 0.23);
                    float eps = 0.6;
                    float d0 = OceanFbm(duv + flow);
                    float dgx = OceanFbm(duv + flow + float2(eps, 0.0));
                    float dgz = OceanFbm(duv + flow + float2(0.0, eps));
                    grad = float3(d0 - dgx, 0.0, d0 - dgz) / eps;
                }
                float3 nDetail = normalize(n + grad * (_DetailStrength * detailFade));

                float2 uv = GetNormalizedScreenSpaceUV(IN.positionCS);
                float rawDepth = SampleSceneDepth(uv);
                #if UNITY_REVERSED_Z
                    bool atFar = rawDepth <= 0.000001;
                #else
                    bool atFar = rawDepth >= 0.999999;
                #endif
                float3 sceneWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float waterDepth = atFar ? 10000.0 : max(IN.positionWS.y - sceneWS.y, 0.0);

                Light light = GetMainLight();

                float rel = (IN.positionWS.y - _OceanSeaLevel) / max(_OceanSignificantHeight, 0.01);
                float h = rel / (1.0 + abs(rel));
                float heightT = 0.5 + 0.5 * h * _DepthContrast;
                float slopeT = saturate((1.0 - nDetail.y) / _SlopeRange);
                float waveT = lerp(heightT, slopeT, _BandSlopeWeight);

                float shallowness = exp(-waterDepth / _DepthFade);
                float lo = lerp(0.0, 0.42, shallowness);
                float hi = lerp(0.58, 1.0, shallowness);
                float rampT = min(lerp(lo, hi, waveT), 0.999);

                float q = rampT * 6.0;
                int band = (int)q;
                float edge = smoothstep(0.5 - _BandSoftness * 0.5, 0.5 + _BandSoftness * 0.5, frac(q));
                float3 rampA = lerp(_DeepColor.rgb, _ShallowColor.rgb, band / 5.0);
                float3 rampB = lerp(_DeepColor.rgb, _ShallowColor.rgb, min(band + 1, 5) / 5.0);
                float3 body = lerp(rampA, rampB, edge);
                body *= lerp(0.94, 1.07, OceanFbm(IN.positionWS.xz * 0.0035));

                float fres = pow(1.0 - saturate(dot(nDetail, view)), _FresnelPower);
                float3 col = lerp(body, _SkyColor.rgb, clamp(fres, 0.0, _FresnelMax));

                float towardSun = saturate(dot(view, -light.direction));
                float sss = pow(towardSun, _SssPower) * saturate(rel) * _SssStrength;
                col = lerp(col, _SssColor.rgb, saturate(sss));

                float2 drift = float2(_OceanTime * _FoamDrift, _OceanTime * _FoamDrift * -0.55);
                float2 fp = IN.positionWS.xz * _FoamScale;
                float2 warp = float2(OceanFbm(fp * 0.8 + drift), OceanFbm(fp * 0.8 + drift + 19.3)) - 0.5;
                fp += warp * _FoamWarp;

                float tear = (OceanFbm(IN.positionWS.xz * 0.9 + drift * 8.0) - 0.5) * 0.12;
                float crestFold = saturate((fold + tear - _CapThreshold) / _CapSoftness);
                float crestHigh = saturate((rel + tear * 2.0 - _CrestHeight) / _CrestHeightSoft);
                float crestField = max(crestFold, crestHigh);
                float trailField = saturate((IN.trail + tear * 0.6 - _CapThreshold) / (_CapSoftness * 2.0)) * _TrailOpacity;

                float2 capUv = (IN.positionWS.xz - _FoamCaptureParams.xy) / max(2.0 * _FoamCaptureParams.z, 1.0) + 0.5;
                float capture = 0.0;
                if (_FoamCaptureParams.z > 0.0 && all(capUv > 0.0) && all(capUv < 1.0))
                {
                    capture = SAMPLE_TEXTURE2D(_FoamCapture, sampler_FoamCapture, capUv).r * _FoamCaptureParams.w;
                }

                float wobble = sin(_OceanTime * 1.1 + OceanFbm(IN.positionWS.xz * 0.12) * OCEAN_TAU) * _ShoreWobble;
                float gap = smoothstep(_ShoreGap - 0.15, _ShoreGap + 0.15, OceanFbm(IN.positionWS.xz * 0.35 + drift * 3.0));
                float wash = 1.0 - smoothstep(_ShoreWashDepth * 0.7, _ShoreWashDepth * (1.0 + wobble * 0.6), waterDepth);
                float lineA = 1.0 - smoothstep(_ShoreLineWidth * 0.7, _ShoreLineWidth, abs(waterDepth - _ShoreLineDepth * (1.0 + wobble * 0.5)));
                float lineB = 1.0 - smoothstep(_ShoreSecondWidth * 0.7, _ShoreSecondWidth, abs(waterDepth - _ShoreSecondDepth * (1.0 - wobble * 0.4)));
                float shore = max(wash, max(lineA * gap, lineB * gap * 0.85)) * step(0.001, waterDepth);

                float field = max(max(crestField, trailField), max(saturate(capture * 1.2), shore));

                float lace = OceanFoamLayer(fp + drift + OceanFoamSwirl(fp, _OceanTime), _FoamHoleSize, _FoamEdge);
                float blotch = OceanFbm(IN.positionWS.xz * _FoamScale * 2.3 + drift * 2.0);
                float shape = field + (blotch - 0.5) * _FoamBlotch * 0.65;
                float foam = smoothstep(_FoamCut - _FoamCutSoft, _FoamCut + _FoamCutSoft, shape) * lace * foamFade;

                float loose = smoothstep(0.88, 0.95, blotch) * lace * _SurfaceFoamOpacity * detailFade;
                foam = max(foam, loose);

                float3 halfVec = normalize(light.direction + view);
                float glint = pow(saturate(dot(nDetail, halfVec)), _GlintSize);
                glint = smoothstep(0.3, 0.5, glint) * _GlintStrength * detailFade * (1.0 - foam) * smoothstep(-0.02, 0.1, light.direction.y);

                float ndl = dot(n, light.direction);
                float lit = smoothstep(_LitBand - 0.03, _LitBand + 0.03, ndl);
                float3 lighting = light.color * lerp(_ShadeLevel, 1.0, lit) + SampleSH(n) * 0.15;

                if (_DebugView > 0.5)
                {
                    if (_DebugView < 1.5) return half4((0.15 + 0.85 * saturate(dot(n, normalize(float3(0.8, 0.35, 0.45))))).xxx, 1.0);
                    if (_DebugView < 2.5) return half4(nDetail * 0.5 + 0.5, 1.0);
                    if (_DebugView < 3.5) return half4(saturate(rel * 0.5 + 0.5).xxx, 1.0);
                    if (_DebugView < 4.5) return half4(crestFold, crestHigh, IN.trail * 2.0, 1.0);
                    if (_DebugView < 5.5) return half4(foam.xxx, 1.0);
                    return half4(saturate(waterDepth / 20.0), atFar ? 1.0 : 0.0, saturate(waterDepth / 2.0), 1.0);
                }

                float3 final = col * lighting + glint * lerp(_SkyColor.rgb, 1.0, 0.6);
                final = lerp(final, _FoamColor.rgb * light.color * lerp(0.86, 1.0, lit), foam);
                final = MixFog(final, IN.fogFactor);
                return half4(final, 1.0);
            }
            ENDHLSL
        }
    }
}
