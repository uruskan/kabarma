// Ot tutamlari (SANAT-SES §6: "Ot, oyuncu yakininda orneklenmis tutamlardir"): kose rengi, ruzgar salinimi, ve
// gorunme yaricapinin kenarinda boyu kisalarak kaybolur (TEKNIK §10 "ot yaricapi").
shader_type spatial;
render_mode diffuse_lambert_wrap, specular_disabled, cull_disabled, world_vertex_coords;

uniform float wind_strength = 1.0;
uniform vec2 wind_dir = vec2(0.8, 0.6);
uniform float wind_speed = 1.6;
uniform float fade_radius = 40.0;   // m, kamera uzakligi
uniform float fade_band = 8.0;

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	float h = max(VERTEX.y - origin.y, 0.0);
	// Kose rengi alfa: bicak boyunca 0 (dip) .. 1 (uc).
	float tip = COLOR.a;
	float dist = length(VERTEX.xz - CAMERA_POSITION_WORLD.xz);
	float shrink = 1.0 - smoothstep(fade_radius - fade_band, fade_radius, dist);
	float phase = dot(VERTEX.xz, vec2(0.21, 0.17));
	float gust = sin(TIME * wind_speed + phase) * 0.7 + sin(TIME * wind_speed * 2.7 + phase * 2.1) * 0.3 + 0.3;
	vec2 sway = wind_dir * gust * wind_strength * 0.16 * tip * tip;
	VERTEX.xz = origin.xz + (VERTEX.xz - origin.xz) * mix(0.3, 1.0, shrink) + sway * shrink;
	VERTEX.y = origin.y + h * shrink;
	// Yumusak isik: normal yukari egilir (ot yigini tek yuzey gibi golgelenir).
	NORMAL = normalize(mix(NORMAL, vec3(0.0, 1.0, 0.0), 0.7));
}

void fragment() {
	ALBEDO = COLOR.rgb;
	ROUGHNESS = 0.9;
	if (!FRONT_FACING) NORMAL = -NORMAL;
}
