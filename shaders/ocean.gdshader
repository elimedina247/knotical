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

uniform float seabed_height = -10.0;
uniform float depth_fade : hint_range(0.5, 120.0) = 22.0;
uniform vec3 sand_color : source_color = vec3(0.886, 0.816, 0.635);
uniform vec3 seabed_dark_color : source_color = vec3(0.431, 0.478, 0.314);
uniform float sand_visibility : hint_range(0.1, 80.0) = 20.0;
uniform float sand_opacity : hint_range(0.0, 1.0) = 0.95;
uniform float seabed_patch_scale = 0.022;
uniform float seabed_contrast : hint_range(0.0, 0.4) = 0.10;
uniform float water_filter : hint_range(0.0, 1.0) = 0.55;
uniform float caustic_strength : hint_range(0.0, 1.5) = 0.45;

uniform vec3 ring_color = vec3(0.42, 0.78, 0.72);
uniform float ring_scale = 0.35;
uniform float ring_width : hint_range(0.01, 0.3) = 0.07;
uniform float ring_strength : hint_range(0.0, 1.0) = 0.55;
uniform float calm_height : hint_range(0.05, 3.0) = 0.8;

// Foam appears where the surface folds, not where it is tall. Raise to make
// whitecaps rarer; the useful range is narrow because folding is rare by nature.
uniform float foam_threshold : hint_range(0.0, 0.6, 0.005) = 0.24;
uniform float foam_softness : hint_range(0.0, 0.4, 0.005) = 0.04;
uniform float foam_noise_scale : hint_range(0.01, 1.0) = 0.10;

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

vec2 hash22(vec2 p) {
    float n = hash21(p);
    return vec2(n, hash21(p + n + 17.17));
}

vec3 seabed_albedo(vec2 p) {
    float patch = fbm(p * seabed_patch_scale);
    float reef = fbm(p * seabed_patch_scale * 3.1 + 31.7);
    float grain = fbm(p * seabed_patch_scale * 11.0);

    vec3 c = mix(sand_color, seabed_dark_color, smoothstep(0.44, 0.44 + seabed_contrast, patch));
    c = mix(c, seabed_dark_color * 0.8, smoothstep(0.58, 0.58 + seabed_contrast, reef) * 0.7);
    return c * mix(0.88, 1.12, grain);
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

    float h = v_height / (1.0 + abs(v_height));
    float height_t = 0.5 + 0.5 * h * depth_contrast;
    float slope_t = clamp((1.0 - n_detail.y) / slope_range, 0.0, 1.0);
    float wave_t = mix(height_t, slope_t, band_slope_weight);

    float water_depth = max(v_world_pos.y - seabed_height, 0.0);
    float shallowness = exp(-water_depth / depth_fade);

    float lo = mix(0.0, 0.42, shallowness);
    float hi = mix(0.58, 1.0, shallowness);
    float raw = min(mix(lo, hi, wave_t), 0.999);

    float q = raw * float(water_ramp_size);
    int band = int(q);
    float edge = smoothstep(0.5 - band_softness * 0.5, 0.5 + band_softness * 0.5, fract(q));
    vec3 body = mix(water_ramp[band], water_ramp[min(band + 1, water_ramp_size - 1)], edge);

    vec3 to_eye = normalize(CAMERA_POSITION_WORLD - v_world_pos);
    float cos_view = max(to_eye.y, 0.1);
    float view_path = water_depth / cos_view;
    vec2 floor_xz = v_world_pos.xz - to_eye.xz * view_path + n_detail.xz * water_depth * 0.35;

    vec3 floor_col = seabed_albedo(floor_xz);
    float web = calm_cell_border(floor_xz * ring_scale, wave_time * 0.4);
    floor_col *= 1.0 + caustic_strength * (1.0 - smoothstep(0.0, 0.22, web));

    vec3 filter_tint = mix(vec3(1.0), water_ramp[water_ramp_size - 1], water_filter);
    body = mix(body, floor_col * filter_tint, exp(-view_path / sand_visibility) * sand_opacity);

    // Very low frequency tint so open water is not a single flat colour
    float tint = fbm(v_world_pos.xz * 0.0035);
    body *= mix(0.94, 1.07, tint);

    // VIEW is view-space, so the normal has to be converted before they meet.
    float fres = pow(1.0 - clamp(dot(n_view, VIEW), 0.0, 1.0), fresnel_power);
    vec3 col = mix(body, sky_color, clamp(fres, 0.0, fresnel_max));

    float calm = (1.0 - smoothstep(calm_height * 0.5, calm_height, significant_height))
        * (1.0 - slope_t) * detail_fade;
    if (calm > 0.001) {
        float border = calm_cell_border(v_world_pos.xz * ring_scale, wave_time * 0.4);
        float ring = 1.0 - smoothstep(ring_width, ring_width + 0.05, border);
        col = mix(col, ring_color, ring * calm * ring_strength);
    }

    // Foam: folding, modulated by drifting noise so the edge is torn rather than
    // a clean contour. Threshold stays hard — the decided edge is the art direction.
    vec2 foam_uv = v_world_pos.xz * foam_noise_scale;
    float patch = fbm(foam_uv + vec2(wave_time * 0.05, wave_time * -0.02));
    float foam_mask = v_fold * v_fold * (0.9 + 0.5 * patch);

    float foam = smoothstep(foam_threshold, foam_threshold + foam_softness, foam_mask);
    foam *= step(0.30, value_noise(foam_uv * 5.0 + vec2(wave_time * 0.07, 0.0)));
    col = mix(col, foam_color, foam);

    ALBEDO = col;
    ROUGHNESS = mix(0.62, 0.9, foam);
    METALLIC = 0.0;
    SPECULAR = 0.15;
    NORMAL = n_view;
}
