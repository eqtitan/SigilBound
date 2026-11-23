## A rigged CC0 hero (one of the three Quaternius bases, imported from godot/models), recoloured
## per hero from art.js LOOKS (LooksData / LookColors), with the base's own clips: idle, attack,
## cast, hit and death. Follows buildRigged() / styleOf() in game3d/three3d.mjs, minus the
## code-built signature kit (simple props stand in for weapons and focuses).
class_name HeroModel
extends Node3D

const FILES := {"hooded": "res://models/hooded_adventurer.glb", "adventurer": "res://models/adventurer.glb", "monarch": "res://models/monarch.glb"}
const CLIP_PREFIX := "CharacterArmature|"
const BRAWLERS := {"odo": 1, "kettle": 1, "gorrum": 1, "thrain": 1}
const SMALL_FOCUS := {"candle": 1, "mirror": 1, "bell": 1, "orb": 1, "lantern": 1}
const CASTERS := ["staff", "orb", "bell", "lantern", "candle", "mirror", "bow"]
const TARGET_HEAD := 1.85

static var _scenes := {}
static var _head_y := {}

var hero_id := ""
var base := "adventurer"
var hollow := false
var player: AnimationPlayer
var skeleton: Skeleton3D
var style := {}
var dead := false
var strike_n := 0
var height := 2.3 ## top of the head (for HUD tags), metres
var _idle := ""


static func scene_for(b: String) -> PackedScene:
	if not _scenes.has(b):
		_scenes[b] = load(FILES[b])
	return _scenes[b]


func build(id: String, is_hollow: bool) -> void:
	hero_id = id
	hollow = is_hollow
	base = LooksData.BASE.get(id, "adventurer")
	var L: Dictionary = LooksData.LOOKS.get(id, LooksData.LOOKS["brannoc"])
	var body: Dictionary = LooksData.BODY.get(id, {})
	var kit: Dictionary = L.get("kit", {})
	var model: Node3D = scene_for(base).instantiate()
	add_child(model)
	skeleton = model.find_child("Skeleton3D", true, false)
	player = model.find_child("AnimationPlayer", true, false)
	var colors := LookColors.role_colors(L, hollow)
	var hide: Array = body.get("hide", [])
	var show_crown: bool = L.get("gear", "") == "crown" and not hide.has("crown")
	var part_of := {}
	var pm: Dictionary = LooksData.PARTMESH.get(base, {})
	for k in pm:
		part_of[pm[k]] = k
	for mi in model.find_children("*", "MeshInstance3D", true, false):
		var m: MeshInstance3D = mi
		m.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		if m.name == "Sword":
			m.visible = L.get("prop", "") in ["sword", "katana"] or (L.get("prop", "") == "shield" and kit.get("side", "") == "sword")
		if m.name == "Backpack":
			m.visible = not (kit.get("cape", false) or kit.get("pack", true) == false or kit.get("back", false))
		# A long robe stands in for the legs in the web game; here the legs take the robe colour.
		var kind: String = part_of.get(String(m.name), "")
		for s in m.mesh.get_surface_count():
			var src: Material = m.mesh.surface_get_material(s)
			var mat_name := src.resource_name if src else ""
			var role := LookColors.role_of(base, kind, mat_name)
			m.set_surface_override_material(s, _recolor(src, role, colors, hide, kind, show_crown, L, kit))
	# Normalise height: base models are ~1.8 m; heroes stand about 2 units tall like the web.
	var arm: Node3D = skeleton.get_parent()
	var head_i := skeleton.find_bone("Head")
	var head_y: float = (arm.transform * skeleton.get_bone_global_rest(head_i)).origin.y
	var h: float = body.get("h", 1.0)
	model.scale = Vector3.ONE * (TARGET_HEAD / head_y * h)
	height = TARGET_HEAD * h + 0.45
	_add_prop(L, kit, colors)
	_setup_anims(L)


func _recolor(src: Material, role: String, colors: Dictionary, hide: Array, kind: String, show_crown: bool, L: Dictionary, kit: Dictionary) -> Material:
	if (role == "crown" and not show_crown) or role == "tongue" or hide.has(role) or hide.has(kind + ":" + role):
		return HiddenMaterial.get_material()
	var m := StandardMaterial3D.new()
	if src is BaseMaterial3D:
		m.albedo_color = (src as BaseMaterial3D).albedo_color
	var shiny := (role == "armor" and base == "monarch") or role in ["trim", "blade", "crown"]
	m.roughness = 0.5 if shiny else 0.8
	m.metallic = 0.3 if shiny else 0.0
	if role == "eye" and (hollow or L.get("glow")):
		m.albedo_color = Color(colors["eye"])
		m.emission_enabled = true
		m.emission = Color(colors["eye"])
		m.emission_energy_multiplier = 3.0
		return m
	if role == "pants" and kit.get("robe", "") == "long" and kit.get("robeColor"):
		m.albedo_color = Color(LookColors.mix(kit["robeColor"], LookColors.HOLLOW_TINT, 0.5) if hollow else kit["robeColor"])
		return m
	if role != "" and colors.has(role):
		m.albedo_color = Color(colors[role])
	elif hollow:
		m.albedo_color = Color(LookColors.mix("#" + m.albedo_color.to_html(false), LookColors.HOLLOW_TINT, 0.5))
	return m


## A simple stand-in for the web game's code-built weapon or focus, held in the right hand.
func _add_prop(L: Dictionary, kit: Dictionary, colors: Dictionary) -> void:
	var prop: String = L.get("prop", "")
	var right: String = kit.get("side", "") if prop == "shield" else prop
	var metal := _mat(Color(colors["blade"]), 0.3, 0.5)
	var wood := _mat(Color(colors["wood"]), 0.0, 0.8)
	var accent := _mat(Color(colors["accent"]), 0.3, 0.5)
	var glow_c := Color(L.get("glow") if L.get("glow") else colors["accent"])
	if hollow:
		glow_c = Color("#ff3355")
	var r := _attach("Wrist.R")
	var l := _attach("Wrist.L")
	# Wrist bones point down the forearm; a held shaft runs along the hand's local X.
	match right:
		"staff", "spear", "trident":
			_part(r, _cyl(0.025, 1.7), wood, Vector3(0, 0.05, 0), Vector3(0, 0, 90))
			if right == "staff":
				_part(r, _sphere(0.09), _glow(glow_c), Vector3(0, 0.05, 0.85).rotated(Vector3.FORWARD, 0), Vector3.ZERO, Vector3(0.85, 0.05, 0))
			else:
				_part(r, _cone(0.05, 0.25), metal, Vector3(0.95, 0.05, 0), Vector3(0, 0, -90))
		"hammer", "club":
			_part(r, _cyl(0.03, 0.8), wood, Vector3(0, 0.05, 0), Vector3(0, 0, 90))
			_part(r, _box(Vector3(0.16, 0.24, 0.16)), metal if right == "hammer" else wood, Vector3(0.42, 0.05, 0))
		"bow":
			_part(l, _torus(0.42, 0.02), wood, Vector3(0, 0.05, 0), Vector3(90, 0, 0), Vector3.ZERO, Vector3(1, 0.25, 1))
		"orb", "bell", "lantern", "candle", "mirror":
			_part(r, _sphere(0.1), _glow(glow_c), Vector3(0.05, 0.12, 0))
		"whip", "hook", "daggers", "dagger":
			_part(r, _box(Vector3(0.38, 0.03, 0.06)), metal, Vector3(0.22, 0.05, 0))
		"katana", "sword":
			if base != "hooded":
				_part(r, _box(Vector3(0.85, 0.03, 0.07)), metal, Vector3(0.45, 0.05, 0))
				_part(r, _box(Vector3(0.05, 0.05, 0.2)), accent, Vector3(0.04, 0.05, 0))
	if prop == "shield" and kit.get("shield"):
		_part(l, _cyl(0.32, 0.05), accent, Vector3(0, -0.08, 0), Vector3(0, 0, 0))


func _attach(bone: String) -> Node3D:
	var a := BoneAttachment3D.new()
	a.bone_name = bone
	skeleton.add_child(a)
	# The skeleton sits under a node scaled by 100 (glTF centimetres): undo it for props.
	var holder := Node3D.new()
	holder.scale = Vector3.ONE * 0.01
	a.add_child(holder)
	return holder


func _part(parent: Node3D, mesh: Mesh, mat: Material, pos: Vector3, rot_deg: Vector3 = Vector3.ZERO, pos2: Vector3 = Vector3.INF, scl: Vector3 = Vector3.ONE) -> void:
	var mi := MeshInstance3D.new()
	mi.mesh = mesh
	mi.material_override = mat
	mi.position = pos2 if pos2 != Vector3.INF else pos
	mi.rotation_degrees = rot_deg
	mi.scale = scl
	parent.add_child(mi)


func _mat(c: Color, metal: float, rough: float) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = c
	m.metallic = metal
	m.roughness = rough
	return m


func _glow(c: Color) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.albedo_color = c
	m.emission_enabled = true
	m.emission = c
	m.emission_energy_multiplier = 2.0
	return m


func _cyl(r: float, h: float) -> CylinderMesh:
	var c := CylinderMesh.new()
	c.top_radius = r
	c.bottom_radius = r
	c.height = h
	c.radial_segments = 10
	return c


func _cone(r: float, h: float) -> CylinderMesh:
	var c := _cyl(r, h)
	c.top_radius = 0.0
	return c


func _sphere(r: float) -> SphereMesh:
	var s := SphereMesh.new()
	s.radius = r
	s.height = r * 2
	s.radial_segments = 16
	s.rings = 8
	return s


func _box(sz: Vector3) -> BoxMesh:
	var b := BoxMesh.new()
	b.size = sz
	return b


func _torus(r: float, t: float) -> TorusMesh:
	var m := TorusMesh.new()
	m.inner_radius = r - t
	m.outer_radius = r + t
	return m


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
