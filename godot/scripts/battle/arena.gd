## The battle arena, after game3d/scenery.mjs buildArena(): a rolling heightfield with grass in
## two tones, worn earth around the fight and along a winding dirt path, a cracked rune circle
## decal under the units, low-poly broadleaf trees in the chapter's (autumn) colours, pines,
## boulders, logs and stumps framing the field, instanced rocks and wind-blown grass tufts, a
## gradient sky with a sun glow, tree-line silhouettes, fog, and drifting leaves. Plus the
## lighting rig from three3d.mjs mount(): sky ambient (hemisphere-like), a soft-shadowed sun, a
## cool rim and a front fill. Everything is code-built; sizes follow the quality tier.
class_name Arena
extends Node3D

## sceneColors / PALETTES: sky top, horizon, ground, props, accent.
const PALETTES := [
	["#1f2b46", "#c9805a", "#2a2a22", "#16141f", "#ffc068"], ["#16283a", "#4f7c8a", "#1c2a2a", "#0e1a22", "#e8f4ff"],
	["#2a1a1e", "#a8483a", "#2e1a1c", "#160c0e", "#ff9a4a"], ["#1a2e2a", "#7fa088", "#23362a", "#101c16", "#d8ff8a"],
	["#28203a", "#c8a870", "#2c2640", "#14101e", "#ffcf6a"],
]
## THEMES (the first five chapters): ground, path, hill, rock, bark, leaves, tufts, silhouettes, motes.
const THEMES := [
	{"grass": "#4f6a2a", "grass2": "#6f7432", "worn": "#8a6844", "hill": "#41582a", "rock": "#7d746a", "bark": "#4e3726",
		"leaves": ["#3c6a26", "#557a2a", "#a8702a", "#b8522a"], "tuft": "#5f7a2c", "sil": "#2e2c46", "mote": "#e08a3a"},
	{"grass": "#c2a87e", "grass2": "#b39b72", "worn": "#8c7a5e", "hill": "#a8956a", "rock": "#6a7078", "bark": "#9a8a76",
		"leaves": ["#4a6a3a", "#5a7a40"], "tuft": "#8f9a52", "sil": "#24404e", "mote": "#e8f6ff"},
	{"grass": "#94503a", "grass2": "#ad6844", "worn": "#c09070", "hill": "#8a4632", "rock": "#a0553a", "bark": "#5e4636",
		"leaves": ["#857040"], "tuft": "#857040", "sil": "#44201f", "mote": "#ffc890"},
	{"grass": "#3a5630", "grass2": "#4f6236", "worn": "#6e5a40", "hill": "#34502e", "rock": "#58605a", "bark": "#433a30",
		"leaves": ["#4a5a30"], "tuft": "#7a8a44", "sil": "#223c36", "mote": "#d8ff7a"},
	{"grass": "#58652f", "grass2": "#76763c", "worn": "#8e8474", "hill": "#4c5a2c", "rock": "#a49a88", "bark": "#5a4636",
		"leaves": ["#4c682c", "#667a32"], "tuft": "#6a7a34", "sil": "#36304c", "mote": "#ffd27a"},
]
const QUALITY := {
	"ultra": {"grid": 150, "tufts": 2200, "scatter2": 600, "rocks": 260, "props": 1.0, "motes": 220, "casters": true},
	"high": {"grid": 120, "tufts": 1500, "scatter2": 420, "rocks": 190, "props": 1.0, "motes": 160, "casters": true},
	"medium": {"grid": 90, "tufts": 850, "scatter2": 240, "rocks": 120, "props": 0.7, "motes": 90, "casters": false},
	"low": {"grid": 64, "tufts": 380, "scatter2": 100, "rocks": 60, "props": 0.45, "motes": 0, "casters": false},
}
# Theme colours are "look" colours scaled for the web's rig (0.42 ground, 0.55 props there). Three.js
# ACES divides by 0.6 and its legacy lights carry an extra PI, so the same rig here needs ~2x albedo.
const AG := 0.42 * 2.5
const AP := 0.55 * 1.9
const PATH := [[0.6, 14], [0.4, 5], [0, -0.5], [-0.8, -6.5], [-2.6, -13], [-1.2, -21], [2.6, -30], [4.5, -42]]
const HILLS := 4.5

const GROUND_SHADER := """
shader_type spatial;
render_mode diffuse_lambert_wrap, specular_disabled;
varying vec3 world;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
void vertex() { world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }
void fragment() {
	vec2 p = world.xz;
	float wear = UV2.x;
	// grass: fine blades/speckle; worn earth: pebbly mottling
	float g = noise(p * 9.0) * 0.55 + noise(p * 31.0) * 0.3 + noise(p * 2.3) * 0.15;
	float d = noise(p * 5.0 + 7.0) * 0.6 + noise(p * 19.0) * 0.4;
	float detail = mix(0.82 + 0.36 * g, 0.86 + 0.28 * d, wear);
	ALBEDO = COLOR.rgb * detail;
	ROUGHNESS = 0.95;
}
"""

const DECAL_SHADER := """
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, diffuse_lambert, specular_disabled;
uniform vec3 tint : source_color;
uniform vec3 glow : source_color;
uniform float glow_energy = 0.0;
varying vec2 q;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
float band(float d, float at, float w) { return 1.0 - smoothstep(0.0, w, abs(d - at)); }
void vertex() { q = VERTEX.xz * 2.0; }
void fragment() {
	// q in -1..1 across the decal; the rune ring sits at 0.86 (R0 = 0.43 of the texture)
	float d = length(q) / 0.86;
	float ang = atan(q.y, q.x);
	float a = 0.0;
	a += band(d, 1.0, 0.026) * 0.55;
	a += band(d, 0.86, 0.014) * 0.55;
	a += band(d, 0.42, 0.01) * 0.55;
	// glyphs: 18 abstract stroke marks in the band at 0.93
	float cell = ang / 6.2831853 * 18.0;
	float fc = fract(cell) - 0.5;
	float id = floor(cell);
	vec2 gp = vec2(fc * 2.6, (d - 0.93) * 34.0);
	float h1 = hash(vec2(id, 1.0)), h2 = hash(vec2(id, 2.0)), h3 = hash(vec2(id, 3.0));
	float st = 1.0 - smoothstep(0.08, 0.16, abs(dot(gp, normalize(vec2(h1 - 0.5, h2 - 0.5))) ));
	float inside = step(abs(gp.x), 0.9) * step(abs(gp.y), 0.9);
	float st2 = 1.0 - smoothstep(0.08, 0.16, abs(gp.x - (h3 - 0.5)));
	a += max(st, st2 * step(0.5, h1)) * inside * 0.6;
	// radial cracks from the ring
	float cr = 0.0;
	for (int i = 0; i < 9; i++) {
		float ca = hash(vec2(float(i), 7.0)) * 6.2831853;
		float wob = (noise(vec2(d * 9.0, float(i) * 3.1)) - 0.5) * 0.25;
		float da = abs(mod(ang - ca - wob + 3.14159265, 6.2831853) - 3.14159265);
		float len = 0.12 + hash(vec2(float(i), 9.0)) * 0.22;
		float along = abs(d - 1.0);
		cr += (1.0 - smoothstep(0.004, 0.02, da * d)) * (1.0 - smoothstep(len * 0.6, len, along));
	}
	a += min(cr, 1.0) * 0.75;
	a += smoothstep(0.7, 0.9, d) * (1.0 - smoothstep(0.95, 1.02, d)) * 0.08;
	a *= 0.75 + 0.5 * noise(q * 14.0);
	ALBEDO = tint;
	EMISSION = glow * glow_energy * a;
	ALPHA = clamp(a, 0.0, 1.0);
	ROUGHNESS = 0.95;
}
"""

const LEAF_WIND := """
shader_type spatial;
render_mode diffuse_lambert_wrap, specular_schlick_ggx, cull_back;
uniform float wind = 0.04;
void vertex() {
	vec3 w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	VERTEX.x += sin(TIME * 1.3 + w.x * 0.35 + w.z * 0.2) * wind * max(VERTEX.y - 1.5, 0.0);
}
void fragment() { ALBEDO = COLOR.rgb; ROUGHNESS = 0.85; SPECULAR = 0.2; }
"""

const TUFT_SHADER := """
shader_type spatial;
render_mode diffuse_lambert_wrap, specular_disabled, cull_disabled;
void vertex() {
	vec3 o = MODEL_MATRIX[3].xyz;
	VERTEX.x += sin(TIME * 2.1 + o.x * 0.7 + o.z * 0.5) * 0.25 * VERTEX.y * VERTEX.y;
}
void fragment() { ALBEDO = COLOR.rgb; ROUGHNESS = 0.9; }
"""

const SKY_SHADER := """
shader_type sky;
uniform vec3 top : source_color;
uniform vec3 horizon : source_color;
uniform vec3 below : source_color;
uniform vec3 sun_dir = vec3(-0.33, 0.1, -0.94);
uniform vec3 hemi_sky : source_color;
uniform vec3 hemi_ground : source_color;
void sky() {
	vec3 d = EYEDIR;
	vec3 glowc = mix(horizon, vec3(1.0), 0.15);
	vec3 c;
	if (d.y >= 0.0) {
		c = mix(glowc, top, pow(min(1.0, d.y * 2.2), 0.55));
		float s = pow(max(0.0, dot(d, normalize(sun_dir))), 6.0);
		c = mix(c, glowc * 1.3, s * 0.6);
	} else {
		c = mix(glowc, below, smoothstep(0.0, 0.08, -d.y));
	}
	// Ambient light (the cubemap pass): the web's hemisphere light, sky colour above, earth below.
	COLOR = AT_CUBEMAP_PASS ? mix(hemi_ground, hemi_sky, smoothstep(-0.4, 0.4, d.y)) : c;
}
"""

var colors: Array
var theme: Dictionary
var sun: DirectionalLight3D
var env: Environment
var quality := "high"
var q: Dictionary
var _seed := 101
var _rng_state := 0
var _gn := 120
var _gh := PackedFloat32Array()
var _placed: Array = []
var _groups := {} # material key -> [verts, normals, colors]
var col := {}


# ---------- noise (scenery.mjs: hash2 / vnoise / fbm, mulberry32) ----------
static func _hash2(ix: int, iy: int, s: int) -> float:
	var h := ((ix * 374761393) ^ (iy * 668265263) ^ (s * 1442695041)) & 0xffffffff
	h = ((h ^ (h >> 13)) * 1274126177) & 0xffffffff
	return float((h ^ (h >> 16)) & 0xffffffff) / 4294967296.0


static func _sm(t: float) -> float:
	return t * t * (3.0 - 2.0 * t)


static func _sstep(a: float, b: float, v: float) -> float:
	return _sm(clampf((v - a) / (b - a), 0.0, 1.0))


static func _vnoise(x: float, y: float, s: int) -> float:
	var x0 := floori(x)
	var y0 := floori(y)
	var fx := _sm(x - x0)
	var fy := _sm(y - y0)
	var a := _hash2(x0, y0, s)
	var b := _hash2(x0 + 1, y0, s)
	var c := _hash2(x0, y0 + 1, s)
	var d := _hash2(x0 + 1, y0 + 1, s)
	return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy


static func _fbm(x: float, y: float, s: int, octaves: int = 4) -> float:
	var sum := 0.0
	var amp := 1.0
	var norm := 0.0
	var f := 1.0
	for o in octaves:
		sum += _vnoise(x * f, y * f, s + o * 17) * amp
		norm += amp
		amp *= 0.5
		f *= 2.0
	return sum / norm


func _rnd() -> float:
	_rng_state = (_rng_state + 0x6d2b79f5) & 0xffffffff
	var t := _rng_state
	t = ((t ^ (t >> 15)) * (t | 1)) & 0xffffffff
	t ^= (t + (((t ^ (t >> 7)) * (t | 61)) & 0xffffffff)) & 0xffffffff
	return float((t ^ (t >> 14)) & 0xffffffff) / 4294967296.0


func R(a: float, b: float) -> float:
	return a + (b - a) * _rnd()


# ---------- terrain functions ----------
func _path_dist(x: float, z: float) -> float:
	var best := 1e9
	for i in PATH.size() - 1:
		var ax: float = PATH[i][0]
		var az: float = PATH[i][1]
		var dx: float = PATH[i + 1][0] - ax
		var dz: float = PATH[i + 1][1] - az
		var t := clampf(((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz), 0.0, 1.0)
		best = minf(best, Vector2(x - ax - dx * t, z - az - dz * t).length())
	return best


static func _arena_r(x: float, z: float) -> float:
	return Vector2(x / 4.8, (z + 0.6) / 4.5).length()


static func _flat_r(x: float, z: float) -> float:
	return Vector2(x / 6.8, (z + 1.2) / 6.8).length()


func height_at(x: float, z: float) -> float:
	var flat := _sstep(1.0, 2.3, _flat_r(x, z))
	var h := (_fbm(x * 0.07, z * 0.07, _seed + 1, 3) - 0.5) * 1.4 * flat
	var back := _sstep(-9, -38, z)
	var side := _sstep(9, 26, absf(x))
	h += HILLS * maxf(back, side * 0.85) * (0.35 + 0.9 * _fbm(x * 0.035 + 3, z * 0.035, _seed + 2, 3)) * flat
	var pd := _path_dist(x, z)
	h = lerpf(h, minf(h, 0.15 + h * 0.2), _sstep(2.5, 0.8, pd) * flat)
	return h


func wear_at(x: float, z: float) -> float:
	var n := _fbm(x * 0.6, z * 0.6, _seed + 7, 3)
	var arena := _sstep(1.08, 0.78, _arena_r(x, z) + (n - 0.5) * 0.35)
	var pw := 0.85 + (_fbm(x * 0.2, z * 0.2, _seed + 8, 2) - 0.5) * 0.6
	var pth := _sstep(pw, pw * 0.45, _path_dist(x, z) + (n - 0.5) * 0.4)
	var w := maxf(arena * (0.75 + 0.25 * n), pth * 0.9)
	w = maxf(w, _sstep(0.72, 0.8, _fbm(x * 0.15 + 2, z * 0.15, _seed + 9, 3)) * 0.8)
	return clampf(w, 0.0, 1.0)


static func _remap(u: float) -> float:
	return 95.0 * (0.22 * u + 0.78 * u * u * u)


static func _unmap(x: float) -> float:
	var lo := -1.0
	var hi := 1.0
	for k in 24:
		var m := (lo + hi) / 2
		if _remap(m) < x:
			lo = m
		else:
			hi = m
	return (lo + hi) / 2


func ground_y(x: float, z: float) -> float:
	var n := _gn
	var gx := (_unmap(x) + 1) / 2 * n
	var gz := (_unmap(z + 6) + 1) / 2 * n
	var i := clampi(floori(gx), 0, n - 1)
	var j := clampi(floori(gz), 0, n - 1)
	var fx := clampf(gx - i, 0, 1)
	var fz := clampf(gz - j, 0, 1)
	var w := n + 1
	var a := _gh[j * w + i]
	var b := _gh[j * w + i + 1]
	var d := _gh[(j + 1) * w + i]
	var e := _gh[(j + 1) * w + i + 1]
	return a + (b - a) * fx + (d - a) * fz if fx + fz <= 1 else e + (d - e) * (1 - fx) + (b - e) * (1 - fz)


# ---------- build ----------
func build(chapter: int, quality_name: String = "high") -> void:
	quality = quality_name.to_lower()
	q = QUALITY.get(quality, QUALITY["high"])
	var ci := chapter % PALETTES.size()
	colors = PALETTES[ci]
	theme = THEMES[ci]
	_seed = 101 + chapter * 7
	_rng_state = _seed
	_gn = q["grid"]
	# Colours are kept linear (three.js colour management), as the web scales them.
	var D := func(h: String, k: float) -> Color: return Color(h).srgb_to_linear() * k
	col = {
		"top": Color(colors[0]), "horizon": Color(colors[1]), "accent": Color(colors[4]),
		"grass": D.call(theme["grass"], AG), "grass2": D.call(theme["grass2"], AG), "worn": D.call(theme["worn"], AG * 0.9), "hill": D.call(theme["hill"], AG),
		"rock": D.call(theme["rock"], AP), "bark": D.call(theme["bark"], AP), "tuft": D.call(theme["tuft"], AG * 1.1), "sil": Color(theme["sil"]),
	}
	col["fog"] = (Color(colors[1]).srgb_to_linear().lerp(Color(colors[0]).srgb_to_linear(), 0.45) * 0.85).linear_to_srgb()
	for c in ["grass", "grass2", "worn", "hill", "rock", "bark", "tuft"]:
		col[c].a = 1.0
	col["fog"].a = 1.0
	_environment()
	_lights()
	_ground()
	_decal()
	_props()
	_rocks_and_tufts()
	_silhouettes()
	if q["motes"] > 0:
		_motes()


func _environment() -> void:
	env = Environment.new()
	var sky_mat := ShaderMaterial.new()
	var sh := Shader.new()
	sh.code = SKY_SHADER
	sky_mat.shader = sh
	sky_mat.set_shader_parameter("top", col["top"])
	sky_mat.set_shader_parameter("horizon", col["horizon"])
	sky_mat.set_shader_parameter("below", Color(theme["worn"]).darkened(0.55))
	sky_mat.set_shader_parameter("hemi_sky", Color(colors[1]).lerp(Color.WHITE, 0.35))
	sky_mat.set_shader_parameter("hemi_ground", (Color(theme["worn"]).srgb_to_linear() * AG * 0.9 * 0.6).linear_to_srgb())
	var sky := Sky.new()
	sky.sky_material = sky_mat
	sky.radiance_size = Sky.RADIANCE_SIZE_32
	sky.process_mode = Sky.PROCESS_MODE_QUALITY
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	# Hemisphere-like ambient: the sky's radiance (horizon-tinted above, earth below).
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_sky_contribution = 1.0
	env.ambient_light_energy = 1.25
	env.reflected_light_source = Environment.REFLECTION_SOURCE_SKY
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.3
	env.tonemap_white = 6.0
	# Fog: distance reads as depth, not grey wash.
	env.fog_enabled = true
	env.fog_mode = Environment.FOG_MODE_DEPTH
	env.fog_light_color = col["fog"]
	env.fog_depth_begin = 25.0
	env.fog_depth_end = 75.0
	env.fog_depth_curve = 1.2
	env.fog_sky_affect = 0.0
	env.fog_light_energy = 1.0
	# Bloom on glows; a warm colour grade (the web's per-arena LUT feel).
	env.glow_enabled = quality != "low"
	env.glow_intensity = 0.7
	env.glow_strength = 1.0
	env.glow_bloom = 0.04
	env.glow_hdr_threshold = 1.0
	env.glow_blend_mode = Environment.GLOW_BLEND_MODE_SOFTLIGHT
	env.adjustment_enabled = true
	env.adjustment_brightness = 1.0
	env.adjustment_contrast = 1.07
	env.adjustment_saturation = 1.04
	# SSAO only exists in the Forward+ renderer; the Mobile renderer (phones) ignores it.
	env.ssao_enabled = quality == "ultra"
	env.ssao_radius = 0.8
	env.ssao_intensity = 1.4
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)


func _lights() -> void:
	sun = DirectionalLight3D.new()
	sun.light_color = Color("#fff1dd")
	sun.light_energy = 2.0
	sun.shadow_enabled = quality != "low"
	sun.shadow_blur = 1.6
	sun.shadow_bias = 0.03
	sun.shadow_normal_bias = 1.0
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	sun.directional_shadow_max_distance = 32.0
	sun.light_angular_distance = 1.5 # soft penumbra (PCSS where supported)
	add_child(sun)
	sun.look_at_from_position(Vector3(-5, 10, 6), Vector3.ZERO)
	var rim := DirectionalLight3D.new()
	rim.light_color = Color("#99aaff")
	rim.light_energy = 0.8
	add_child(rim)
	rim.look_at_from_position(Vector3(4, 5, -8), Vector3.ZERO)
	var front := DirectionalLight3D.new()
	front.light_energy = 0.6
	add_child(front)
	front.look_at_from_position(Vector3(0, 4, 10), Vector3.ZERO)


func _ground() -> void:
	var n := _gn
	_gh.resize((n + 1) * (n + 1))
	var verts := PackedVector3Array()
	var cols := PackedColorArray()
	var uv2 := PackedVector2Array()
	verts.resize((n + 1) * (n + 1))
	cols.resize(verts.size())
	uv2.resize(verts.size())
	for j in n + 1:
		for i in n + 1:
			var x := _remap(float(i) / n * 2 - 1)
			var z := _remap(float(j) / n * 2 - 1) - 6
			var k := j * (n + 1) + i
			var h := height_at(x, z)
			var w := wear_at(x, z)
			verts[k] = Vector3(x, h, z)
			_gh[k] = h
			var nz := _fbm(x * 0.12, z * 0.12, _seed + 10, 3)
			var n2 := _fbm(x * 0.5, z * 0.5, _seed + 11, 2)
			var c: Color = Color(col["grass"]).lerp(col["grass2"], _sstep(0.35, 0.7, nz))
			c = c.lerp(col["hill"], clampf(h / HILLS, 0, 1) * 0.8)
			c = c * (0.9 + 0.2 * n2)
			c = c.lerp(col["worn"], w)
			c.a = 1.0
			cols[k] = c
			uv2[k] = Vector2(w, 0)
	var idx := PackedInt32Array()
	for j in n:
		for i in n:
			var a := j * (n + 1) + i
			var b := a + 1
			var d := a + n + 1
			var e := d + 1
			idx.append_array([a, b, d, b, e, d])
	# Smooth normals from the neighbouring grid points (central differences).
	var norms := PackedVector3Array()
	norms.resize(verts.size())
	for j in n + 1:
		for i in n + 1:
			var l := verts[j * (n + 1) + maxi(i - 1, 0)]
			var r := verts[j * (n + 1) + mini(i + 1, n)]
			var d := verts[maxi(j - 1, 0) * (n + 1) + i]
			var u := verts[mini(j + 1, n) * (n + 1) + i]
			norms[j * (n + 1) + i] = (u - d).cross(r - l).normalized()
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = verts
	arr[Mesh.ARRAY_NORMAL] = norms
	arr[Mesh.ARRAY_COLOR] = cols
	arr[Mesh.ARRAY_TEX_UV2] = uv2
	arr[Mesh.ARRAY_INDEX] = idx
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	var mat := ShaderMaterial.new()
	var sh := Shader.new()
	sh.code = GROUND_SHADER
	mat.shader = sh
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)


func _decal() -> void:
	var mat := ShaderMaterial.new()
	var sh := Shader.new()
	sh.code = DECAL_SHADER
	mat.shader = sh
	mat.set_shader_parameter("tint", Color(theme["worn"]).darkened(0.72))
	mat.set_shader_parameter("glow", col["accent"])
	mat.set_shader_parameter("glow_energy", 0.0)
	var pm := PlaneMesh.new()
	pm.size = Vector2(1, 1)
	var mi := MeshInstance3D.new()
	mi.mesh = pm
	mi.material_override = mat
	mi.scale = Vector3(10.4, 1, 9.8)
	mi.position = Vector3(0, 0.03, -0.6)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.sorting_offset = 1.0
	add_child(mi)


# ---------- framing props (merged per material, as mergeParts) ----------
static var _geo := {}


static func _lumpy(seed: int, amt: float, freq: float) -> Array:
	var key := "%d|%s|%s" % [seed, amt, freq]
	if _geo.has(key):
		return _geo[key]
	var ico := SphereMesh.new()
	ico.radius = 1.0
	ico.height = 2.0
	ico.radial_segments = 8
	ico.rings = 5
	var arr := ico.get_mesh_arrays()
	var v: PackedVector3Array = arr[Mesh.ARRAY_VERTEX]
	for i in v.size():
		var p := v[i]
		var nn := _fbm(p.x * freq + 13.1, p.z * freq + p.y * freq * 0.7 + 7.7, seed, 2) - 0.5
		var k := 1.0 + nn * amt * 2
		v[i] = Vector3(p.x * k, p.y * (1 + nn * amt), p.z * k)
	# flat-shaded faces (low-poly look)
	var idx: PackedInt32Array = arr[Mesh.ARRAY_INDEX]
	var out := [PackedVector3Array(), PackedVector3Array()]
	for t in range(0, idx.size(), 3):
		var a := v[idx[t]]
		var b := v[idx[t + 1]]
		var c := v[idx[t + 2]]
		var n := (c - a).cross(b - a).normalized()
		for p in [a, b, c]:
			out[0].append(p)
			out[1].append(n)
	_geo[key] = out
	return out


static func _prim(kind: String) -> Array:
	if _geo.has(kind):
		return _geo[kind]
	var m: Mesh
	match kind:
		"trunk":
			var c := CylinderMesh.new()
			c.top_radius = 0.32
			c.bottom_radius = 0.5
			c.height = 1.0
			c.radial_segments = 7
			c.rings = 1
			m = c
		"cone":
			var c := CylinderMesh.new()
			c.top_radius = 0.0
			c.bottom_radius = 0.5
			c.height = 1.0
			c.radial_segments = 7
			c.rings = 1
			m = c
	var arr: Array = m.get_mesh_arrays()
	var v: PackedVector3Array = arr[Mesh.ARRAY_VERTEX]
	var n: PackedVector3Array = arr[Mesh.ARRAY_NORMAL]
	var idx: PackedInt32Array = arr[Mesh.ARRAY_INDEX]
	var out := [PackedVector3Array(), PackedVector3Array()]
	for t in idx:
		out[0].append(v[t])
		out[1].append(n[t])
	_geo[kind] = out
	return out


func _add(mk: String, g: Array, pos: Vector3, rot: Vector3, scl: Vector3, c: Color, near: bool) -> void:
	var key := "%s|%d" % [mk, 1 if near else 0]
	if not _groups.has(key):
		_groups[key] = [PackedVector3Array(), PackedVector3Array(), PackedColorArray()]
	var xf := Transform3D(Basis.from_euler(rot, EULER_ORDER_XYZ).scaled_local(scl), pos)
	var nb := xf.basis.inverse().transposed()
	var b: Array = _groups[key]
	var vs: PackedVector3Array = g[0]
	var ns: PackedVector3Array = g[1]
	var lc := c
	for i in vs.size():
		b[0].append(xf * vs[i])
		b[1].append((nb * ns[i]).normalized())
		b[2].append(lc)


func _vary(c: Color, amt: float = 0.12) -> Color:
	var k := 1.0 - amt + _rnd() * amt * 2
	return Color(c.r * k, c.g * k, c.b * k)


func _clear_ok(x: float, z: float, m: float = 0.0) -> bool:
	return not (absf(x) < 4.5 + m and z > -7 - m and z < 4.5 + m) and _path_dist(x, z) > 1.3 + m * 0.5


func _spot(x0: float, x1: float, z0: float, z1: float, min_d: float, margin: float = 0.0) -> Variant:
	for k in 40:
		var x := R(x0, x1)
		var z := R(z0, z1)
		if not _clear_ok(x, z, margin):
			continue
		var ok := true
		for p in _placed:
			if Vector2(p[0] - x, p[1] - z).length() < maxf(min_d, p[2]):
				ok = false
				break
		if not ok:
			continue
		_placed.append([x, z, min_d])
		return Vector3(x, ground_y(x, z), z)
	return null


func _broad_tree(p: Vector3, near: bool, k: float) -> void:
	var s := R(0.85, 1.35) * k
	_add("bark", _prim("trunk"), p + Vector3(0, 1.3 * s, 0), Vector3(R(-0.08, 0.08), 0, R(-0.08, 0.08)), Vector3(0.42 * s, 2.6 * s, 0.42 * s), _vary(col["bark"]), near)
	var leaves: Array = theme["leaves"]
	var nb := 3 + int(_rnd() * 2)
	for i in nb:
		var a := _rnd() * 6.28
		var r := 0.0 if i == 0 else R(0.6, 1.0) * s
		var yy := (3.3 if i == 0 else R(2.5, 3.2)) * s
		_add("leaf", _lumpy(_seed + (5 if i % 2 else 3), 0.22 if i % 2 else 0.18, 1.6 if i % 2 else 1.2), p + Vector3(cos(a) * r, yy, sin(a) * r * 0.8),
			Vector3(_rnd() * 3, _rnd() * 3, 0), Vector3(R(1.0, 1.35) * s, R(0.8, 1.0) * s, R(1.0, 1.35) * s), _vary(Color(leaves[int(_rnd() * leaves.size())]).srgb_to_linear() * AP, 0.1), near)


func _pine(p: Vector3, near: bool, k: float) -> void:
	var s := R(0.9, 1.5) * k
	_add("bark", _prim("trunk"), p + Vector3(0, 0.8 * s, 0), Vector3.ZERO, Vector3(0.3 * s, 1.6 * s, 0.3 * s), _vary(col["bark"]), near)
	var lc := _vary(Color(theme["leaves"][0]).srgb_to_linear() * (AP * 0.75), 0.1)
	for i in 3:
		_add("leaf", _prim("cone"), p + Vector3(0, (1.5 + i * 1.05) * s, 0), Vector3(0, _rnd() * 3, 0), Vector3((2.2 - i * 0.55) * s, 1.8 * s, (2.2 - i * 0.55) * s), lc, near)


func _boulder(p: Vector3, near: bool, k: float) -> void:
	var s := R(0.6, 1.3) * k
	_add("stone", _lumpy(_seed + 7, 0.25, 1.1), p + Vector3(0, 0.25 * s, 0), Vector3(_rnd() * 3, _rnd() * 3, _rnd() * 3), Vector3(s * R(0.9, 1.4), s * R(0.6, 0.9), s * R(0.9, 1.3)), _vary(col["rock"]), near)


func _log(p: Vector3, near: bool, k: float) -> void:
	var s := R(0.8, 1.3) * k
	_add("bark", _prim("trunk"), p + Vector3(0, 0.22 * s, 0), Vector3(PI / 2, 0, _rnd() * 3), Vector3(0.45 * s, R(2.2, 3.4) * s, 0.45 * s), _vary(col["bark"]), near)


func _stump(p: Vector3, near: bool, _k: float) -> void:
	_add("bark", _prim("trunk"), p + Vector3(0, 0.3, 0), Vector3(0, _rnd() * 3, 0), Vector3(0.6, 0.6, 0.6), _vary(col["bark"]), near)


func _props() -> void:
	var n_props := func(n: int) -> int: return maxi(1, roundi(n * q["props"]))
	var tree := func(s: Vector3, near: bool, k: float) -> void:
		if _rnd() < 0.7:
			_broad_tree(s, near, k)
		else:
			_pine(s, near, k)
	# frameSlots(): two per side, four at the back, a forest beyond
	for side in [-1, 1]:
		for i in 2:
			var s = _spot(side * 4.8, side * 7.5, -8.5, -1, 2.4 * 1.1)
			if s != null:
				tree.call(s, true, 1.0)
	for i in 4:
		var s = _spot(-10, 10, -15, -8.2, 2.6 * 1.1)
		if s != null:
			tree.call(s, s.z > -13, 0.95)
	for i in n_props.call(30):
		var s = _spot(-28, 28, -42, -14, 3 * 1.1)
		if s != null:
			tree.call(s, false, 1.2)
	for i in n_props.call(8):
		var s = _spot(-14, 14, -20, -7.2, 1.5)
		if s != null:
			if i % 3:
				_boulder(s, s.z > -12, 0.9)
			elif i % 2:
				_log(s, s.z > -12, 0.9)
			else:
				_stump(s, s.z > -12, 0.9)
	for i in n_props.call(10):
		var s = _spot(-30, 30, -45, -20, 4)
		if s != null:
			_pine(s, false, 1.6)
	var mats := {}
	var leaf_sh := Shader.new()
	leaf_sh.code = LEAF_WIND
	for key in _groups:
		var mk: String = key.split("|")[0]
		var near: bool = key.ends_with("|1")
		if not mats.has(mk):
			if mk == "leaf":
				var lm := ShaderMaterial.new()
				lm.shader = leaf_sh
				mats[mk] = lm
			else:
				var sm := StandardMaterial3D.new()
				sm.vertex_color_use_as_albedo = true
				sm.vertex_color_is_srgb = false
				sm.roughness = 1.0 if mk == "stone" else 0.95
				mats[mk] = sm
		var b: Array = _groups[key]
		var arr := []
		arr.resize(Mesh.ARRAY_MAX)
		arr[Mesh.ARRAY_VERTEX] = b[0]
		arr[Mesh.ARRAY_NORMAL] = b[1]
		arr[Mesh.ARRAY_COLOR] = b[2]
		var mesh := ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
		var mi := MeshInstance3D.new()
		mi.mesh = mesh
		mi.material_override = mats[mk]
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if near and q["casters"] else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		add_child(mi)


func _in_view() -> Vector2:
	var u := _rnd()
	var z := 6.5 - 44 * pow(u, 1.25)
	var hw := 3.2 + (6.5 - z) * 0.32
	return Vector2(R(-hw, hw), z)


static func _unit_clear(x: float, z: float, r: float) -> bool:
	# keep the rim slots (allies near arc, enemies far arc) clear of scatter
	var d := Vector2(x / 4.2, (z + 0.6) / 3.95).length()
	return absf(d - 1.0) > 0.12 + r * 0.25 or absf(x) > 3.6


## tuftGeometry(): n tapered blades fanning out, dark base -> light tip.
func _tuft_mesh(n: int, h: float, spread: float, lean: float, blade_w: float) -> ArrayMesh:
	var v := PackedVector3Array()
	var nn := PackedVector3Array()
	var c := PackedColorArray()
	for b in n:
		var a := (float(b) / n) * TAU + _rnd() * 0.8
		var r := spread * (0.2 + _rnd() * 0.8)
		var bh := h * (0.6 + _rnd() * 0.5)
		var w := blade_w * (0.7 + _rnd() * 0.6)
		var o := Vector3(cos(a) * r * 0.3, 0, sin(a) * r * 0.3)
		var t := Vector3(cos(a) * (r + lean * bh), bh, sin(a) * (r + lean * bh))
		var pp := Vector3(-sin(a) * w, 0, cos(a) * w)
		var m := Vector3((o.x + t.x) * 0.5, bh * 0.55, (o.z + t.z) * 0.5)
		var pts := [o - pp, o + pp, m + pp * 0.6, m - pp * 0.6, t]
		for tri in [[0, 2, 1], [0, 3, 2], [3, 4, 2]]:
			for i in tri:
				var p: Vector3 = pts[i]
				v.append(p)
				nn.append(Vector3(cos(a) * 0.35, 0.9, sin(a) * 0.35).normalized())
				var kk := p.y / bh
				c.append(Color(0.45 + 0.65 * kk, 0.5 + 0.6 * kk, 0.45 + 0.55 * kk))
	var arr := []
	arr.resize(Mesh.ARRAY_MAX)
	arr[Mesh.ARRAY_VERTEX] = v
	arr[Mesh.ARRAY_NORMAL] = nn
	arr[Mesh.ARRAY_COLOR] = c
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	return mesh


func _rocks_and_tufts() -> void:
	# rocks: pebbles round the rim and path, stones in view, a few big ones
	var rock_g := _lumpy(_seed + 21, 0.28, 1.4)
	var rm := ArrayMesh.new()
	var ra := []
	ra.resize(Mesh.ARRAY_MAX)
	ra[Mesh.ARRAY_VERTEX] = rock_g[0]
	ra[Mesh.ARRAY_NORMAL] = rock_g[1]
	rm.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, ra)
	var rmat := StandardMaterial3D.new()
	rmat.vertex_color_use_as_albedo = true
	rmat.vertex_color_is_srgb = false
	rmat.roughness = 1.0
	var xfs := []
	var cs := []
	var n: int = q["rocks"]
	for k in n * 6:
		if xfs.size() >= n:
			break
		var i := xfs.size()
		var kind := float(i) / n
		var x := 0.0
		var z := 0.0
		var s := 0.0
		if kind < 0.45:
			var a := _rnd() * 6.28
			var rr := R(0.85, 1.25)
			if _rnd() < 0.6:
				x = cos(a) * 5.0 * rr
				z = sin(a) * 3.9 * rr - 0.5
			else:
				var p := _in_view()
				x = p.x
				z = p.y
				if _path_dist(x, z) > 1.4:
					continue
			s = R(0.04, 0.12)
		elif kind < 0.85:
			var p := _in_view()
			x = p.x
			z = p.y
			s = R(0.12, 0.38)
			if _arena_r(x, z) < 0.9:
				continue
		else:
			var p := _in_view()
			x = p.x
			z = p.y
			s = R(0.4, 0.9)
			if not _clear_ok(x, z, 0.3):
				continue
		if not _unit_clear(x, z, s):
			continue
		var y := ground_y(x, z)
		var b := Basis.from_euler(Vector3(_rnd() * 3, _rnd() * 3, _rnd() * 3)).scaled_local(Vector3(s * R(0.9, 1.5), s * R(0.5, 0.9), s * R(0.9, 1.4)))
		xfs.append(Transform3D(b, Vector3(x, y + s * 0.15, z)))
		cs.append(_vary(col["rock"], 0.18))
	_multimesh(rm, rmat, xfs, cs, false)
	# grass tufts, then taller grass in the unworn ground
	var tm := ShaderMaterial.new()
	var sh := Shader.new()
	sh.code = TUFT_SHADER
	tm.shader = sh
	for pass_i in 2:
		var mesh := _tuft_mesh(8, 0.22, 0.1, 0.4, 0.024) if pass_i == 0 else _tuft_mesh(8, 0.55, 0.15, 0.8, 0.07)
		var count: int = q["tufts"] if pass_i == 0 else q["scatter2"]
		xfs = []
		cs = []
		for k in count * 6:
			if xfs.size() >= count:
				break
			var p := _in_view()
			var x := p.x
			var z := p.y
			var y := ground_y(x, z)
			if pass_i == 0:
				var w := wear_at(x, z)
				var dens := _fbm(x * 0.25, z * 0.25, _seed + 33, 2)
				if _rnd() < w * 1.2 or _rnd() > 0.35 + dens or not _unit_clear(x, z, 0.2):
					continue
			elif not _unit_clear(x, z, 0.3) or wear_at(x, z) > 0.4 or _arena_r(x, z) < 1.05:
				continue
			var s := R(0.6, 1.2) if pass_i == 0 else R(0.7, 1.3)
			var b := Basis(Vector3.UP, _rnd() * 6.28).scaled_local(Vector3(s, s * (R(0.8, 1.3) if pass_i == 0 else 1.0), s))
			xfs.append(Transform3D(b, Vector3(x, y, z)))
			cs.append(_vary(Color(col["tuft"]) * (1.0 if pass_i == 0 else 0.8), 0.2 if pass_i == 0 else 0.15))
		_multimesh(mesh, tm, xfs, cs, false)


func _multimesh(mesh: Mesh, mat: Material, xfs: Array, cs: Array, shadows: bool) -> void:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = true
	mm.mesh = mesh
	mm.instance_count = xfs.size()
	for i in xfs.size():
		mm.set_instance_transform(i, xfs[i])
		var c: Color = cs[i]
		mm.set_instance_color(i, c)
	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	mmi.material_override = mat
	mmi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON if shadows else GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mmi)


## ring(): tree-line silhouettes at the horizon, fading into the fog.
func _silhouettes() -> void:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for layer in 2:
		var radius := 62.0 if layer == 0 else 90.0
		var c: Color = Color(theme["sil"]).lerp(col["fog"], 0.45 if layer == 0 else 0.7)
		var seg := 160
		var prev := Vector3.ZERO
		var prev_top := Vector3.ZERO
		for i in seg + 1:
			var a := -PI * 0.95 + float(i) / seg * PI * 1.9
			var nf := func(f: float, o: int) -> float: return _fbm(cos(a) * f + 50, sin(a) * f + 50, _seed + 60 + layer, o)
			var h: float
			if layer == 0:
				var base: float = 4 + nf.call(1.5, 4) * 6
				var sp := absf(fmod(fmod(a * 70, 2) + 2, 2) - 1)
				h = base + sp * 2.2 * (0.6 + nf.call(20, 2))
			else:
				var r := 1 - absf(nf.call(2.2, 4) * 2 - 1)
				h = 3 + pow(r, 2.2) * 16 + nf.call(9, 2) * 2
			var p := Vector3(sin(a) * radius, -2, -cos(a) * radius - 4)
			var top := Vector3(p.x, h, p.z)
			if i > 0:
				for v in [prev, prev_top, top, prev, top, p]:
					st.set_color(c)
					st.set_normal(Vector3(0, 0, 1))
					st.add_vertex(v)
			prev = p
			prev_top = top
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.vertex_color_use_as_albedo = true
	m.vertex_color_is_srgb = true
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	m.disable_fog = true
	var mi := MeshInstance3D.new()
	mi.mesh = st.commit()
	mi.material_override = m
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)


## Drifting leaves (the dusk forest's motes).
func _motes() -> void:
	var p := CPUParticles3D.new()
	p.amount = q["motes"]
	p.lifetime = 9.0
	p.preprocess = 9.0
	p.emission_shape = CPUParticles3D.EMISSION_SHAPE_BOX
	p.emission_box_extents = Vector3(12, 4, 10)
	p.position = Vector3(0, 3.4, -5)
	p.direction = Vector3(0.25, -0.32, 0.05)
	p.spread = 25.0
	p.gravity = Vector3(0, -0.05, 0)
	p.initial_velocity_min = 0.25
	p.initial_velocity_max = 0.5
	p.angular_velocity_min = -90
	p.angular_velocity_max = 90
	p.scale_amount_min = 0.7
	p.scale_amount_max = 1.4
	var q2 := QuadMesh.new()
	q2.size = Vector2(0.13, 0.09)
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(theme["mote"]) * 0.5
	m.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	q2.material = m
	p.mesh = q2
	add_child(p)
