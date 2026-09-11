shader_type spatial;
render_mode world_vertex_coords, cull_disabled, depth_draw_always;

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
uniform float wave_steepness[24];
uniform float wave_phase[24];
uniform float wave_response[24];
uniform int wave_count = 16;
uniform float wave_time = 0.0;

// Seabed-baked sea-state field: 1 = baseline, <1 calm shallows, >1 sculpted deeps.
// Mirrors Ocean.Field(); each wave responds by pow(field, its band response).
uniform sampler2D sea_field : filter_linear, repeat_disable, hint_default_white;
uniform float sea_field_enabled = 0.0;
uniform float sea_field_extent = 6000.0;

instance uniform float inner_extent = 0.0;

uniform float camera_submerged = 0.0;

uniform float significant_height = 2.0;

uniform vec3 water_ramp[8];
uniform int water_ramp_size = 6;
uniform vec3 sky_color : source_color = vec3(0.651, 0.855, 0.933);
uniform vec3 foam_color : source_color = vec3(1.0, 1.0, 1.0);
uniform vec3 sun_direction = vec3(0.0, 1.0, 0.0);

uniform float depth_contrast : hint_range(0.0, 1.0) = 0.55;
uniform float band_softness : hint_range(0.0, 1.0) = 0.08;
uniform float slope_range : hint_range(0.05, 0.6) = 0.22;
uniform float band_slope_weight : hint_range(0.0, 1.0) = 0.65;

uniform sampler2D depth_texture : hint_depth_texture;

uniform float depth_fade : hint_range(0.5, 120.0) = 12.0;
uniform float sand_visibility : hint_range(0.1, 120.0) = 7.0;
uniform float view_angle_influence : hint_range(0.0, 1.0) = 0.45;
uniform float min_opacity : hint_range(0.0, 1.0) = 0.55;
uniform float foam_scale = 0.03;
uniform float foam_warp : hint_range(0.0, 1.5) = 0.35;
uniform float foam_drift : hint_range(0.0, 0.3) = 0.03;
uniform float foam_edge : hint_range(0.002, 0.3) = 0.005;
uniform float surface_foam_amount : hint_range(0.0, 1.0) = 0.5;
uniform float surface_foam_opacity : hint_range(0.0, 1.0) = 0.12;
uniform float crest_foam_opacity : hint_range(0.0, 1.0) = 0.9;

// Whitecaps live where the surface folds. The pinch normalisation keeps the fold
// below ~0.5 by design, so the trigger sits inside 0.2..0.45 and slides with wind.
uniform float wind_speed = 9.0;
uniform float cap_wind_full = 16.0;
uniform float cap_threshold_calm : hint_range(0.1, 0.6, 0.005) = 0.30;
uniform float cap_threshold_gale : hint_range(0.1, 0.6, 0.005) = 0.15;
uniform float cap_softness : hint_range(0.005, 0.3, 0.005) = 0.07;
uniform float cap_tear : hint_range(0.0, 0.4, 0.005) = 0.12;
uniform float trail_seconds : hint_range(0.5, 8.0, 0.1) = 3.0;
uniform float trail_opacity : hint_range(0.0, 1.0, 0.01) = 0.6;

uniform float shore_wash_depth : hint_range(0.02, 2.0, 0.01) = 0.22;
uniform float shore_line_depth : hint_range(0.05, 4.0, 0.01) = 0.7;
uniform float shore_line_width : hint_range(0.02, 1.0, 0.01) = 0.14;
uniform float shore_second_depth : hint_range(0.05, 6.0, 0.01) = 1.6;
uniform float shore_second_width : hint_range(0.02, 1.0, 0.01) = 0.1;
uniform float shore_wobble : hint_range(0.0, 1.0, 0.01) = 0.3;
uniform float shore_gap : hint_range(0.0, 1.0, 0.01) = 0.35;

uniform vec4 wake_points[32];
uniform int wake_point_count = 0;
uniform vec4 wake_bows[4];
uniform int wake_bow_count = 0;
uniform float wake_width = 12.0;
uniform float wake_hull_length = 30.0;
uniform float wake_life = 22.0;
uniform float wake_spread = 0.5;
uniform float wake_strength : hint_range(0.0, 1.0) = 0.9;

// a: centre xz, forward xz. b: waterline half length, half beam, inset scale, ring width (m).
uniform vec4 hull_masks_a[4];
uniform vec4 hull_masks_b[4];
uniform int hull_mask_count = 0;

uniform sampler2D foam_capture : filter_linear, repeat_disable, hint_default_black;
uniform float foam_capture_extent = 6000.0;
uniform float foam_capture_strength = 0.55;

uniform float fresnel_power : hint_range(0.5, 8.0) = 4.0;
uniform float fresnel_max : hint_range(0.0, 1.0) = 0.16;

uniform float detail_strength : hint_range(0.0, 2.0) = 0.35;
uniform float detail_far : hint_range(50.0, 2000.0) = 600.0;
uniform float glint_strength : hint_range(0.0, 2.0) = 0.5;
uniform float glint_size : hint_range(10.0, 600.0) = 160.0;
uniform float lit_band : hint_range(0.0, 1.0) = 0.2;
uniform float shade_level : hint_range(0.0, 1.0) = 0.82;
uniform float debug_view = 0.0;

// Baked FFT ocean flipbook (Girardot): 8x8 grid of 512px tiles, greyscale height,
// spatially tileable per frame. Sampled with manual wrap so tiles repeat seamlessly.
uniform sampler2D detail_flipbook : filter_linear, repeat_disable, hint_default_black;
uniform float detail_flip_enabled = 0.0;
uniform float detail_flip_tile : hint_range(4.0, 100.0) = 22.0;
uniform float detail_flip_fps : hint_range(1.0, 60.0) = 14.0;

const float GRAVITY = 9.81;

varying vec3 v_world_pos;
varying vec3 v_world_normal;
varying float v_height;
varying float v_fold;    // Gerstner compression; high where the surface pinches
varying float v_trail;   // the same fold a few seconds ago, decayed: foam left behind a crest
varying float v_field;

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

// --- FFT flipbook sampling. Manual wrap-within-tile bilinear, because the frames
// live in an atlas and hardware repeat would bleed across neighbouring frames.
// Girardot's packing: R = up, G/B = the two horizontal normal components. ---------

vec2 flip_sample(vec2 uv, float frame) {
    float f = mod(floor(frame), 64.0);
    ivec2 tile_base = ivec2(int(mod(f, 8.0)), int(floor(f / 8.0))) * 512;
    vec2 p = fract(uv) * 512.0 - 0.5;
    vec2 i = floor(p);
    vec2 fr = p - i;
    ivec2 i00 = ivec2(mod(i, vec2(512.0)));
    ivec2 i11 = ivec2(mod(i + 1.0, vec2(512.0)));
    vec2 h00 = texelFetch(detail_flipbook, tile_base + i00, 0).gb;
    vec2 h10 = texelFetch(detail_flipbook, tile_base + ivec2(i11.x, i00.y), 0).gb;
    vec2 h01 = texelFetch(detail_flipbook, tile_base + ivec2(i00.x, i11.y), 0).gb;
    vec2 h11 = texelFetch(detail_flipbook, tile_base + i11, 0).gb;
    return mix(mix(h00, h10, fr.x), mix(h01, h11, fr.x), fr.y);
}

vec3 flip_grad(vec2 uv, float frame) {
    vec2 s = mix(flip_sample(uv, frame), flip_sample(uv, frame + 1.0), fract(frame));
    vec2 hor = s * 2.0 - 1.0;
    return vec3(hor.y, 0.0, hor.x);
}

float fold_of(float jxx, float jxz, float jzz) {
    float jacobian = (1.0 - jxx) * (1.0 - jzz) - jxz * jxz;
    return clamp(1.0 - jacobian, 0.0, 1.0);
}

// Same plan shape as HullForm.PlanFactor: 0 = stern, 1 = bow, returns half-width factor.
float hull_plan(float t) {
    float m = abs(t * 2.0 - 1.0);
    float sharpness = t > 0.5 ? 2.2 : 3.0;
    float f = pow(max(0.0, 1.0 - pow(m, sharpness)), 0.65);
    if (t < 0.5) f = mix(f, 1.0, 0.55 * m * m);
    return f;
}

void vertex() {
    vec3 pos = VERTEX;
    vec2 base_xz = pos.xz;

    float dist = length(base_xz - CAMERA_POSITION_WORLD.xz);
    float field = sea_field_enabled > 0.5
        ? textureLod(sea_field, base_xz / (sea_field_extent * 2.0) + 0.5, 0.0).r
        : 1.0;

    float height = 0.0;
    vec2 pinch = vec2(0.0);
    vec2 slope = vec2(0.0);

    // Jacobian of the horizontal displacement. Where this collapses toward zero the
    // surface is folding over itself, which is physically where whitecaps form.
    float jxx = 0.0;
    float jxz = 0.0;
    float jzz = 0.0;

    // The same Jacobian at three earlier moments. A crest that folded a moment ago
    // leaves foam behind it, and because the sea is a function of time the past is
    // just a phase offset: no history texture needed.
    vec3 pxx = vec3(0.0);
    vec3 pxz = vec3(0.0);
    vec3 pzz = vec3(0.0);
    vec3 taus = vec3(1.0, 2.0, 3.0) * (trail_seconds / 3.0);

    for (int i = 0; i < wave_count; i++) {
        vec2 dir = waves[i].xy;
        float amp = waves[i].z;
        float wavelength = waves[i].w;

        // Fade a wave out once the mesh is too coarse to resolve it, or the far
        // field shimmers as vertices slide through high-frequency waves.
        float fade = 1.0 - smoothstep(wavelength * 12.0, wavelength * 32.0, dist);
        if (fade <= 0.0) continue;
        float scale = field == 1.0 ? 1.0 : pow(field, wave_response[i]);
        amp *= fade * scale;

        float k = TAU / wavelength;
        float omega = sqrt(GRAVITY * k);
        float phase = k * dot(dir, base_xz) - omega * wave_time + wave_phase[i];

        float s = sin(phase);
        float c = cos(phase);

        height += amp * s;

        // GPU-Gems steepness normalisation: pinch is steepness over k and the wave
        // count, so the summed displacement can never fold through itself. The pinch
        // follows the field down in calm water but caps at baseline in big water —
        // giants grow taller and rounder, never folded.
        float qa = wave_steepness[i] / (k * float(wave_count)) * fade * min(scale, 1.0);
        pinch += qa * dir * c;

        float qak = qa * k;
        jxx += qak * dir.x * dir.x * s;
        jxz += qak * dir.x * dir.y * s;
        jzz += qak * dir.y * dir.y * s;

        vec3 past = sin(vec3(phase) + omega * taus);
        pxx += qak * dir.x * dir.x * past;
        pxz += qak * dir.x * dir.y * past;
        pzz += qak * dir.y * dir.y * past;

        slope += dir * (amp * k * c);
    }

    pos.y += height;
    pos.xz += pinch;

    VERTEX = pos;

    vec3 tangent_x = vec3(1.0 - jxx, slope.x, -jxz);
    vec3 tangent_z = vec3(-jxz, slope.y, 1.0 - jzz);

    v_world_pos = pos;
    v_world_normal = normalize(cross(tangent_z, tangent_x));
    v_height = height / max(significant_height * field, 0.01);
    v_field = field;

    v_fold = fold_of(jxx, jxz, jzz);
    v_trail = max(max(fold_of(pxx.x, pxz.x, pzz.x) * 0.9,
                      fold_of(pxx.y, pxz.y, pzz.y) * 0.7),
                      fold_of(pxx.z, pxz.z, pzz.z) * 0.5);
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

    // Hull footprints: the water is simply not drawn inside a hull, and a foam ring
    // hugs the outside. No vertex deformation, so no bowl around the boat.
    float hull_ring = 0.0;
    for (int i = 0; i < hull_mask_count; i++) {
        vec2 fwd = hull_masks_a[i].zw;
        vec2 rel = v_world_pos.xz - hull_masks_a[i].xy;
        vec2 side = vec2(-fwd.y, fwd.x);
        float lz = dot(rel, fwd);
        float lx = dot(rel, side);
        float half_len = max(hull_masks_b[i].x, 0.1);
        float half_beam = max(hull_masks_b[i].y, 0.1);
        float t = lz / half_len * 0.5 + 0.5;
        float d;
        if (t > 0.0 && t < 1.0) {
            float w = hull_plan(t) * half_beam;
            if (abs(lx) < w * hull_masks_b[i].z) {
                discard;
            }
            d = abs(lx) - w;
        } else {
            d = length(vec2(lx, abs(lz) - half_len));
        }
        float ring_w = max(hull_masks_b[i].w, 0.05);
        hull_ring = max(hull_ring, 1.0 - smoothstep(ring_w * 0.6, ring_w, d));
    }

    vec3 n = normalize(v_world_normal);

    float detail_fade = 1.0 - smoothstep(detail_far * 0.25, detail_far, length(v_world_pos - CAMERA_POSITION_WORLD));

    vec3 grad;
    if (detail_flip_enabled > 0.5) {
        float frame = wave_time * detail_flip_fps;
        grad = flip_grad(v_world_pos.xz / detail_flip_tile, frame);
        grad += flip_grad(v_world_pos.xz / (detail_flip_tile * 0.313) + vec2(37.7, 11.3),
            frame * 1.31 + 17.0) * 0.45;
    } else {
        vec2 duv = v_world_pos.xz * 0.35;
        vec2 flow = vec2(wave_time * 0.55, wave_time * 0.23);
        float eps = 0.6;
        float d0 = fbm(duv + flow);
        float dgx = fbm(duv + flow + vec2(eps, 0.0));
        float dgz = fbm(duv + flow + vec2(0.0, eps));
        grad = vec3(d0 - dgx, 0.0, d0 - dgz) / eps;
    }

    vec3 n_detail = normalize(n + grad * (detail_strength * detail_fade));

    vec3 n_view = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);

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

    // Whitecaps: a torn-edged cap on the fold, and a softer patch trailing behind it.
    float wind01 = clamp(wind_speed / cap_wind_full, 0.0, 1.0);
    float threshold = mix(cap_threshold_calm, cap_threshold_gale, wind01) * mix(1.0, 0.8, clamp(v_field - 1.0, 0.0, 1.0));
    float tear = (fbm(v_world_pos.xz * 0.9 + drift * 8.0) - 0.5) * cap_tear;
    float cap = smoothstep(threshold, threshold + cap_softness, v_fold + tear);
    float trail = smoothstep(threshold, threshold + cap_softness * 2.0, v_trail + tear * 0.6) * trail_opacity;

    float wake = 0.0;

    for (int i = 0; i < wake_point_count; i++) {
        vec4 pt = wake_points[i];
        float age = wave_time - pt.z;
        float life = 1.0 - age / wake_life;
        if (life <= 0.0) continue;
        float radius = wake_width * mix(0.15, 0.5, pt.w) + wake_spread * age;
        float d = distance(v_world_pos.xz, pt.xy);
        wake = max(wake, pt.w * life * (1.0 - smoothstep(radius * 0.4, radius, d)));
    }

    for (int i = 0; i < wake_bow_count; i++) {
        vec4 bow = wake_bows[i];
        if (bow.w <= 0.0) continue;
        vec2 dir = vec2(sin(bow.z), cos(bow.z));
        vec2 rel = v_world_pos.xz - bow.xy;
        float back = -dot(rel, dir);
        float across = abs(rel.x * dir.y - rel.y * dir.x);
        float reach = wake_hull_length * mix(0.25, 1.1, bow.w);
        float along_hull = smoothstep(-3.0, 2.0, back)
            * (1.0 - smoothstep(reach * 0.7, reach, back));
        float hw = mix(0.8, wake_width * 0.6, smoothstep(0.0, reach * 0.4, back))
            * mix(0.45, 1.0, bow.w);
        float band = 1.0 - smoothstep(hw * 0.55, hw, across);
        float taper = mix(1.0, 0.45, smoothstep(0.0, reach, back));
        wake = max(wake, bow.w * along_hull * band * taper);
    }

    float fold = max(cap, wake * wake_strength);
    fold = max(fold, trail);

    vec2 cap_uv = clamp(v_world_pos.xz / (foam_capture_extent * 2.0) + 0.5, vec2(0.0), vec2(1.0));
    fold = max(fold, texture(foam_capture, cap_uv).r * foam_capture_strength);

    float radius_scale = mix(mix(1.35, 0.65, surface_foam_amount), 0.25, fold);
    float lace = foam_layer(fp + drift + foam_swirl(fp, wave_time), radius_scale, foam_edge);
    float coverage = mix(1.0 - slope_t * 0.7, 1.0, fold);

    float foam = lace * coverage * detail_fade * mix(surface_foam_opacity, crest_foam_opacity, fold);
    foam = max(foam, cap * crest_foam_opacity * (0.55 + 0.45 * lace));

    // Graphic shore and contact lines, drawn from how much water sits under this pixel:
    // a solid wash at the very edge, then one or two crisp lines further out. Anything
    // that pierces the surface (piles, hulls, rocks) gets the same treatment for free.
    float wobble = sin(wave_time * 1.1 + fbm(v_world_pos.xz * 0.12) * TAU) * shore_wobble;
    float gap = smoothstep(shore_gap - 0.15, shore_gap + 0.15, fbm(v_world_pos.xz * 0.35 + drift * 3.0));
    float wash = 1.0 - smoothstep(shore_wash_depth * 0.7, shore_wash_depth * (1.0 + wobble * 0.6), water_depth);
    float line_a = 1.0 - smoothstep(shore_line_width * 0.7, shore_line_width,
        abs(water_depth - shore_line_depth * (1.0 + wobble * 0.5)));
    float line_b = 1.0 - smoothstep(shore_second_width * 0.7, shore_second_width,
        abs(water_depth - shore_second_depth * (1.0 - wobble * 0.4)));
    float shore = max(wash, max(line_a * gap, line_b * gap * 0.85)) * step(0.001, water_depth);
    shore = max(shore, hull_ring * (0.6 + 0.4 * gap));

    foam = max(foam, shore * crest_foam_opacity);

    col = mix(col, foam_color, foam);
    alpha = max(alpha, foam);

    // Sun glint on the fine ripples, kept out of the lighting bands so the cel shading
    // stays broad and the sparkle stays small.
    vec3 sun = normalize(sun_direction);
    vec3 to_eye = normalize(CAMERA_POSITION_WORLD - v_world_pos);
    vec3 half_vec = normalize(sun + to_eye);
    float glint = pow(max(dot(n_detail, half_vec), 0.0), glint_size);
    glint = smoothstep(0.3, 0.5, glint) * glint_strength * detail_fade * (1.0 - foam) * smoothstep(-0.02, 0.1, sun.y);

    if (debug_view > 0.5) {
        col = vec3(v_fold * 2.0, cap, v_trail * 2.0);
        alpha = 1.0;
        glint = 0.0;
    }
    ALBEDO = col;
    ALPHA = alpha;
    EMISSION = vec3(glint) * mix(sky_color, vec3(1.0), 0.6);
    ROUGHNESS = 1.0;
    METALLIC = 0.0;
    SPECULAR = 0.0;
    NORMAL = n_view;
}

void light() {
    float ndl = dot(NORMAL, LIGHT) * ATTENUATION;
    float lit = smoothstep(lit_band - 0.03, lit_band + 0.03, ndl);
    DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR / PI * mix(shade_level, 1.0, lit);
}
