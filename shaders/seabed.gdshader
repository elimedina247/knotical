shader_type spatial;
render_mode world_vertex_coords;

// ---------------------------------------------------------------------------
// The water is transparent, so this is the one and only seabed: it is seen
// directly through the surface from above and below. The foam-shadow web here
// is the surface's calm_cell_border pattern from ocean.gdshader, projected
// along the sun direction — same function, same scale, same clock, or the
// shadows stop lining up with the foam that casts them.
// ---------------------------------------------------------------------------

uniform vec3 sand_color : source_color = vec3(0.886, 0.816, 0.635);
uniform vec3 seabed_dark_color : source_color = vec3(0.431, 0.478, 0.314);
uniform vec3 murk_color : source_color = vec3(0.247, 0.596, 0.541);
uniform float seabed_patch_scale = 0.022;
uniform float seabed_contrast : hint_range(0.0, 0.4) = 0.10;
uniform float ring_scale = 0.35;
uniform float ring_width : hint_range(0.01, 0.3) = 0.07;
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

vec2 hash22(vec2 p) {
    float n = hash21(p);
    return vec2(n, hash21(p + n + 17.17));
}

float calm_cell_border(vec2 p, float t) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    float f1 = 8.0;
    float f2 = 8.0;
    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            vec2 g = vec2(float(x), float(y));
            vec2 o = hash22(cell + g);
            o = 0.5 + 0.35 * sin(t + TAU * o);
            vec2 r = g + o - f;
            float d = dot(r, r);
            if (d < f1) {
                f2 = f1;
                f1 = d;
            } else if (d < f2) {
                f2 = d;
            }
        }
    }
    return sqrt(f2) - sqrt(f1);
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
    float web = calm_cell_border(surface_xz * ring_scale, wave_time * 0.4);
    float line = 1.0 - smoothstep(ring_width, ring_width + 0.05, web);
    c *= 1.0 - foam_shadow_strength * line;

    float dist = length(v_world_pos - CAMERA_POSITION_WORLD);
    c = mix(c, murk_color, smoothstep(murk_distance * 0.15, murk_distance, dist));

    ALBEDO = c;
    ROUGHNESS = 0.95;
    METALLIC = 0.0;
    SPECULAR = 0.05;
}
