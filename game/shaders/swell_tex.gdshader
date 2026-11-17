// Dokulu modeller icin Kabarma malzemesi (SANAT-SES §4): kendi dokusunu korur, ustune
// sisme (normal yonunde), yay titremesi, Maya damarlari, kenar isimasi ve isabet parlamasi ekler.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap, repeat_enable;
uniform bool has_albedo_tex = false;
uniform vec4 albedo_color : source_color = vec4(1.0);
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap, repeat_enable;
uniform bool has_normal_tex = false;
uniform float roughness_value = 0.8;

uniform float swell : hint_range(0.0, 1.0) = 0.0;
uniform float swell_push = 0.0;        // metre: normal yonunde ek sisme (olcek disinda, yumusak gorunum)
uniform float wobble_amp = 0.03;
uniform float glow : hint_range(0.0, 4.0) = 0.0;
uniform float vein_scale = 3.0;
uniform vec4 maya_a : source_color = vec4(0.373, 0.941, 0.784, 1.0);
uniform vec4 maya_b : source_color = vec4(0.831, 0.357, 1.0, 1.0);
uniform float flash = 0.0;
uniform vec4 tint : source_color = vec4(1.0);

varying vec3 obj_pos;

float hash(vec3 p) { p = fract(p * 0.1031); p += dot(p, p.zyx + 31.32); return fract((p.x + p.y) * p.z); }
float noise(vec3 p) {
	vec3 i = floor(p); vec3 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i), hash(i + vec3(1,0,0)), f.x), mix(hash(i + vec3(0,1,0)), hash(i + vec3(1,1,0)), f.x), f.y),
	           mix(mix(hash(i + vec3(0,0,1)), hash(i + vec3(1,0,1)), f.x), mix(hash(i + vec3(0,1,1)), hash(i + vec3(1,1,1)), f.x), f.y), f.z);
}

void vertex() {
	obj_pos = VERTEX;
	float w = sin(TIME * 7.0 + VERTEX.y * 6.0 + VERTEX.x * 3.0) * wobble_amp * swell;
	VERTEX += NORMAL * (w + swell_push * swell);
}

void fragment() {
	vec4 base = albedo_color;
	if (has_albedo_tex) base *= texture(albedo_tex, UV);
	ALBEDO = base.rgb * tint.rgb;
	ROUGHNESS = roughness_value;
	if (has_normal_tex) NORMAL_MAP = texture(normal_tex, UV).rgb;
	float n = noise(obj_pos * vein_scale + vec3(0.0, TIME * 0.6, 0.0));
	float vein = smoothstep(0.47, 0.5, n) * (1.0 - smoothstep(0.5, 0.53, n));
	vec3 maya = mix(maya_a.rgb, maya_b.rgb, 0.5 + 0.5 * sin(TIME * 1.7 + obj_pos.y * 2.0));
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	EMISSION = maya * (vein * 2.2 + fres * 0.9) * glow + vec3(1.0, 0.95, 0.85) * flash;
}
