shader_type sky;

uniform vec3 sun_direction = vec3(1.0, 0.0, 0.0);
uniform vec3 moon_direction = vec3(-1.0, 0.0, 0.0);
uniform vec2 cloud_offset = vec2(0.0);

uniform vec3 day_zenith : source_color = vec3(0.25, 0.52, 0.82);
uniform vec3 day_horizon : source_color = vec3(0.74, 0.89, 0.95);
uniform vec3 dusk_zenith : source_color = vec3(0.23, 0.18, 0.42);
uniform vec3 dusk_horizon : source_color = vec3(0.98, 0.55, 0.28);
uniform vec3 night_zenith : source_color = vec3(0.004, 0.007, 0.018);
uniform vec3 night_horizon : source_color = vec3(0.012, 0.02, 0.04);

uniform vec3 sun_disc_color : source_color = vec3(1.0, 0.93, 0.72);
uniform vec3 moon_disc_color : source_color = vec3(0.82, 0.87, 0.95);
uniform float sun_size : hint_range(0.01, 0.2) = 0.045;
uniform float moon_size : hint_range(0.01, 0.2) = 0.035;

uniform float cloud_coverage : hint_range(0.0, 1.0) = 0.42;
uniform float cloud_scale = 1.4;

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
    for (int i = 0; i < 4; i++) {
        v += a * value_noise(p);
        p *= 2.07;
        a *= 0.5;
    }
    return v;
}

void sky() {
    float sun_h = smoothstep(-0.03, 0.28, sun_direction.y);
    float dusk = max(0.0, 1.0 - abs(sun_direction.y) / 0.22);

    vec3 zenith = mix(night_zenith, day_zenith, sun_h);
    zenith = mix(zenith, dusk_zenith, dusk * 0.55);
    vec3 horizon = mix(night_horizon, day_horizon, sun_h);

    vec2 flat_eye = EYEDIR.xz;
    float flat_len = length(flat_eye);
    float sun_side = 0.5;
    if (flat_len > 0.001 && length(sun_direction.xz) > 0.001) {
        sun_side = 0.5 + 0.5 * dot(flat_eye / flat_len, normalize(sun_direction.xz));
    }
    horizon = mix(horizon, dusk_horizon, dusk * (0.25 + 0.65 * sun_side));

    float up = EYEDIR.y;
    vec3 col = mix(horizon, zenith, pow(clamp(up, 0.0, 1.0), 0.5));
    col *= mix(0.4, 1.0, smoothstep(-0.1, 0.0, up));

    float sun_ang = acos(clamp(dot(EYEDIR, sun_direction), -1.0, 1.0));
    float sun_disc = 1.0 - smoothstep(sun_size * 0.85, sun_size, sun_ang);
    col = mix(col, sun_disc_color, sun_disc * smoothstep(-0.06, 0.0, sun_direction.y));
    col += sun_disc_color * exp(-sun_ang * 9.0) * (0.10 + 0.30 * dusk) * smoothstep(-0.06, 0.05, sun_direction.y);

    float moon_ang = acos(clamp(dot(EYEDIR, moon_direction), -1.0, 1.0));
    float moon_disc = 1.0 - smoothstep(moon_size * 0.8, moon_size, moon_ang);
    col = mix(col, moon_disc_color, moon_disc * smoothstep(-0.04, 0.02, moon_direction.y));

    if (up > 0.01) {
        vec2 cuv = EYEDIR.xz / (up + 0.2) * cloud_scale + cloud_offset;
        float cn = fbm(cuv);
        float cmask = smoothstep(cloud_coverage, cloud_coverage + 0.06, cn) * smoothstep(0.01, 0.12, up);
        float ctop = smoothstep(cloud_coverage + 0.10, cloud_coverage + 0.30, cn);
        vec3 lit = mix(vec3(0.028, 0.032, 0.05), mix(vec3(0.99, 0.99, 0.985), dusk_horizon, dusk * 0.65), sun_h);
        vec3 shade = lit * vec3(0.62, 0.68, 0.80);
        col = mix(col, mix(shade, lit, ctop), cmask);
    }

    COLOR = col;
}
