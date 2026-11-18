// Ot alani (gorsel-dunya, GrassField.cs): kameraya bagli bir kafes; her ornek bir tutam. Kafes ornek araligina
// oturtularak tasinir, kokler dunyada sabit kalir. Kok konumu, donus, boy, renk dunya hucresinin karmasindan;
// yukseklik ve ot sikligi dunya dokularindan (sikligi CPU hesaplar: World.Ground.cs). Kose basina en cok 4 doku okumasi.
// Uzakliga gore tutamlar sirayla seyrelir (kalanlar genisler), halkanin kenarinda erir. Ruzgar: suruklenen gurultu
// lekeleri (esinti) + dalgacik + bicak titremesi; egilen bicak boyunu korur. Oyuncu ayagi otu iter.
// Kose rengi alfa: bicak boyunca 0 (dip) .. 1 (uc). UV.x: bicak sirasi (titreme payi).
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx, skip_vertex_transform;

uniform sampler2D height_tex : filter_linear, repeat_disable;
uniform float height_res = 301.0;
uniform sampler2D control : filter_linear, repeat_disable;
uniform sampler2D macro_noise : filter_linear_mipmap, repeat_enable;
uniform float world_size = 300.0;
uniform float spacing = 0.3;
uniform float radius_in = 0.0;     // halkanin ic kenari (0: yok); ic kenarda fade_band boyunca belirir
uniform float radius_out = 24.0;
uniform float fade_band = 4.0;
uniform float keep_far = 0.5;      // dis kenarda kalan tutam payi
uniform float widen = 1.0;         // uzak halka tutamlari genis
uniform vec2 blade_height = vec2(0.3, 0.6);
uniform float wind_strength = 1.0;
uniform vec2 wind_dir = vec2(0.8, 0.6);
uniform vec4 trample = vec4(0.0);  // xyz oyuncu ayagi, w yaricap
uniform vec4 lush_root : source_color = vec4(0.10, 0.16, 0.05, 1.0);
uniform vec4 lush_tip : source_color = vec4(0.45, 0.58, 0.22, 1.0);
uniform vec4 dry_root : source_color = vec4(0.22, 0.18, 0.08, 1.0);
uniform vec4 dry_tip : source_color = vec4(0.86, 0.72, 0.38, 1.0);
uniform float translucency = 0.45;
// Biyom karisimi (dunya2): R = Sis Korusu agirligi; Koru otu mavi-yesil.
uniform sampler2D biome_tex : filter_linear, repeat_disable;
uniform vec4 biome_tip : source_color = vec4(0.3, 0.5, 0.45, 1.0);
uniform vec4 biome_root : source_color = vec4(0.05, 0.1, 0.1, 1.0);

varying vec3 v_col;
varying float v_t;
varying vec3 v_up;

float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }

void vertex() {
	vec3 cellw = MODEL_MATRIX[3].xyz;
	vec2 cell = floor(cellw.xz / spacing + 0.5);
	float r1 = h21(cell), r2 = h21(cell + 17.31), r3 = h21(cell + 41.7), r4 = h21(cell + 73.1), r5 = h21(cell + 5.9);
	vec2 root = (cell + (vec2(r1, r2) - 0.5) * 0.95) * spacing;
	float dist = length(root - CAMERA_POSITION_WORLD.xz);

	// Once dokusuz eleme (uzaklik), sonra tek doku okumasiyla ot sikligi (World.Ground.cs, denetim A).
	float keep = mix(1.0, keep_far, smoothstep(radius_in, radius_out, dist));
	keep *= 1.0 - smoothstep(radius_out - fade_band, radius_out, dist);
	if (radius_in > 0.0) keep *= smoothstep(radius_in - fade_band * 0.5, radius_in + fade_band * 0.5, dist);
	float size = 0.0;
	vec4 ctl = vec4(0.0);
	if (keep > r3) {
		ctl = textureLod(control, (root + world_size * 0.5) / world_size, 0.0);
		size = clamp((keep * ctl.a - r3) / 0.08, 0.0, 1.0);
	}

	v_t = COLOR.a;
	v_col = vec3(0.0);
	v_up = vec3(0.0, 1.0, 0.0);	if (size <= 0.001) {
		VERTEX = vec3(0.0, 0.0, 1e6); // kameranin arkasina, tum ucgenler bir noktada
		NORMAL = vec3(0.0, 0.0, 1.0);
	} else {
		float gh = textureLod(height_tex, (root + world_size * 0.5 + 0.5) / height_res, 0.0).r;
		float m1 = textureLod(macro_noise, root / 110.0, 0.0).r;
		float m3 = r5 * 0.6 + 0.2;
		float m2 = r1 * 0.5 + 0.25;
		float allow = ctl.a;
		float lush = smoothstep(0.3, 0.6, m1 + (m3 - 0.5) * 0.25);
		float ang = r4 * 6.2831853;
		float ca = cos(ang), sa = sin(ang);
		float wide = widen * clamp(inversesqrt(max(keep, 0.25)), 1.0, 1.6);
		float height = mix(blade_height.x, blade_height.y, r5) * mix(0.75, 1.15, lush) * mix(0.6, 1.0, size) * mix(0.75, 1.1, m3);
		vec2 off = vec2(ca * VERTEX.x - sa * VERTEX.z, sa * VERTEX.x + ca * VERTEX.z) * wide * mix(0.85, 1.15, r2);
		float t = COLOR.a;
		vec3 world = vec3(root.x + off.x, gh + VERTEX.y * height, root.y + off.y);
		vec3 n = vec3(ca * NORMAL.x - sa * NORMAL.z, NORMAL.y, sa * NORMAL.x + ca * NORMAL.z);

		// Ruzgar: esinti lekeleri ruzgar yonunde suruklenir.
		vec2 wd = normalize(wind_dir);
		float gust = textureLod(macro_noise, root / 45.0 - wd * TIME * 0.06, 0.0).r;
		float ripple = sin(dot(root, wd) * 0.6 - TIME * 2.2 + r1 * 6.2831) * 0.5 + 0.5;
		float push = wind_strength * (0.15 + 1.1 * smoothstep(0.35, 0.75, gust)) * (0.6 + 0.4 * ripple);
		float flutter = sin(TIME * (3.5 + 2.0 * UV.x) + r2 * 40.0 + UV.x * 9.0) * 0.06 * wind_strength;
		vec3 wside = vec3(-wd.y, 0.0, wd.x);
		vec3 bend = vec3(wd.x, 0.0, wd.y) * (push * 0.32 * t * t * height) + wside * (flutter * t * height);
		if (trample.w > 0.0) {
			vec2 away = world.xz - trample.xz;
			float dd = length(away);
			float k = (1.0 - smoothstep(trample.w * 0.3, trample.w, dd)) * step(abs(gh - trample.y), 1.5);
			bend.xz += away / max(dd, 1e-3) * k * 0.7 * t * height;
		}
		float b = length(bend);
		world += bend - vec3(0.0, min(b * b / max(height, 0.05), height * 0.6) * t, 0.0);
		n = normalize(n - vec3(0.0, 1.0, 0.0) * dot(bend, n) * 1.5 / max(height, 0.1));

		vec3 root_c = mix(dry_root.rgb, lush_root.rgb, lush);
		vec3 tip_c = mix(dry_tip.rgb, lush_tip.rgb, lush);
		// Tutam basina ton: bazilari daha kuru, bazilari daha taze; patika kenari kurumus.
		float dry = clamp((r1 - 0.6) * 0.5 + ctl.r * 1.5 + (1.0 - allow) * 0.25, 0.0, 1.0);
		tip_c = mix(tip_c, dry_tip.rgb * 0.9, dry * 0.6);
		v_col = mix(root_c, tip_c, smoothstep(0.0, 1.0, t)) * mix(0.85, 1.12, r5) * (0.9 + 0.2 * m2);

		float kb = textureLod(biome_tex, (root + world_size * 0.5) / world_size, 0.0).r;
		if (kb > 0.003) v_col = mix(v_col, mix(biome_root.rgb, biome_tip.rgb, smoothstep(0.0, 1.0, t)) * mix(0.85, 1.12, r5), kb);
		v_up = normalize((VIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
		VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
		NORMAL = normalize((VIEW_MATRIX * vec4(n, 0.0)).xyz);
	}
}

void fragment() {
	vec3 n = FRONT_FACING ? NORMAL : -NORMAL;
	// Isik: bicak normali tutamin kubbesine dogru yumusar (uzakta duzlesir, titreme yok).
	float far = smoothstep(4.0, 30.0, length(VERTEX));
	NORMAL = normalize(mix(n, v_up, mix(0.35, 0.75, far)));
	ALBEDO = v_col;
	ROUGHNESS = 0.55;
	SPECULAR = 0.3;
	AO = mix(0.35, 1.0, smoothstep(0.0, 0.7, v_t));
	AO_LIGHT_AFFECT = 0.55;
	BACKLIGHT = v_col * translucency * v_t;
}
