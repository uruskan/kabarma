// Gol suyu (gorsel-dunya; surface_water tekniklerinden): dalgacik normalleri (iki suruklenen normal haritasi, uc olcek),
// gokyuzu ve gunes yansimasi (Fresnel; motorun isik kupu ve SSR), tabanin kirilmis gorunusu (ekran dokusu, dalgaya
// gore bukulur), derinlikle once kirmiziyi yutan su (Beer-Lambert), sig yerde turkuaz, kiyida kirik kopuk ve kiyiya
// vuran ince kopuk cizgisi. Renkler biomes.json'dan (shallow, deep, foam), gun saatine gore tint (World.Sky.cs).
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, diffuse_burley, specular_schlick_ggx;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform sampler2D wave_a : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D wave_b : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform sampler2D foam_noise : filter_linear_mipmap, repeat_enable;
uniform vec4 shallow : source_color = vec4(0.5, 0.78, 0.75, 1.0);
uniform vec4 deep : source_color = vec4(0.24, 0.47, 0.70, 1.0);
uniform vec4 foam : source_color = vec4(0.93, 0.9, 0.84, 1.0);
uniform vec4 tint : source_color = vec4(1.0);
uniform float depth_scale = 0.9;   // bu derinlikte "deep" rengine varir (m)
uniform float foam_width = 0.1;    // kopuk bandi (m, dikey derinlik)
uniform float clarity = 0.6;       // 0 bulanik .. 1 berrak
uniform float wave_strength = 0.35;
uniform float refraction = 0.035;

varying vec3 world_pos;

void vertex() {
	world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

vec3 unpack(vec3 t) { return t * 2.0 - 1.0; }

void fragment() {
	vec2 p = world_pos.xz;
	float dist = length(world_pos - CAMERA_POSITION_WORLD);
	float calm = mix(1.0, 0.45, smoothstep(10.0, 80.0, dist));
	vec3 n1 = unpack(texture(wave_a, p / 6.0 + vec2(0.013, 0.006) * TIME).rgb);
	vec3 n2 = unpack(texture(wave_b, p / 2.7 - vec2(0.008, 0.017) * TIME).rgb);
	vec3 n3 = unpack(texture(wave_a, p / 1.1 + vec2(-0.021, 0.011) * TIME).rgb);
	vec2 slope = (n1.xy * 0.55 + n2.xy * 0.45 + n3.xy * 0.3 * (1.0 - smoothstep(5.0, 25.0, dist))) * wave_strength * calm;
	vec3 nw = normalize(vec3(slope.x, 1.0, slope.y));
	NORMAL = normalize((VIEW_MATRIX * vec4(nw, 0.0)).xyz);

	// Su yuzeyiyle arkasindaki zemin arasindaki dikey derinlik ve bakis yolu.
	float raw = texture(depth_tex, SCREEN_UV).r;
	vec4 v = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, raw, 1.0);
	v.xyz /= v.w;
	vec3 behind = (INV_VIEW_MATRIX * vec4(v.xyz, 1.0)).xyz;
	float depth = max(world_pos.y - behind.y, 0.0);
	float path = max(length(v.xyz) - length(VERTEX), 0.0);

	// Kirilma: tabanin goruntusu dalgaya gore kayar; kayan nokta suyun onundeyse kaydirma yok.
	vec2 bent = clamp(SCREEN_UV + slope * refraction * clamp(depth / 0.5, 0.0, 1.0), vec2(0.001), vec2(0.999));
	float braw = texture(depth_tex, bent).r;
	vec4 bv = INV_PROJECTION_MATRIX * vec4(bent * 2.0 - 1.0, braw, 1.0);
	bv.xyz /= bv.w;
	if (-bv.z < -VERTEX.z) bent = SCREEN_UV;
	vec3 floor_col = textureLod(screen_tex, bent, 0.0).rgb;

	// Yutma: once kirmizi; bulaniklik clarity ile. Sig yerde su rengi turkuaz, derinde koyu mavi.
	float murk = mix(2.2, 0.8, clarity);
	vec3 absorb = vec3(0.9, 0.32, 0.22) * murk / max(depth_scale, 0.1);
	vec3 through = exp(-absorb * path);
	float dmix = smoothstep(0.0, depth_scale, depth);
	vec3 body = mix(shallow.rgb, deep.rgb, dmix) * tint.rgb;
	float ndv = clamp(dot(nw, normalize(CAMERA_POSITION_WORLD - world_pos)), 0.0, 1.0);
	float fresnel = 0.02 + 0.98 * pow(1.0 - ndv, 5.0);

	// Kopuk: kiyida kirik bant, nefes alir; bandin disinda seyrek benekler.
	float fn = texture(foam_noise, p / 3.0 + vec2(TIME * 0.02, 0.0)).r;
	float fn2 = texture(foam_noise, p / 1.3 - vec2(0.0, TIME * 0.03)).r;
	float w = foam_width * (0.7 + 0.8 * fn) * (0.85 + 0.15 * sin(TIME * 1.1 + fn * 6.0));
	float band = 1.0 - smoothstep(w * 0.5, w, depth);
	float lace = smoothstep(0.55, 0.75, fn2 + fn * 0.3) * (1.0 - smoothstep(w, w * 3.0, depth));
	float f = clamp(max(band * smoothstep(0.35, 0.6, fn2 + 0.25), lace * 0.6), 0.0, 1.0);

	// Govde rengi isikla aydinlanir (gunes, ay, ortam); taban ekran dokusundan, zaten isikli: yayilim (emission).
	ALBEDO = mix(body * (1.0 - dot(through, vec3(0.333))) * 0.55, foam.rgb * tint.rgb, f);
	EMISSION = floor_col * through * (1.0 - fresnel) * (1.0 - f);
	ROUGHNESS = mix(0.04, 0.7, f);
	SPECULAR = 0.5;
	METALLIC = 0.0;
	// Kiyi kenarinda yumusak gecis (sert cizgi yok).
	ALPHA = clamp(smoothstep(0.0, 0.03, depth) + f, 0.0, 1.0);
}
