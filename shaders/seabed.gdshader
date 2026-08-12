shader_type spatial;
render_mode world_vertex_coords;

#include "res://shaders/foam.gdshaderinc"

// ---------------------------------------------------------------------------
// The water is transparent, so this is the one and only seabed: it is seen
// directly through the surface from above and below. The foam shadow here is
// the surface's foam_layer pattern from foam.gdshaderinc, projected along the sun
// direction — same function, same clock, or the shadows stop lining up with the
// foam that casts them. The shape uniforms are copied off ocean.gdshader by
// SeabedSurface.cs at load, so tune the foam there and this follows.
// ---------------------------------------------------------------------------

uniform vec3 sand_color : source_color = vec3(0.886, 0.816, 0.635);
uniform vec3 seabed_dark_color : source_color = vec3(0.431, 0.478, 0.314);
uniform vec3 murk_color : source_color = vec3(0.247, 0.596, 0.541);
uniform float seabed_patch_scale = 0.022;
uniform float seabed_contrast : hint_range(0.0, 0.4) = 0.10;
uniform float foam_scale = 0.04;
uniform float foam_warp : hint_range(0.0, 1.5) = 0.35;
uniform float foam_drift : hint_range(0.0, 0.3) = 0.03;
uniform float foam_edge : hint_range(0.002, 0.3) = 0.005;
uniform float surface_foam_amount : hint_range(0.0, 1.0) = 0.5;
uniform float wave_time = 0.0;
uniform float murk_distance : hint_range(5.0, 400.0) = 90.0;

uniform vec3 light_direction = vec3(0.0, 1.0, 0.0);
uniform float foam_shadow_strength : hint_range(0.0, 1.0) = 0.35;

varying vec3 v_world_pos;

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

vec3 seabed_albedo(vec2 p) {
    float patch = fbm(p * seabed_patch_scale);
    float reef = fbm(p * seabed_patch_scale * 3.1 + 31.7);
    float grain = fbm(p * seabed_patch_scale * 11.0);

    vec3 c = mix(sand_color, seabed_dark_color, smoothstep(0.44, 0.44 + seabed_contrast, patch));
    c = mix(c, seabed_dark_color * 0.8, smoothstep(0.58, 0.58 + seabed_contrast, reef) * 0.7);
    return c * mix(0.88, 1.12, grain);
}

void vertex() {
    v_world_pos = VERTEX;
}

void fragment() {
    vec3 c = seabed_albedo(v_world_pos.xz);

    float depth = max(-v_world_pos.y, 0.0);
    vec2 surface_xz = v_world_pos.xz + light_direction.xz / max(light_direction.y, 0.2) * depth;
    vec2 fp = surface_xz * foam_scale;
    vec2 drift = vec2(wave_time * foam_drift, wave_time * foam_drift * -0.55);
    vec2 warp = vec2(fbm(fp * 0.8 + drift), fbm(fp * 0.8 + drift + 19.3)) - 0.5;
    fp += warp * foam_warp;

    float radius_scale = mix(1.35, 0.65, surface_foam_amount);
    float shadow = foam_layer(fp + drift + foam_swirl(fp, wave_time), radius_scale, foam_edge);
    c *= 1.0 - foam_shadow_strength * shadow;

    float dist = length(v_world_pos - CAMERA_POSITION_WORLD);
    c = mix(c, murk_color, smoothstep(murk_distance * 0.15, murk_distance, dist));

    ALBEDO = c;
    ROUGHNESS = 0.95;
    METALLIC = 0.0;
    SPECULAR = 0.05;
}
