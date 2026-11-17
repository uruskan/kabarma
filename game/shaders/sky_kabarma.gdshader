// Gokyuzu (SANAT-SES §6): palet bantlari, gunes ve iki ay, stilize bulut, gece yildizlar; Kabarma gecesinde
// ufukta Maya aurorasi (SANAT-SES §2). Renkler ve acilar gun saatine gore World.Sky.cs'den gelir (data/biomes.json).
shader_type sky;

uniform vec4 top_color : source_color = vec4(0.55, 0.78, 0.91, 1.0);
uniform vec4 horizon_color : source_color = vec4(0.95, 0.85, 0.64, 1.0);
uniform vec4 ground_color : source_color = vec4(0.6, 0.5, 0.4, 1.0);
uniform float horizon_curve = 0.35;

uniform vec3 sun_dir = vec3(0.0, 1.0, 0.0);
uniform vec4 sun_color : source_color = vec4(1.0, 0.95, 0.87, 1.0);
uniform float sun_disk = 1.0;
uniform float sun_size = 0.035;

uniform vec3 moon_dir = vec3(0.0, 0.5, 0.86);
uniform vec3 moon2_dir = vec3(0.3, 0.6, 0.7);
uniform vec4 moon_color : source_color = vec4(0.86, 0.88, 0.96, 1.0);
uniform vec4 moon2_color : source_color = vec4(0.95, 0.80, 0.70, 1.0);
uniform float moon_disk = 0.0;
uniform float moon_size = 0.06;

uniform float stars = 0.0;
uniform float aurora = 0.0;
uniform vec4 aurora_a : source_color = vec4(0.373, 0.941, 0.784, 1.0);
uniform vec4 aurora_b : source_color = vec4(0.831, 0.357, 1.0, 1.0);

uniform float clouds = 0.4;
uniform vec4 cloud_color : source_color = vec4(1.0);
uniform vec4 cloud_shade : source_color = vec4(0.8, 0.8, 0.9, 1.0);
uniform sampler2D cloud_noise : filter_linear_mipmap, repeat_enable;
uniform float cloud_speed = 0.004;

float hash3(vec3 p) { p = fract(p * 0.1031); p += dot(p, p.zyx + 31.32); return fract((p.x + p.y) * p.z); }

vec3 moon(vec3 eye, vec3 dir, float size, vec3 col, vec3 lit_from) {
	float d = dot(eye, dir);
	float disk = smoothstep(cos(size * 1.08), cos(size), d);
	if (disk <= 0.0) return vec3(0.0);
	// Hilal: diske ay isigi yonunden bir golge diski kayar.
	vec3 side = normalize(lit_from - dir * dot(lit_from, dir) + vec3(1e-4));
	float lit = smoothstep(cos(size * 1.02), cos(size * 0.98), dot(eye, normalize(dir - side * size * 0.55)));
	return col * disk * mix(0.18, 1.0, 1.0 - lit * 0.75);
}

void sky() {
	vec3 eye = EYEDIR;
	float up = eye.y;
	// Palet bantlari: tepe -> ufuk; ufkun altinda zemin rengi (ufuk tepeleri ve sis cogunu kapatir).
	float k = pow(clamp(1.0 - max(up, 0.0), 0.0, 1.0), 1.0 / max(horizon_curve, 0.05));
	vec3 col = mix(top_color.rgb, horizon_color.rgb, k);
	col = mix(col, ground_color.rgb, smoothstep(0.0, -0.08, up));

	{
		// Yildizlar: hucre basina bir yildiz, ufka dogru soner. Isik kupunde (radiance) gerekmez.
		if (!AT_CUBEMAP_PASS && stars > 0.0 && up > 0.0) {
			vec3 p = eye * 220.0;
			vec3 cell = floor(p);
			float h = hash3(cell);
			if (h > 0.985) {
				vec3 c = cell + 0.5 + (vec3(hash3(cell + 1.7), hash3(cell + 4.1), hash3(cell + 7.3)) - 0.5) * 0.6;
				float s = smoothstep(0.32, 0.0, length(p - c));
				float tw = 0.7 + 0.3 * sin(TIME * (1.0 + h * 3.0) + h * 40.0);
				col += vec3(0.9, 0.93, 1.0) * s * tw * stars * smoothstep(0.02, 0.25, up) * (h - 0.985) * 66.0;
			}
		}
		// Aurora: ufukta dalgali Maya perdesi (yalniz Kabarma gecesi).
		if (aurora > 0.0 && up > -0.02) {
			float az = atan(eye.x, eye.z);
			float wave = sin(az * 5.0 + TIME * 0.15) * 0.5 + sin(az * 11.0 - TIME * 0.22) * 0.25;
			float band = smoothstep(0.0, 0.06, up) * (1.0 - smoothstep(0.10 + wave * 0.06, 0.34 + wave * 0.1, up));
			float curtain = 0.6 + 0.4 * sin(az * 37.0 + TIME * 0.4 + wave * 4.0);
			vec3 ac = mix(aurora_a.rgb, aurora_b.rgb, 0.5 + 0.5 * sin(az * 3.0 + TIME * 0.1));
			col += ac * band * curtain * aurora * 0.9;
		}
		// Bulutlar: ust yarim kureye izdusumlu duz katman, iki tonlu (stilize).
		if (clouds > 0.0 && up > 0.0) {
			vec2 uv = eye.xz / (up + 0.18) * 0.22 + vec2(TIME * cloud_speed, TIME * cloud_speed * 0.6);
			float n = texture(cloud_noise, uv).r * 0.7 + texture(cloud_noise, uv * 2.3 + 0.37).r * 0.3;
			float th = 1.0 - clouds;
			float c = smoothstep(th, th + 0.06, n);
			float core = smoothstep(th + 0.08, th + 0.16, n);
			float fade = smoothstep(0.0, 0.18, up);
			vec3 cc = mix(cloud_shade.rgb, cloud_color.rgb, mix(0.35, 1.0, core));
			cc = mix(cc, horizon_color.rgb, (1.0 - smoothstep(0.0, 0.35, up)) * 0.55);
			col = mix(col, cc, c * fade * 0.92);
		}
		// Gunes: yumusak kenarli disk ve hale.
		float sd = dot(eye, normalize(sun_dir));
		col += sun_color.rgb * sun_disk * (smoothstep(cos(sun_size * 1.15), cos(sun_size), sd) * 3.2 + pow(max(sd, 0.0), 220.0) * 0.6 + pow(max(sd, 0.0), 12.0) * 0.12);
		// Iki ay (SANAT-SES §6).
		col += moon(eye, normalize(moon_dir), moon_size, moon_color.rgb * 1.6, normalize(sun_dir)) * moon_disk;
		col += moon(eye, normalize(moon2_dir), moon_size * 0.55, moon2_color.rgb * 1.4, normalize(sun_dir)) * moon_disk;
		col += moon_color.rgb * pow(max(dot(eye, normalize(moon_dir)), 0.0), 60.0) * 0.18 * moon_disk;
	}
	COLOR = col;
}
