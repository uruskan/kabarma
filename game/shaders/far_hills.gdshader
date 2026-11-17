// Ufuk tepeleri (gorsel-dunya): 240-460 m'deki siluetler duz renk degil; cayir zemini, orman lekeleri ve dik
// yamaclarda kaya. Dunya konumundan gurultu; renk biyom paletinden (World.Sky.cs). Hava perspektifini sis verir.
shader_type spatial;
render_mode diffuse_burley, specular_disabled;

uniform sampler2D macro_noise : filter_linear_mipmap, repeat_enable;
uniform vec4 forest : source_color = vec4(0.25, 0.32, 0.2, 1.0);
uniform vec4 meadow : source_color = vec4(0.6, 0.55, 0.3, 1.0);
uniform vec4 rock : source_color = vec4(0.5, 0.47, 0.42, 1.0);

varying vec3 v_world;
varying vec3 v_nw;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_nw = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	float m1 = texture(macro_noise, v_world.xz / 160.0).r;
	float m2 = texture(macro_noise, v_world.xz / 40.0 + 0.3).r;
	float m3 = texture(macro_noise, v_world.xz / 9.0 + 0.7).r;
	vec3 col = meadow.rgb * (0.8 + 0.4 * m2);
	// Orman: buyuk lekeler, kenarlari tek tek agac gibi benekli.
	float f = smoothstep(0.5, 0.56, m1 + (m3 - 0.5) * 0.25 + (m2 - 0.5) * 0.2);
	col = mix(col, forest.rgb * (0.7 + 0.5 * m3), f);
	// Dik yuz: kaya.
	float slope = 1.0 - clamp(v_nw.y, 0.0, 1.0);
	col = mix(col, rock.rgb * (0.8 + 0.4 * m3), smoothstep(0.45, 0.7, slope + (m2 - 0.5) * 0.2));
	ALBEDO = col;
	ROUGHNESS = 1.0;
	AO = mix(0.7, 1.0, 1.0 - f * (1.0 - m3));
}
