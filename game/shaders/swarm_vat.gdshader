// Suru: kose animasyon dokusu (VAT). Her ornek kendi fazinda ziplar (INSTANCE_CUSTOM.x), hizi .y, kabarmis ise .z = 1.
shader_type spatial;
render_mode diffuse_lambert_wrap, specular_disabled;

uniform sampler2D vat : filter_nearest;
uniform int frames = 16;
uniform vec4 maya : source_color = vec4(0.373, 0.941, 0.784, 1.0);

varying float swollen;

vec3 to_linear(vec3 c) { return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(0.04045, c)); }

void vertex() {
	float t = fract(TIME * 1.6 * INSTANCE_CUSTOM.y + INSTANCE_CUSTOM.x) * float(frames);
	int f0 = int(floor(t)) % frames;
	int f1 = (f0 + 1) % frames;
	vec3 a = texelFetch(vat, ivec2(VERTEX_ID, f0), 0).xyz;
	vec3 b = texelFetch(vat, ivec2(VERTEX_ID, f1), 0).xyz;
	VERTEX += mix(a, b, fract(t));
	swollen = INSTANCE_CUSTOM.z;
}

void fragment() {
	vec3 base = to_linear(COLOR.rgb);
	ALBEDO = base;
	ROUGHNESS = 0.85;
	float fres = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0);
	EMISSION = maya.rgb * fres * (0.25 + swollen * 1.2);
}
