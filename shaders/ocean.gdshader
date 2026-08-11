shader_type spatial;
render_mode world_vertex_coords, cull_disabled, specular_schlick_ggx;

// ---------------------------------------------------------------------------
// Geometry here must stay in lockstep with Ocean.cs — same spectrum, same
// dispersion, same signs. Buoyancy is computed from the CPU version and drawn
// from this one, so divergence shows up as boats floating in mid-air.
//
// Shading is NOT under that constraint. Anything below vertex() is free to use
// noise and view-dependent terms, because nothing samples it on the CPU.
// ---------------------------------------------------------------------------

// xy = unit direction, z = amplitude (m), w = wavelength (m).
// Array size must match OceanSettings.MaxWaves.
uniform vec4 waves[24];
uniform float wave_phase[24];
uniform int wave_count = 16;
uniform float wave_time = 0.0;
uniform float steepness : hint_range(0.0, 1.0) = 0.55;

uniform vec3 deep_color : source_color = vec3(0.055, 0.235, 0.400);
uniform vec3 shallow_color : source_color = vec3(0.145, 0.545, 0.706);
uniform vec3 crest_color : source_color = vec3(0.325, 0.729, 0.847);
uniform vec3 sky_color : source_color = vec3(0.651, 0.855, 0.933);
uniform vec3 foam_color : source_color = vec3(1.0, 1.0, 1.0);

// Foam appears where the surface folds, not where it is tall. Raise to make
// whitecaps rarer; the useful range is narrow because folding is rare by nature.
uniform float foam_threshold : hint_range(0.0, 0.6, 0.005) = 0.26;
uniform float foam_softness : hint_range(0.0, 0.4, 0.005) = 0.06;
uniform float foam_noise_scale : hint_range(0.01, 1.0) = 0.14;

uniform float fresnel_power : hint_range(0.5, 8.0) = 4.0;
uniform float fresnel_max : hint_range(0.0, 1.0) = 0.82;

const float GRAVITY = 9.81;

varying vec3 v_world_pos;
varying vec3 v_world_normal;
varying float v_crest;   // 0 at trough, 1 at peak
varying float v_fold;    // Gerstner compression; high where the surface pinches

// --- cheap value noise, used only for breaking up shading edges ------------

float hash21(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float value_noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash21(i), hash21(i + vec2(1.0, 0.0)), u.x),
               mix(hash21(i + vec2(0.0, 1.0)), hash21(i + vec2(1.0, 1.0)), u.x), u.y);
}

float fbm(vec2 p) {
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 3; i++) {
        v += a * value_noise(p);
        p *= 2.03;
        a *= 0.5;
    }
    return v;
}

void vertex() {
    vec3 pos = VERTEX;
    vec2 base_xz = pos.xz;

    float dist = length(base_xz - CAMERA_POSITION_WORLD.xz);

    float height = 0.0;
    vec2 pinch = vec2(0.0);
    vec2 slope = vec2(0.0);
    float amplitude_total = 0.0;

    // Jacobian of the horizontal displacement. Where this collapses toward zero the
    // surface is folding over itself, which is physically where whitecaps form.
    float jxx = 0.0;
    float jxz = 0.0;
    float jzz = 0.0;

    for (int i = 0; i < wave_count; i++) {
        vec2 dir = waves[i].xy;
        float amp = waves[i].z;
        float wavelength = waves[i].w;

        // Fade a wave out once the mesh is too coarse to resolve it, or the far
        // field shimmers as vertices slide through high-frequency waves.
        float fade = 1.0 - smoothstep(wavelength * 12.0, wavelength * 32.0, dist);
        if (fade <= 0.0) continue;
        amp *= fade;

        float k = TAU / wavelength;
        float omega = sqrt(GRAVITY * k);
        float phase = k * dot(dir, base_xz) - omega * wave_time + wave_phase[i];

        float s = sin(phase);
        float c = cos(phase);

        height += amp * s;

        // Normalised so crests sharpen without the surface looping through itself
        float q = steepness / (k * max(amp, 0.0001) * float(wave_count));
        pinch += q * amp * dir * c;

        float qak = q * amp * k;
        jxx += qak * dir.x * dir.x * s;
        jxz += qak * dir.x * dir.y * s;
        jzz += qak * dir.y * dir.y * s;

        slope += dir * (amp * k * c);
        amplitude_total += amp;
    }

    pos.y += height;
    pos.xz += pinch;
    VERTEX = pos;

    v_world_pos = pos;
    v_world_normal = normalize(vec3(-slope.x, 1.0, -slope.y));
    v_crest = amplitude_total > 0.0001
        ? clamp(height / amplitude_total * 0.5 + 0.5, 0.0, 1.0)
        : 0.5;

    float jacobian = (1.0 - jxx) * (1.0 - jzz) - jxz * jxz;
    v_fold = clamp(1.0 - jacobian, 0.0, 1.0);
}

void fragment() {
    vec3 n = normalize(v_world_normal);
    vec3 n_view = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);

    // Body colour: smooth, no floor(). Banding by wave height draws topographic
    // contours along every crest. The stepped bands we actually want are keyed to
    // water depth over the seabed, so they arrive with the seabed.
    vec3 body = mix(deep_color, shallow_color, smoothstep(0.30, 0.86, v_crest));
    body = mix(body, crest_color, smoothstep(0.74, 1.0, v_crest));

    // Very low frequency tint so open water is not a single flat colour
    float tint = fbm(v_world_pos.xz * 0.0035);
    body *= mix(0.94, 1.07, tint);

    // VIEW is view-space, so the normal has to be converted before they meet.
    float fres = pow(1.0 - clamp(dot(n_view, VIEW), 0.0, 1.0), fresnel_power);
    vec3 col = mix(body, sky_color, clamp(fres, 0.0, fresnel_max));

    // Foam: folding, modulated by drifting noise so the edge is torn rather than
    // a clean contour. Threshold stays hard — the decided edge is the art direction.
    vec2 foam_uv = v_world_pos.xz * foam_noise_scale;
    float n1 = fbm(foam_uv + vec2(wave_time * 0.06, wave_time * -0.03));
    float n2 = fbm(foam_uv * 2.7 - vec2(wave_time * 0.04, 0.0));
    float foam_mask = v_fold * (0.45 + 1.25 * n1 * n2 * 2.0);

    float foam = smoothstep(foam_threshold, foam_threshold + foam_softness, foam_mask);
    col = mix(col, foam_color, foam);

    ALBEDO = col;
    ROUGHNESS = mix(0.10, 0.88, foam);
    METALLIC = 0.0;
    SPECULAR = mix(0.65, 0.10, foam);
    NORMAL = n_view;
}
