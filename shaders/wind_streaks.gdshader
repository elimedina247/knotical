shader_type spatial;
render_mode world_vertex_coords, unshaded, cull_disabled, depth_draw_never, blend_mix;

uniform vec3 tint : source_color = vec3(0.92, 0.97, 1.0);
uniform float streak_width = 0.11;
uniform float bend = 0.05;
uniform float bend_waves = 1.3;
uniform float near_fade_start = 2.0;
uniform float near_fade_end = 8.0;

varying float alpha;

void vertex() {
	vec3 axis = (MODEL_MATRIX * vec4(1.0, 0.0, 0.0, 0.0)).xyz;
	float span = length(axis);
	vec3 dir = span > 0.0001 ? axis / span : vec3(1.0, 0.0, 0.0);

	float t = UV.x;
	float across = UV.y * 2.0 - 1.0;
	float phase = INSTANCE_CUSTOM.x;

	vec3 raw_perp = cross(dir, vec3(0.0, 1.0, 0.0));
	float perp_len = length(raw_perp);
	vec3 flat_perp = perp_len > 0.0001 ? raw_perp / perp_len : vec3(1.0, 0.0, 0.0);
	vec3 lift = normalize(cross(flat_perp, dir));

	float s = (t * bend_waves + phase) * 6.2831853;
	vec3 centre = MODEL_MATRIX[3].xyz
		+ axis * (t - 0.5)
		+ flat_perp * sin(s) * bend * span
		+ lift * sin(s * 0.63 + 1.7) * bend * span * 0.6;

	vec3 to_cam = CAMERA_POSITION_WORLD - centre;
	float dist = length(to_cam);
	vec3 view_dir = dist > 0.0001 ? to_cam / dist : vec3(0.0, 0.0, 1.0);

	vec3 raw_side = cross(dir, view_dir);
	float edge_on = length(raw_side);
	vec3 side = edge_on > 0.0001 ? raw_side / edge_on : vec3(0.0, 1.0, 0.0);

	float taper = pow(sin(t * 3.14159265), 0.7);

	VERTEX = centre + side * across * streak_width * taper;

	alpha = COLOR.a * taper
		* smoothstep(0.08, 0.45, edge_on)
		* smoothstep(near_fade_start, near_fade_end, dist);
}

void fragment() {
	ALBEDO = tint;
	ALPHA = alpha;
}
