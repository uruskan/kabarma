// Sis Korusu: yere yakin sis katmani (dunya2, World.Fog.cs). Araziyi izleyen seyrek ag; kose rengi alfasi biyom
// agirligi. Hacimsel sis kapali oldugu icin ("yuksek" on ayar, TEKNIK §10) mavi-yesil katmanlar bu kartlarla kurulur:
// suruklenen gurultu, zemine ve nesnelere yakinlikta yumusayan kenar (proximity fade), kameraya cok yakinda seyrelir.
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform vec4 fog_color : source_color = vec4(0.66, 0.79, 0.77, 1.0);
uniform float alpha = 0.3;
uniform float strength = 1.0;      // gun saati (gece biraz koyu ve ince)
uniform sampler2D noise_tex : filter_linear_mipmap, repeat_enable;
uniform float scale = 60.0;
uniform vec2 drift = vec2(0.012, 0.007);
uniform float soft = 2.5;          // zemine ve govdelere yakinlikta yumusama (m)
uniform float near_fade = 6.0;     // kameranin bu kadar yakininda seyrelir (m)
uniform sampler2D depth_tex : hint_depth_texture, filter_linear_mipmap;

varying vec3 v_world;
varying float v_w;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_w = COLOR.a;
}

void fragment() {
	float n1 = texture(noise_tex, v_world.xz / scale + TIME * drift).r;
	float n2 = texture(noise_tex, v_world.xz / (scale * 0.37) - TIME * drift * 1.7 + 0.31).r;
	float n = smoothstep(0.28, 0.8, n1 * 0.65 + n2 * 0.35);
	// Yumusak kenar: sahnenin derinligine yakinlik.
	float depth = textureLod(depth_tex, SCREEN_UV, 0.0).r;
	vec4 up = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	float scene = -up.z / up.w;
	float self_d = -VERTEX.z;
	float edge = clamp((scene - self_d) / soft, 0.0, 1.0);
	float cam = smoothstep(near_fade * 0.3, near_fade, length(VERTEX));
	ALBEDO = fog_color.rgb;
	ALPHA = clamp(alpha * strength * v_w * (0.35 + 0.65 * n) * edge * cam, 0.0, 1.0);
}
