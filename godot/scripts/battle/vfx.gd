## Skill effects after game3d/vfx.mjs: per-element palettes in HDR (so glows cross the bloom
## threshold), a rune spell circle with a light wall and element flourishes under casters, energy
## charging into the caster, projectiles with trails (orb, bolt, arrow, shard), crescent melee
## slashes, impacts (flash, shock ring, sparks, the element's signature burst, a ground glow),
## heal / buff / debuff / shield auras, poison ticks, death fades and a crit screen flash.
## Particles are CPUParticles3D one-shots (work on every renderer); sprites are code-drawn.
class_name Vfx
extends Node3D

## Element palettes (linear HDR): core (white-hot centre), main (body), deep (edges), dark (smoke).
const PAL := {
	"flame": {"core": Color(4.2, 2.3, 0.8), "main": Color(3.4, 0.85, 0.16), "deep": Color(1.5, 0.22, 0.04), "dark": Color(0.09, 0.035, 0.02)},
	"gale": {"core": Color(1.9, 3.6, 2.2), "main": Color(0.45, 2.6, 1.1), "deep": Color(0.12, 0.8, 0.32), "dark": Color(0.04, 0.12, 0.05)},
	"tide": {"core": Color(1.9, 3.0, 4.0), "main": Color(0.3, 1.3, 3.6), "deep": Color(0.06, 0.35, 1.3), "dark": Color(0.02, 0.06, 0.14)},
	"light": {"core": Color(4.2, 3.8, 2.6), "main": Color(3.6, 2.5, 0.65), "deep": Color(1.4, 0.8, 0.14), "dark": Color(0.12, 0.09, 0.03)},
	"shadow": {"core": Color(2.6, 1.1, 4.0), "main": Color(1.4, 0.35, 3.0), "deep": Color(0.4, 0.06, 0.9), "dark": Color(0.018, 0.004, 0.035)},
	"physical": {"core": Color(3.4, 3.2, 2.9), "main": Color(2.4, 2.1, 1.75), "deep": Color(0.8, 0.65, 0.5), "dark": Color(0.06, 0.05, 0.045)},
}
const HEAL := {"core": Color(2.2, 4.0, 2.0), "main": Color(0.55, 3.0, 0.8), "deep": Color(0.15, 1.0, 0.25)}
const GOLD := {"core": Color(4.0, 3.4, 1.8), "main": Color(3.4, 2.2, 0.5), "deep": Color(1.2, 0.6, 0.1)}
const CURSE := {"core": Color(2.6, 1.2, 4.0), "main": Color(1.5, 0.4, 3.0), "deep": Color(0.4, 0.08, 0.9)}
const AQUA := {"core": Color(1.8, 3.4, 4.2), "main": Color(0.5, 1.9, 3.8), "deep": Color(0.1, 0.5, 1.4)}

const SPRITE_SHADER := """
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform sampler2D tex : source_color, filter_linear;
uniform float k = 1.0;
void vertex() {
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = MODELVIEW_MATRIX * mat4(vec4(length(MODEL_MATRIX[0].xyz), 0.0, 0.0, 0.0), vec4(0.0, length(MODEL_MATRIX[1].xyz), 0.0, 0.0), vec4(0.0, 0.0, length(MODEL_MATRIX[2].xyz), 0.0), vec4(0.0, 0.0, 0.0, 1.0));
}
void fragment() {
	vec4 t = texture(tex, UV);
	ALBEDO = COLOR.rgb * t.rgb * k;
	ALPHA = t.a * COLOR.a;
}
"""

const GROUND_SHADER := """
shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;
uniform vec4 color : source_color = vec4(1.0);
uniform float energy = 1.0;
uniform float fade = 1.0;
uniform int kind = 0; // 0 glow, 1 rune circle, 2 shock ring, 3 cracks
uniform float spin = 0.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float band(float d, float at, float w) { return 1.0 - smoothstep(0.0, w, abs(d - at)); }
void fragment() {
	vec2 q = (UV - 0.5) * 2.0;
	float d = length(q);
	float a = 0.0;
	if (kind == 0) {
		a = pow(max(0.0, 1.0 - d), 2.0);
	} else if (kind == 1) {
		float ang = atan(q.y, q.x) + spin;
		a = band(d, 0.92, 0.03) + band(d, 0.78, 0.02) * 0.8 + band(d, 0.45, 0.02) * 0.6;
		float cell = fract(ang / 6.2831853 * 16.0);
		a += step(0.79, d) * step(d, 0.91) * step(0.3, cell) * step(cell, 0.55) * 0.9;
		for (int i = 0; i < 2; i++) {
			// two triangles: a hexagram
			for (int s = 0; s < 3; s++) {
				float an = float(s) * 2.0943951 + float(i) * 1.0471976 + spin * 0.5;
				vec2 n = vec2(cos(an), sin(an));
				a += (1.0 - smoothstep(0.0, 0.018, abs(dot(q, n) - 0.39))) * step(d, 0.78) * 0.7;
			}
		}
		a += pow(max(0.0, 1.0 - d), 3.0) * 0.25;
	} else if (kind == 2) {
		a = band(d, 0.82, 0.16) * smoothstep(1.0, 0.7, d);
	} else {
		float ang = atan(q.y, q.x);
		for (int i = 0; i < 7; i++) {
			float ca = hash(vec2(float(i), 3.0)) * 6.2831853;
			float da = abs(mod(ang - ca + 3.14159265, 6.2831853) - 3.14159265);
			a += (1.0 - smoothstep(0.0, 0.05, da * d + (hash(vec2(floor(d * 12.0), float(i))) - 0.5) * 0.02)) * step(d, 0.9 * hash(vec2(float(i), 5.0)) + 0.3);
		}
		a += pow(max(0.0, 1.0 - d), 2.0) * 0.6;
	}
	ALBEDO = color.rgb * energy * clamp(a, 0.0, 1.5);
	ALPHA = clamp(a, 0.0, 1.0) * fade * step(d, 1.0);
}
"""

static var _tex := {}
var cam: Camera3D
var quality := "high"
var mult := 1.0


func setup(camera: Camera3D, tier: String) -> void:
	cam = camera
	quality = tier.to_lower()
	mult = {"ultra": 1.2, "high": 1.0, "medium": 0.6, "low": 0.35}.get(quality, 1.0)


func N(n: float) -> int:
	return maxi(1, roundi(n * mult))


static func pal(el: String) -> Dictionary:
	return PAL.get(el, PAL["physical"])


# ---------- code-drawn sprites ----------
static func sprite(kind: String) -> Texture2D:
	if _tex.has(kind):
		return _tex[kind]
	var S := 64
	var img := Image.create(S, S, false, Image.FORMAT_RGBA8)
	for y in S:
		for x in S:
			var u := (x + 0.5) / S * 2 - 1
			var v := (y + 0.5) / S * 2 - 1
			var d := Vector2(u, v).length()
			var a := 0.0
			match kind:
				"glow": a = pow(maxf(0, 1 - d), 2.2)
				"streak": a = pow(maxf(0, 1 - absf(u) * 3.0), 2) * pow(maxf(0, 1 - absf(v)), 1.5)
				"star":
					var ang := atan2(v, u)
					var r := 0.25 + 0.75 * pow(absf(cos(ang * 2.0)), 18.0)
					a = pow(maxf(0, 1 - d / r), 2.0) + pow(maxf(0, 1 - d), 6.0)
				"ring": a = pow(maxf(0, 1 - absf(d - 0.75) / 0.18), 2)
				"leaf": a = 1.0 if (u * u) / 0.25 + (v * v) / 0.9 < 1.0 else 0.0
				"drop": a = pow(maxf(0, 1 - Vector2(u * 1.6, v * 1.0).length()), 1.5)
				"smoke": a = pow(maxf(0, 1 - d), 1.4) * (0.6 + 0.4 * sin(u * 7.0 + v * 5.0))
				"plus": a = (1.0 if (absf(u) < 0.22 and absf(v) < 0.8) or (absf(v) < 0.22 and absf(u) < 0.8) else 0.0) * pow(maxf(0, 1 - d * 0.7), 0.5)
			a = clampf(a, 0, 1)
			img.set_pixel(x, y, Color(1, 1, 1, a))
	var t := ImageTexture.create_from_image(img)
	_tex[kind] = t
	return t


static var _sprite_mats := {}


static func sprite_mat(kind: String, additive := true) -> Material:
	var key := kind + str(additive)
	if _sprite_mats.has(key):
		return _sprite_mats[key]
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.blend_mode = BaseMaterial3D.BLEND_MODE_ADD if additive else BaseMaterial3D.BLEND_MODE_MIX
	m.billboard_mode = BaseMaterial3D.BILLBOARD_PARTICLES
	m.vertex_color_use_as_albedo = true
	m.albedo_texture = sprite(kind)
	m.no_depth_test = false
	m.disable_receive_shadows = true
	_sprite_mats[key] = m
	return m


# ---------- primitives ----------
## A one-shot particle burst. o: n, life, speed (min,max), dir, spread, gravity, size (s0,s1),
## c0, c1, sprite, add, box (emission extents), ring (radius), up (extra upward), orbit.
func burst(pos: Vector3, o: Dictionary) -> CPUParticles3D:
	var p := CPUParticles3D.new()
	p.one_shot = true
	p.emitting = false
	p.amount = maxi(1, o.get("n", 12))
	p.lifetime = o.get("life", 0.6)
	p.explosiveness = o.get("explode", 1.0)
	p.randomness = 0.5
	p.direction = o.get("dir", Vector3.UP)
	p.spread = o.get("spread", 180.0)
	p.initial_velocity_min = o.get("v0", 1.0)
	p.initial_velocity_max = o.get("v1", 2.5)
	p.gravity = Vector3(0, o.get("g", -4.0), 0)
	p.damping_min = o.get("drag", 1.5)
	p.damping_max = o.get("drag", 1.5) * 1.5
	if o.has("box"):
		p.emission_shape = CPUParticles3D.EMISSION_SHAPE_BOX
		p.emission_box_extents = o["box"]
	elif o.has("ring"):
		p.emission_shape = CPUParticles3D.EMISSION_SHAPE_RING
		p.emission_ring_axis = Vector3.UP
		p.emission_ring_radius = o["ring"]
		p.emission_ring_inner_radius = o["ring"] * 0.9
		p.emission_ring_height = 0.05
	if o.has("orbit"):
		p.orbit_velocity_min = o["orbit"]
		p.orbit_velocity_max = o["orbit"] * 1.4
	if o.get("rot", false):
		p.angle_min = 0
		p.angle_max = 360
		p.angular_velocity_min = -180
		p.angular_velocity_max = 180
	var s0: float = o.get("s0", 0.2)
	var s1: float = o.get("s1", 0.05)
	p.scale_amount_min = 0.7
	p.scale_amount_max = 1.3
	var curve := Curve.new()
	curve.max_value = maxf(s0, s1) * 1.2
	curve.add_point(Vector2(0, s0))
	curve.add_point(Vector2(1, s1))
	p.scale_amount_curve = curve
	var grad := Gradient.new()
	var c0: Color = o.get("c0", Color.WHITE)
	var c1: Color = o.get("c1", c0)
	grad.set_color(0, Color(c0, 0.0))
	grad.set_color(1, Color(c1, 0.0))
	grad.add_point(0.12, Color(c0, o.get("a", 1.0)))
	grad.add_point(0.6, Color(c0.lerp(c1, 0.6), o.get("a", 1.0) * 0.8))
	p.color_ramp = grad
	var q := QuadMesh.new()
	q.size = Vector2.ONE
	q.material = sprite_mat(o.get("sprite", "glow"), o.get("add", true))
	p.mesh = q
	p.position = pos
	add_child(p)
	if o.get("delay", 0.0) > 0:
		get_tree().create_timer(o["delay"]).timeout.connect(func() -> void: if is_instance_valid(p): p.emitting = true)
	else:
		p.emitting = true
	get_tree().create_timer(p.lifetime + o.get("delay", 0.0) + 0.6).timeout.connect(p.queue_free)
	return p


## A flat decal on the ground (glow, rune circle, shock ring, cracks) that grows and fades.
func ground(pos: Vector3, kind: int, c: Color, s0: float, s1: float, life: float, energy := 1.0, spin := 0.0) -> void:
	var sh: Shader = _tex.get("ground_shader")
	if sh == null:
		sh = Shader.new()
		sh.code = GROUND_SHADER
		_tex["ground_shader"] = sh
	var m := ShaderMaterial.new()
	m.shader = sh
	m.set_shader_parameter("color", Color(c.r, c.g, c.b) / maxf(1.0, maxf(c.r, maxf(c.g, c.b))))
	m.set_shader_parameter("energy", energy * maxf(1.0, maxf(c.r, maxf(c.g, c.b))))
	m.set_shader_parameter("kind", kind)
	var mi := MeshInstance3D.new()
	var pm := PlaneMesh.new()
	pm.size = Vector2(1, 1)
	mi.mesh = pm
	mi.material_override = m
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.position = Vector3(pos.x, 0.05 + kind * 0.004, pos.z)
	mi.scale = Vector3.ONE * s0
	add_child(mi)
	var tw := create_tween().set_parallel(true)
	tw.tween_property(mi, "scale", Vector3.ONE * s1, life).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	tw.tween_method(func(f: float) -> void: m.set_shader_parameter("fade", f), 0.0, 1.0, life * 0.15)
	tw.tween_method(func(f: float) -> void: m.set_shader_parameter("fade", f), 1.0, 0.0, life * 0.5).set_delay(life * 0.5)
	if spin != 0.0:
		tw.tween_method(func(a: float) -> void: m.set_shader_parameter("spin", a), 0.0, spin, life)
	tw.chain().tween_callback(mi.queue_free)


## A billboard flash (bright core + soft halo).
func flash(pos: Vector3, size: float, c: Color, life := 0.16) -> void:
	for layer in [[size, c, 1.0], [size * 0.45, Color(3, 3, 3), 1.0]]:
		var mi := MeshInstance3D.new()
		var q := QuadMesh.new()
		q.size = Vector2.ONE
		mi.mesh = q
		var m: StandardMaterial3D = sprite_mat("glow").duplicate()
		m.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
		m.vertex_color_use_as_albedo = false
		m.albedo_color = layer[1]
		mi.material_override = m
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		mi.position = pos
		mi.scale = Vector3.ONE * layer[0] * 0.6
		add_child(mi)
		var tw := create_tween().set_parallel(true)
		tw.tween_property(mi, "scale", Vector3.ONE * layer[0], life)
		tw.tween_property(m, "albedo_color:a", 0.0, life).set_delay(life * 0.3)
		tw.chain().tween_callback(mi.queue_free)


## A cylindrical wall of light rising out of the ground (castCircle's pillar, heal column).
func wall(pos: Vector3, r: float, h: float, c: Color, life: float) -> void:
	var mi := MeshInstance3D.new()
	var cm := CylinderMesh.new()
	cm.top_radius = r * 0.9
	cm.bottom_radius = r
	cm.height = h
	cm.cap_top = false
	cm.cap_bottom = false
	cm.radial_segments = 24
	mi.mesh = cm
	var sh: Shader = _tex.get("wall_shader")
	if sh == null:
		sh = Shader.new()
		sh.code = "shader_type spatial;\nrender_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;\nuniform vec4 color : source_color;\nuniform float energy = 1.0;\nuniform float fade = 1.0;\nvoid fragment() { float v = 1.0 - UV.y; float streak = 0.6 + 0.4 * sin(UV.x * 62.8318 + TIME * 3.0); ALBEDO = color.rgb * energy * streak; ALPHA = pow(1.0 - v, 1.6) * fade * smoothstep(0.0, 0.1, v); }"
		_tex["wall_shader"] = sh
	var m := ShaderMaterial.new()
	m.shader = sh
	var mx := maxf(1.0, maxf(c.r, maxf(c.g, c.b)))
	m.set_shader_parameter("color", Color(c.r / mx, c.g / mx, c.b / mx))
	m.set_shader_parameter("energy", mx * 0.6)
	mi.material_override = m
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.position = pos + Vector3(0, h / 2, 0)
	mi.scale = Vector3(1, 0.2, 1)
	add_child(mi)
	var tw := create_tween().set_parallel(true)
	tw.tween_property(mi, "scale", Vector3.ONE, life * 0.35).set_ease(Tween.EASE_OUT)
	tw.tween_method(func(f: float) -> void: m.set_shader_parameter("fade", f), 1.0, 0.0, life * 0.5).set_delay(life * 0.5)
	tw.chain().tween_callback(mi.queue_free)


# ---------- public effects ----------
## Ground spell circle under a caster, with a light wall and the element's flourish.
func cast_circle(pos: Vector3, el: String) -> void:
	var P := pal(el)
	ground(pos, 1, P["main"], 0.8, 2.0, 1.05, 1.0, 1.4)
	ground(pos, 1, P["core"], 0.4, 1.15, 0.95, 0.55, -2.6)
	ground(pos, 0, P["main"], 2.0, 2.6, 1.0, 0.45)
	wall(pos, 0.9, 0.8, P["main"], 0.85)
	match el:
		"flame":
			burst(pos + Vector3(0, 0.12, 0), {"n": N(16), "ring": 0.85, "v0": 0.5, "v1": 1.1, "g": 1.5, "s0": 0.38, "s1": 0.12, "c0": P["core"], "c1": P["deep"], "life": 0.6, "explode": 0.6})
			burst(pos + Vector3(0, 0.1, 0), {"n": N(14), "box": Vector3(0.8, 0.05, 0.8), "v0": 1.0, "v1": 2.2, "g": 0.5, "s0": 0.07, "s1": 0.02, "c0": P["core"], "c1": P["main"], "life": 0.9})
		"gale":
			burst(pos + Vector3(0, 0.1, 0), {"n": N(16), "ring": 0.8, "v0": 0.6, "v1": 1.2, "g": 0.4, "orbit": 0.9, "sprite": "streak", "s0": 0.18, "s1": 0.05, "c0": P["core"], "c1": P["main"], "life": 0.8, "dir": Vector3.UP, "spread": 20})
			burst(pos + Vector3(0, 0.1, 0), {"n": N(5), "ring": 0.7, "v0": 0.6, "v1": 1.2, "g": -0.4, "orbit": 0.6, "sprite": "leaf", "add": false, "rot": true, "s0": 0.12, "s1": 0.08, "c0": Color(0.25, 0.6, 0.2), "life": 0.9})
		"tide":
			for i in 3:
				ground(pos, 2, P["core"], 0.3, 2.2, 0.7, 0.6)
			burst(pos + Vector3(0, 0.05, 0), {"n": N(16), "ring": 0.6, "v0": 1.6, "v1": 2.8, "g": -7.0, "sprite": "drop", "s0": 0.12, "s1": 0.06, "c0": AQUA["core"], "c1": P["main"], "life": 0.7, "dir": Vector3.UP, "spread": 15})
		"light":
			burst(pos + Vector3(0, 0.25, 0), {"n": N(16), "box": Vector3(0.8, 0.15, 0.8), "v0": 0.4, "v1": 1.0, "g": 0.0, "sprite": "star", "rot": true, "s0": 0.2, "s1": 0.02, "c0": P["core"], "life": 0.9})
			burst(pos + Vector3(0, 0.4, 0), {"n": N(7), "box": Vector3(0.6, 0.05, 0.6), "v0": 2.6, "v1": 3.0, "g": 0.0, "sprite": "streak", "s0": 0.3, "s1": 0.1, "c0": P["core"], "life": 0.5, "dir": Vector3.UP, "spread": 5})
		"shadow":
			burst(pos + Vector3(0, 0.25, 0), {"n": N(8), "ring": 1.1, "v0": 0.2, "v1": 0.5, "g": 0.2, "orbit": 0.4, "sprite": "smoke", "add": false, "rot": true, "s0": 0.45, "s1": 0.15, "c0": P["dark"], "a": 0.8, "life": 1.0})
			burst(pos + Vector3(0, 0.3, 0), {"n": N(16), "ring": 1.0, "v0": 0.2, "v1": 0.6, "g": 0.3, "orbit": 0.6, "sprite": "streak", "s0": 0.12, "s1": 0.03, "c0": P["core"], "life": 0.8})
		_:
			burst(pos + Vector3(0, 0.1, 0), {"n": N(10), "box": Vector3(0.8, 0.05, 0.8), "v0": 0.8, "v1": 1.6, "g": 0.0, "s0": 0.08, "s1": 0.02, "c0": P["core"], "life": 0.7})


## Energy gathering into a caster.
func charge(pos: Vector3, el: String, dur := 0.6) -> void:
	var P := pal(el)
	var c := pos + Vector3(0, 1.1, 0)
	var sprite_kind = {"flame": "glow", "gale": "streak", "tide": "drop", "light": "star", "shadow": "streak"}.get(el, "glow")
	burst(c, {"n": N(36), "box": Vector3(1.2, 1.0, 1.2), "v0": -2.6, "v1": -1.8, "g": 0.0, "dir": Vector3.UP, "spread": 180, "explode": 0.2, "sprite": sprite_kind, "s0": 0.05, "s1": 0.14, "c0": P["main"], "c1": P["core"], "life": dur, "drag": 0.0})
	get_tree().create_timer(dur * 0.85).timeout.connect(func() -> void: flash(c, 0.9, P["main"], 0.14))


## A projectile flying from a to b on an arc, with a glowing trail (lands after ~0.2-0.6 s).
func projectile(a: Vector3, b: Vector3, el: String, kind := "orb") -> void:
	var P := pal(el)
	var spd: float = {"orb": 11.0, "bolt": 22.0, "arrow": 16.0, "shard": 18.0}.get(kind, 11.0)
	var arc: float = {"orb": 0.7, "bolt": 0.12, "arrow": 0.45, "shard": 0.2}.get(kind, 0.5)
	var dur := clampf(a.distance_to(b) / spd, 0.2, 0.62)
	var head := Node3D.new()
	add_child(head)
	head.position = a
	var big := kind == "orb"
	var halo := MeshInstance3D.new()
	var q := QuadMesh.new()
	q.size = Vector2.ONE
	halo.mesh = q
	var hm: StandardMaterial3D = sprite_mat("glow").duplicate()
	hm.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
	hm.vertex_color_use_as_albedo = false
	hm.albedo_color = P["main"]
	halo.material_override = hm
	halo.scale = Vector3.ONE * (0.95 if big else 0.45)
	head.add_child(halo)
	var core := halo.duplicate() as MeshInstance3D
	var cm: StandardMaterial3D = hm.duplicate()
	cm.albedo_color = P["core"]
	core.material_override = cm
	core.scale = Vector3.ONE * (0.38 if big else 0.18)
	head.add_child(core)
	# trail: a stream of fading glows left behind
	var trail := CPUParticles3D.new()
	trail.amount = N(28 if big else 18)
	trail.lifetime = 0.25
	trail.local_coords = false
	trail.direction = Vector3.UP
	trail.spread = 180
	trail.initial_velocity_min = 0.0
	trail.initial_velocity_max = 0.3
	trail.gravity = Vector3.ZERO
	var tc := Curve.new()
	tc.add_point(Vector2(0, (0.42 if big else 0.2)))
	tc.add_point(Vector2(1, 0.02))
	trail.scale_amount_curve = tc
	var tg := Gradient.new()
	tg.set_color(0, P["core"])
	tg.set_color(1, Color(P["deep"], 0))
	trail.color_ramp = tg
	var tq := QuadMesh.new()
	tq.size = Vector2.ONE
	tq.material = sprite_mat("streak" if kind == "arrow" or kind == "bolt" else "glow")
	trail.mesh = tq
	head.add_child(trail)
	var tw := create_tween()
	tw.tween_method(func(k: float) -> void:
		head.position = a.lerp(b, k) + Vector3(0, sin(k * PI) * arc, 0), 0.0, 1.0, dur)
	tw.tween_callback(func() -> void:
		trail.emitting = false
		halo.queue_free()
		core.queue_free())
	tw.tween_interval(0.4)
	tw.tween_callback(head.queue_free)


## A crescent melee slash across a hit point, facing the camera.
func slash(pos: Vector3, el: String) -> void:
	var P := pal(el)
	var mi := MeshInstance3D.new()
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var seg := 20
	var pts := []
	for i in seg + 1:
		var u := float(i) / seg
		var a := -1.2 + u * 2.4
		var w := sin(u * PI) * 0.22
		pts.append([Vector3(cos(a) * 0.85, sin(a) * 0.5, 0), Vector3(cos(a) * (0.85 - w), sin(a) * (0.5 - w * 0.6), 0), u])
	for i in seg:
		var p0: Array = pts[i]
		var p1: Array = pts[i + 1]
		for v in [[p0[0], p0[2], 0.0], [p1[0], p1[2], 0.0], [p0[1], p0[2], 1.0], [p1[0], p1[2], 0.0], [p1[1], p1[2], 1.0], [p0[1], p0[2], 1.0]]:
			st.set_uv(Vector2(v[1], v[2]))
			st.add_vertex(v[0])
	mi.mesh = st.commit()
	var sh: Shader = _tex.get("slash_shader")
	if sh == null:
		sh = Shader.new()
		sh.code = "shader_type spatial;\nrender_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled;\nuniform vec4 color : source_color;\nuniform vec4 hot : source_color;\nuniform float energy = 2.0;\nuniform float sweep = 1.0;\nvoid fragment() { float head = smoothstep(sweep, sweep - 0.25, UV.x); float edge = smoothstep(1.0, 0.0, UV.y); vec3 c = mix(color.rgb, hot.rgb, edge * edge); ALBEDO = c * energy; ALPHA = head * smoothstep(0.0, 0.3, UV.x) * (0.35 + 0.65 * edge) * color.a; }"
		_tex["slash_shader"] = sh
	var m := ShaderMaterial.new()
	m.shader = sh
	var main: Color = P["main"]
	var mx := maxf(1.0, maxf(main.r, maxf(main.g, main.b)))
	m.set_shader_parameter("color", Color(main.r / mx, main.g / mx, main.b / mx, 1.0))
	m.set_shader_parameter("hot", Color(1, 1, 1))
	m.set_shader_parameter("energy", mx * 0.8)
	mi.material_override = m
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)
	var to_cam := (cam.global_position - pos).normalized()
	mi.global_position = pos + to_cam * 0.45
	mi.look_at(mi.global_position - to_cam, Vector3.UP)
	mi.rotate_object_local(Vector3.FORWARD, randf_range(-0.6, 0.6) + (PI if randf() < 0.5 else 0.0))
	var tw := create_tween()
	tw.tween_method(func(s: float) -> void: m.set_shader_parameter("sweep", s), 0.0, 1.25, 0.16)
	tw.tween_method(func(a: float) -> void: m.set_shader_parameter("color", Color(main.r / mx, main.g / mx, main.b / mx, a)), 1.0, 0.0, 0.18)
	tw.tween_callback(mi.queue_free)
	flash(mi.global_position, 0.9, P["main"], 0.14)
	burst(mi.global_position, {"n": N(10), "v0": 2.5, "v1": 4.2, "g": -5.0, "sprite": "streak", "s0": 0.16, "s1": 0.04, "c0": P["core"], "c1": P["main"], "life": 0.35})


## Hit impact at a unit (pos = its feet). crit = bigger, with an extra ring.
func impact(pos: Vector3, el: String, crit := false) -> void:
	var P := pal(el)
	var s := 1.35 if crit else 1.0
	var to_cam := (cam.global_position - pos).normalized() if cam else Vector3.BACK
	var p := pos + Vector3(0, 1.1, 0) + to_cam * 0.55
	flash(p, 1.2 * s, P["main"], 0.14)
	burst(p, {"n": N(22 if crit else 12), "v0": 2.0, "v1": 5.5 if crit else 4.2, "g": -5.0, "drag": 2.2, "sprite": "streak", "s0": 0.18, "s1": 0.04, "c0": P["core"], "c1": P["main"], "life": 0.4})
	ground(pos, 3, P["main"], 1.0 * s, 1.2 * s, 0.9, 0.45)
	ground(pos, 0, P["main"], 1.3 * s, 1.8 * s, 0.5, 0.25)
	match el:
		"flame":
			burst(p, {"n": N(14 * s), "v0": 1.2, "v1": 2.6, "g": 2.2, "drag": 4.0, "s0": 0.42 * s, "s1": 0.16 * s, "c0": P["core"], "c1": P["deep"], "life": 0.5})
			burst(p + Vector3(0, -0.2, 0), {"n": N(5 * s), "v0": 0.3, "v1": 0.8, "g": 0.6, "sprite": "smoke", "add": false, "rot": true, "s0": 0.4 * s, "s1": 1.1 * s, "c0": P["dark"], "a": 0.5, "life": 0.9, "delay": 0.1})
		"gale":
			burst(p, {"n": N(12 * s), "v0": 2.0, "v1": 4.5, "g": 0.0, "drag": 3.5, "orbit": 1.2, "sprite": "streak", "s0": 0.22, "s1": 0.05, "c0": P["core"], "c1": P["main"], "life": 0.45})
			burst(p, {"n": N(7 * s), "v0": 1.0, "v1": 2.2, "g": -1.6, "sprite": "leaf", "add": false, "rot": true, "s0": 0.12, "s1": 0.1, "c0": Color(0.3, 0.6, 0.25), "life": 0.9})
		"tide":
			burst(p + Vector3(0, -0.1, 0), {"n": N(22 * s), "v0": 2.2, "v1": 3.8, "g": -9.0, "dir": Vector3.UP, "spread": 55, "sprite": "drop", "s0": 0.13 * s, "s1": 0.05, "c0": AQUA["core"], "c1": P["main"], "life": 0.6})
			ground(pos, 2, P["core"], 0.3 * s, 1.3 * s, 0.35, 0.7)
		"light":
			burst(p, {"n": N(10 * s), "v0": 4.0, "v1": 7.0, "g": 0.0, "drag": 3.0, "sprite": "streak", "s0": 0.3, "s1": 0.05, "c0": P["core"], "life": 0.3})
			burst(p, {"n": N(14 * s), "box": Vector3(0.4, 0.4, 0.4) * s, "v0": 0.2, "v1": 0.8, "g": 0.5, "sprite": "star", "rot": true, "s0": 0.22, "s1": 0.02, "c0": P["core"], "life": 0.6})
		"shadow":
			burst(p, {"n": N(6 * s), "box": Vector3(0.5, 0.4, 0.5) * s, "v0": 0.1, "v1": 0.4, "g": 0.2, "sprite": "smoke", "add": false, "rot": true, "s0": 0.6 * s, "s1": 0.2, "c0": P["dark"], "a": 0.9, "life": 0.6})
			burst(p, {"n": N(14 * s), "v0": 2.0, "v1": 4.0, "drag": 4.0, "g": 0.0, "sprite": "streak", "s0": 0.18, "s1": 0.03, "c0": P["core"], "c1": P["main"], "life": 0.45, "delay": 0.3})
		_:
			burst(p + Vector3(0, -0.3, 0), {"n": N(6 * s), "v0": 0.3, "v1": 0.9, "g": 0.4, "sprite": "smoke", "add": false, "rot": true, "s0": 0.3 * s, "s1": 0.8 * s, "c0": Color(0.25, 0.22, 0.2), "a": 0.6, "life": 0.7})
	if crit:
		ground(pos, 2, P["core"], 0.4, 2.2, 0.26, 0.8)
		burst(p, {"n": N(1), "v0": 0.0, "v1": 0.0, "g": 0.0, "sprite": "star", "s0": 1.0, "s1": 2.4, "c0": P["core"], "life": 0.24})


## Healing: a green column with rising plus-motes and a ground ring.
func heal(pos: Vector3) -> void:
	wall(pos, 0.7, 1.6, HEAL["main"], 0.9)
	ground(pos, 0, HEAL["main"], 1.2, 1.9, 0.8, 0.5)
	burst(pos + Vector3(0, 0.3, 0), {"n": N(16), "box": Vector3(0.5, 0.2, 0.5), "v0": 0.8, "v1": 1.6, "g": 0.6, "dir": Vector3.UP, "spread": 15, "sprite": "plus", "s0": 0.18, "s1": 0.06, "c0": HEAL["core"], "c1": HEAL["main"], "life": 1.0})


## Buff (gold, rising) or debuff (violet, sinking) aura.
func buff(pos: Vector3, good: bool) -> void:
	var C: Dictionary = GOLD if good else CURSE
	ground(pos, 2, C["main"], 0.5, 1.5, 0.6, 0.6)
	burst(pos + Vector3(0, 0.4 if good else 2.0, 0), {"n": N(14), "ring": 0.5, "v0": 1.2, "v1": 2.2, "g": 0.0, "dir": Vector3.UP if good else Vector3.DOWN, "spread": 10,
		"sprite": "streak", "s0": 0.22, "s1": 0.05, "c0": C["core"], "c1": C["main"], "life": 0.7})


## Shield: a fresnel bubble that pops in and fades.
func shield(pos: Vector3) -> void:
	var mi := MeshInstance3D.new()
	var sm := SphereMesh.new()
	sm.radius = 1.0
	sm.height = 2.0
	mi.mesh = sm
	var sh: Shader = _tex.get("dome_shader")
	if sh == null:
		sh = Shader.new()
		sh.code = "shader_type spatial;\nrender_mode unshaded, blend_add, depth_draw_never, cull_back, shadows_disabled;\nuniform float fade = 1.0;\nvoid fragment() { float f = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 2.2); float hex = 0.5 + 0.5 * sin(UV.x * 120.0) * sin(UV.y * 60.0); ALBEDO = vec3(0.5, 1.2, 2.4) * (f + hex * 0.08); ALPHA = (f * 0.9 + 0.06) * fade; }"
		_tex["dome_shader"] = sh
	var m := ShaderMaterial.new()
	m.shader = sh
	mi.material_override = m
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	mi.position = pos + Vector3(0, 1.0, 0)
	mi.scale = Vector3(0.6, 0.7, 0.6)
	add_child(mi)
	var tw := create_tween()
	tw.tween_property(mi, "scale", Vector3(1.0, 1.2, 1.0), 0.2).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_BACK)
	tw.tween_interval(0.5)
	tw.tween_method(func(f: float) -> void: m.set_shader_parameter("fade", f), 1.0, 0.0, 0.4)
	tw.tween_callback(mi.queue_free)


## Damage over time ticking (poison bubbles).
func debuff_tick(pos: Vector3) -> void:
	burst(pos + Vector3(0, 0.6, 0), {"n": N(10), "box": Vector3(0.35, 0.4, 0.35), "v0": 0.4, "v1": 1.0, "g": 0.6, "dir": Vector3.UP, "spread": 20, "s0": 0.14, "s1": 0.04, "c0": Color(1.0, 2.6, 0.5), "c1": Color(0.3, 0.8, 0.1), "life": 0.8})


## Death: dark smoke and the element's motes drifting up.
func death_fade(pos: Vector3, el: String) -> void:
	var P := pal(el)
	burst(pos + Vector3(0, 0.6, 0), {"n": N(10), "box": Vector3(0.4, 0.5, 0.4), "v0": 0.2, "v1": 0.6, "g": 0.4, "sprite": "smoke", "add": false, "rot": true, "s0": 0.5, "s1": 1.0, "c0": Color(0.12, 0.1, 0.14), "a": 0.7, "life": 1.4, "delay": 1.2})
	burst(pos + Vector3(0, 0.6, 0), {"n": N(18), "box": Vector3(0.4, 0.6, 0.4), "v0": 0.4, "v1": 1.0, "g": 0.8, "dir": Vector3.UP, "spread": 30, "s0": 0.09, "s1": 0.02, "c0": P["core"], "c1": P["main"], "life": 1.6, "delay": 1.3, "explode": 0.5})
