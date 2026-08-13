shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform vec4 plank_color : source_color = vec4(0.700, 0.540, 0.330, 1.0);
uniform vec4 grain_color : source_color = vec4(0.588, 0.435, 0.259, 1.0);
uniform vec4 seam_color : source_color = vec4(0.239, 0.157, 0.098, 1.0);

uniform float plank_width : hint_range(0.05, 3.0, 0.005) = 0.42;
uniform float plank_length : hint_range(0.5, 30.0, 0.1) = 6.0;
uniform float seam_width : hint_range(0.0, 0.2, 0.001) = 0.03;
uniform float tone_variation : hint_range(0.0, 0.5, 0.005) = 0.09;
uniform float grain_strength : hint_range(0.0, 1.0, 0.01) = 0.3;
uniform float grain_scale : hint_range(1.0, 200.0, 1.0) = 52.0;
uniform float roughness_value : hint_range(0.0, 1.0, 0.01) = 0.88;
uniform bool cross_planks = false;

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
		face = cross_planks
			? vec2(object_position.x, object_position.z)
			: vec2(object_position.z, object_position.x);
	} else if (n.x >= n.z) {
		face = vec2(object_position.z, object_position.y);
	} else {
		face = vec2(object_position.x, object_position.y);
	}

	float across = face.y / plank_width;
	float row = floor(across);
	float along = (face.x + hash(vec2(row, 3.7)) * plank_length) / plank_length;
	float board = floor(along);

	float run = fract(along);
	float lay = fract(across);

	float bevel = min(
		smoothstep(0.0, seam_width / plank_width + fwidth(across), min(lay, 1.0 - lay)),
		smoothstep(0.0, seam_width / plank_length + fwidth(along), min(run, 1.0 - run)));

	float tint = hash(vec2(row, board));
	float tone = 1.0 + (tint - 0.5) * 2.0 * tone_variation;

	float detail = clamp(1.0 - fwidth(face.y) * grain_scale, 0.0, 1.0);
	float rings = 0.5 + 0.5 * sin(face.y * grain_scale + tint * 24.0 + sin(face.x * 1.7) * 2.0);

	vec3 wood = mix(plank_color.rgb, grain_color.rgb, rings * grain_strength * detail) * tone;

	ALBEDO = mix(seam_color.rgb, wood, bevel);
	ROUGHNESS = mix(1.0, roughness_value, bevel);
	METALLIC = 0.0;
	SPECULAR = 0.12;
}
