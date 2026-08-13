shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform float deployment : hint_range(0.0, 1.0, 0.001) = 1.0;
uniform float head_width = 20.0;
uniform float foot_width = 22.0;
uniform float drop = 12.0;
uniform float camber : hint_range(0.0, 0.8, 0.005) = 0.16;
uniform float pressure : hint_range(-1.0, 1.0, 0.01) = 1.0;
uniform float furl_thickness : hint_range(0.0, 3.0, 0.01) = 0.55;
uniform float luff_amplitude : hint_range(0.0, 2.0, 0.01) = 0.35;
uniform float luff_speed : hint_range(0.0, 20.0, 0.1) = 5.0;

uniform vec4 canvas_color : source_color = vec4(0.855, 0.816, 0.718, 1.0);
uniform vec4 seam_color : source_color = vec4(0.702, 0.655, 0.549, 1.0);
uniform vec4 backlight_tint : source_color = vec4(0.310, 0.267, 0.196, 1.0);
uniform float seams : hint_range(0.0, 32.0, 1.0) = 9.0;
uniform float seam_width : hint_range(0.0, 0.5, 0.005) = 0.045;
uniform float roughness_value : hint_range(0.0, 1.0, 0.01) = 0.86;

vec3 sail_point(vec2 uv) {
	float dep = clamp(deployment, 0.0, 1.0);
	float u = uv.x;
	float v = uv.y;
	float vv = v * dep;

	float w = mix(head_width, foot_width, vv);
	vec3 p = vec3((u - 0.5) * w, -vv * drop, 0.0);

	float across = sin(PI * u);
	float down = sin(PI * pow(clamp(v, 0.0, 1.0), 0.8));
	float belly = camber * drop * dep * pow(abs(across), 0.8) * down;

	float luff = 1.0 - abs(pressure);
	float flutter = luff_amplitude * luff * dep * down * across
		* sin(TIME * luff_speed + u * 9.0 + v * 5.0);

	p.z += belly * pressure + flutter;

	float roll = (1.0 - dep) * furl_thickness;
	float ang = v * PI;
	p.z += roll * sin(ang) * (0.65 + 0.35 * sin(u * 23.0));
	p.y -= roll * (1.0 - cos(ang)) * 0.35;

	return p;
}

void vertex() {
	float du = 0.008;
	float dv = 0.008;

	vec3 p = sail_point(UV);
	vec3 tu = sail_point(vec2(UV.x + du, UV.y)) - sail_point(vec2(UV.x - du, UV.y));
	vec3 tv = sail_point(vec2(UV.x, UV.y + dv)) - sail_point(vec2(UV.x, UV.y - dv));

	vec3 n = normalize(cross(tv, tu));

	VERTEX = p;
	NORMAL = n;
	TANGENT = normalize(tu);
	BINORMAL = normalize(cross(n, TANGENT));
}

void fragment() {
	float s = fract(UV.x * seams);
	float edge = min(s, 1.0 - s);
	float line = 1.0 - smoothstep(0.0, seam_width, edge);

	float weave = 0.97 + 0.03 * sin(UV.y * 140.0);

	ALBEDO = mix(canvas_color.rgb, seam_color.rgb, line) * weave;
	ROUGHNESS = roughness_value;
	METALLIC = 0.0;
	SPECULAR = 0.15;
	BACKLIGHT = backlight_tint.rgb;
}
