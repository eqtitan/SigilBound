## A rigged CC0 hero (one of the three Quaternius bases, imported from godot/models), built the
## way buildRigged() in game3d/three3d.mjs does: parts borrowed across bases (BODY.parts, long
## robes drop the legs), hidden material roles, beard/scalp trimming for shaved and bald heroes,
## recolouring from art.js LOOKS with the character shading (HeroShading), the code-built kit
## and visible gear (HeroKit), per-hero proportions (height, head, chest bulk, limbs, shoulders),
## and the base's own clips: idle, attack, cast, hit, death, victory.
class_name HeroModel
extends Node3D

const FILES := {"hooded": "res://models/hooded_adventurer.glb", "adventurer": "res://models/adventurer.glb", "monarch": "res://models/monarch.glb"}
const CLIP_PREFIX := "CharacterArmature|"
const BRAWLERS := {"odo": 1, "kettle": 1, "gorrum": 1, "thrain": 1}
const SMALL_FOCUS := {"candle": 1, "mirror": 1, "bell": 1, "orb": 1, "lantern": 1}
const CASTERS := ["staff", "orb", "bell", "lantern", "candle", "mirror", "bow"]
const TARGET_HEAD := 1.85

static var _scenes := {}
static var _libs := {} # base -> an instanced scene kept to borrow parts from
static var _trimmed := {}

var hero_id := ""
var base := "adventurer"
var hollow := false
var player: AnimationPlayer
var skeleton: Skeleton3D
var armature: Node3D
var style := {}
var dead := false
var strike_n := 0
var height := 2.3 ## top of the head (for HUD tags), metres
var _idle := ""
var _fx: Array = [] # [node, fn(node, t)]
var _t := 0.0


static func scene_for(b: String) -> PackedScene:
	if not _scenes.has(b):
		_scenes[b] = load(FILES[b])
	return _scenes[b]


static func _lib(b: String) -> Node:
	if not _libs.has(b):
		_libs[b] = scene_for(b).instantiate()
	return _libs[b]


## gear: {weapon|helm|chest|boots: {r, s}} as art.js heroGearLook(), or null.
func build(id: String, is_hollow: bool, el: String = "", gear = null) -> void:
	hero_id = id
	hollow = is_hollow
	base = LooksData.BASE.get(id, "adventurer")
	var L: Dictionary = LooksData.LOOKS.get(id, LooksData.LOOKS["brannoc"])
	var body: Dictionary = LooksData.BODY.get(id, {})
	var kit: Dictionary = L.get("kit", {})
	var model: Node3D = scene_for(base).instantiate()
	add_child(model)
	skeleton = model.find_child("Skeleton3D", true, false)
	armature = skeleton.get_parent()
	player = model.find_child("AnimationPlayer", true, false)
	var rim := Color("#ff3355") if hollow else (UiKit.element_color(el) if el != "" else Color("#9fb4ff"))
	# A long robe replaces the legs (the feet still show under the hem).
	var parts: Dictionary = body.get("parts", {}).duplicate()
	if kit.get("robe", "") == "long" and not parts.has("legs"):
		parts["legs"] = "none"
	_swap_parts(parts)
	var colors := LookColors.role_colors(L, hollow)
	var hide: Array = body.get("hide", [])
	var show_crown: bool = L.get("gear", "") == "crown" and not hide.has("crown")
	for mi in skeleton.find_children("*", "MeshInstance3D", false, false):
		var m: MeshInstance3D = mi
		if m.skin == null:
			continue
		m.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		var src_base: String = m.get_meta("sb_base", base)
		var kind: String = m.get_meta("sb_kind", _part_kind(src_base, String(m.name)))
		# Shaved heroes lose the beard; bald ones keep only the beard (heads with hair+beard in one mesh).
		if kind == "head" and src_base != "hooded" and (body.get("shave") or body.get("bald")):
			_trim_hair(m, src_base, "beard" if body.get("bald") else "scalp")
		for s in m.mesh.get_surface_count():
			var src: Material = m.mesh.surface_get_material(s)
			var mat_name := src.resource_name if src else ""
			var role := LookColors.role_of(src_base, kind, mat_name)
			m.set_surface_override_material(s, _recolor(src, role, colors, hide, kind, show_crown, L, src_base, rim))
	# Normalise height: base models are ~1.8 m; heroes stand about 2 units tall like the web.
	var head_i := skeleton.find_bone("Head")
	var head_y: float = (armature.transform * skeleton.get_bone_global_rest(head_i)).origin.y
	var h: float = body.get("h", 1.0)
	model.scale = Vector3.ONE * (TARGET_HEAD / head_y * h)
	height = TARGET_HEAD * h + 0.45
	var hk := HeroKit.new(self)
	hk.build(id, L, body, base, hollow, gear, rim)
	_shape_body(body)
	_setup_anims(L)


static func _part_kind(b: String, node_name: String) -> String:
	var pm: Dictionary = LooksData.PARTMESH.get(b, {})
	for k in pm:
		if pm[k] == node_name:
			return k
	return ""


## swapParts(): borrow skinned parts from another base (same skeleton, bound by bone name).
func _swap_parts(parts: Dictionary) -> void:
	var own_parts: Dictionary = LooksData.PARTMESH.get(base, {})
	for kind in parts:
		var src: String = parts[kind]
		if src == base:
			continue
		var own: Node = skeleton.get_node_or_null(NodePath(own_parts.get(kind, "")))
		var from: MeshInstance3D = null
		if src != "none":
			var lib := _lib(src)
			from = lib.find_child(LooksData.PARTMESH[src][kind], true, false)
			if from == null:
				continue
		if own:
			own.free()
		if src == "none":
			continue
		var copy: MeshInstance3D = from.duplicate()
		copy.set_meta("sb_base", src)
		copy.set_meta("sb_kind", kind)
		skeleton.add_child(copy)
		copy.skeleton = NodePath("..")


## trimHair(): split a head's hair+beard surface by position in head-bone space.
func _trim_hair(m: MeshInstance3D, src_base: String, keep: String) -> void:
	var key := "%s|%s|%s" % [src_base, m.mesh.resource_path if m.mesh.resource_path != "" else str(m.mesh.get_instance_id()), keep]
	if _trimmed.has(key):
		m.mesh = _trimmed[key]
		return
	var head_bind := -1
	for b in m.skin.get_bind_count():
		if m.skin.get_bind_name(b) == "Head":
			head_bind = b
	if head_bind < 0:
		return
	var to_head: Transform3D = m.skin.get_bind_pose(head_bind)
	var mesh := ArrayMesh.new()
	for s in m.mesh.get_surface_count():
		var arrays: Array = m.mesh.surface_get_arrays(s)
		var mat: Material = m.mesh.surface_get_material(s)
		if LookColors.role_of(src_base, "head", mat.resource_name if mat else "") == "hair":
			var v: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var idx: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
			var out := PackedInt32Array()
			for i in range(0, idx.size(), 3):
				var c := Vector3.ZERO
				for k in 3:
					c += to_head * v[idx[i + k]] * 100.0
				c /= 3.0
				var beard := c.y < 0.075 and c.z > -0.03
				if beard == (keep == "beard"):
					out.append_array([idx[i], idx[i + 1], idx[i + 2]])
			arrays[Mesh.ARRAY_INDEX] = out
		var flags: int = m.mesh.surface_get_format(s) & Mesh.ARRAY_FLAG_USE_8_BONE_WEIGHTS
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays, [], {}, flags)
		mesh.surface_set_material(mesh.get_surface_count() - 1, mat)
	_trimmed[key] = mesh
	m.mesh = mesh


## shapeBody(): per-hero proportions by scaling bones once (the clips only key rotations, plus
## translations on Body and the feet). Child bones get the inverse scale so only that segment grows.
func _shape_body(Bd: Dictionary) -> void:
	var scale_bone := func(name: String, v: Vector3) -> void:
		var i := skeleton.find_bone(name)
		if i < 0:
			return
		skeleton.set_bone_pose_scale(i, v)
		for c in skeleton.get_bone_children(i):
			var R := Basis(skeleton.get_bone_pose_rotation(c))
			var inv := Vector3(1.0 / v.x, 1.0 / v.y, 1.0 / v.z)
			# diag(Rt * S^-1 * R): exact when the child's rotation permutes the parent's axes.
			var cs := Vector3.ZERO
			for row in 3:
				var acc := 0.0
				for k in 3:
					acc += R[row][k] * R[row][k] * inv[k]
				cs[row] = acc
			skeleton.set_bone_pose_scale(c, cs)
	if Bd.has("bulk"):
		scale_bone.call("Chest", Vector3(Bd["bulk"][0], Bd["bulk"][1], Bd["bulk"][2]))
	if Bd.has("arms"):
		for n in ["UpperArm.L", "UpperArm.R", "LowerArm.L", "LowerArm.R"]:
			scale_bone.call(n, Vector3(Bd["arms"], 1, Bd["arms"]))
	if Bd.has("legs"):
		for n in ["UpperLeg.L", "UpperLeg.R"]:
			scale_bone.call(n, Vector3(Bd["legs"], 1, Bd["legs"]))
	if Bd.has("sh"):
		for pair in [["Shoulder.L", 1.0], ["Shoulder.R", -1.0]]:
			var i := skeleton.find_bone(pair[0])
			if i >= 0:
				var p := skeleton.get_bone_pose_position(i)
				p.x += pair[1] * Bd["sh"] / armature.scale.x
				skeleton.set_bone_pose_position(i, p)
	if Bd.has("head"):
		var hi := skeleton.find_bone("Head")
		skeleton.set_bone_pose_scale(hi, skeleton.get_bone_pose_scale(hi) * Bd["head"])


func _recolor(src: Material, role: String, colors: Dictionary, hide: Array, kind: String, show_crown: bool, L: Dictionary, src_base: String, rim: Color) -> Material:
	if (role == "crown" and not show_crown) or role == "tongue" or hide.has(role) or hide.has(kind + ":" + role):
		return HiddenMaterial.get_material()
	var c := Color.WHITE
	if src is BaseMaterial3D:
		c = (src as BaseMaterial3D).albedo_color
	if role == "eye" and (hollow or L.get("glow")):
		return HeroShading.emissive(Color(colors["eye"]), 3.0, rim)
	if role != "" and colors.has(role):
		c = Color(colors[role])
	elif hollow:
		c = Color(LookColors.mix("#" + c.to_html(false), LookColors.HOLLOW_TINT, 0.5))
	return HeroShading.material(c, _shade_role(role, src_base, src.resource_name if src else ""), rim)


## roleFromGame() in shading.mjs: model colour role -> shading finish.
static func _shade_role(role: String, src_base: String, mat_name: String) -> String:
	match role:
		"skin": return "skin"
		"hair", "brow": return "hair"
		"eye": return "eye"
		"crown": return "gold"
		"trim", "blade": return "metal"
		"accent": return "gold" if mat_name.to_lower().contains("gold") else "leather"
		"armor": return "metal" if src_base == "monarch" else ("leather" if src_base == "hooded" else "cloth")
		"boot", "wood": return "leather"
		"cloth", "pants", "hood": return "cloth"
	return "default"


# ---------- live kit parts (pulse, flicker, spin, cloth on springs) ----------
func add_fx(node: Node3D, fn: Callable) -> void:
	_fx.append([node, fn])


func _process(delta: float) -> void:
	_t += delta
	for f in _fx:
		if is_instance_valid(f[0]):
			f[1].call(f[0], _t)


## springy(): wraps a live part's animation so it also swings against its anchor's motion, as a
## critically damped spring in the anchor's axes (moving forward pushes it back).
static func springy(fn: Callable, gain: float) -> Callable:
	return func(o: Node3D, t: float) -> void:
		var s: Dictionary = o.get_meta("sp", {})
		if s.is_empty():
			s = {"t": t, "ax": 0.0, "az": 0.0, "vx": 0.0, "vz": 0.0, "on": false, "addX": 0.0, "addZ": 0.0, "p": Vector3.ZERO}
		o.rotation.x -= s["addX"]
		o.rotation.z -= s["addZ"]
		s["addX"] = 0.0
		s["addZ"] = 0.0
		fn.call(o, t)
		var dt := clampf(t - s["t"], 0.001, 0.05)
		s["t"] = t
		var par := o.get_parent() as Node3D
		if par == null or not par.is_inside_tree():
			o.set_meta("sp", s)
			return
		var wp := par.global_position
		if not s["on"]:
			s["p"] = wp
			s["on"] = true
		var vel: Vector3 = (wp - s["p"]) / dt
		s["p"] = wp
		if vel.length() > 3.0:
			vel = vel.normalized() * 3.0
		vel = par.global_basis.orthonormalized().inverse() * vel
		var tx := clampf((vel.z * 0.08 - vel.y * 0.04) * gain, -0.12, 0.42)
		var tz := clampf(-vel.x * 0.08 * gain, -0.3, 0.3)
		s["vx"] += ((tx - s["ax"]) * 70.0 - s["vx"] * 16.7) * dt
		s["ax"] += s["vx"] * dt
		s["vz"] += ((tz - s["az"]) * 70.0 - s["vz"] * 16.7) * dt
		s["az"] += s["vz"] * dt
		s["ax"] = clampf(s["ax"], -0.15, 0.45)
		s["az"] = clampf(s["az"], -0.32, 0.32)
		o.rotation.x += s["ax"]
		o.rotation.z += s["az"]
		s["addX"] = s["ax"]
		s["addZ"] = s["az"]
		o.set_meta("sp", s)


# ---------- animation (styleOf / makeAnim in three3d.mjs) ----------
func _setup_anims(L: Dictionary) -> void:
	var p: String = L.get("prop", "")
	var strike := ["Sword_Slash", "Sword_Slash", "Kick_Right"]
	if BRAWLERS.has(hero_id):
		strike = ["Punch_Right", "Punch_Left", "Kick_Right"]
	elif p == "daggers":
		strike = ["Sword_Slash", "Punch_Left", "Kick_Left"]
	elif p == "spear" or p == "trident":
		strike = ["Sword_Slash", "Kick_Right"]
	style = {
		"idle": "Idle_Neutral" if SMALL_FOCUS.has(p) else ("Idle_Gun" if p == "bow" else "Idle_Sword"),
		"strike": strike, "cast": "Gun_Shoot" if p == "bow" else "Interact", "support": "Interact",
		"hit": "HitRecieve", "bigHit": "HitRecieve_2", "death": "Death", "win": "Wave", "run": "Run",
	}
	for n in [style["idle"], "Run", "Wave", "Run_Back"]:
		var a := player.get_animation(CLIP_PREFIX + n)
		if a:
			a.loop_mode = Animation.LOOP_LINEAR
	_idle = CLIP_PREFIX + style["idle"]
	player.animation_finished.connect(_on_finished)
	player.play(_idle, 0.0)
	player.seek(randf() * 2.0, true)


func is_caster() -> bool:
	return LooksData.LOOKS.get(hero_id, {}).get("prop", "") in CASTERS


func play(clip: String, loop := false, blend := 0.15, speed := 1.0) -> float:
	var name := CLIP_PREFIX + clip
	if not player.has_animation(name):
		return 0.0
	player.play(name, blend, speed)
	if not loop:
		player.seek(0.0, true)
	return player.get_animation(name).length / speed


func idle() -> void:
	if not dead:
		player.play(_idle, 0.22)


func strike() -> float:
	var list: Array = style["strike"]
	var n: String = list[strike_n % list.size()]
	strike_n += 1
	return play(n, false, 0.1)


func cast(support: bool) -> float:
	return play(style["support"] if support else style["cast"], false, 0.12)


func hit(big: bool) -> void:
	if not dead:
		play(style["bigHit"] if big else style["hit"], false, 0.08)


func die() -> void:
	dead = true
	play(style["death"], false, 0.12)


func win() -> void:
	if not dead:
		play(style["win"], true, 0.25)


func run(on: bool) -> void:
	if on:
		play("Run", true, 0.12, 1.4)
	else:
		idle()


func _on_finished(anim: StringName) -> void:
	if dead:
		return # Death clamps on its last frame.
	if String(anim) != _idle and player.get_animation(anim).loop_mode == Animation.LOOP_NONE:
		idle()
