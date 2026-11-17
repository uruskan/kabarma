// Gokyuzu (gorsel-dunya): tepe -> ufuk renk gecisi, ufukta pus bandi, gunes cevresinde ileri sacilma (Mie) ve gun
// batiminda gunes tarafinda isiyan ufuk; isikli bulutlar (alan bukulmus kesirli gurultu, gunese dogru ikinci ornekle
// oz golge, gunes yaninda gumus kenar, ufka dogru ufuk rengine karisir); kucuk parlak gunes diski (HDR, bloom'a girer);
// iki ay, yildizlar ve Kabarma gecesinde ufukta Maya aurorasi (SANAT-SES §2). Renkler ve acilar World.Sky.cs'den.
shader_type sky;

uniform vec4 top_color : source_color = vec4(0.55, 0.78, 0.91, 1.0);
uniform vec4 horizon_color : source_color = vec4(0.95, 0.85, 0.64, 1.0);
uniform vec4 ground_color : source_color = vec4(0.6, 0.5, 0.4, 1.0);
uniform float horizon_curve = 0.35;
uniform float exposure = 1.0;

uniform vec3 sun_dir = vec3(0.0, 1.0, 0.0);
uniform vec4 sun_color : source_color = vec4(1.0, 0.95, 0.87, 1.0);
uniform float sun_disk = 1.0;
uniform float sun_size = 0.012;

uniform vec3 moon_dir = vec3(0.0, 0.5, 0.86);
uniform vec3 moon2_dir = vec3(0.3, 0.6, 0.7);
uniform vec4 moon_color : source_color = vec4(0.86, 0.88, 0.96, 1.0);
uniform vec4 moon2_color : source_color = vec4(0.95, 0.80, 0.70, 1.0);
uniform float moon_disk = 0.0;
uniform float moon_size = 0.045;

uniform float stars = 0.0;
uniform float aurora = 0.0;
uniform vec4 aurora_a : source_color = vec4(0.373, 0.941, 0.784, 1.0);
uniform vec4 aurora_b : source_color = vec4(0.831, 0.357, 1.0, 1.0);

uniform float clouds = 0.4;
uniform vec4 cloud_color : source_color = vec4(1.0);
uniform vec4 cloud_shade : source_color = vec4(0.8, 0.8, 0.9, 1.0);
uniform sampler2D cloud_noise : filter_linear_mipmap, repeat_enable;
uniform float cloud_speed = 0.003;

float hash3(vec3 p) { p = fract(p * 0.1031); p += dot(p, p.zyx + 31.32); return fract((p.x + p.y) * p.z); }

float vnoise(vec3 p) {
	vec3 i = floor(p); vec3 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1,0,0)), f.x), mix(hash3(i + vec3(0,1,0)), hash3(i + vec3(1,1,0)), f.x), f.y),
	           mix(mix(hash3(i + vec3(0,0,1)), hash3(i + vec3(1,0,1)), f.x), mix(hash3(i + vec3(0,1,1)), hash3(i + vec3(1,1,1)), f.x), f.y), f.z);
}

// Bulut yogunlugu: dort oktav, ilk ikisiyle alan bukme. uv bulut tabakasinin duzleminde.
float cloud_density(vec2 uv, float lod) {
	vec2 drift = vec2(TIME * cloud_speed, TIME * cloud_speed * 0.55);
	vec2 w = vec2(textureLod(cloud_noise, uv * 0.35 + drift * 0.5, lod).r, textureLod(cloud_noise, uv * 0.35 + vec2(0.31, 0.77) - drift * 0.3, lod).r) - 0.5;
	vec2 p = uv + w * 0.35 + drift;
	float n = textureLod(cloud_noise, p, lod).r * 0.55
	        + textureLod(cloud_noise, p * 2.13 + 0.37, lod).r * 0.27
	        + textureLod(cloud_noise, p * 4.71 + 0.71, lod).r * 0.12
	        + textureLod(cloud_noise, p * 9.37 + 0.13, lod).r * 0.06;
	return n;
}

vec3 moon(vec3 eye, vec3 dir, float size, vec3 col, vec3 lit_from) {
	float d = dot(eye, dir);
	float disk = smoothstep(cos(size * 1.04), cos(size), d);
	if (disk <= 0.0) return vec3(0.0);
	// Yuzey: yumusak mare lekeleri; hilal golgesi isik yonunden kayar.
	vec3 side = normalize(lit_from - dir * dot(lit_from, dir) + vec3(1e-4));
	float lit = smoothstep(cos(size * 1.02), cos(size * 0.94), dot(eye, normalize(dir - side * size * 0.55)));
	vec3 local = (eye - dir) / size;
	float maria = smoothstep(0.45, 0.65, vnoise(local * 2.5 + 3.0) * 0.7 + vnoise(local * 6.0) * 0.3);
	return col * disk * mix(0.08, 1.0, 1.0 - lit * 0.85) * mix(1.0, 0.72, maria);
}

void sky() {
	vec3 eye = normalize(EYEDIR);
	float up = eye.y;
	vec3 sd = normalize(sun_dir);
	float cs = dot(eye, sd);
	float sun_low = 1.0 - smoothstep(0.05, 0.45, sd.y);         // gunes alcakken 1

	// Tepe -> ufuk: ufuk bandi dar ve parlak; gunes tarafinda ufuk gunes rengine kayar.
	float k = pow(clamp(1.0 - max(up, 0.0), 0.0, 1.0), 1.0 / max(horizon_curve * 0.55, 0.05));
	vec3 hor = horizon_color.rgb;
	float toward = pow(dot(normalize(eye.xz + 1e-4), normalize(sd.xz + 1e-4)) * 0.5 + 0.5, 3.0);
	hor = mix(hor, hor * 0.55 + sun_color.rgb * 0.6, toward * sun_low * sun_disk * 0.8);
	vec3 col = mix(top_color.rgb, hor, k);
	// Zenit biraz daha koyu ve doygun.
	col = mix(col, top_color.rgb * 0.82, smoothstep(0.55, 1.0, up) * 0.5);
	// Ufkun alti: zemin rengine iner (sis cogunu kapatir).
	col = mix(col, mix(hor, ground_color.rgb, 0.65), smoothstep(0.0, -0.12, up));

	// Gunes cevresi isima (ileri sacilma): genis hale + dar parlama; alcak guneste guclu.
	float mie = pow(max(cs, 0.0), 6.0) * (0.12 + 0.35 * sun_low) + pow(max(cs, 0.0), 48.0) * 0.45 + pow(max(cs, 0.0), 600.0) * 1.2;
	col += sun_color.rgb * mie * sun_disk * smoothstep(-0.15, 0.05, up);

	// Yildizlar: hucre basina bir yildiz, ufka dogru soner. Isik kupunde gerekmez.
	if (!AT_CUBEMAP_PASS && stars > 0.0 && up > 0.0) {
		vec3 p = eye * 260.0;
		vec3 cell = floor(p);
		float h = hash3(cell);
		if (h > 0.982) {
			vec3 c = cell + 0.5 + (vec3(hash3(cell + 1.7), hash3(cell + 4.1), hash3(cell + 7.3)) - 0.5) * 0.6;
			float s = smoothstep(0.3, 0.0, length(p - c));
			float tw = 0.7 + 0.3 * sin(TIME * (1.0 + h * 3.0) + h * 40.0);
			vec3 sc = mix(vec3(1.0, 0.82, 0.68), vec3(0.75, 0.85, 1.0), hash3(cell + 9.1));
			col += sc * s * tw * stars * smoothstep(0.02, 0.3, up) * (h - 0.982) * 90.0;
		}
		// Samanyolu benzeri ince pus bandi.
		float band = exp(-pow(dot(eye, normalize(vec3(0.3, 0.2, 1.0))) / 0.18, 2.0));
		col += vec3(0.5, 0.55, 0.75) * band * stars * 0.06 * (0.5 + vnoise(eye * 18.0));
	}

	// Aurora: ufukta dalgali Maya perdesi (yalniz Kabarma gecesi).
	if (aurora > 0.0 && up > -0.02) {
		float az = atan(eye.x, eye.z);
		float wave = sin(az * 5.0 + TIME * 0.15) * 0.5 + sin(az * 11.0 - TIME * 0.22) * 0.25;
		float band = smoothstep(0.0, 0.08, up) * (1.0 - smoothstep(0.12 + wave * 0.06, 0.38 + wave * 0.1, up));
		float curtain = 0.55 + 0.45 * sin(az * 37.0 + TIME * 0.4 + wave * 4.0) * (0.6 + 0.4 * vnoise(vec3(az * 8.0, up * 20.0, TIME * 0.2)));
		vec3 ac = mix(aurora_a.rgb, aurora_b.rgb, smoothstep(0.05, 0.3, up + wave * 0.05));
		col += ac * band * curtain * aurora * 0.8;
	}

	// Bulutlar: ust yarim kurede egri tabaka; isikli (oz golge), ufka dogru pusla karisir.
	if (clouds > 0.0 && up > 0.0) {
		vec2 uv = eye.xz / (up + 0.12) * 0.16;
		float lod = AT_CUBEMAP_PASS ? 2.0 : 0.0;
		float d = cloud_density(uv, lod);
		float th = 1.0 - clouds * 0.95;
		float a = smoothstep(th, th + 0.22, d);
		if (a > 0.001) {
			float d2 = cloud_density(uv + sd.xz * 0.025, lod);
			float shade = exp(-max(d2 - th, 0.0) * 5.0);                 // gunese dogru yogunluk: golge
			float thick = smoothstep(th, th + 0.45, d);
			vec3 lit = mix(cloud_shade.rgb, cloud_color.rgb, clamp(shade * 1.2 - thick * 0.25, 0.0, 1.0));
			// Gumus kenar: ince kenarlar gunes yanindayken parlar.
			lit += sun_color.rgb * pow(max(cs, 0.0), 8.0) * (1.0 - thick) * 0.9 * sun_disk;
			// Ufka dogru hava perspektifi.
			float aer = 1.0 - smoothstep(0.0, 0.32, up);
			lit = mix(lit, hor, aer * 0.65);
			a *= smoothstep(0.0, 0.1, up) * 0.95;
			col = mix(col, lit, a);
		}
	}

	// Gunes diski (bulutlarin onunde, HDR).
	if (!AT_CUBEMAP_PASS) {
		float disc = smoothstep(cos(sun_size * 1.12), cos(sun_size), cs);
		col += sun_color.rgb * disc * sun_disk * 22.0 * smoothstep(-0.02, 0.02, up);
	}
	// Iki ay (SANAT-SES §6).
	col += moon(eye, normalize(moon_dir), moon_size, moon_color.rgb * 2.2, sd) * moon_disk;
	col += moon(eye, normalize(moon2_dir), moon_size * 0.55, moon2_color.rgb * 1.8, sd) * moon_disk;
	col += moon_color.rgb * pow(max(dot(eye, normalize(moon_dir)), 0.0), 40.0) * 0.12 * moon_disk;
	COLOR = col * exposure;
}
