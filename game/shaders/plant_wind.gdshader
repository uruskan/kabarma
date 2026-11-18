// Dokulu bitkiler icin ruzgar salinimi (SANAT-SES §6: "Taclar ruzgarda kose shader'iyla salinir").
// Modelin kendi dokusu korunur. MultiMesh orneklerinde her ornek kendi evresinde salinir.
// bend_start: salinimin basladigi bagil yukseklik (agacta govde durur, tac salinir).
// Yaprak yuzeyleri (bitki-pbr): alfa makasi (golge de kesilir), puruzluluk dokusu, arkadan gelen isik (yaprak
// ince: gunes arkadayken parlar). volume_normals: kart normalleri tac hacmine egik, arka yuzde cevrilmez.
shader_type spatial;
render_mode diffuse_burley, specular_schlick_ggx, cull_disabled, world_vertex_coords;

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform bool has_albedo_tex = false;
uniform vec4 albedo_color : source_color = vec4(1.0);
uniform sampler2D normal_tex : hint_normal, filter_linear_mipmap, repeat_enable;
uniform bool has_normal_tex = false;
uniform float roughness_value = 0.85;
uniform sampler2D roughness_tex : hint_roughness_g, filter_linear_mipmap, repeat_enable;
uniform bool has_roughness_tex = false;
uniform float alpha_scissor = 0.0;
uniform float alpha_mip_boost = 0.35;
uniform float backlight_amount = 0.0;
uniform bool volume_normals = false;

uniform float wind_strength = 0.5;
uniform float model_height = 1.0;
uniform float bend_start = 0.0;
uniform vec2 wind_dir = vec2(0.8, 0.6);
uniform float wind_speed = 1.3;

void vertex() {
	vec3 origin = MODEL_MATRIX[3].xyz;
	float sc = length(MODEL_MATRIX[1].xyz);
	float h = clamp((VERTEX.y - origin.y) / max(model_height * sc, 0.01), 0.0, 1.0);
	float bend = smoothstep(bend_start, 1.0, h);
	bend *= bend;
	float phase = dot(origin.xz, vec2(0.13, 0.17));
	float gust = sin(TIME * wind_speed + phase) * 0.6 + sin(TIME * wind_speed * 2.3 + phase * 1.7) * 0.25 + 0.35;
	float flutter = sin(TIME * 7.0 + dot(VERTEX, vec3(3.1, 1.7, 2.3))) * 0.08;
	vec2 sway = wind_dir * (gust + flutter) * wind_strength * 0.12 * bend * model_height * sc;
	VERTEX.xz += sway;
	VERTEX.y -= length(sway) * 0.25 * bend;
}

void fragment() {
	vec4 base = albedo_color;
	if (has_albedo_tex) base *= texture(albedo_tex, UV);
	ALBEDO = base.rgb;
	if (alpha_scissor > 0.0) {
		// Mip ortusu: ince yapraklarin ortalama alfasi uzak mip'lerde esigin altina duser ve tac ciplak kalir.
		// Mip seviyesiyle alfa buyutulur (kapsama korunur).
		float lod = has_albedo_tex ? max(textureQueryLod(albedo_tex, UV).x, 0.0) : 0.0;
		ALPHA = base.a * (1.0 + lod * alpha_mip_boost);
		ALPHA_SCISSOR_THRESHOLD = alpha_scissor;
	}
	ROUGHNESS = has_roughness_tex ? texture(roughness_tex, UV).g : roughness_value;
	if (has_normal_tex) NORMAL_MAP = texture(normal_tex, UV).rgb;
	if (!FRONT_FACING && !volume_normals) NORMAL = -NORMAL;
	if (backlight_amount > 0.0) BACKLIGHT = base.rgb * backlight_amount;
}
