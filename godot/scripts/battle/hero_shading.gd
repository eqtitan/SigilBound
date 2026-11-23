## Character materials, after game3d/shading.mjs: per-role finishes (skin, hair, cloth, leather,
## metal, gold, gem, eye) with wrap diffuse on soft materials, a fresnel rim in the hero's element
## colour that favours up-facing surfaces, painted-metal sky/ground reflection, and unshaded
## glows that the bloom pass picks up. Kit parts carry their colour per vertex so a whole socket
## merges into one mesh per finish (see HeroKit), as the web's draw-call merging does.
class_name HeroShading

const SOLID := """
shader_type spatial;
render_mode %s;
uniform vec4 albedo : source_color = vec4(1.0);
uniform bool use_vcolor = false;
uniform float roughness = 0.8;
uniform float metallic = 0.0;
uniform float specular = 0.5;
uniform vec3 rim_color : source_color = vec3(0.62, 0.7, 1.0);
uniform float rim = 0.7;
uniform float sheen = 0.0;
uniform float env_metal = 0.0;
uniform vec3 sky : source_color = vec3(0.81, 0.88, 1.0);
uniform vec3 ground : source_color = vec3(0.29, 0.23, 0.19);
uniform float gain = 1.0;
uniform vec3 emission_color : source_color = vec3(0.0);
uniform float emission_energy = 0.0;

void fragment() {
	vec3 c = albedo.rgb * (use_vcolor ? COLOR.rgb : vec3(1.0)) * gain;
	// albedo ceiling: very light albedos keep form under the bright rig
	float luma = dot(c, vec3(0.2126, 0.7152, 0.0722));
	c *= 1.0 - 0.2 * smoothstep(0.45, 0.85, luma) * (1.0 - metallic);
	ALBEDO = c;
	ROUGHNESS = roughness;
	METALLIC = metallic;
	SPECULAR = specular;
	float nv = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	vec3 nw = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	float rimf = smoothstep(0.5, 0.95, 1.0 - nv) * (0.45 + 0.55 * clamp(nw.y + 0.4, 0.0, 1.0));
	vec3 e = rim_color * rim * rimf;
	// cloth sheen at grazing angles
	e += c * sheen * pow(1.0 - nv, 3.0) * 0.35;
	// painted metal: a sky/ground reflection plus a horizon streak so metal always reads as metal
	vec3 rw = reflect(-(INV_VIEW_MATRIX * vec4(VIEW, 0.0)).xyz, nw);
	float s = smoothstep(-0.3, 0.5, rw.y);
	vec3 env = mix(ground, sky, s) + sky * (1.2 * smoothstep(0.05, 0.2, rw.y) * (1.0 - smoothstep(0.2, 0.45, rw.y)));
	e += env * mix(vec3(0.04), c, metallic) * env_metal * 0.45;
	EMISSION = e + emission_color * emission_energy;
}
"""

const GLOW := """
shader_type spatial;
render_mode unshaded, %s;
uniform vec4 albedo : source_color = vec4(1.0);
uniform bool use_vcolor = false;
uniform float energy = 1.6;
uniform bool soft = false;
void fragment() {
	vec3 c = albedo.rgb * (use_vcolor ? COLOR.rgb : vec3(1.0));
	ALBEDO = c * energy;
	// translucent halos fade toward their silhouette, so they read as glow rather than a disc
	ALPHA = albedo.a * (soft ? pow(clamp(dot(NORMAL, VIEW), 0.0, 1.0), 1.6) : 1.0);
}
"""

## Role parameters: roughness, metallic, specular, rim scale, sheen, env metal, gain, wrap.
const ROLES := {
	"default": [0.8, 0.0, 0.5, 1.0, 0.0, 0.0, 1.0, false],
	"skin": [0.5, 0.0, 0.3, 0.75, 0.0, 0.0, 1.0, true],
	"hair": [0.55, 0.0, 0.25, 1.0, 0.15, 0.0, 1.0, true],
	"cloth": [0.88, 0.0, 0.2, 1.0, 0.4, 0.0, 1.0, true],
	"leather": [0.55, 0.0, 0.45, 0.9, 0.15, 0.25, 1.0, false],
	"leatherM": [0.5, 0.35, 0.5, 0.9, 0.0, 0.5, 1.0, false],
	"metal": [0.32, 0.75, 0.6, 0.75, 0.0, 1.0, 1.15, false],
	"gold": [0.26, 1.0, 0.6, 0.75, 0.0, 1.0, 1.1, false],
	"wood": [0.75, 0.0, 0.3, 0.9, 0.0, 0.0, 1.0, false],
	"eye": [0.18, 0.0, 0.6, 0.25, 0.0, 0.0, 1.0, false],
}

static var _shaders := {}
static var _cache := {}
static var rim_strength := 0.7


static func _shader(kind: String) -> Shader:
	if not _shaders.has(kind):
		var sh := Shader.new()
		match kind:
			"solid": sh.code = SOLID % "diffuse_burley, specular_schlick_ggx"
			"wrap": sh.code = SOLID % "diffuse_lambert_wrap, specular_schlick_ggx"
			"cloth": sh.code = SOLID % "diffuse_lambert_wrap, specular_schlick_ggx, cull_disabled"
			"glow": sh.code = GLOW % "cull_back"
			"glowA": sh.code = GLOW % "blend_add, cull_back, depth_draw_never, shadows_disabled"
		_shaders[kind] = sh
	return _shaders[kind]


## A material for one finish. `vcolor` multiplies the albedo by the vertex colour (merged kit).
static func material(color: Color, role: String, rim_color: Color, vcolor := false) -> ShaderMaterial:
	var key := "%s|%s|%s|%s" % [color.to_html(), role, rim_color.to_html(), vcolor]
	if _cache.has(key):
		return _cache[key]
	var p: Array = ROLES.get(role, ROLES["default"])
	var kind := "cloth" if role == "cloth" else ("wrap" if p[7] else "solid")
	var m := ShaderMaterial.new()
	m.shader = _shader(kind)
	m.set_shader_parameter("albedo", color)
	m.set_shader_parameter("use_vcolor", vcolor)
	m.set_shader_parameter("roughness", p[0])
	m.set_shader_parameter("metallic", p[1])
	m.set_shader_parameter("specular", p[2])
	m.set_shader_parameter("rim_color", rim_color)
	m.set_shader_parameter("rim", rim_strength * p[3])
	m.set_shader_parameter("sheen", p[4])
	m.set_shader_parameter("env_metal", p[5])
	m.set_shader_parameter("gain", p[6])
	_cache[key] = m
	return m


## An unshaded glow (not tone-mapped down: energy > 1 so bloom catches it). opacity < 1 is additive.
static func glow(color: Color, opacity := 1.0, vcolor := false) -> ShaderMaterial:
	var key := "glow|%s|%s|%s" % [color.to_html(), opacity, vcolor]
	if _cache.has(key):
		return _cache[key]
	var m := ShaderMaterial.new()
	m.shader = _shader("glow" if opacity >= 1.0 else "glowA")
	m.set_shader_parameter("albedo", Color(color, opacity))
	m.set_shader_parameter("use_vcolor", vcolor)
	m.set_shader_parameter("energy", 1.6 if opacity >= 1.0 else 1.0)
	m.set_shader_parameter("soft", opacity < 1.0)
	_cache[key] = m
	return m


## The eye material for glowing-eyed heroes and Hollow enemies.
static func emissive(color: Color, energy: float, rim_color: Color) -> ShaderMaterial:
	var key := "em|%s|%s|%s" % [color.to_html(), energy, rim_color.to_html()]
	if _cache.has(key):
		return _cache[key]
	var m: ShaderMaterial = material(color, "eye", rim_color).duplicate()
	m.set_shader_parameter("emission_color", color)
	m.set_shader_parameter("emission_energy", energy)
	_cache[key] = m
	return m
