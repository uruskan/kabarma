// Doku yok, renk var: kose rengi + yumusak golgeleme (SANAT-SES §1).
// Kabarma icin: sisme, yay titremesi ve Maya damarlari ayni shader'da.
shader_type spatial;
render_mode diffuse_lambert_wrap, specular_disabled;

uniform float swell : hint_range(0.0, 1.0) = 0.0;      // 0 normal, 1 tam kabarmis
uniform float wobble_amp = 0.04;
uniform float glow : hint_range(0.0, 4.0) = 0.0;       // damar isimasi
uniform vec4 maya_a : source_color = vec4(0.373, 0.941, 0.784, 1.0);
uniform vec4 maya_b : source_color = vec4(0.831, 0.357, 1.0, 1.0);
uniform float flash = 0.0;                              // isabet parlamasi
uniform float rim = 0.25;
uniform vec4 tint : source_color = vec4(1.0);
uniform float vein_scale = 3.5;
// Uretim hattindan (Blender, glTF COLOR_0) gelen kose renkleri Godot'a dogrusal gelir; koddan kurulanlar sRGB.
uniform bool vertex_linear = false;

varying vec3 obj_pos;

// Palet renkleri sRGB yazili; isik hesabi dogrusal ister.
vec3 to_linear(vec3 c) { return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c)); }

float hash(vec3 p) { return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453); }
float noise(vec3 p) {
	vec3 i = floor(p); vec3 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i), hash(i + vec3(1,0,0)), f.x), mix(hash(i + vec3(0,1,0)), hash(i + vec3(1,1,0)), f.x), f.y),
	           mix(mix(hash(i + vec3(0,0,1)), hash(i + vec3(1,0,1)), f.x), mix(hash(i + vec3(0,1,1)), hash(i + vec3(1,1,1)), f.x), f.y), f.z);
}

void vertex() {
	obj_pos = VERTEX;
	float w = sin(TIME * 7.0 + VERTEX.y * 6.0 + VERTEX.x * 3.0) * wobble_amp * swell;
	VERTEX += NORMAL * w;
}

void fragment() {
	vec3 base = (vertex_linear ? COLOR.rgb : to_linear(COLOR.rgb)) * tint.rgb;
	ALBEDO = base;
	ROUGHNESS = 0.85;
	float n = noise(obj_pos * vein_scale + vec3(0.0, TIME * 0.6, 0.0));
	float vein = smoothstep(0.47, 0.5, n) * (1.0 - smoothstep(0.5, 0.53, n));
	vec3 maya = mix(maya_a.rgb, maya_b.rgb, 0.5 + 0.5 * sin(TIME * 1.7 + obj_pos.y * 2.0));
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	EMISSION = maya * (vein * 2.5 + fres * 0.8) * glow + vec3(1.0, 0.95, 0.85) * flash + base * fres * rim * 0.35;
}
