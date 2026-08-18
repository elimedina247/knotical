shader_type canvas_item;

uniform int mode = 0;
uniform vec2 map_center = vec2(0.0);
uniform float map_half_extent = 600.0;
uniform float px_per_metre = 1.0;
uniform float grid_step = 200.0;
uniform float body_alpha = 0.94;

uniform vec4 waves[24];
uniform float wave_phase[24];
uniform int wave_count = 0;
uniform float wave_time = 0.0;
uniform float significant_height = 1.0;

uniform vec4 sea_sources[16];
uniform float sea_falloffs[16];
uniform int sea_source_count = 0;
uniform float sea_scale_max = 2.0;

uniform vec2 wind_dir = vec2(1.0, 0.0);
uniform float wind_speed = 0.0;
uniform vec2 wind_drift = vec2(0.0);
uniform float drift_scale = 4.0;

uniform vec3 wave_ramp[5];
uniform vec3 wind_ramp[6];
uniform float wind_ramp_speeds[6];

const float GRAVITY = 9.81;

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

float sea_scale(vec2 p) {
    float s = 1.0;
    for (int i = 0; i < sea_source_count; i++) {
        vec4 src = sea_sources[i];
        float w = 1.0 - smoothstep(src.z, src.z + max(sea_falloffs[i], 0.01), distance(p, src.xy));
        s = mix(s, src.w, w);
    }
    return clamp(s, 0.0, sea_scale_max);
}

float swell_height(vec2 world) {
    float h = 0.0;
    for (int i = 0; i < wave_count; i++) {
        vec2 dir = waves[i].xy;
        float k = TAU / waves[i].w;
        float omega = sqrt(GRAVITY * k);
        h += waves[i].z * sin(k * dot(dir, world) - omega * wave_time + wave_phase[i]);
    }
    return h * sea_scale(world);
}

vec3 wave_color(float h) {
    float t = clamp(0.5 + h / max(1.2 * significant_height, 0.05), 0.0, 1.0);
    float q = min(t, 0.999) * 4.0;
    int band = int(q);
    vec3 col = mix(wave_ramp[band], wave_ramp[band + 1], fract(q));
    return mix(col, vec3(1.0), smoothstep(0.94, 1.0, t) * 0.85);
}

vec3 wind_color(float speed) {
    vec3 col = wind_ramp[0];
    for (int i = 0; i < 5; i++) {
        col = mix(col, wind_ramp[i + 1],
            smoothstep(wind_ramp_speeds[i], wind_ramp_speeds[i + 1], speed));
    }
    return col;
}

void fragment() {
    vec2 world = map_center + (UV - 0.5) * 2.0 * map_half_extent;

    vec3 col;
    if (mode == 0) {
        col = wave_color(swell_height(world));
    } else {
        col = wind_color(wind_speed);

        vec2 along = normalize(wind_dir + vec2(1e-5, 0.0));
        vec2 perp = vec2(-along.y, along.x);
        vec2 q = world - wind_drift * drift_scale;
        float streak = fbm(vec2(
            dot(q, along) / (map_half_extent * 0.15),
            dot(q, perp) / (map_half_extent * 0.023)));
        col *= mix(0.74, 1.2, smoothstep(0.25, 0.75, streak));

        float crest = smoothstep(0.35, 0.8, swell_height(world) / max(significant_height, 0.05));
        col = mix(col, vec3(0.10, 0.25, 0.45), crest * 0.35);
    }

    vec2 cell = abs(fract(world / grid_step + 0.5) - 0.5) * grid_step;
    float w = 1.5 / max(px_per_metre, 0.0001);
    float line = 1.0 - smoothstep(w * 0.5, w, min(cell.x, cell.y));
    col = mix(col, vec3(0.04, 0.08, 0.12), line * 0.12);

    COLOR = vec4(col, body_alpha);
}
