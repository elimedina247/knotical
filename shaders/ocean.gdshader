shader_type spatial;
render_mode world_vertex_coords, cull_disabled, specular_schlick_ggx, depth_draw_always;

#include "res://shaders/foam.gdshaderinc"

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

// Sum of k*amplitude over the live spectrum, supplied by Ocean.cs. Normalising the
// Gerstner pinch by this — rather than by each wave's own amplitude — is what lets a
// component's horizontal displacement vanish as the wind takes its height away.
uniform float ka_sum = 1.0;

instance uniform float inner_extent = 0.0;

uniform float camera_submerged = 0.0;

uniform float significant_height = 2.0;

uniform vec3 water_ramp[8];
uniform int water_ramp_size = 6;
uniform vec3 sky_color : source_color = vec3(0.651, 0.855, 0.933);
uniform vec3 foam_color : source_color = vec3(1.0, 1.0, 1.0);

uniform float depth_contrast : hint_range(0.0, 1.0) = 0.55;
uniform float band_softness : hint_range(0.0, 1.0) = 0.25;
uniform float slope_range : hint_range(0.05, 0.6) = 0.22;
uniform float band_slope_weight : hint_range(0.0, 1.0) = 0.65;

uniform sampler2D depth_texture : hint_depth_texture;

uniform float depth_fade : hint_range(0.5, 120.0) = 22.0;
uniform float sand_visibility : hint_range(0.1, 120.0) = 30.0;
uniform float view_angle_influence : hint_range(0.0, 1.0) = 0.45;
uniform float min_opacity : hint_range(0.0, 1.0) = 0.3;
uniform float foam_scale = 0.04;
uniform float foam_warp : hint_range(0.0, 1.5) = 0.35;
uniform float foam_drift : hint_range(0.0, 0.3) = 0.03;
uniform float foam_edge : hint_range(0.002, 0.3) = 0.005;
uniform float surface_foam_amount : hint_range(0.0, 1.0) = 0.5;
uniform float surface_foam_opacity : hint_range(0.0, 1.0) = 0.20;
uniform float crest_foam_opacity : hint_range(0.0, 1.0) = 0.85;

// Foam appears where the surface folds, not where it is tall. Raise to make
// whitecaps rarer; the useful range is narrow because folding is rare by nature.
uniform float foam_threshold : hint_range(0.0, 0.6, 0.005) = 0.24;
uniform float foam_softness : hint_range(0.0, 0.4, 0.005) = 0.04;

uniform float fresnel_power : hint_range(0.5, 8.0) = 4.0;
uniform float fresnel_max : hint_range(0.0, 1.0) = 0.38;

uniform float detail_scale : hint_range(0.05, 2.0) = 0.35;
uniform float detail_strength : hint_range(0.0, 1.0) = 0.35;
uniform float detail_far : hint_range(50.0, 2000.0) = 600.0;

const float GRAVITY = 9.81;

varying vec3 v_world_pos;
varying vec3 v_world_normal;
varying float v_height;
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

    // Jacobian of the horizontal displacement. Where this collapses toward zero the
    // surface is folding over itself, which is physically where whitecaps form.
    float jxx = 0.0;
    float jxz = 0.0;
    float jzz = 0.0;

    // Loop-invariant: every component shares one steepness budget, divided between them
    // in proportion to k*amp. The total sum of k*a*q then lands exactly on `steepness`,
    // and steepness < 1 is the condition for the surface never looping through itself.
    float q = steepness / max(ka_sum, 0.0001);

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

        pinch += q * amp * dir * c;

        float qak = q * amp * k;
        jxx += qak * dir.x * dir.x * s;
        jxz += qak * dir.x * dir.y * s;
        jzz += qak * dir.y * dir.y * s;

        slope += dir * (amp * k * c);
    }

    pos.y += height;
    pos.xz += pinch;
    VERTEX = pos;

    vec3 tangent_x = vec3(1.0 - jxx, slope.x, -jxz);
    vec3 tangent_z = vec3(-jxz, slope.y, 1.0 - jzz);

    v_world_pos = pos;
    v_world_normal = normalize(cross(tangent_z, tangent_x));
    v_height = height / max(significant_height, 0.01);

    float jacobian = (1.0 - jxx) * (1.0 - jzz) - jxz * jxz;
    v_fold = clamp(1.0 - jacobian, 0.0, 1.0);
}

void fragment() {
    if (inner_extent > 0.0) {
        if (camera_submerged > 0.5 && !FRONT_FACING) {
            discard;
        }
        vec2 rel = v_world_pos.xz - CAMERA_POSITION_WORLD.xz;
        if (max(abs(rel.x), abs(rel.y)) < inner_extent) {
            discard;
        }
    }

    vec3 n = normalize(v_world_normal);

    float detail_fade = 1.0 - smoothstep(detail_far * 0.25, detail_far, length(v_world_pos - CAMERA_POSITION_WORLD));
    vec2 duv = v_world_pos.xz * detail_scale;
    vec2 flow = vec2(wave_time * 0.55, wave_time * 0.23);
    float eps = 0.6;
    float d0 = fbm(duv + flow);
    float dgx = fbm(duv + flow + vec2(eps, 0.0));
    float dgz = fbm(duv + flow + vec2(0.0, eps));
    vec3 grad = vec3(d0 - dgx, 0.0, d0 - dgz);

    vec2 duv2 = v_world_pos.xz * detail_scale * 2.7;
    vec2 flow2 = vec2(wave_time * -0.31, wave_time * 0.47);
    float e0 = fbm(duv2 + flow2);
    float egx = fbm(duv2 + flow2 + vec2(eps, 0.0));
    float egz = fbm(duv2 + flow2 + vec2(0.0, eps));
    grad += vec3(e0 - egx, 0.0, e0 - egz) * 0.55;

    vec3 n_detail = normalize(n + grad * (detail_strength * detail_fade / eps));

    vec3 n_view = normalize((VIEW_MATRIX * vec4(n_detail, 0.0)).xyz);

    float depth_raw = texture(depth_texture, SCREEN_UV).x;
    vec3 ndc = vec3(SCREEN_UV * 2.0 - 1.0, depth_raw);
    vec4 scene_h = INV_VIEW_MATRIX * (INV_PROJECTION_MATRIX * vec4(ndc, 1.0));
    vec3 scene_world = scene_h.xyz / scene_h.w;

    float thickness = distance(scene_world, v_world_pos);
    float water_depth = max(v_world_pos.y - scene_world.y, 0.0);
    float view_path = mix(water_depth, thickness, view_angle_influence);

    float h = v_height / (1.0 + abs(v_height));
    float height_t = 0.5 + 0.5 * h * depth_contrast;
    float slope_t = clamp((1.0 - n_detail.y) / slope_range, 0.0, 1.0);
    float wave_t = mix(height_t, slope_t, band_slope_weight);

    float shallowness = exp(-water_depth / depth_fade);

    float lo = mix(0.0, 0.42, shallowness);
    float hi = mix(0.58, 1.0, shallowness);
    float raw = min(mix(lo, hi, wave_t), 0.999);

    float q = raw * float(water_ramp_size);
    int band = int(q);
    float edge = smoothstep(0.5 - band_softness * 0.5, 0.5 + band_softness * 0.5, fract(q));
    vec3 body = mix(water_ramp[band], water_ramp[min(band + 1, water_ramp_size - 1)], edge);

    // Very low frequency tint so open water is not a single flat colour
    float tint = fbm(v_world_pos.xz * 0.0035);
    body *= mix(0.94, 1.07, tint);

    // VIEW is view-space, so the normal has to be converted before they meet.
    float fres = pow(1.0 - clamp(dot(n_view, VIEW), 0.0, 1.0), fresnel_power);
    vec3 col = mix(body, sky_color, clamp(fres, 0.0, fresnel_max));

    float alpha = 1.0 - exp(-view_path / sand_visibility);
    alpha = max(alpha, min_opacity);
    alpha = max(alpha, clamp(fres, 0.0, fresnel_max));

    vec2 fp = v_world_pos.xz * foam_scale;
    vec2 drift = vec2(wave_time * foam_drift, wave_time * foam_drift * -0.55);
    vec2 warp = vec2(fbm(fp * 0.8 + drift), fbm(fp * 0.8 + drift + 19.3)) - 0.5;
    fp += warp * foam_warp;

    float fold = smoothstep(foam_threshold, foam_threshold + foam_softness, v_fold * v_fold);

    float radius_scale = mix(mix(1.35, 0.65, surface_foam_amount), 0.25, fold);
    float lace = foam_layer(fp + drift + foam_swirl(fp, wave_time), radius_scale, foam_edge);
    float coverage = mix(1.0 - slope_t * 0.7, 1.0, fold);

    float foam = lace * coverage * detail_fade * mix(surface_foam_opacity, crest_foam_opacity, fold);

    col = mix(col, foam_color, foam);
    alpha = max(alpha, foam);

    ALBEDO = col;
    ALPHA = alpha;
    ROUGHNESS = mix(0.62, 0.9, foam);
    METALLIC = 0.0;
    SPECULAR = 0.15;
    NORMAL = n_view;
}
