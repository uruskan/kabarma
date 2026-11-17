// Gol suyu (SANAT-SES §6): stilize, duz renkli, kiyida kopuk cizgisi, derinlige gore renk.
// Derinlik: sahne derinlik dokusundan, su yuzeyi ile alttaki zemin arasindaki dikey fark.
shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_lambert_wrap, specular_schlick_ggx;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform vec4 shallow : source_color = vec4(0.5, 0.78, 0.75, 1.0);
uniform vec4 deep : source_color = vec4(0.24, 0.47, 0.70, 1.0);
uniform vec4 foam : source_color = vec4(0.93, 0.9, 0.84, 1.0);
uniform vec4 tint : source_color = vec4(1.0);   // gun saatine gore (World.Sky.cs)
uniform float depth_scale = 0.9;   // bu derinlikte "deep" rengine varir (m)
uniform float foam_width = 0.35;   // kopuk bandi (m, dikey derinlik)
uniform float clarity = 0.6;       // kiyida saydamlik

varying vec3 world_pos;

float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float raw = texture(depth_tex, SCREEN_UV).r;
	vec4 v = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, raw, 1.0);
	v.xyz /= v.w;
	vec3 scene = (INV_VIEW_MATRIX * vec4(v.xyz, 1.0)).xyz;
	float d = max(world_pos.y - scene.y, 0.0);
	float t = smoothstep(0.0, 1.0, clamp(d / depth_scale, 0.0, 1.0));
	vec3 col = mix(shallow.rgb, deep.rgb, t);
	// Stilize dalgaciklar: iki yonde kayan gurultu, yalniz renkte (geometri duz).
	vec2 uv = world_pos.xz;
	float rip = noise(uv * 0.9 + vec2(TIME * 0.12, TIME * 0.07)) * noise(uv * 1.7 - vec2(TIME * 0.09, -TIME * 0.05));
	col = mix(col, col * 1.18 + vec3(0.03), smoothstep(0.28, 0.36, rip) * 0.5);
	// Kopuk cizgisi: kiyida sert bantli, gurultuyle kirik ve nefes alir.
	float n = noise(uv * 2.2 + vec2(TIME * 0.2, TIME * 0.13));
	float w = foam_width * (0.65 + 0.7 * n) * (0.85 + 0.15 * sin(TIME * 1.3 + n * 6.0));
	float band = 1.0 - smoothstep(w * 0.75, w, d);
	float band2 = (1.0 - smoothstep(w * 1.6, w * 1.75, d)) * smoothstep(w * 1.25, w * 1.4, d) * step(0.55, n);
	float f = max(band, band2 * 0.8);
	col = mix(col, foam.rgb, f);
	ALBEDO = col * tint.rgb;
	ROUGHNESS = mix(0.12, 0.6, f);
	SPECULAR = 0.35;
	ALPHA = clamp(mix(clarity, 0.97, t) + f, 0.0, 1.0);
}
