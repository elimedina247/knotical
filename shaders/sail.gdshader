shader_type spatial;
render_mode cull_disabled, diffuse_lambert, specular_disabled;

uniform float deployment : hint_range(0.0, 1.0, 0.001) = 1.0;
uniform float head_width = 20.0;
uniform float foot_width = 22.0;
uniform float drop = 12.0;
uniform float phase = 0.0;

uniform float pressure : hint_range(-1.0, 1.0, 0.01) = 1.0;
uniform float wind_strength : hint_range(0.0, 1.0, 0.01) = 1.0;
uniform float camber : hint_range(0.0, 0.8, 0.005) = 0.30;
uniform float belly_roundness : hint_range(0.2, 1.5, 0.01) = 0.6;
uniform float draft_position : hint_range(0.15, 0.85, 0.01) = 0.45;
uniform float draft_height : hint_range(0.1, 0.9, 0.01) = 0.55;
uniform float fill_ease : hint_range(0.05, 2.0, 0.01) = 0.55;
uniform float min_fill : hint_range(0.0, 1.0, 0.01) = 0.55;
uniform float gust_depth : hint_range(0.0, 1.0, 0.01) = 0.22;
uniform float foot_freedom : hint_range(0.1, 4.0, 0.01) = 0.8;
uniform float foot_pinch : hint_range(0.0, 0.9, 0.01) = 0.45;
uniform float foot_scallop : hint_range(0.0, 1.5, 0.01) = 0.55;
uniform float clew_tension : hint_range(0.0, 1.0, 0.01) = 0.85;

uniform float luff_knee : hint_range(0.05, 1.0, 0.01) = 0.45;
uniform float luff_amplitude : hint_range(0.0, 2.0, 0.01) = 0.55;
uniform float luff_speed : hint_range(0.0, 20.0, 0.1) = 5.0;
uniform float leech_flutter : hint_range(0.0, 2.0, 0.01) = 0.55;
uniform float furl_thickness : hint_range(0.0, 3.0, 0.01) = 0.55;

uniform vec4 canvas_color : source_color = vec4(0.855, 0.816, 0.718, 1.0);
uniform vec4 seam_color : source_color = vec4(0.702, 0.655, 0.549, 1.0);
uniform vec4 backlight_tint : source_color = vec4(0.310, 0.267, 0.196, 1.0);
uniform float seams : hint_range(0.0, 32.0, 1.0) = 9.0;
uniform float seam_width : hint_range(0.0, 0.5, 0.005) = 0.045;
uniform float roughness_value : hint_range(0.0, 1.0, 0.01) = 0.86;
uniform float wrinkle_depth : hint_range(0.0, 2.0, 0.01) = 0.75;
uniform float wrinkle_scale : hint_range(1.0, 40.0, 0.5) = 7.0;

float gust_curve(float t) {
	return 0.55 * sin(t * 0.81) + 0.30 * sin(t * 1.93 + 1.7) + 0.15 * sin(t * 3.37 + 4.1);
}

float draft_bell(float u) {
	float d = clamp(draft_position, 0.05, 0.95);
	float b = u < d ? 0.5 * u / d : 0.5 + 0.5 * (u - d) / (1.0 - d);
	return sin(PI * clamp(b, 0.0, 1.0));
}

float cusp(float x) {
	return sign(x) * pow(abs(x), 0.7);
}

float fill_amount() {
	float t = TIME + phase;
	float side = pressure < 0.0 ? -1.0 : 1.0;
	float mag = max(pow(abs(pressure), fill_ease), min_fill * wind_strength);
	return clamp(side * mag * (1.0 + gust_depth * wind_strength * gust_curve(t)), -1.25, 1.25);
}

float luff_amount() {
	return clamp(1.0 - pow(abs(pressure), fill_ease) / max(luff_knee, 0.05), 0.0, 1.0);
}

vec3 sail_point(vec2 uv) {
	float dep = clamp(deployment, 0.0, 1.0);
	float u = uv.x;
	float v = clamp(uv.y, 0.0, 1.0);
	float vv = v * dep;
	float t = TIME + phase;
	float chord = mix(head_width, foot_width, 0.5);

	float fill = fill_amount();
	float bag = abs(fill) * dep;

	float rise = pow(smoothstep(0.0, clamp(draft_height, 0.1, 0.9), v), foot_freedom);
	float taper = 1.0 - foot_pinch * smoothstep(draft_height * 0.7, 1.0, v);
	float down = rise * taper;
	float foot = smoothstep(0.55, 1.0, v);
	float side = abs(u - 0.5) * 2.0;
	float bell = draft_bell(u);

	float w = mix(head_width, foot_width, vv) * (1.0 - 0.22 * bag * smoothstep(0.15, 1.0, v));
	vec3 p = vec3((u - 0.5) * w, -vv * drop * (1.0 - 0.08 * bag * rise), 0.0);

	p.z += camber * chord * dep * pow(bell, belly_roundness) * down * fill;
	p.y += foot_scallop * bag * dep * drop * 0.22 * foot * pow(bell, 0.65);

	float corner = smoothstep(0.4, 1.0, foot) * smoothstep(0.45, 1.0, side);
	float held = 1.0 - clew_tension * corner;

	float luff = luff_amount();
	float spread = mix(0.40, 1.0, pow(side, 1.3));
	float flog = sin(t * luff_speed - u * 6.5 + v * 2.2)
		+ 0.55 * sin(t * luff_speed * 1.73 - u * 11.0 - v * 5.0);
	p.z += luff_amplitude * chord * 0.11 * luff * wind_strength * dep * rise * spread * held
		* cusp(flog * 0.65);

	float trem = max(smoothstep(0.55, 1.0, side), foot) * (0.35 + 0.65 * luff) * held;
	p.z += leech_flutter * chord * 0.012 * trem * wind_strength * dep
		* sin(t * luff_speed * 2.1 + u * 13.0 + v * 6.0);

	float roll = (1.0 - dep) * furl_thickness;
	float ang = v * PI;
	p.z += roll * sin(ang) * (0.65 + 0.35 * sin(u * 23.0));
	p.y -= roll * (1.0 - cos(ang)) * 0.35;

	return p;
}

float wrinkle_field(vec2 uv) {
	float t = TIME + phase;
	float tension = clamp(pow(abs(pressure), fill_ease), 0.0, 1.0) * clamp(deployment, 0.0, 1.0);

	vec2 cl = uv - vec2(0.03, 1.0);
	vec2 cr = uv - vec2(0.97, 1.0);
	float dl = length(cl);
	float dr = length(cr);
	float fan = sin(atan(cl.y, cl.x) * wrinkle_scale) * exp(-dl * 2.0) * smoothstep(0.0, 0.10, dl)
		+ sin(atan(cr.y, cr.x) * wrinkle_scale) * exp(-dr * 2.0) * smoothstep(0.0, 0.10, dr);

	float slack = sin((uv.x * 7.0 + sin(uv.y * 5.0 + t * 0.6) * 1.1) * PI + t * 1.4)
		* smoothstep(0.0, 0.45, uv.y);

	float gather = sin(uv.x * wrinkle_scale * 2.2) * exp(-uv.y * 6.0);

	return (fan * 0.55 * tension + slack * 0.7 * (1.0 - tension) + gather * 0.45) * wrinkle_depth;
}

void vertex() {
	float du = 0.006;
	float dv = 0.006;

	vec3 p = sail_point(UV);
	vec3 tu = sail_point(vec2(UV.x + du, UV.y)) - sail_point(vec2(UV.x - du, UV.y));
	vec3 tv = sail_point(vec2(UV.x, UV.y + dv)) - sail_point(vec2(UV.x, UV.y - dv));

	vec3 n = normalize(cross(tv, tu));
	vec3 tang = normalize(tu);
	vec3 bin = normalize(cross(n, tang));
	bin *= dot(bin, tv) < 0.0 ? -1.0 : 1.0;

	VERTEX = p;
	NORMAL = n;
	TANGENT = tang;
	BINORMAL = bin;
}

void fragment() {
	float e = 0.0035;
	float gx = wrinkle_field(UV + vec2(e, 0.0)) - wrinkle_field(UV - vec2(e, 0.0));
	float gy = wrinkle_field(UV + vec2(0.0, e)) - wrinkle_field(UV - vec2(0.0, e));
	vec3 wn = normalize(vec3(-vec2(gx, gy) / (2.0 * e) * 0.02, 1.0));

	float creases = wrinkle_field(UV);
	float shade = 1.0 - 0.10 * clamp(-creases, 0.0, 1.0);

	float s = fract(UV.x * seams);
	float line = 1.0 - smoothstep(0.0, seam_width, min(s, 1.0 - s));
	float band = min(min(UV.x, 1.0 - UV.x), min(UV.y, 1.0 - UV.y));
	float tabling = 1.0 - smoothstep(0.012, 0.030, band);
	float weave = 0.97 + 0.03 * sin(UV.y * 140.0);

	ALBEDO = mix(canvas_color.rgb, seam_color.rgb, max(line, tabling * 0.85)) * weave * shade;
	NORMAL_MAP = wn * 0.5 + 0.5;
	NORMAL_MAP_DEPTH = 0.35;
	ROUGHNESS = clamp(roughness_value - 0.06 * line, 0.0, 1.0);
	METALLIC = 0.0;
	SPECULAR = 0.12;
	BACKLIGHT = backlight_tint.rgb;
}

void light() {
	float ndl = dot(NORMAL, LIGHT) * ATTENUATION;
	float lit = smoothstep(0.08, 0.13, ndl);
	float mid = smoothstep(0.5, 0.55, ndl);
	DIFFUSE_LIGHT += ALBEDO * LIGHT_COLOR / PI * mix(0.35, 1.0, lit) * mix(0.8, 1.0, mid);
}
