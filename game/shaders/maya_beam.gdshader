// Krater isik sutunu (gorsel-dunya): katli seffaf demet. Kenarlarda (bakisa teget) yogun, ortada seffaf; tepeye dogru
// soner; icinde yukari akan isik telleri. Toplamali karisim: arkasindaki gokyuzunu kapatmaz, ustune isik ekler.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, fog_disabled;

uniform vec4 color_a : source_color = vec4(0.373, 0.941, 0.784, 1.0);
uniform vec4 color_b : source_color = vec4(0.831, 0.357, 1.0, 1.0);
uniform float intensity = 1.1;

varying vec3 v_local;

float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}

void vertex() {
	v_local = VERTEX;
}

void fragment() {
	float h = v_local.y / 900.0 + 0.5;                 // 0 dip, 1 tepe
	float ndv = abs(dot(normalize(NORMAL), normalize(VIEW)));
	float edge = pow(1.0 - ndv, 1.6);                  // kenar: hacmin kalin gorundugu yer
	float core = pow(ndv, 4.0) * 0.35;                 // orta: ince parlak cekirdek
	float ang = atan(v_local.x, v_local.z);
	float strands = noise(vec2(ang * 6.0, h * 18.0 - TIME * 0.35)) * 0.6 + noise(vec2(ang * 15.0, h * 40.0 - TIME * 0.8)) * 0.4;
	float fade = smoothstep(0.0, 0.04, h) * (1.0 - smoothstep(0.35, 1.0, h));
	vec3 c = mix(color_a.rgb, color_b.rgb, smoothstep(0.2, 0.9, h + (strands - 0.5) * 0.3));
	ALBEDO = c * (edge * 0.55 + core + strands * 0.25) * fade * intensity;
}
