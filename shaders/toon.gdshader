shader_type spatial;
render_mode cull_back, depth_draw_opaque, diffuse_lambert, specular_disabled;

uniform vec4 albedo : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform bool use_vertex_color = true;
uniform float lit_threshold : hint_range(-0.5, 0.9, 0.01) = 0.12;
uniform float band_edge : hint_range(0.001, 0.2, 0.001) = 0.02;
uniform float shadow_light : hint_range(0.0, 1.0, 0.01) = 0.0;
uniform float half_band : hint_range(0.0, 1.0, 0.01) = 0.35;
uniform float half_band_at : hint_range(0.0, 1.0, 0.01) = 0.5;
uniform float glint : hint_range(0.0, 1.0, 0.01) = 0.0;
uniform float glint_size : hint_range(4.0, 400.0, 1.0) = 90.0;
uniform float rim : hint_range(0.0, 1.0, 0.01) = 0.0;
uniform float rim_width : hint_range(0.05, 1.0, 0.01) = 0.35;

void fragment() {
    vec3 base = albedo.rgb;
    if (use_vertex_color) base *= COLOR.rgb;
    ALBEDO = base;
    ROUGHNESS = 1.0;
    METALLIC = 0.0;
    SPECULAR = 0.0;
    if (rim > 0.0) {
        float facing = 1.0 - clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
        float edge = smoothstep(1.0 - rim_width, 1.0 - rim_width + 0.05, facing);
        EMISSION = base * edge * rim;
    }
}

void light() {
    float ndl = dot(NORMAL, LIGHT) * ATTENUATION;
    float lit = smoothstep(lit_threshold - band_edge, lit_threshold + band_edge, ndl);
    float mid = smoothstep(half_band_at - band_edge, half_band_at + band_edge, ndl);
    float level = mix(shadow_light, 1.0, lit) * mix(1.0 - half_band, 1.0, mid);
    DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR / PI * level;
    if (glint > 0.0) {
        vec3 h = normalize(VIEW + LIGHT);
        float spec = pow(max(dot(NORMAL, h), 0.0), glint_size);
        SPECULAR_LIGHT += LIGHT_COLOR / PI * glint * smoothstep(0.4, 0.5, spec) * lit;
    }
}
