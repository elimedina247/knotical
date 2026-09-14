#ifndef KNOTICAL_OCEAN_WAVES_INCLUDED
#define KNOTICAL_OCEAN_WAVES_INCLUDED

#define OCEAN_MAX_WAVES 24
#define OCEAN_GRAVITY 9.81
#define OCEAN_TAU 6.28318530718

int _OceanWaveCount;
float _OceanTime;
float _OceanSeaLevel;
float _OceanSignificantHeight;
float _OceanFadeStart;
float _OceanFadeEnd;
float4 _OceanWaveA[OCEAN_MAX_WAVES];
float4 _OceanWaveB[OCEAN_MAX_WAVES];

float OceanWeight(float wavelength, float dist, float fadeStart, float fadeEnd)
{
    if (fadeEnd <= 0.0) return 1.0;
    return 1.0 - smoothstep(wavelength * fadeStart, wavelength * fadeEnd, dist);
}

float3 OceanSurfacePoint(float2 p, float dist, float fadeStart, float fadeEnd)
{
    float2 pinch = 0.0;
    float height = 0.0;

    [loop]
    for (int i = 0; i < _OceanWaveCount; i++)
    {
        float4 a = _OceanWaveA[i];
        float4 b = _OceanWaveB[i];
        float weight = OceanWeight(a.w, dist, fadeStart, fadeEnd);
        float k = OCEAN_TAU / a.w;
        float omega = b.w;
        float phase = k * dot(a.xy, p) - omega * _OceanTime + b.y;
        float q = b.x / (k * _OceanWaveCount);

        pinch += a.xy * (q * weight * cos(phase));
        height += a.z * weight * sin(phase);
    }

    return float3(p.x + pinch.x, _OceanSeaLevel + height, p.y + pinch.y);
}

void OceanFrame(float2 p, float dist, float fadeStart, float fadeEnd, out float3 normal, out float jacobian)
{
    float dx = 0.0;
    float dz = 0.0;
    float jxx = 0.0;
    float jxz = 0.0;
    float jzz = 0.0;

    [loop]
    for (int i = 0; i < _OceanWaveCount; i++)
    {
        float4 a = _OceanWaveA[i];
        float4 b = _OceanWaveB[i];
        float weight = OceanWeight(a.w, dist, fadeStart, fadeEnd);
        float k = OCEAN_TAU / a.w;
        float omega = b.w;
        float phase = k * dot(a.xy, p) - omega * _OceanTime + b.y;
        float q = b.x / (k * _OceanWaveCount);

        float s = sin(phase);
        float c = cos(phase) * a.z * k * weight;

        dx += c * a.x;
        dz += c * a.y;

        float qak = q * k * s * weight;
        jxx += qak * a.x * a.x;
        jxz += qak * a.x * a.y;
        jzz += qak * a.y * a.y;
    }

    float3 tangentX = float3(1.0 - jxx, dx, -jxz);
    float3 tangentZ = float3(-jxz, dz, 1.0 - jzz);

    normal = normalize(cross(tangentZ, tangentX));
    jacobian = (1.0 - jxx) * (1.0 - jzz) - jxz * jxz;
}

float3 OceanNormal(float2 p, float dist, float fadeStart, float fadeEnd)
{
    float3 normal;
    float jacobian;
    OceanFrame(p, dist, fadeStart, fadeEnd, normal, jacobian);
    return normal;
}

float OceanFoldAgo(float2 p, float dist, float fadeStart, float fadeEnd, float secondsAgo)
{
    float jxx = 0.0;
    float jxz = 0.0;
    float jzz = 0.0;

    [loop]
    for (int i = 0; i < _OceanWaveCount; i++)
    {
        float4 a = _OceanWaveA[i];
        float4 b = _OceanWaveB[i];
        float weight = OceanWeight(a.w, dist, fadeStart, fadeEnd);
        float k = OCEAN_TAU / a.w;
        float omega = b.w;
        float phase = k * dot(a.xy, p) - omega * (_OceanTime - secondsAgo) + b.y;
        float q = b.x / (k * _OceanWaveCount);

        float qak = q * k * sin(phase) * weight;
        jxx += qak * a.x * a.x;
        jxz += qak * a.x * a.y;
        jzz += qak * a.y * a.y;
    }

    float jacobian = (1.0 - jxx) * (1.0 - jzz) - jxz * jxz;
    return saturate(1.0 - jacobian);
}

float OceanHash(float2 p)
{
    p = frac(p * float2(0.1031, 0.1030));
    p += dot(p, p.yx + 33.33);
    return frac((p.x + p.y) * p.x);
}

float OceanValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = OceanHash(i);
    float b = OceanHash(i + float2(1.0, 0.0));
    float c = OceanHash(i + float2(0.0, 1.0));
    float d = OceanHash(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float OceanFbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++)
    {
        v += a * OceanValueNoise(p);
        p *= 2.03;
        a *= 0.5;
    }
    return v;
}

float OceanMod(float x, float y)
{
    return x - y * floor(x / y);
}

static const float3 OCEAN_FOAM_DISCS[75] =
{
    float3(0.37378, 0.277169, 0.0268181), float3(0.0317477, 0.540372, 0.0193742), float3(0.430044, 0.882218, 0.0232337),
    float3(0.641033, 0.695106, 0.0117864), float3(0.0146398, 0.0791346, 0.0299458), float3(0.43871, 0.394445, 0.0289087),
    float3(0.909446, 0.878141, 0.028466), float3(0.310149, 0.686637, 0.0128496), float3(0.928617, 0.195986, 0.0152041),
    float3(0.0438506, 0.868153, 0.0268601), float3(0.308619, 0.194937, 0.00806102), float3(0.349922, 0.449714, 0.00928667),
    float3(0.0449556, 0.953415, 0.023126), float3(0.117761, 0.503309, 0.0151272), float3(0.563517, 0.244991, 0.0292322),
    float3(0.566936, 0.954457, 0.00981141), float3(0.0489944, 0.200931, 0.0178746), float3(0.569297, 0.624893, 0.0132408),
    float3(0.298347, 0.710972, 0.0114426), float3(0.878141, 0.771279, 0.00322719), float3(0.150995, 0.376221, 0.00216157),
    float3(0.119673, 0.541984, 0.0124621), float3(0.629598, 0.295629, 0.0198736), float3(0.334357, 0.266278, 0.0187145),
    float3(0.918044, 0.968163, 0.0182928), float3(0.965445, 0.505026, 0.006348), float3(0.514847, 0.865444, 0.00623523),
    float3(0.710575, 0.0415131, 0.00322689), float3(0.71403, 0.576945, 0.0215641), float3(0.748873, 0.413325, 0.0110795),
    float3(0.0623365, 0.896713, 0.0236203), float3(0.980482, 0.473849, 0.00573439), float3(0.647463, 0.654349, 0.0188713),
    float3(0.651406, 0.981297, 0.00710875), float3(0.428928, 0.382426, 0.0298806), float3(0.811545, 0.62568, 0.00265539),
    float3(0.400787, 0.74162, 0.00486609), float3(0.331283, 0.418536, 0.00598028), float3(0.894762, 0.0657997, 0.00760375),
    float3(0.525104, 0.572233, 0.0141796), float3(0.431526, 0.911372, 0.0213234), float3(0.658212, 0.910553, 0.000741023),
    float3(0.514523, 0.243263, 0.0270685), float3(0.0249494, 0.252872, 0.00876653), float3(0.502214, 0.47269, 0.0234534),
    float3(0.693271, 0.431469, 0.0246533), float3(0.415, 0.884418, 0.0271696), float3(0.149073, 0.41204, 0.00497198),
    float3(0.533816, 0.897634, 0.00650833), float3(0.0409132, 0.83406, 0.0191398), float3(0.638585, 0.646019, 0.0206129),
    float3(0.660342, 0.966541, 0.0053511), float3(0.513783, 0.142233, 0.00471653), float3(0.124305, 0.644263, 0.00116724),
    float3(0.99871, 0.583864, 0.0107329), float3(0.894879, 0.233289, 0.00667092), float3(0.246286, 0.682766, 0.00411623),
    float3(0.0761895, 0.16327, 0.0145935), float3(0.949386, 0.802936, 0.0100873), float3(0.480122, 0.196554, 0.0110185),
    float3(0.896854, 0.803707, 0.013969), float3(0.292865, 0.762973, 0.00566413), float3(0.0995585, 0.117457, 0.00869407),
    float3(0.377713, 0.00335442, 0.0063147), float3(0.506365, 0.531118, 0.0144016), float3(0.408806, 0.894771, 0.0243923),
    float3(0.143579, 0.85138, 0.00418529), float3(0.0902811, 0.181775, 0.0108896), float3(0.780695, 0.394644, 0.00475475),
    float3(0.298036, 0.625531, 0.00325285), float3(0.218423, 0.714537, 0.00157212), float3(0.658836, 0.159556, 0.00225897),
    float3(0.987324, 0.146545, 0.0288391), float3(0.222646, 0.251694, 0.00092276), float3(0.159826, 0.528063, 0.00605293),
};

float OceanFoamDisc(float2 pos, float2 c, float r, float edge)
{
    c = abs(pos - c);
    c = min(c, 1.0 - c);
    return smoothstep(0.0, edge, r - sqrt(dot(c, c))) * -1.0;
}

float OceanFoamLayer(float2 uv, float radiusScale, float edge)
{
    uv = frac(uv);
    float ret = 1.0;
    [loop]
    for (int i = 0; i < 75; i++)
    {
        ret += OceanFoamDisc(uv, OCEAN_FOAM_DISCS[i].xy, sqrt(OCEAN_FOAM_DISCS[i].z) * radiusScale, edge);
    }
    return max(ret, 0.0);
}

float2 OceanFoamSwirl(float2 uv, float t)
{
    float d1 = t * 0.07 + OceanMod(uv.x + uv.y, OCEAN_TAU);
    float d2 = t * 0.5 + OceanMod((uv.x + uv.y + 0.25) * 1.3, 3.0 * OCEAN_TAU);
    return float2(sin(d1) * 0.15 + sin(d2) * 0.05, cos(d1) * 0.15 + cos(d2) * 0.05);
}

#endif
