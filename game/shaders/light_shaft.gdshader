// Sis Korusu: tac arasindan inen isik huzmesi (dunya2, World.Fog.cs). Capraz iki kart; UV.y boyunca (0 tepe, 1 dip)
// incelen ve dipte sonen, kenarlari yumusak toplamali parilti. Gunes yuksekligine ve gun saatine gore guclenir
// (strength); kameraya yakinda ve uzakta soner. Golge dusurmez.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;

uniform vec4 shaft_color : source_color = vec4(0.96, 0.9, 0.72, 1.0);
uniform float alpha = 0.14;
uniform float strength = 1.0;
uniform float near_fade = 5.0;
uniform float far_fade = 180.0;

varying vec2 v_custom;

void vertex() {
	v_custom = INSTANCE_CUSTOM.xy;
}

void fragment() {
	float across = 1.0 - abs(UV.x * 2.0 - 1.0);
	float along = smoothstep(0.0, 0.18, UV.y) * (1.0 - smoothstep(0.55, 1.0, UV.y));
	float flicker = 0.85 + 0.15 * sin(TIME * 0.7 + v_custom.x * 40.0);
	float d = length(VERTEX);
	float fade = smoothstep(near_fade * 0.4, near_fade, d) * (1.0 - smoothstep(far_fade * 0.6, far_fade, d));
	float a = alpha * strength * pow(across, 1.6) * along * flicker * fade * (0.6 + 0.4 * v_custom.y);
	ALBEDO = shaft_color.rgb * a;
	ALPHA = 1.0;
}
