## The battle arena: sky and fog in the chapter's colours (three3d.mjs sceneColors()), the
## lighting rig (hemisphere-like ambient, a shadow-casting sun, rim and front fills), a ground
## with the rune circle the units stand on, and simple scenery outside the circle.
class_name Arena
extends Node3D

## sceneColors(chapter): sky top, horizon, ground, props.
const SCENE_COLORS := [
	["#1f2b46", "#c9805a", "#2a2a22", "#16141f"], ["#16283a", "#4f7c8a", "#1c2a2a", "#0e1a22"],
	["#2a1a1e", "#a8483a", "#2e1a1c", "#160c0e"], ["#1a2e2a", "#7fa088", "#23362a", "#101c16"],
	["#28203a", "#c8a870", "#2c2640", "#14101e"],
]

const GROUND_SHADER := """
shader_type spatial;
render_mode diffuse_burley, specular_disabled;
uniform vec3 ground_a : source_color;
uniform vec3 ground_b : source_color;
uniform vec3 rune : source_color;
uniform vec2 centre = vec2(0.0, -0.6);
uniform vec2 radii = vec2(4.2, 3.95);
varying vec3 world;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
float band(float d, float at, float w) { return 1.0 - smoothstep(0.0, w, abs(d - at)); }

void vertex() { world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; }

void fragment() {
	vec2 p = world.xz;
	float n = noise(p * 0.7) * 0.6 + noise(p * 2.3) * 0.3 + noise(p * 9.0) * 0.1;
	vec3 col = mix(ground_a, ground_b, n);
	vec2 q = (p - centre) / radii;
	float d = length(q);
	float ang = atan(q.y, q.x);
	// worn fighting circle: a slightly lighter, smoother floor inside the rim
	col = mix(col, col * 1.18 + vec3(0.02), (1.0 - smoothstep(0.85, 1.05, d)) * 0.6);
	float glow = 0.0;
	glow += band(d, 1.0, 0.012) * 1.0;
	glow += band(d, 1.06, 0.006) * 0.6;
	glow += band(d, 0.8, 0.006) * 0.55;
	// rune marks between the rings
	float seg = fract(ang / 6.2831853 * 36.0);
	float mark = step(0.86, d) * step(d, 0.95) * step(0.35, seg) * step(seg, 0.6) * step(0.5, fract(ang / 6.2831853 * 36.0 * 0.5 + 0.13));
	glow += mark * 0.5;
	// a six-pointed sigil inside
	for (int k = 0; k < 6; k++) {
		float a = float(k) * 1.0471976;
		vec2 dir = vec2(cos(a), sin(a));
		float dl = abs(dot(q, vec2(-dir.y, dir.x)) - 0.4);
		glow += (1.0 - smoothstep(0.0, 0.006, dl)) * step(abs(dot(q, dir)), 0.69) * 0.35;
	}
	float fade = 1.0 - smoothstep(10.0, 28.0, length(p - centre));
	ALBEDO = col * (0.55 + 0.45 * fade) + rune * glow * 0.25;
	EMISSION = rune * glow * 0.9;
	ROUGHNESS = 1.0;
}
"""

var colors: Array
var sun: DirectionalLight3D


func build(chapter: int) -> void:
	colors = SCENE_COLORS[chapter % SCENE_COLORS.size()]
	var sky_top := Color(colors[0])
	var horizon := Color(colors[1])
	var ground := Color(colors[2])
	# Sky, ambient and fog.
	var env := Environment.new()
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = sky_top
	sky_mat.sky_horizon_color = horizon
	sky_mat.ground_horizon_color = horizon.darkened(0.3)
	sky_mat.ground_bottom_color = ground.darkened(0.5)
	sky_mat.sun_angle_max = 0.0
	var sky := Sky.new()
	sky.sky_material = sky_mat
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = horizon.lerp(Color.WHITE, 0.35).lerp(ground, 0.35)
	env.ambient_light_energy = 0.7
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.15
	env.fog_enabled = true
	env.fog_light_color = horizon.darkened(0.45)
	env.fog_density = 0.006
	env.fog_sky_affect = 0.3
	env.glow_enabled = true
	env.glow_intensity = 0.6
	env.glow_bloom = 0.05
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)
	# Lights (three3d.mjs mount(): sun at (-5, 10, 6) with soft shadows, a cool rim and a front fill).
	sun = DirectionalLight3D.new()
	sun.light_color = Color("#fff1dd")
	sun.light_energy = 1.6
	sun.shadow_enabled = true
	sun.shadow_blur = 2.0
	sun.shadow_bias = 0.04
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_ORTHOGONAL
	sun.directional_shadow_max_distance = 40.0
	add_child(sun)
	sun.look_at_from_position(Vector3(-5, 10, 6), Vector3.ZERO)
	var rim := DirectionalLight3D.new()
	rim.light_color = Color("#99aaff")
	rim.light_energy = 0.6
	add_child(rim)
	rim.look_at_from_position(Vector3(4, 5, -8), Vector3.ZERO)
	var front := DirectionalLight3D.new()
	front.light_energy = 0.35
	add_child(front)
	front.look_at_from_position(Vector3(0, 4, 10), Vector3.ZERO)
	_ground(ground)
	_scenery(Color(colors[3]), chapter)


func _ground(ground: Color) -> void:
	var sh := Shader.new()
	sh.code = GROUND_SHADER
	var mat := ShaderMaterial.new()
	mat.shader = sh
	var lifted := ground.lerp(Color("#8c8680"), 0.3)
	mat.set_shader_parameter("ground_a", lifted.darkened(0.25))
	mat.set_shader_parameter("ground_b", lifted.lightened(0.08))
	mat.set_shader_parameter("rune", Color("#f2c45c"))
	var plane := PlaneMesh.new()
	plane.size = Vector2(120, 120)
	var mi := MeshInstance3D.new()
	mi.mesh = plane
	mi.material_override = mat
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)


## buildScenery(kind 0): trees and rocks scattered outside the circle, deterministic per chapter.
func _scenery(prop: Color, seed: int) -> void:
	var rng := Rules.Seeded.new(seed)
	var rnd := func() -> float: return rng.next()
	var bark := StandardMaterial3D.new()
	bark.albedo_color = prop.lightened(0.15)
	bark.roughness = 1.0
	var leaf := StandardMaterial3D.new()
	leaf.albedo_color = Color("#2c3a28")
	leaf.roughness = 1.0
	var stone := StandardMaterial3D.new()
	stone.albedo_color = Color("#4a4550")
	stone.roughness = 0.95
	for i in 46:
		var ang: float = rnd.call() * TAU
		var rad: float = 9.0 + rnd.call() * 12.0
		var x := cos(ang) * rad
		var z := sin(ang) * rad * 0.8 - 5.0
		if z > 4.0 or (z > -4.5 and absf(x) < 7.0):
			continue # keep the battlefield and the camera's view clear
		var k: float = 0.6 + rnd.call() * 1.4
		if i % 3 == 0:
			var rock := MeshInstance3D.new()
			var sm := SphereMesh.new()
			sm.radius = 0.6 * k
			sm.height = 0.8 * k
			sm.radial_segments = 8
			sm.rings = 4
			rock.mesh = sm
			rock.material_override = stone
			rock.position = Vector3(x, 0.15 * k, z)
			rock.rotation = Vector3(rnd.call() * 0.4, rnd.call() * TAU, 0)
			add_child(rock)
		else:
			var trunk := MeshInstance3D.new()
			var cm := CylinderMesh.new()
			cm.top_radius = 0.12 * k
			cm.bottom_radius = 0.22 * k
			cm.height = 1.4 * k
			cm.radial_segments = 6
			trunk.mesh = cm
			trunk.material_override = bark
			trunk.position = Vector3(x, 0.7 * k, z)
			add_child(trunk)
			var crown := MeshInstance3D.new()
			var cone := CylinderMesh.new()
			cone.top_radius = 0.0
			cone.bottom_radius = 1.0 * k
			cone.height = 2.6 * k
			cone.radial_segments = 7
			crown.mesh = cone
			crown.material_override = leaf
			crown.position = Vector3(x, 2.4 * k, z)
			add_child(crown)
