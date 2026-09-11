shader_type spatial;
render_mode unshaded, depth_test_disabled, depth_draw_never, cull_disabled, blend_mix, fog_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler2D normal_tex : hint_normal_roughness_texture, filter_nearest;
uniform vec4 ink : source_color = vec4(0.08, 0.06, 0.12, 1.0);
uniform float thickness : hint_range(0.5, 4.0, 0.1) = 1.2;
uniform float depth_sensitivity : hint_range(0.001, 0.2, 0.001) = 0.02;
uniform float normal_sensitivity : hint_range(0.0, 2.0, 0.01) = 0.6;
uniform float fade_start = 250.0;
uniform float fade_end = 900.0;
uniform float normal_fade = 140.0;

void vertex() {
    POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float linear_depth(vec2 uv, mat4 inv_projection) {
    float d = texture(depth_tex, uv).r;
    vec3 ndc = vec3(uv * 2.0 - 1.0, d);
    vec4 view = inv_projection * vec4(ndc, 1.0);
    view.xyz /= view.w;
    return min(-view.z, 50000.0);
}

void fragment() {
    vec2 px = thickness / VIEWPORT_SIZE;
    vec2 uv = SCREEN_UV;

    float dc = linear_depth(uv, INV_PROJECTION_MATRIX);
    float dl = linear_depth(uv - vec2(px.x, 0.0), INV_PROJECTION_MATRIX);
    float dr = linear_depth(uv + vec2(px.x, 0.0), INV_PROJECTION_MATRIX);
    float du = linear_depth(uv - vec2(0.0, px.y), INV_PROJECTION_MATRIX);
    float dd = linear_depth(uv + vec2(0.0, px.y), INV_PROJECTION_MATRIX);

    float near = min(dc, min(min(dl, dr), min(du, dd)));
    float curve = max(abs(dl + dr - 2.0 * dc), abs(du + dd - 2.0 * dc));
    float depth_edge = curve / max(near, 0.5);
    float edge = smoothstep(depth_sensitivity, depth_sensitivity * 3.0, depth_edge);

    vec3 nc = texture(normal_tex, uv).rgb * 2.0 - 1.0;
    vec3 nl = texture(normal_tex, uv - vec2(px.x, 0.0)).rgb * 2.0 - 1.0;
    vec3 nr = texture(normal_tex, uv + vec2(px.x, 0.0)).rgb * 2.0 - 1.0;
    vec3 nu = texture(normal_tex, uv - vec2(0.0, px.y)).rgb * 2.0 - 1.0;
    vec3 nd = texture(normal_tex, uv + vec2(0.0, px.y)).rgb * 2.0 - 1.0;
    float normal_edge = max(max(1.0 - dot(nc, nl), 1.0 - dot(nc, nr)), max(1.0 - dot(nc, nu), 1.0 - dot(nc, nd)));
    edge = max(edge, smoothstep(normal_sensitivity, normal_sensitivity + 0.25, normal_edge) * (1.0 - smoothstep(normal_fade * 0.5, normal_fade, near)));

    float fade = 1.0 - smoothstep(fade_start, fade_end, near);
    ALBEDO = ink.rgb;
    ALPHA = edge * ink.a * fade;
}
