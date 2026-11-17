// Arazi (gorsel-dunya): bes dosenebilir PBR katmani (World.Ground.cs), dunya XZ'de olculur.
// Katman sirasi: cimen taban; ustune toprak (patika, aciklik), kum (gol kiyisi), tarla (surulmus), kaya (egim).
// Gecisler katmanlarin kendi yuksekligiyle kirilir (cakil toprak cimenin arasindan cikar). Tekrar kirici: iki olcek,
// biri donuk; buyuk olcekli gurultu rengi ve olcek payini degistirir. Kaya egimde uc duzlemli (ayrinti 2).
// Gol kenarinda islaklik (koyu, parlak). Kabarma gecesi Maya damarlari (glow, vein_scale) toon_vc'deki gibi.
// Normal dokusu: R,G = dunya X ve Z bileseni (pipeline/gen_terrain_textures.py), B puruzluluk, A AO.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx;

uniform sampler2DArray layers_albedo : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2DArray layers_nrm : filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D control : filter_linear, repeat_disable;
uniform sampler2D macro_noise : filter_linear_mipmap, repeat_enable;
uniform float world_size = 300.0;
// Karo boylari (m): cimen, toprak, kaya, kum; tarla ayri.
uniform vec4 tile_m = vec4(3.0, 3.0, 6.0, 2.5);
uniform float tarla_tile = 2.0;
uniform vec3 tint_lush = vec3(1.0);
uniform vec3 tint_dry = vec3(1.0);
uniform float normal_strength = 1.0;
uniform vec2 rock_slope = vec2(0.22, 0.38);   // 1 - n.y araligi: kaya baslar, tam kaya
uniform float water_level = -1000.0;
uniform int detail = 2;

uniform float glow : hint_range(0.0, 4.0) = 0.0;
uniform float vein_scale = 0.35;
uniform vec4 maya_a : source_color = vec4(0.373, 0.941, 0.784, 1.0);
uniform vec4 maya_b : source_color = vec4(0.831, 0.357, 1.0, 1.0);
uniform float flash = 0.0;
uniform float wobble_amp = 0.0; // MeshKit canli malzemesiyle uyum (kullanilmaz)
uniform float rim = 0.0;

varying vec3 v_world;
varying vec3 v_nw;

const float L_CIMEN = 0.0;
const float L_TOPRAK = 1.0;
const float L_KAYA = 2.0;
const float L_KUM = 3.0;
const float L_TARLA = 4.0;

float hash(vec3 p) { p = fract(p * 0.1031); p += dot(p, p.zyx + 31.32); return fract((p.x + p.y) * p.z); }
float vnoise(vec3 p) {
	vec3 i = floor(p); vec3 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash(i), hash(i + vec3(1,0,0)), f.x), mix(hash(i + vec3(0,1,0)), hash(i + vec3(1,1,0)), f.x), f.y),
	           mix(mix(hash(i + vec3(0,0,1)), hash(i + vec3(1,0,1)), f.x), mix(hash(i + vec3(0,1,1)), hash(i + vec3(1,1,1)), f.x), f.y), f.z);
}

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_nw = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

// Bir katmanin ust izdusumu: iki olcek (ikincisi donuk ve 3,7 kat buyuk), pay buyuk olcekli gurultuden.
void planar(float layer, vec2 xz, float tile, float mixk, out vec4 alb, out vec4 nrm) {
	vec2 uv1 = xz / tile;
	alb = texture(layers_albedo, vec3(uv1, layer));
	nrm = texture(layers_nrm, vec3(uv1, layer));
	if (detail > 0) {
		const float c = 0.6967067; const float s = 0.7173561; // ~45,8 derece
		vec2 uv2 = vec2(c * xz.x - s * xz.y, s * xz.x + c * xz.y) / (tile * 3.7) + vec2(0.37, 0.71);
		vec4 a2 = texture(layers_albedo, vec3(uv2, layer));
		vec4 n2 = texture(layers_nrm, vec3(uv2, layer));
		alb = mix(alb, a2, mixk);
		nrm = mix(nrm, n2, mixk);
	}
}

// Yukseklik farkiyla kirilan karisim: w 0 ve 1'de aynen kalir, arada ust katmanin yuksekligi kenari yer.
float hblend(float w, float h_top, float h_base) {
	float t = w + (h_top - h_base) * min(w, 1.0 - w) * 1.6;
	return clamp((t - 0.5) * 2.2 + 0.5, 0.0, 1.0);
}

void fragment() {
	vec2 xz = v_world.xz;
	vec3 ng = normalize(v_nw);
	float dist = length(v_world - CAMERA_POSITION_WORLD);
	float far = smoothstep(15.0, 140.0, dist);
	vec4 ctl = texture(control, (xz + world_size * 0.5) / world_size);
	float m1 = texture(macro_noise, xz / 110.0).r;   // buyuk lekeler (yesil / altin)
	float m2 = texture(macro_noise, xz / 27.0 + 0.5).r;
	float m3 = texture(macro_noise, xz / 7.0 + 0.25).r;
	float mixk = clamp(0.25 + 0.5 * m2 + 0.35 * far, 0.15, 0.85);

	// Cimen taban: yesil-altin ton buyuk olcekte degisir.
	vec4 alb; vec4 nrm;
	planar(L_CIMEN, xz, tile_m.x, mixk, alb, nrm);
	float lush = smoothstep(0.3, 0.6, m1 + (m3 - 0.5) * 0.25);
	alb.rgb *= mix(tint_dry, tint_lush, lush) * (0.86 + 0.28 * m2);

	// Toprak (patika, aciklik).
	float w_toprak = ctl.r;
	if (w_toprak > 0.003) {
		vec4 a; vec4 n;
		planar(L_TOPRAK, xz, tile_m.y, mixk, a, n);
		a.rgb *= 0.92 + 0.16 * m2;
		float t = hblend(w_toprak, a.a, alb.a);
		alb = mix(alb, a, t); nrm = mix(nrm, n, t);
	}
	// Kum (gol kiyisi).
	float w_kum = ctl.g;
	if (w_kum > 0.003) {
		vec4 a; vec4 n;
		planar(L_KUM, xz, tile_m.w, mixk, a, n);
		float t = hblend(w_kum, a.a, alb.a);
		alb = mix(alb, a, t); nrm = mix(nrm, n, t);
	}
	// Tarla (surulmus): tek olcek, egat yonu sabit.
	float w_tarla = ctl.b;
	if (w_tarla > 0.003) {
		vec4 a = texture(layers_albedo, vec3(xz / tarla_tile, L_TARLA));
		vec4 n = texture(layers_nrm, vec3(xz / tarla_tile, L_TARLA));
		float t = hblend(w_tarla, a.a, alb.a * 0.6);
		alb = mix(alb, a, t); nrm = mix(nrm, n, t);
	}

	// Egim: kaya. Gurultuyle kirik sinir; tepelerde biraz erken baslar.
	float slope = 1.0 - ng.y;
	float w_kaya = smoothstep(rock_slope.x, rock_slope.y, slope + (m3 - 0.5) * 0.12 + (m2 - 0.5) * 0.08);
	vec3 pert = vec3(0.0);
	if (w_kaya > 0.003) {
		vec4 a; vec4 n;
		vec3 kp = vec3(0.0);
		if (detail >= 2) {
			// Uc duzlemli: X ve Z yuzlerinde yandan izdusum, ustte duz.
			vec3 bw = pow(abs(ng), vec3(4.0));
			bw /= (bw.x + bw.y + bw.z);
			vec3 p = v_world / tile_m.z;
			vec4 ax = texture(layers_albedo, vec3(p.zy, L_KAYA)); vec4 nx = texture(layers_nrm, vec3(p.zy, L_KAYA));
			vec4 ay = texture(layers_albedo, vec3(p.xz, L_KAYA)); vec4 ny = texture(layers_nrm, vec3(p.xz, L_KAYA));
			vec4 az = texture(layers_albedo, vec3(p.xy, L_KAYA)); vec4 nz = texture(layers_nrm, vec3(p.xy, L_KAYA));
			a = ax * bw.x + ay * bw.y + az * bw.z;
			n = nx * bw.x + ny * bw.y + nz * bw.z;
			// Yan yuzlerde dokunun egimi dunyaya: x duzleminde (z, y), z duzleminde (x, y).
			vec2 dx = nx.rg * 2.0 - 1.0; vec2 dy = ny.rg * 2.0 - 1.0; vec2 dz = nz.rg * 2.0 - 1.0;
			kp = vec3(0.0, dx.y, dx.x) * bw.x
			   + vec3(dy.x, 0.0, dy.y) * bw.y
			   + vec3(dz.x, dz.y, 0.0) * bw.z;
		} else {
			planar(L_KAYA, xz, tile_m.z, mixk, a, n);
			vec2 d = n.rg * 2.0 - 1.0;
			kp = vec3(d.x, 0.0, d.y);
		}
		a.rgb *= 0.9 + 0.2 * m2;
		float t = hblend(w_kaya, a.a, alb.a * 0.8);
		vec2 d0 = nrm.rg * 2.0 - 1.0;
		pert = mix(vec3(d0.x, 0.0, d0.y), kp, t);
		alb = mix(alb, a, t); nrm = mix(nrm, n, t);
	} else {
		vec2 d0 = nrm.rg * 2.0 - 1.0;
		pert = vec3(d0.x, 0.0, d0.y);
	}

	// Uzakta ayrinti normali soner (titreme yok), renk degisimi kalir.
	float ns = normal_strength * mix(1.0, 0.45, far);
	vec3 n_w = normalize(ng + pert * ns * 1.4);

	// Islaklik: su seviyesinin hemen ustu ve alti koyu, parlak.
	float wet = smoothstep(water_level + 0.45, water_level - 0.02, v_world.y);
	vec3 col = alb.rgb * mix(1.0, 0.58, wet);
	float rough = mix(nrm.b, 0.18, wet * 0.85);

	// Buyuk olcekli parlaklik dalgalanmasi (tekrar kirici; uzakta etkili).
	col *= mix(1.0, 0.82 + 0.36 * m1, 0.35 + 0.4 * far);

	ALBEDO = col;
	ROUGHNESS = rough;
	SPECULAR = 0.4;
	AO = mix(nrm.a, 1.0, far * 0.6);
	AO_LIGHT_AFFECT = 0.35;
	NORMAL = normalize((VIEW_MATRIX * vec4(n_w, 0.0)).xyz);

	// Maya damarlari (Kabarma gecesi).
	if (glow > 0.0) {
		float vn = vnoise(v_world * vein_scale + vec3(0.0, TIME * 0.6, 0.0));
		float vein = smoothstep(0.485, 0.5, vn) * (1.0 - smoothstep(0.5, 0.515, vn));
		vec3 maya = mix(maya_a.rgb, maya_b.rgb, 0.5 + 0.5 * sin(TIME * 1.7 + v_world.y * 2.0));
		EMISSION = maya * vein * 1.3 * glow;
	}
	EMISSION += vec3(1.0, 0.95, 0.85) * flash;
}
