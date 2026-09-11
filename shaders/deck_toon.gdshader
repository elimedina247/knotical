shader_type spatial;
render_mode cull_back, depth_draw_opaque, diffuse_lambert, specular_disabled;

uniform vec4 plank_color : source_color = vec4(0.800, 0.640, 0.400, 1.0);
uniform vec4 plank_alt_color : source_color = vec4(0.720, 0.560, 0.340, 1.0);
uniform vec4 seam_color : source_color = vec4(0.300, 0.190, 0.110, 1.0);
uniform float plank_width : hint_range(0.05, 3.0, 0.005) = 0.5;
uniform float plank_length : hint_range(0.5, 30.0, 0.1) = 4.5;
uniform float seam_width : hint_range(0.0, 0.2, 0.001) = 0.035;
uniform bool use_vertex_color = false;
uniform float lit_threshold : hint_range(-0.5, 0.9, 0.01) = 0.12;
uniform float band_edge : hint_range(0.001, 0.2, 0.001) = 0.02;
uniform float half_band : hint_range(0.0, 1.0, 0.01) = 0.3;
uniform float half_band_at : hint_range(0.0, 1.0, 0.01) = 0.5;

varying vec3 object_position;
varying vec3 object_normal;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(37.31, 91.77))) * 43758.5453);
}

void vertex() {
    object_position = VERTEX;
    object_normal = NORMAL;
}

void fragment() {
    vec3 n = abs(normalize(object_normal));
    vec2 face;
    if (n.y >= n.x && n.y >= n.z) {
        face = vec2(object_position.z, object_position.x);
    } else if (n.x >= n.z) {
        face = vec2(object_position.y, object_position.z);
    } else {
        face = vec2(object_position.y, object_position.x);
    }

    float across = face.y / plank_width;
    float row = floor(across);
    float along = (face.x + hash(vec2(row, 3.7)) * plank_length) / plank_length;
    float board = floor(along);
    float run = fract(along);
    float lay = fract(across);

    float seam = min(
        smoothstep(0.0, seam_width / plank_width + fwidth(across), min(lay, 1.0 - lay)),
        smoothstep(0.0, seam_width / plank_length + fwidth(along), min(run, 1.0 - run)));

    float tint = hash(vec2(row, board));
    vec3 wood = mix(plank_color.rgb, plank_alt_color.rgb, step(0.5, tint));
    if (use_vertex_color) wood *= COLOR.rgb;

    ALBEDO = mix(seam_color.rgb, wood, seam);
    ROUGHNESS = 1.0;
    METALLIC = 0.0;
    SPECULAR = 0.0;
}

void light() {
    float ndl = dot(NORMAL, LIGHT) * ATTENUATION;
    float lit = smoothstep(lit_threshold - band_edge, lit_threshold + band_edge, ndl);
    float mid = smoothstep(half_band_at - band_edge, half_band_at + band_edge, ndl);
    float level = lit * mix(1.0 - half_band, 1.0, mid);
    DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR / PI * level;
}
