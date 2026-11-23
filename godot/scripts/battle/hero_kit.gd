## The code-built signature kit, ported recipe for recipe from buildKit() in game3d/three3d.mjs:
## weapons and foci on the right fist (shields and second daggers on the left), headgear, hair
## pieces and faces, robes/coats/tabards/aprons/belts at the hips, pauldrons, mantles, collars
## and scarves, capes in two hinged panels that sway on a damped spring, quivers and wings on
## the back, plus the visible gear pieces (weapon trim, helm bands, pauldrons, breastplates and
## greaves that change with rarity and set). Static parts of each socket are merged into one mesh
## per finish with per-vertex colours (the web's mergeKit); animated parts stay separate.
class_name HeroKit
extends RefCounted

const D2R := PI / 180.0
const GEAR_METAL := ["", "#6b5d50", "#9aa3ad", "#c6ced8", "#dfe3ea", "#f0e6d0"]
const GEAR_TRIM := ["", "#8a7a68", "#5bc46a", "#4aa3ff", "#b46cff", "#ffb02e"]
const SET_GLOW := {"fury": "#ff4a3a", "vigor": "#4ae07a", "bulwark": "#b8b4aa", "haste": "#3ad8cc", "keen": "#ffd040"}
const HELM_GEAR := {"helm": 1, "horns": 1}
const ADORN_GEAR := {"crown": 1, "halo": 1, "circlet": 1, "veil": 1, "mask": 1}
const TILT := {"spear": 25, "staff": 32, "lantern": 32, "candle": 42, "bell": 35, "orb": 40, "mirror": 30, "bow": 15, "trident": 30, "club": 12, "hammer": 15, "axe": 15, "hook": 25, "whip": 10}
const ROLL := {"bow": 90}
const RSCALE := [1, 1, 1.04, 1.08, 1.13, 1.18]
const GRIP_POS := Vector3(0, 0.075, 0.01)

static var _baked := {} # cache key -> Array of [ArrayMesh, Material]

var m: HeroModel
var id := ""
var L: Dictionary
var K: Dictionary
var B: Dictionary
var base := ""
var hollow := false
var gear = null
var rim := Color("#9fb4ff")
var C := {}
var _live: Array = []  # [node, fn(node, t)] for the socket being built


func _init(model: HeroModel) -> void:
	m = model


# ---------- materials (finish specs merged per socket) ----------
func tint(c: String) -> String:
	return LookColors.mix(c, LookColors.HOLLOW_TINT, 0.5) if hollow else c


func M(c: String, metal: float = 0.0) -> Dictionary:
	if metal >= 0.5:
		return {"r": "metal", "c": tint(c)}
	return {"r": "leatherM" if metal > 0 else "leather", "c": tint(c)}


func Mc(c: String) -> Dictionary: return {"r": "cloth", "c": tint(c)}
func Mw(c: String) -> Dictionary: return {"r": "wood", "c": tint(c)}
func Mg(c: String) -> Dictionary: return {"r": "gold", "c": tint(c)}
func Mh(c: String) -> Dictionary: return {"r": "hair", "c": tint(c)}


func Gl(c: String, op: float = 1.0) -> Dictionary:
	return {"r": "glow", "c": LookColors.mix(c, "#ff3355", 0.45) if hollow else c, "o": op}


func _mat(spec: Dictionary, vcolor: bool) -> Material:
	var col := Color.WHITE if vcolor else Color(spec["c"])
	if spec["r"] == "glow":
		var op: float = spec.get("o", 1.0)
		return HeroShading.glow(col, 0.3 if op < 0.45 else (0.6 if op < 1.0 else 1.0), vcolor)
	return HeroShading.material(col, spec["r"], rim, vcolor)


func _finish_key(spec: Dictionary) -> String:
	if spec["r"] == "glow":
		var op: float = spec.get("o", 1.0)
		return "glow|%s" % (0.3 if op < 0.45 else (0.6 if op < 1.0 else 1.0))
	return spec["r"]


# ---------- part helpers ----------
## P(): a primitive part, three.js-style: position, scale and rotation (degrees, YXZ order).
func P(g: Node3D, kind: Variant, pos: Array, scl: Array, mat: Dictionary, rot: Array = []) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	mi.mesh = kind if kind is Mesh else KitGeo.get_mesh(kind)
	mi.position = Vector3(pos[0], pos[1], pos[2])
	mi.scale = Vector3(scl[0], scl[1], scl[2])
	if not rot.is_empty():
		mi.rotation_degrees = Vector3(rot[0], rot[1], rot[2])
	mi.set_meta("spec", mat)
	g.add_child(mi)
	return mi


func pivot(parent: Node3D, pos: Array) -> Node3D:
	var n := Node3D.new()
	n.position = Vector3(pos[0], pos[1], pos[2])
	parent.add_child(n)
	return n


## A part that animates (pulse, flicker, spin): kept as its own mesh.
func live_mesh(g: Node3D, kind: String, pos: Array, s: Variant, mat: Dictionary, fn: Callable, rot: Array = []) -> MeshInstance3D:
	var sc: Array = s if s is Array else [s, s, s]
	var mi := P(g, kind, pos, sc, mat, rot)
	mi.set_meta("base", Vector3(sc[0], sc[1], sc[2]))
	mi.set_meta("live", true)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	_live.append([mi, fn])
	return mi


## A group of parts moving together; with `spring` it also lags behind its anchor's motion.
func live_group(g: Node3D, pos: Array, fn: Callable, spring: float = 0.0) -> Node3D:
	var p := pivot(g, pos)
	p.set_meta("live", true)
	_live.append([p, HeroModel.springy(fn, spring) if spring > 0 else fn])
	return p


static func pulse(o: Node3D, t: float) -> void:
	var b: Vector3 = o.get_meta("base")
	var s := 1.0 + sin(t * 3.0 + o.get_instance_id() % 97) * 0.12
	o.scale = b * s


static func flicker(o: Node3D, t: float) -> void:
	var b: Vector3 = o.get_meta("base")
	var s := 1.0 + sin(t * 17.0 + o.get_instance_id() % 97) * 0.12 + sin(t * 29.0) * 0.06
	o.scale = Vector3(b.x * s, b.y * (2.0 - s), b.z * s)


static func spin(axis: String, rate: float) -> Callable:
	return func(o: Node3D, t: float) -> void:
		if axis == "x":
			o.rotation.x = t * rate
		else:
			o.rotation.y = t * rate


## Builds one socket's parts, merges the static ones per finish (cached per hero, gear and slot)
## and hangs both merged and live parts on `sock`.
func attach(slot: String, sock: Node3D, build: Callable, sig: String = "") -> void:
	var key := "%s|%s|%s|%s|%s" % [id, 1 if hollow else 0, slot, sig, rim.to_html()]
	var g := Node3D.new()
	_live = []
	build.call(g)
	var live := _live.duplicate()
	_live = []
	var groups: Array
	if _baked.has(key):
		groups = _baked[key]
	else:
		groups = _bake(g)
		_baked[key] = groups
	for gr in groups:
		var mi := MeshInstance3D.new()
		mi.mesh = gr[0]
		mi.material_override = gr[1]
		mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF if gr[2] else GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		sock.add_child(mi)
	# live parts: detach them (with their transforms) from the temp tree and keep them
	for l in live:
		var o: Node3D = l[0]
		var xf := _xf_to(o, g)
		o.get_parent().remove_child(o)
		o.transform = xf
		sock.add_child(o)
		for c in [o] + o.find_children("*", "MeshInstance3D", true, false):
			if c is MeshInstance3D and c.has_meta("spec"):
				c.material_override = _mat(c.get_meta("spec"), false)
		m.add_fx(o, l[1])
	g.free()


static func _xf_to(o: Node3D, root: Node3D) -> Transform3D:
	var xf := Transform3D.IDENTITY
	var n: Node = o.get_parent()
	while n and n != root:
		xf = (n as Node3D).transform * xf
		n = n.get_parent()
	return xf * o.transform


func _bake(root: Node3D) -> Array:
	var buckets := {}
	var specs := {}
	_collect(root, root, Transform3D.IDENTITY, buckets, specs)
	var out := []
	for k in buckets:
		var arr: Array = buckets[k]
		var mesh := ArrayMesh.new()
		var a := []
		a.resize(Mesh.ARRAY_MAX)
		a[Mesh.ARRAY_VERTEX] = arr[0]
		a[Mesh.ARRAY_NORMAL] = arr[1]
		a[Mesh.ARRAY_COLOR] = arr[2]
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, a)
		var spec: Dictionary = specs[k]
		out.append([mesh, _mat(spec, true), spec["r"] == "glow"])
	return out


func _collect(n: Node, root: Node3D, xf: Transform3D, buckets: Dictionary, specs: Dictionary) -> void:
	for c in n.get_children():
		if not (c is Node3D) or c.has_meta("live"):
			continue
		var cx: Transform3D = xf * (c as Node3D).transform
		if c is MeshInstance3D and c.has_meta("spec"):
			var spec: Dictionary = c.get_meta("spec")
			var k := _finish_key(spec)
			if not buckets.has(k):
				buckets[k] = [PackedVector3Array(), PackedVector3Array(), PackedColorArray()]
				specs[k] = spec
			var arrs := (c as MeshInstance3D).mesh.surface_get_arrays(0)
			var vs: PackedVector3Array = arrs[Mesh.ARRAY_VERTEX]
			var ns: PackedVector3Array = arrs[Mesh.ARRAY_NORMAL]
			var idx = arrs[Mesh.ARRAY_INDEX]
			var nb := cx.basis.inverse().transposed()
			var col := Color(spec["c"]).srgb_to_linear()
			var flip := cx.basis.determinant() < 0
			var order: PackedInt32Array = idx if idx is PackedInt32Array and idx.size() > 0 else PackedInt32Array(range(vs.size()))
			var b: Array = buckets[k]
			for i in range(0, order.size() - 2, 3):
				var tri := [order[i], order[i + 2], order[i + 1]] if flip else [order[i], order[i + 1], order[i + 2]]
				for j in tri:
					b[0].append(cx * vs[j])
					b[1].append((nb * ns[j]).normalized())
					b[2].append(col)
		_collect(c, root, cx, buckets, specs)


# ---------- sockets ----------
static func bone_name(three_name: String) -> String:
	# three.js drops the dot: "WristR" -> "Wrist.R"
	if three_name.length() > 1 and (three_name.ends_with("L") or three_name.ends_with("R")) and three_name not in ["Head"]:
		var stem := three_name.substr(0, three_name.length() - 1)
		if stem in ["Wrist", "UpperArm", "LowerArm", "Shoulder", "UpperLeg", "LowerLeg", "Foot"]:
			return stem + "." + three_name.right(1)
	return three_name


func has_bone(n: String) -> bool:
	return m.skeleton.find_bone(bone_name(n)) >= 0


func _attachment(bone: String) -> Node3D:
	var a := BoneAttachment3D.new()
	a.bone_name = bone_name(bone)
	m.skeleton.add_child(a)
	# The armature node is scaled 100x (glTF centimetres): sockets work in the model's metres.
	var outer := Node3D.new()
	outer.scale = Vector3.ONE * (1.0 / m.armature.scale.x)
	a.add_child(outer)
	return outer


## socket(): a group under `bone` in the bone's axes; `dir` (bone space) is where +Y points.
func socket(bone: String, pos: Vector3, dir: Variant = null, roll: float = 0.0) -> Node3D:
	var outer := _attachment(bone)
	var inner := Node3D.new()
	inner.position = pos
	if dir != null:
		var d: Vector3 = (dir as Vector3).normalized()
		var q := Quaternion(Vector3.UP, d)
		if roll != 0.0:
			q = q * Quaternion(Vector3.UP, roll * D2R)
		inner.quaternion = q
	outer.add_child(inner)
	return inner


## socketM(): a group under `bone` aligned with the model's own axes in the rest pose.
func socket_m(bone: String, pos: Array = [0, 0, 0]) -> Node3D:
	var outer := _attachment(bone)
	var i := m.skeleton.find_bone(bone_name(bone))
	var rest: Transform3D = m.skeleton.get_bone_global_rest(i)
	var q := (m.armature.transform.basis.orthonormalized() * rest.basis.orthonormalized()).get_rotation_quaternion()
	var inner := Node3D.new()
	inner.quaternion = q.inverse()
	inner.position = inner.quaternion * Vector3(pos[0], pos[1], pos[2])
	outer.add_child(inner)
	return inner


func bone_y(bone: String) -> float:
	var i := m.skeleton.find_bone(bone_name(bone))
	return (m.armature.transform * m.skeleton.get_bone_global_rest(i)).origin.y


static func grip_dir(side: String, tilt: float) -> Vector3:
	return Vector3(-cos(tilt * D2R) if side == "R" else cos(tilt * D2R), -sin(tilt * D2R), 0)


static func gear_sig(gr) -> String:
	return "%d%s" % [gr["r"], String(gr["s"]).substr(0, 1) if gr.get("s", "") != "" else "-"] if gr else "0"


func gmetal(r: int, k: float = 0.7) -> String:
	return "#5a4232" if r == 1 else LookColors.mix(GEAR_METAL[r], L["armor"], k)


func gtrim(r: int) -> Dictionary:
	if hollow:
		return M(LookColors.mix(GEAR_TRIM[r], "#3a3340", 0.75), 0.5)
	return Mg(GEAR_TRIM[r]) if r >= 4 else M(GEAR_TRIM[r], 0.4)


# ---------- build ----------
func build(hero_id: String, look: Dictionary, body: Dictionary, base_name: String, is_hollow: bool, gear_look, rim_color: Color) -> void:
	id = hero_id
	L = look
	K = look.get("kit", {})
	B = body
	base = base_name
	hollow = is_hollow
	gear = gear_look
	rim = rim_color
	var gw = gear.get("weapon") if gear else null
	C = {
		"steel": M(GEAR_METAL[gw["r"]] if gw else "#c4ccd6", 0.3 if gw and gw["r"] == 1 else 0.6), "iron": M("#4a4a52", 0.5), "wood": Mw("#6b4a29"), "leather": M("#3a2a20"),
		"acc": M(L["accent"], 0.35), "accG": Gl(L["accent"]), "accH": Gl(L["accent"], 0.28), "armor": M(L["armor"]),
		"armorD": M(LookColors.mul(L["armor"], 0.6)), "gold": Mg(LookColors.mix(L["accent"], "#e8c070", 0.5)),
		"glowC": Gl(L.get("glow") if L.get("glow") else L["accent"]), "glowH": Gl(L.get("glow") if L.get("glow") else L["accent"], 0.3),
		"cloth": Mc(L["cloth"] if L.get("cloth") else LookColors.mul(L["armor"], 0.62)), "hair": Mh(L["hair"]),
	}
	_weapons(gw)
	_head()
	_hips()
	_shoulders()
	_back()
	_boots()


# ---- weapons, built along +Y from the fist at the origin ----
func _weapon(kind: String, g: Node3D) -> void:
	var dy := 0.0
	match kind:
		"spear":
			P(g, "rod", [0, 0.3, 0], [0.042, 1.75, 0.042], C.wood)
			P(g, "rod", [0, 1.15, 0], [0.06, 0.07, 0.06], C.acc)
			P(g, "cone", [0, 1.32, 0], [0.09, 0.28, 0.03], C.steel)
			P(g, "cone", [0, 1.08, 0], [0.09, 0.12, 0.09], C.acc, [180, 0, 0])
			if K.get("pennant"):
				var f := Mc(K["pennant"])
				P(g, "box", [0.0, 1.0, 0.11], [0.012, 0.13, 0.2], f)
				P(g, "cone", [0.0, 1.0, 0.27], [0.12, 0.14, 0.012], f, [90, 90, 0])
		"staff":
			var style: String = K.get("staff", "orb")
			var f: float = K.get("staffLen", 1.0)
			dy = 1.5 * (f - 1)
			var shaft: Dictionary = M("#cfe8f4", 0.2) if style == "ice" else (C.gold if style == "sun" else (M("#8a7458") if style == "drift" else C.wood))
			P(g, "rod", [0, 0.3 + dy / 2, 0], [0.05, 1.5 * f, 0.05], shaft)
			if style == "ice":
				P(g, "octa", [0, 1.17 + dy, 0], [0.13, 0.32, 0.13], C.accG)
				P(g, "octa", [0.07, 1.06 + dy, 0], [0.06, 0.16, 0.06], C.accG, [0, 0, -30])
				P(g, "octa", [-0.07, 1.06 + dy, 0], [0.06, 0.16, 0.06], C.accG, [0, 0, 30])
				live_mesh(g, "sphere", [0, 1.17 + dy, 0], 0.34, C.accH, pulse)
			elif style == "sun":
				P(g, "sphere", [0, 1.16 + dy, 0], [0.16, 0.16, 0.16], C.glowC)
				for i in 8:
					var a := i * 45 * D2R
					P(g, "cone", [cos(a) * 0.15, 1.16 + dy + sin(a) * 0.15, 0], [0.04, 0.1, 0.04], C.gold, [0, 0, i * 45 - 90])
				live_mesh(g, "sphere", [0, 1.16 + dy, 0], 0.42, C.glowH, pulse)
			else:
				if style == "drift":
					for s in [-1, 1]:
						P(g, "rod", [s * 0.06, 1.04 + dy, 0], [0.035, 0.22, 0.035], shaft, [0, 0, s * -25])
					P(g, "sphere", [0, 0.86 + dy, 0], [0.08, 0.06, 0.08], shaft)
					P(g, "cone", [0.06, 0.8 + dy, 0.03], [0.06, 0.08, 0.02], M("#f0e0c8"), [180, 0, 0])
				else:
					P(g, "ring", [0, 1.12 + dy, 0], [0.2, 0.2, 0.2], C.acc)
					P(g, "cone", [0, 1.01 + dy, 0], [0.07, 0.1, 0.07], C.acc, [180, 0, 0])
				P(g, "sphere", [0, 1.12 + dy, 0], [0.13, 0.13, 0.13], C.accG)
				live_mesh(g, "sphere", [0, 1.12 + dy, 0], 0.3, C.accH, pulse)
		"hammer":
			var big := 1.25 if K.get("big") else 1.0
			P(g, "rod", [0, 0.2 if K.get("big") else 0.25, 0], [0.045 * big, 0.85 if K.get("big") else 0.95, 0.045 * big], C.wood)
			if K.get("big"):
				P(g, "box", [0, 0.62, 0], [0.32, 0.2, 0.2], M("#2a2426", 0.5))
				P(g, "box", [0, 0.62, 0], [0.34, 0.045, 0.21], C.accG)
				P(g, "box", [0, 0.62, 0], [0.045, 0.22, 0.22], C.accG)
				P(g, "cone", [0.19, 0.62, 0], [0.11, 0.11, 0.11], M("#2a2426", 0.5), [0, 0, -90])
				P(g, "rod", [0, -0.2, 0], [0.07, 0.06, 0.07], C.iron)
			else:
				P(g, "box", [0, 0.68, 0], [0.3, 0.16, 0.16], C.iron)
				P(g, "box", [0, 0.68, 0], [0.32, 0.04, 0.17], C.acc)
				P(g, "box", [0.16, 0.68, 0], [0.02, 0.15, 0.15], C.accG)
				P(g, "box", [-0.16, 0.68, 0], [0.02, 0.15, 0.15], C.accG)
				live_mesh(g, "sphere", [0.16, 0.68, 0], 0.22, C.accH, pulse)
		"club":
			var moss := M(L["accent"])
			P(g, "taper", [0, 0.38, 0], [0.16, 0.8, 0.16], C.wood)
			P(g, "sphere", [0, 0.78, 0], [0.16, 0.14, 0.16], C.wood)
			P(g, "sphere", [0.07, 0.62, 0.04], [0.08, 0.08, 0.08], C.wood)
			P(g, "sphere", [-0.06, 0.74, -0.05], [0.07, 0.07, 0.07], C.wood)
			P(g, "sphere", [0.02, 0.84, 0.05], [0.12, 0.07, 0.1], moss)
			P(g, "sphere", [-0.08, 0.66, 0.02], [0.07, 0.1, 0.07], moss)
			P(g, "rod", [0, 0.08, 0], [0.11, 0.12, 0.11], C.leather)
		"trident":
			P(g, "rod", [0, 0.28, 0], [0.042, 1.6, 0.042], C.armorD)
			P(g, "box", [0, 1.08, 0], [0.3, 0.04, 0.04], C.gold)
			P(g, "cone", [0, 1.27, 0], [0.06, 0.34, 0.06], C.acc)
			for x in [-0.13, 0.13]:
				P(g, "rod", [x, 1.16, 0], [0.03, 0.16, 0.03], C.acc)
				P(g, "cone", [x, 1.3, 0], [0.05, 0.14, 0.05], C.acc)
			P(g, "sphere", [0, 1.08, 0], [0.07, 0.07, 0.07], C.accG)
		"katana":
			P(g, "rod", [0, 0.03, 0], [0.04, 0.26, 0.04], M("#1c1c22"))
			P(g, "disc", [0, 0.17, 0], [0.1, 0.02, 0.1], C.gold)
			P(g, "box", [0, 0.48, 0], [0.012, 0.6, 0.045], C.steel)
			P(g, "box", [0, 0.92, -0.025], [0.012, 0.3, 0.04], C.steel, [-9, 0, 0])
			P(g, "box", [0, 0.48, 0.03], [0.014, 0.6, 0.016], C.glowC)
			P(g, "box", [0, 0.91, 0.006], [0.014, 0.3, 0.016], C.glowC, [-9, 0, 0])
		"sword":
			var s := 0.8 if K.get("side") == "sword" and L.get("prop") == "shield" else 1.0
			P(g, "rod", [0, 0.02, 0], [0.04, 0.22, 0.04], C.leather)
			P(g, "sphere", [0, -0.1, 0], [0.06, 0.06, 0.06], C.gold)
			P(g, "box", [0, 0.14, 0], [0.26, 0.04, 0.05], C.gold)
			P(g, "box", [0, 0.16 + 0.45 * s, 0], [0.075, 0.9 * s, 0.022], C.steel)
			P(g, "cone", [0, 0.16 + 0.9 * s + 0.05, 0], [0.075, 0.1, 0.022], C.steel)
			if L.get("glow") or K.get("flame"):
				var fl: Dictionary = Gl(L["accent"], 0.55) if K.get("flame") else C.glowH
				live_mesh(g, "box", [0, 0.16 + 0.47 * s, 0], [0.13, 0.98 * s, 0.05], fl, flicker if K.get("flame") else pulse)
		"axe":
			P(g, "rod", [0, 0.25, 0], [0.045, 0.85, 0.045], C.wood)
			P(g, "half", [0, 0.6, 0], [0.34, 0.025, 0.3], C.steel, [90, 90, 0])
			P(g, "box", [0, 0.6, 0], [0.08, 0.12, 0.06], C.acc)
			P(g, "cone", [0, 0.72, 0], [0.05, 0.1, 0.05], C.acc)
		"dagger":
			P(g, "rod", [0, 0.01, 0], [0.034, 0.13, 0.034], C.leather)
			P(g, "box", [0, 0.09, 0], [0.12, 0.025, 0.03], C.acc)
			P(g, "cone", [0, 0.28, 0], [0.065, 0.36, 0.015], C.steel)
			P(g, "cone", [0, 0.28, 0], [0.09, 0.4, 0.02], C.glowH)
		"whip":
			P(g, "rod", [0, 0.06, 0], [0.045, 0.3, 0.045], C.leather)
			P(g, "sphere", [0, -0.1, 0], [0.055, 0.055, 0.055], C.acc)
			P(g, "rod", [0, 0.2, 0], [0.06, 0.04, 0.06], C.acc)
		"hook":
			P(g, "rod", [0, 0.2, 0], [0.04, 0.7, 0.04], M("#5a4034", 0.4))
			P(g, "hook", [0.09, 0.56, 0], [0.18, 0.18, 0.18], M("#7a5a48", 0.5), [0, 0, -20])
			P(g, "cone", [0.2, 0.52, 0], [0.04, 0.1, 0.04], M("#7a5a48", 0.5), [0, 0, 150])
			P(g, "rod", [0, -0.12, 0], [0.06, 0.05, 0.06], C.acc)
			P(g, "rod", [0, -0.2, 0], [0.012, 0.14, 0.012], C.iron)
		"bow":
			P(g, "rod", [0, 0, 0], [0.04, 0.16, 0.04], C.leather)
			for sgn in [1, -1]:
				P(g, "rod", [0, sgn * 0.22, -0.02], [0.03, 0.32, 0.03], C.wood, [sgn * -8, 0, 0])
				P(g, "rod", [0, sgn * 0.5, -0.09], [0.024, 0.28, 0.024], C.wood, [sgn * -28, 0, 0])
				P(g, "sphere", [0, sgn * 0.63, -0.16], [0.035, 0.035, 0.035], C.acc)
			P(g, "rod", [0, 0, -0.16], [0.006, 1.26, 0.006], C.accG)
		"lantern":
			P(g, "rod", [0, 0.3, 0], [0.04, 1.5, 0.04], C.wood)
			P(g, "crook", [0.1, 1.05, 0], [0.2, 0.2, 0.2], C.gold)
			P(g, "rod", [0.2, 0.97, 0], [0.008, 0.12, 0.008], C.gold)
			P(g, "box", [0.2, 0.84, 0], [0.13, 0.17, 0.13], M(LookColors.mix(L["accent"], "#6a4a20", 0.6), 0.5))
			P(g, "cone", [0.2, 0.95, 0], [0.16, 0.06, 0.16], C.gold)
			P(g, "sphere", [0.2, 0.84, 0], [0.15, 0.13, 0.15], C.accG)
			live_mesh(g, "sphere", [0.2, 0.84, 0], 0.4, C.accH, pulse)
		"candle":
			P(g, "disc", [0, 0.06, 0], [0.22, 0.03, 0.22], C.gold)
			P(g, "rod", [0, -0.03, 0], [0.045, 0.16, 0.045], C.gold)
			P(g, "rod", [0, 0.2, 0], [0.11, 0.28, 0.11], M("#f2e6c8"))
			P(g, "ring", [0.13, 0.03, 0], [0.08, 0.08, 0.2], C.gold, [90, 0, 0])
			live_mesh(g, "cone", [0, 0.42, 0], [0.11, 0.22, 0.11], C.accG, flicker)
			live_mesh(g, "sphere", [0, 0.4, 0], 0.42, C.accH, pulse)
		"mirror":
			P(g, "rod", [0, 0.05, 0], [0.035, 0.2, 0.035], C.gold)
			P(g, "ring", [0, 0.3, 0], [0.24, 0.24, 0.3], C.gold)
			P(g, "disc", [0, 0.3, 0], [0.22, 0.012, 0.22], C.accG, [90, 0, 0])
			live_mesh(g, "sphere", [0, 0.3, 0], 0.4, C.accH, pulse)
		"bell":
			P(g, "rod", [0, 0.05, 0], [0.04, 0.18, 0.04], C.wood)
			P(g, "sphere", [0, -0.08, 0], [0.12, 0.1, 0.12], C.gold)
			P(g, "bell", [0, -0.22, 0], [0.32, 0.28, 0.32], C.gold, [180, 0, 0])
			P(g, "ring", [0, -0.36, 0], [0.32, 0.32, 0.4], C.gold, [90, 0, 0])
			P(g, "sphere", [0, -0.39, 0], [0.07, 0.07, 0.07], C.glowC)
			live_mesh(g, "sphere", [0, -0.24, 0], 0.55, C.glowH, pulse)
		"orb":
			P(g, "sphere", [0, 0.22, 0], [0.18, 0.18, 0.18], M("#140a1e", 0.3))
			live_mesh(g, "sphere", [0, 0.22, 0], 0.3, C.accH, pulse)
			live_mesh(g, "ring", [0, 0.22, 0], 0.3, C.accG, spin("y", 2.2), [70, 0, 0])
			live_mesh(g, "ring", [0, 0.22, 0], 0.24, C.glowC, spin("x", -1.6), [0, 0, 60])


## Gear trim on a held weapon: rarity-coloured grip wraps, a set gem (Superior+), glow (Mythic).
func _weapon_gear(g: Node3D, w: Dictionary) -> void:
	var r: int = w["r"]
	var trim := gtrim(r)
	var gem := Gl(SET_GLOW.get(w.get("s", ""), L["accent"]))
	P(g, "ring", [0, 0.06, 0], [0.075, 0.075, 0.3], trim, [90, 0, 0])
	if r >= 2:
		P(g, "ring", [0, -0.02, 0], [0.07, 0.07, 0.3], trim, [90, 0, 0])
	if r >= 3:
		P(g, "gem", [0, -0.14, 0], [0.065, 0.085, 0.065], gem)
	if r >= 4:
		for s in [-1, 1]:
			P(g, "cone", [s * 0.06, 0.12, 0], [0.03, 0.09, 0.03], trim, [0, 0, s * -60])
	if r >= 5:
		P(g, "octa", [0, 0.13, 0], [0.07, 0.09, 0.07], gem)
		live_mesh(g, "sphere", [0, 0.13, 0], 0.13, Gl(SET_GLOW.get(w.get("s", ""), L["accent"]), 0.3), pulse)


func _weapons(gw) -> void:
	var prop: String = L.get("prop", "")
	var right: String = K.get("side", "") if prop == "shield" else ("dagger" if prop == "daggers" else prop)
	for mi in m.find_children("*", "MeshInstance3D", true, false):
		if mi.name == "Sword":
			mi.visible = false # the code-built sword replaces the model's own
		if mi.name == "Backpack" and (K.get("cape") or K.get("pack", true) == false or K.get("back")):
			mi.visible = false
	if right != "" and has_bone("WristR"):
		var tilt: float = 58 if right == "hammer" and K.get("big") else TILT.get(right, 0)
		var sock := socket("WristR", GRIP_POS, grip_dir("R", tilt), ROLL.get(right, 0))
		if gw:
			sock.scale *= RSCALE[gw["r"]]
		attach("R", sock, func(g: Node3D) -> void:
			_weapon(right, g)
			if gw:
				_weapon_gear(g, gw), gear_sig(gw))
	if prop == "whip" and has_bone("WristR"):
		attach("Wl", socket_m("WristR"), func(g: Node3D) -> void:
			var lash := live_group(g, [0, 0, 0], func(o: Node3D, t: float) -> void:
				o.rotation.y = sin(t * 1.7) * 0.35
				o.rotation.x = sin(t * 2.3) * 0.06, 0.6)
			var pts := []
			for i in 13:
				var k := i / 12.0
				var y := -pow(k / 0.7, 1.4) * 0.92 if k < 0.7 else -0.92
				var fwd := 0.12 + k * 0.75
				var side := sin(k * 9) * 0.1 * k
				pts.append(Vector3(-0.08 - side, y, fwd))
			for i in 12:
				var a: Vector3 = pts[i]
				var b: Vector3 = pts[i + 1]
				var seg := P(lash, "rod", [(a.x + b.x) / 2, (a.y + b.y) / 2, (a.z + b.z) / 2], [0.035 - i * 0.002, a.distance_to(b) + 0.01, 0.035 - i * 0.002], C.accG)
				seg.quaternion = Quaternion(Vector3.UP, (b - a).normalized())
			var tip: Vector3 = pts[12]
			P(lash, "sphere", [tip.x, tip.y + 0.03, tip.z], [0.09, 0.07, 0.09], C.glowC)
			P(lash, "cone", [tip.x, tip.y + 0.12, tip.z], [0.08, 0.2, 0.08], C.glowC))
	if has_bone("WristL") and prop == "daggers":
		attach("L", socket("WristL", GRIP_POS, grip_dir("L", 0)), func(g: Node3D) -> void:
			_weapon("dagger", g)
			if gw:
				_weapon_gear(g, gw), gear_sig(gw))
	if has_bone("WristL") and prop == "shield":
		# Shield frame: face (+Z) along the wrist's X axis, up (+Y) along the forearm.
		var sock := socket("WristL", Vector3(0, 0.0, 0.08))
		sock.basis = Basis(Vector3(0, 0, 1), Vector3(0, -1, 0), Vector3(1, 0, 0))
		var style: String = K.get("shield", "round")
		attach("S", sock, func(g: Node3D) -> void:
			var face := M(K.get("shieldFace", LookColors.mix(L["armor"], L["accent"], 0.25)), 0.4)
			var rimm := M(GEAR_TRIM[gw["r"]] if gw and gw["r"] >= 3 else LookColors.mix(L["accent"], "#c8ccd4", 0.4), 0.6)
			if style == "kite":
				P(g, "box", [0, 0.1, 0], [0.4, 0.42, 0.035], face)
				P(g, "cone", [0, -0.27, 0], [0.4, 0.32, 0.035], face, [180, 0, 0])
				P(g, "box", [0, 0.0, 0.022], [0.06, 0.62, 0.012], rimm)
				P(g, "box", [0, 0.14, 0.022], [0.34, 0.06, 0.012], rimm)
				P(g, "ring", [0, 0.1, 0.0], [0.4, 0.42, 0.35], rimm)
			elif style == "tower":
				P(g, "box", [0, 0.05, 0], [0.44, 0.78, 0.04], M(L["armor"], 0.4))
				P(g, "box", [0, 0.05, -0.006], [0.5, 0.84, 0.03], Gl(L.get("glow") if L.get("glow") else L["accent"], 0.7))
				P(g, "sphere", [0, 0.12, 0.03], [0.14, 0.14, 0.05], C.glowC)
				P(g, "box", [0, 0.05, 0.024], [0.05, 0.66, 0.012], C.gold)
			else:
				var r := 0.62 if K.get("big") else 0.56
				P(g, "disc", [0, 0, 0], [r, 0.035, r], face, [90, 0, 0])
				P(g, "ring", [0, 0, 0], [r, r, 0.6], Gl(L["accent"]) if K.get("big") else rimm)
				P(g, "boss", [0, 0, 0.012], [0.16, 0.16, 0.12], rimm, [90, 0, 0])
				if K.get("studs"):
					for i in 6:
						var a := i * 60 * D2R
						P(g, "sphere", [cos(a) * r * 0.36, sin(a) * r * 0.36, 0.02], [0.045, 0.045, 0.03], M(L["accent"]))
				if K.get("big"):
					for i in 8:
						var a := i * 45 * D2R
						P(g, "box", [cos(a) * r * 0.3, sin(a) * r * 0.3, 0.02], [0.03, r * 0.5, 0.012], rimm, [0, 0, i * 45 + 90]), str(gw["r"]) if gw and gw["r"] >= 3 else "")


# ---- head: headgear, hair pieces, face ----
func _head() -> void:
	if not has_bone("Head"):
		return
	var parts: Dictionary = B.get("parts", {})
	var head_src: String = parts.get("head", base)
	var hide: Array = B.get("hide", [])
	var hooded := head_src == "hooded" and not hide.has("hood")
	var Hd: Dictionary = LooksData.HEAD["hooded"] if hooded else (LooksData.HEAD["hair"] if head_src == "hooded" else (LooksData.HEAD["bald"] if B.get("bald") or hide.has("hair") else LooksData.HEAD.get(head_src, LooksData.HEAD["adventurer"])))
	var gh = gear.get("helm") if gear else null
	var gear_head: String = L.get("gear", "")
	var head_class := "helm" if HELM_GEAR.has(gear_head) else ("adorn" if ADORN_GEAR.has(gear_head) or hooded or K.get("hat") else "open")
	var head := socket("Head", Vector3.ZERO)
	var top: float = Hd["top"]
	var r: float = Hd["r"]
	attach("H", head, func(g: Node3D) -> void:
		if gear_head == "helm":
			var metal := M(LookColors.mix(GEAR_METAL[gh["r"]], L["armor"], 0.3) if gh else LookColors.mix(L["armor"], "#c8ccd4", 0.2 if K.get("helm") == "winged" else 0.5), 0.6)
			P(g, "helm", [0, top - 0.12, -0.01], [r * 2.2, r * 1.75, r * 2.3], metal)
			P(g, "ring", [0, top - 0.105, -0.01], [r * 2.28, r * 2.4, 0.4], metal, [90, 0, 0])
			for i in 8:
				var a := i / 8.0 * TAU
				P(g, "sphere", [sin(a) * r * 1.13, top - 0.1, -0.01 + cos(a) * r * 1.18], [0.016, 0.016, 0.016], metal)
			P(g, "box", [0, top - 0.17, Hd["face"] + 0.012], [0.03, 0.1, 0.02], metal)
			if K.get("helm") != "great":
				for s in [-1, 1]:
					P(g, "box", [s * r * 0.98, top - 0.21, 0.04], [0.03, 0.12, 0.1], metal)
			if K.get("helm") == "winged":
				for s in [-1, 1]:
					P(g, "cone", [s * (r + 0.03), top, -0.05], [0.1, 0.32, 0.03], M("#ffffff", 0.2), [-35, 90, s * -35])
					P(g, "cone", [s * (r + 0.02), top - 0.06, -0.08], [0.08, 0.24, 0.025], C.glowC, [-50, 90, s * -50])
			elif K.get("helm") == "horned":
				for s in [-1, 1]:
					P(g, "cone", [s * (r + 0.05), top, 0], [0.075, 0.3, 0.075], M("#e8e4da"), [0, 0, s * -35])
					P(g, "cone", [s * (r + 0.13), top + 0.15, 0], [0.035, 0.08, 0.035], C.accG, [0, 0, s * -35])
				P(g, "box", [0, top, 0], [0.03, 0.07, 0.26], C.acc)
			elif K.get("helm") == "great":
				P(g, "rod", [0, top - 0.2, 0.0], [r * 2.25, 0.16, r * 2.3], metal)
				P(g, "box", [0, Hd["eye"] + 0.005, Hd["face"] + 0.005], [0.17, 0.022, 0.02], M("#141418"))
				P(g, "box", [0, top - 0.04, 0], [0.025, 0.05, r * 2.1], C.acc)
				for s in [-1, 1]:
					P(g, "sphere", [s * r * 1.1, top - 0.15, 0], [0.04, 0.04, 0.04], C.acc)
		elif gear_head == "horns":
			P(g, "dome", [0, top - 0.12, -0.01], [r * 2.3, r * 1.7, r * 2.45], M("#26201e", 0.5))
			var bone := M("#e8dcc0")
			for s in [-1, 1]:
				P(g, "rod", [s * (r + 0.03), top - 0.08, 0.01], [0.085, 0.12, 0.085], bone, [0, 0, s * 75])
				P(g, "rod", [s * (r + 0.1), top - 0.03, 0.02], [0.07, 0.12, 0.07], bone, [-10, 0, s * 35])
				P(g, "cone", [s * (r + 0.13), top + 0.07, 0.04], [0.065, 0.16, 0.065], bone, [-20, 0, s * 5])
			P(g, "box", [0, top - 0.15, Hd["face"] + 0.01], [0.16, 0.025, 0.02], C.glowC)
		elif gear_head == "halo":
			var hp := pivot(g, [0, top + 0.13, -0.06])
			hp.rotation.x = 70 * D2R
			P(hp, "ring", [0, 0, 0], [0.34, 0.34, 0.5], C.glowC)
			if K.get("staff") == "sun":
				for i in 8:
					var a := i * 45 * D2R
					P(hp, "cone", [cos(a) * 0.23, sin(a) * 0.23, 0], [0.035, 0.08, 0.035], C.gold, [0, 0, i * 45 - 90])
			live_mesh(g, "sphere", [0, top + 0.13, -0.06], [0.42, 0.12, 0.42], C.glowH, pulse)
		elif gear_head == "circlet":
			var cy: float = Hd["eye"] + 0.07
			P(g, "ring", [0, cy, 0.0], [r * 1.85, r * 1.95, 0.2], C.gold, [80, 0, 0])
			P(g, "octa", [0, cy - 0.005, r * 0.95 + 0.02], [0.05, 0.075, 0.03], C.glowC)
		elif gear_head == "bandana":
			var cloth := Mc(K.get("band", L["accent"]))
			var by: float = Hd["eye"] + 0.08
			P(g, "ring", [0, by, 0], [r * 2.25, r * 2.4, 0.35], cloth, [80, 0, 0])
			var tails := live_group(g, [0, by, -r - 0.03], func(o: Node3D, t: float) -> void:
				o.rotation.x = 0.5 + sin(t * 3.1 + o.get_instance_id() % 97) * 0.18
				o.rotation.z = sin(t * 2.3) * 0.15, 1.6)
			P(tails, "box", [0.03, -0.12, -0.02], [0.05, 0.26, 0.012], cloth, [0, 0, 10])
			P(tails, "box", [-0.03, -0.15, -0.01], [0.05, 0.32, 0.012], cloth, [0, 0, -8])
			if L.get("glow"):
				P(g, "sphere", [0, by, r * 1.1], [0.04, 0.04, 0.02], C.glowC)
		elif gear_head == "crown":
			var band := M(K.get("crownBand", LookColors.mix(L["accent"], "#e8c070", 0.4)), 0.6)
			var cy: float = Hd["crown"]
			var cr := r * 1.05
			P(g, "ring", [0, cy, 0], [cr * 2, cr * 2.1, 0.6], band, [90, 0, 0])
			var style: String = K.get("crown", "spike")
			for i in 7:
				var a := (i / 7.0) * TAU + PI / 2
				var x := cos(a) * cr
				var z := sin(a) * cr * 1.05
				var h: float = (0.12 if i % 2 else 0.24) if style == "jagged" else (0.12 + (i % 3) * 0.05 if style == "coral" else (0.16 if i == 0 else 0.11))
				var tl: float = (18 if i % 2 else -14) if style == "coral" else 0
				P(g, "cone", [x, cy + h / 2, z], [0.05, h, 0.05], M("#1a1020", 0.4) if style == "jagged" else band, [0, 0, tl])
				if style != "spike" or i == 0:
					P(g, "sphere", [x, cy + h + 0.005, z], [0.035, 0.035, 0.035], C.glowC)
			if K.get("flame"):
				for i in 3:
					live_mesh(g, "cone", [(i - 1) * 0.07, cy + 0.13, cr * 0.95], [0.05, 0.13, 0.05], C.glowC, flicker)
		elif gear_head == "mask":
			var wrap := Mc(LookColors.mix(L["armor"], "#000000", 0.25))
			P(g, "dome", [0, Hd["eye"] - 0.07, Hd["face"] - 0.06], [0.22, 0.1, 0.17], wrap, [90, 0, 0])
			P(g, "box", [0, Hd["eye"] - 0.035, Hd["face"]], [0.2, 0.01, 0.03], C.acc)
		elif gear_head == "veil":
			P(g, "cone", [0, top + 0.02, -0.01], [0.3, 0.24, 0.3], M("#1a1028"))
			P(g, "disc", [0, top - 0.09, -0.01], [0.42, 0.012, 0.42], M("#1a1028"))
			P(g, "cone", [0, top - 0.2, -0.04], [0.5, 0.42, 0.5], Gl("#1a1028", 0.55))
			P(g, "ring", [0, top - 0.09, 0], [0.21, 0.21, 0.3], C.accG, [90, 0, 0])
		# ---- hair pieces ----
		var hair: Dictionary = C.hair
		match K.get("hair", ""):
			"ponytail":
				P(g, "sphere", [0, top - 0.05, -r * 0.95], [0.07, 0.07, 0.07], C.acc)
				var pt := live_group(g, [0, top - 0.05, -r - 0.02], func(o: Node3D, t: float) -> void:
					o.rotation.x = sin(t * 2.2 + 1) * 0.08
					o.rotation.z = sin(t * 1.6) * 0.06, 1.3)
				P(pt, "capsule", [0, -0.1, -0.06], [0.11, 0.1, 0.09], hair, [-30, 0, 0])
				P(pt, "capsule", [0, -0.24, -0.1], [0.08, 0.1, 0.07], hair, [-12, 0, 0])
				P(pt, "cone", [0, -0.37, -0.11], [0.07, 0.12, 0.06], hair, [180, 0, 0])
			"braid":
				for i in 6:
					P(g, "sphere", [0, top - 0.12 - i * 0.075, -r - 0.03 - i * 0.008], [0.085 - i * 0.006, 0.09, 0.08 - i * 0.005], hair)
				P(g, "ring", [0, top - 0.6, -r - 0.07], [0.05, 0.05, 0.4], C.acc, [90, 0, 0])
				P(g, "cone", [0, top - 0.66, -r - 0.07], [0.06, 0.1, 0.05], hair, [180, 0, 0])
			"bun":
				P(g, "sphere", [0, top - 0.02, -r * 0.8], [0.13, 0.12, 0.12], hair)
				P(g, "rod", [0.04, top + 0.02, -r * 0.8], [0.015, 0.2, 0.015], C.gold, [0, 0, 60])
			"flame":
				for i in 5:
					var a := (i - 2) * 0.5
					live_mesh(g, "cone", [sin(a) * 0.08, top + 0.04 - absi(i - 2) * 0.03, -0.04 - cos(a) * 0.03], [0.09, 0.26 - absi(i - 2) * 0.05, 0.09], C.glowC if i % 2 else Gl(L["hair"]), flicker, [-20, 0, (i - 2) * -14])
			"long":
				P(g, "box", [0, top - 0.33, -r * 0.6], [r * 1.9, 0.36, 0.08], hair, [8, 0, 0])
				P(g, "cone", [0, top - 0.55, -r * 0.62], [r * 1.9, 0.12, 0.08], hair, [180, 0, 0])
		if K.get("beardBraid"):
			P(g, "ring", [0, -0.06, 0.13], [0.05, 0.05, 0.4], C.gold, [90, 0, 0])
			for i in 3:
				P(g, "sphere", [0, -0.1 - i * 0.05, 0.13 + i * 0.01], [0.055 - i * 0.008, 0.06, 0.05], hair)
			P(g, "sphere", [0, -0.25, 0.155], [0.04, 0.04, 0.04], C.gold)
		if K.get("hat") == "straw":
			var straw := M("#d8bb6a")
			P(g, "disc", [0, top - 0.04, 0], [0.62, 0.02, 0.62], straw, [-6, 0, 0])
			P(g, "cone", [0, top + 0.06, -0.01], [0.32, 0.2, 0.32], straw)
			P(g, "rod", [0, top - 0.02, -0.01], [0.27, 0.04, 0.27], M(L["accent"]))
		# ---- face: eyes, brows and a mouth that read at the battle camera ----
		if not hollow and K.get("face", true) != false:
			var F: Dictionary = {"brow": 6, "mouth": "flat", "iris": "#4a3a2a"}
			F.merge(LooksData.FACE.get(id, {}), true)
			var ex := 0.046 if head_src == "hooded" else 0.045
			var ey := 0.113 if head_src == "hooded" else 0.109
			var ez := 0.118 if head_src == "hooded" else 0.11
			var iris := Gl(F["iris"])
			var white := Gl("#fbf6ee")
			var brow_m := M(LookColors.mul(L["hair"], 0.55))
			var lip := M(LookColors.mix(LooksData.SKIN.get(L["skin"], "#c99068"), "#5a2a2a", 0.55))
			for s in [-1, 1]:
				P(g, "sphere", [s * ex, ey, ez], [0.026, 0.028, 0.008], white)
				P(g, "sphere", [s * ex, ey - 0.001, ez + 0.003], [0.016, 0.02, 0.006], iris)
				P(g, "sphere", [s * ex + 0.004, ey + 0.005, ez + 0.006], [0.006, 0.006, 0.003], white)
				if not (gear_head == "helm" and K.get("helm") == "great"):
					P(g, "box", [s * (ex + 0.002), ey + 0.026, ez + 0.002], [0.036, 0.009, 0.008], brow_m, [0, 0, s * F["brow"]])
			var has_beard: bool = head_src != "hooded" and not B.get("shave") and not hide.has("hair")
			if not has_beard and gear_head != "mask":
				var my := ey - 0.06
				var mz := ez + 0.004
				match F["mouth"]:
					"smile":
						for s in [-1, 1]:
							P(g, "box", [s * 0.011, my + 0.002, mz], [0.022, 0.005, 0.006], lip, [0, 0, s * 14])
					"frown":
						for s in [-1, 1]:
							P(g, "box", [s * 0.011, my, mz], [0.022, 0.005, 0.006], lip, [0, 0, s * -12])
					"smirk":
						P(g, "box", [-0.004, my, mz], [0.022, 0.005, 0.006], lip)
						P(g, "box", [0.013, my + 0.003, mz], [0.012, 0.005, 0.006], lip, [0, 0, 22])
					_:
						P(g, "box", [0, my, mz], [0.03, 0.005, 0.006], lip)
		if hooded and (L.get("glow") or hollow) and gear_head != "halo":
			for x in [-0.05, 0.05]:
				P(g, "sphere", [x, Hd["eye"], Hd["face"]], [0.035, 0.022, 0.02], Gl("#ff2a2a" if hollow else L["glow"])))
	# ---- helm gear ----
	if gh:
		attach("gH", head, func(g: Node3D) -> void:
			var ghr: int = gh["r"]
			var metal := M(gmetal(ghr, 0.4), 0.0 if ghr == 1 else 0.6)
			var trim := gtrim(ghr)
			var gem := Gl(SET_GLOW.get(gh.get("s", ""), L["accent"]))
			if head_class == "open" or head_class == "adorn":
				var by: float = Hd["eye"] + (0.11 if hooded else 0.065)
				var br := r * (1.02 if hooded else 1.0)
				if K.get("hat"):
					P(g, "rod", [0, top - 0.005, -0.01], [0.28, 0.025, 0.28], trim)
				else:
					P(g, "ring", [0, by, -0.005], [br * 1.98, br * 2.08, 0.22 if ghr == 1 else 0.16], M("#5a4232") if ghr == 1 else trim, [80, 0, 0])
				var fy: float = top if K.get("hat") else by
				var fz: float = 0.14 if K.get("hat") else (Hd["face"] if hooded else br * 0.98 + 0.012)
				if ghr >= 2:
					P(g, "octa", [0, fy, fz], [0.035 + ghr * 0.005, 0.045 + ghr * 0.007, 0.022], gem if ghr >= 3 else metal)
				if ghr >= 3 and not K.get("hat") and not hooded:
					for s in [-1, 1]:
						P(g, "box", [s * (br + 0.005), by - 0.035, -0.01], [0.022, 0.07, 0.09], metal)
				if ghr >= 4:
					for s in [-1, 1]:
						P(g, "cone", [s * (br + 0.02), by + 0.025, -0.03], [0.045, 0.11 + (ghr - 4) * 0.06, 0.016], trim, [-40, 90, s * -40])
				if ghr >= 5 and not K.get("hat"):
					P(g, "octa", [0, by + 0.03, -br - 0.01], [0.05, 0.08, 0.03], gem)
			else:
				P(g, "rod", [0, top - 0.115, -0.01], [r * 2.4, 0.03, r * 2.5], trim)
				if ghr >= 2:
					P(g, "box", [0, top - 0.085, Hd["face"] - 0.01], [0.12 + ghr * 0.01, 0.03, 0.025], metal)
				if ghr >= 3:
					P(g, "octa", [0, top - 0.06, Hd["face"]], [0.04, 0.055, 0.028], gem)
				if ghr >= 4:
					P(g, "box", [0, top + 0.02, -0.02], [0.025, 0.06 + (ghr - 4) * 0.04, r * 2.0], trim), head_class + gear_sig(gh))


# ---- body: robes, coats, tabards, aprons, belts (hip-mounted, model-aligned) ----
func _hips() -> void:
	if not has_bone("Hips") or not (K.get("robe") or K.get("coat") or K.get("tabard") or K.get("apron") or K.get("belt") or K.get("scabbard") or K.get("coil")):
		return
	var hip_y := bone_y("Hips")
	attach("Hp", socket_m("Hips"), func(g: Node3D) -> void:
		var robe_src: String = K["robeColor"] if K.get("robeColor") else (L["cloth"] if L.get("cloth") else L["armor"])
		var robe_c := Mc(robe_src)
		var trim_c := M(L["accent"], 0.3)
		if K.get("robe"):
			var top := 0.12
			var len: float = hip_y + top - 0.07 if K["robe"] == "long" else 0.45
			var w: float = K.get("robeW", 1.0)
			P(g, "taper", [0, top - len / 2, 0.0], [0.5 * w, len, 0.42 * w], robe_c, [180, 0, 0])
			if K["robe"] == "long":
				var ol := len * 0.42
				var over := Mc(LookColors.mix(robe_src, "#000000", 0.18))
				P(g, "taper", [0, top - ol / 2 + 0.01, 0.0], [0.53 * w, ol, 0.45 * w], over, [180, 0, 0])
				P(g, "taper", [0, top - ol + 0.02, 0.0], [0.555 * w, 0.03, 0.47 * w], trim_c, [180, 0, 0])
			P(g, "taper", [0, top - len + 0.03, 0.0], [0.53 * w, 0.06, 0.45 * w], trim_c, [180, 0, 0])
			if K["robe"] == "long" and K.get("stole", true) != false:
				P(g, "box", [0, top - len / 2, 0.205 * w], [0.12, len * 0.96, 0.02], trim_c, [-9, 0, 0])
			if K.get("bells"):
				for i in 6:
					var a := (i / 6.0) * TAU + 0.3
					P(g, "bell", [sin(a) * 0.235 * w, top - len + 0.05, cos(a) * 0.195 * w], [0.05, 0.06, 0.05], C.gold, [180, 0, 0])
		if K.get("coat"):
			var coat_c := Mc(L["armor"] if K["coat"] is bool else K["coat"])
			var len := 0.5
			P(g, "box", [0, 0.02 - len / 2, -0.12], [0.36, len, 0.03], coat_c, [8, 0, 0])
			for s in [-1, 1]:
				P(g, "box", [s * 0.15, 0.02 - len / 2, 0.0], [0.03, len, 0.22], coat_c, [0, 0, s * 4])
				P(g, "box", [s * 0.12, 0.02 - len / 2, 0.1], [0.1, len, 0.03], coat_c, [-6, s * 20, 0])
			P(g, "box", [0, 0.02 - len + 0.02, -0.135], [0.37, 0.03, 0.035], trim_c, [8, 0, 0])
		if K.get("tabard"):
			var tc := Mc(K["tabard"])
			var len := 0.5
			P(g, "box", [0, 0.25 - len / 2 + 0.05, 0.16], [0.22, len + 0.25, 0.02], tc, [-6, 0, 0])
			P(g, "box", [0, 0.25 - len / 2 + 0.05, -0.14], [0.22, len + 0.25, 0.02], tc, [6, 0, 0])
			P(g, "box", [0, 0.25 - len - 0.05, 0.19], [0.24, 0.03, 0.025], trim_c, [-6, 0, 0])
			P(g, "box", [0, 0.25 - len - 0.05, -0.17], [0.24, 0.03, 0.025], trim_c, [6, 0, 0])
			if K.get("emblem"):
				P(g, "octa", [0, 0.2, 0.18], [0.08, 0.11, 0.02], M(K["emblem"], 0.3))
		if K.get("apron"):
			var ac := M("#4a3426")
			P(g, "box", [0, 0.05, 0.17], [0.3, 0.75, 0.025], ac, [-4, 0, 0])
			P(g, "box", [0, -0.31, 0.19], [0.31, 0.03, 0.03], C.acc, [-4, 0, 0])
			P(g, "box", [0.08, -0.05, 0.19], [0.1, 0.1, 0.02], M("#3a281c"), [-4, 0, 0])
			P(g, "rod", [0.08, -0.01, 0.205], [0.012, 0.12, 0.012], C.iron, [0, 0, 20])
		if K.get("belt"):
			var bcol: String = "#3a2a20" if K["belt"] is bool else K["belt"]
			var bc := M(bcol)
			P(g, "rod", [0, 0.1, 0.0], [0.38, 0.05, 0.32], bc)
			P(g, "ring", [0, 0.1, 0.162], [0.06, 0.05, 0.3], C.gold)
			P(g, "box", [0, 0.1, 0.165], [0.008, 0.045, 0.01], C.gold)
			P(g, "box", [-0.16, 0.04, 0.05], [0.065, 0.1, 0.08], bc)
			P(g, "box", [-0.165, 0.08, 0.06], [0.07, 0.035, 0.085], M(LookColors.mul(bcol, 0.8)), [-12, 0, 0])
			P(g, "sphere", [-0.165, 0.065, 0.1], [0.014, 0.014, 0.014], C.gold)
			P(g, "rod", [0.16, 0.05, 0.07], [0.03, 0.07, 0.03], Gl(LookColors.mix(L["accent"], "#ffffff", 0.2), 0.85))
			P(g, "rod", [0.16, 0.095, 0.07], [0.02, 0.02, 0.02], C.wood)
		if K.get("scabbard"):
			P(g, "box", [-0.19, -0.05, -0.02], [0.03, 0.7, 0.05], M("#1c1c22"), [62, 0, -8])
			P(g, "box", [-0.19, -0.05, -0.02], [0.035, 0.05, 0.06], C.gold, [62, 0, -8])
		if K.get("coil"):
			for i in 3:
				P(g, "ring", [0.2, 0.0 - i * 0.012, 0.03], [0.14, 0.14, 0.3], C.accG if i == 1 else C.leather, [10, 90, 0]))


# ---- shoulders: pauldrons (kit and/or chest gear), breastplate, mantles, collars, scarves ----
func _shoulders() -> void:
	var gc = gear.get("chest") if gear else null
	var p_style = K.get("pauldrons", ("leather" if gc["r"] == 1 else "plate") if gc else null)
	if p_style:
		for pair in [[1, "UpperArmL"], [-1, "UpperArmR"]]:
			var side: int = pair[0]
			if not has_bone(pair[1]):
				continue
			attach("P%d" % side, socket_m(pair[1], [side * 0.02, 0.01, 0]), func(g: Node3D) -> void:
				var size: float = K.get("pauldronSize", 1.0) * (0.72 + gc["r"] * 0.05 if gc else 1.0)
				var base0: Dictionary = M(gmetal(gc["r"], 0.75), 0.0 if gc["r"] == 1 else 0.55) if gc else M(K.get("pauldronColor", LookColors.mix(L["armor"], "#c8ccd4", 0.25)), 0.4)
				var trim: Dictionary = gtrim(gc["r"]) if gc else C.acc
				var tz := side * -50
				var pg := pivot(g, [0, 0, 0])
				pg.scale = Vector3.ONE * size
				match p_style:
					"leather":
						P(pg, "sphere", [side * 0.02, 0.0, 0], [0.2, 0.12, 0.2], M("#5a4232"), [0, 0, tz])
					"shell":
						P(pg, "shell", [side * 0.02, -0.03, 0], [0.24, 0.26, 0.22], base0, [0, 0, tz])
						for i in [-1, 0, 1]:
							P(pg, "box", [side * 0.06, 0.03, i * 0.05], [0.02, 0.1, 0.02], trim, [i * 20, 0, tz])
					"ice":
						P(pg, "shell", [side * 0.01, -0.03, 0], [0.2, 0.2, 0.2], base0, [0, 0, tz])
						for i in [-1, 0, 1]:
							P(pg, "octa", [side * (0.06 + absi(i) * 0.01), 0.07 - absi(i) * 0.02, i * 0.05], [0.04, 0.16 - absi(i) * 0.04, 0.04], C.accG, [i * 15, 0, side * -25])
					"spike":
						P(pg, "shell", [side * 0.02, -0.03, 0], [0.24, 0.27, 0.24], base0, [0, 0, tz])
						for i in [-1, 0, 1]:
							P(pg, "cone", [side * 0.08, 0.06, i * 0.06], [0.04, 0.14, 0.04], trim, [i * 15, 0, side * -45])
					_:
						P(pg, "shell", [side * 0.02, -0.03, 0], [0.26, 0.27, 0.25], base0, [0, 0, tz])
						P(pg, "ring", [0.0, -0.035, 0], [0.13, 0.13, 0.4], trim, [90, 0, tz])
						for i in [1, 2]:
							P(pg, "dome", [side * (0.03 + i * 0.012), -0.035 - i * 0.03, 0], [0.24 - i * 0.025, 0.1, 0.23 - i * 0.025], base0, [0, 0, tz - side * 8 * i])
						for i in [-1, 0, 1]:
							P(pg, "sphere", [side * 0.075, 0.035, i * 0.06], [0.018, 0.018, 0.018], trim)
						if gc and gc["r"] >= 3:
							P(pg, "dome", [side * 0.05, -0.07, 0], [0.22, 0.12, 0.22], base0, [0, 0, side * -65])
						if gc and gc["r"] >= 4:
							P(pg, "ring", [side * 0.035, -0.075, 0], [0.12, 0.12, 0.35], trim, [90, 0, side * -65])
				if gc and gc["r"] >= 3 and p_style != "leather":
					P(pg, "octa", [side * 0.1, 0.02, 0.0], [0.035, 0.045, 0.035], Gl(SET_GLOW.get(gc.get("s", ""), L["accent"])))
				if K.get("aura"):
					P(g, "sphere", [side * 0.05, 0.06, 0], [0.11, 0.11, 0.11], C.glowC)
					live_mesh(g, "sphere", [side * 0.05, 0.06, 0], 0.2, C.glowH, pulse), "%d%s" % [gc["r"], String(gc.get("s", "")).substr(0, 1)] if gc else "")
	elif K.get("aura"):
		for pair in [[1, "UpperArmL"], [-1, "UpperArmR"]]:
			if not has_bone(pair[1]):
				continue
			attach("A%d" % pair[0], socket_m(pair[1], [pair[0] * 0.03, 0.04, 0]), func(g: Node3D) -> void:
				P(g, "sphere", [0, 0, 0], [0.15, 0.15, 0.15], C.glowC)
				live_mesh(g, "sphere", [0, 0, 0], 0.3, C.glowH, pulse))
	if gc and gc["r"] >= 2 and has_bone("Chest"):
		attach("gC", socket_m("Chest"), func(g: Node3D) -> void:
			var gcr: int = gc["r"]
			var metal := M(gmetal(gcr), 0.55)
			var trim := gtrim(gcr)
			var gem := Gl(SET_GLOW.get(gc.get("s", ""), L["accent"]))
			var fz := 0.035 if base == "hooded" else 0.045
			P(g, "rod", [0, 0.13, 0.0], [0.24, 0.05, 0.2], metal)
			P(g, "rod", [0, 0.13, 0.0], [0.25, 0.012, 0.21], trim)
			var ps := 0.92 + gcr * 0.03
			P(g, "breast", [0, 0.0, fz + 0.075], [ps, ps, 1], metal, [-6, 0, 0])
			P(g, "box", [0, 0.0, fz + 0.1], [0.022, 0.24, 0.02], trim, [-6, 0, 0])
			if gcr >= 3:
				P(g, "octa", [0, 0.03, fz + 0.115], [0.045 + gcr * 0.004, 0.06 + gcr * 0.005, 0.025], gem)
			if gcr >= 4:
				P(g, "box", [0, -0.13, fz + 0.085], [0.24, 0.025, 0.03], trim, [-6, 0, 0])
			if gcr >= 5:
				for s in [-1, 1]:
					P(g, "box", [s * 0.1, 0.08, fz + 0.1], [0.07, 0.02, 0.02], trim, [-6, 0, s * -25]), gear_sig(gc))
	if has_bone("Chest") and (K.get("mantle") or K.get("collar") or K.get("scarf")):
		attach("Mn", socket_m("Chest"), func(g: Node3D) -> void:
			var mantle: String = K.get("mantle", "")
			if mantle == "fur" or mantle == "moss":
				var fc: String = K.get("furColor", "#e8e4dc") if mantle == "fur" else L["accent"]
				var fur := M(fc)
				var fur_d := M(LookColors.mul(fc, 0.75))
				for i in 9:
					var a := (i / 8.0) * PI * 1.25 - PI * 0.625
					P(g, "sphere", [sin(a) * 0.2, 0.14 - absf(sin(a)) * 0.03, -cos(a) * 0.1 - 0.02], [0.16, 0.12, 0.14], fur if i % 2 else fur_d)
				P(g, "sphere", [0, 0.06, -0.15], [0.36, 0.2, 0.14], fur_d)
				if mantle == "moss":
					for i in 5:
						P(g, "cone", [-0.16 + i * 0.08, 0.2, -0.12], [0.02, 0.16, 0.02], M("#8a9a50"), [-20 + i * 5, 0, (i - 2) * 12])
			elif mantle == "ember":
				for i in 5:
					for s in [-1, 1]:
						live_mesh(g, "cone", [s * (0.12 + i * 0.03), 0.17 - i * 0.015, -0.06 - i * 0.02], [0.06, 0.2 - i * 0.02, 0.06], C.glowC if i % 2 else C.accG, flicker, [-25, 0, s * (-20 - i * 8)])
			if K.get("collar") == "spike":
				for i in 7:
					var a := (i - 3) * 0.32
					P(g, "cone", [sin(a) * 0.17, 0.27 + cos(a * 1.4) * 0.05, -0.1 - cos(a) * 0.03], [0.05, 0.26 - absi(i - 3) * 0.03, 0.03], M("#1a1020", 0.4) if i % 2 else C.acc, [-15, 0, -a * 40])
			if K.get("scarf"):
				var sc := Mc(K["scarf"])
				P(g, "rod", [0, 0.16, 0.0], [0.22, 0.07, 0.2], sc)
				var tails := live_group(g, [0.06, 0.15, -0.1], func(o: Node3D, t: float) -> void:
					o.rotation.x = 0.6 + sin(t * 3.4 + o.get_instance_id() % 97) * 0.2
					o.rotation.y = sin(t * 1.9) * 0.25, 1.6)
				P(tails, "box", [0.0, -0.18, 0], [0.07, 0.36, 0.012], sc, [0, 0, 6])
				P(tails, "box", [-0.06, -0.14, 0], [0.06, 0.28, 0.012], sc, [0, 0, -8]))


# ---- back: capes in two hinged panels on a spring, quiver, shovel, wings, sunburst ----
func _back() -> void:
	if not has_bone("Chest") or not (K.get("cape") or L.get("prop") == "bow" or K.get("back")):
		return
	var back := socket_m("Chest")
	attach("B", back, func(g: Node3D) -> void:
		var z := -0.15 if base == "hooded" else -0.14
		if K.get("cape"):
			var cloth := Mc(K["capeColor"] if K.get("capeColor") else LookColors.mul(L["armor"], 0.55))
			var trim := Mc(L["accent"])
			var cape_len: float = K.get("capeLen", 1.0)
			for sx in [-1, 1]:
				P(g, "sphere", [sx * 0.12, 0.14, z + 0.04], [0.05, 0.05, 0.03], C.gold)
			var upper := live_group(g, [0, 0.14, z], func(o: Node3D, t: float) -> void:
				var ph := float(o.get_instance_id() % 97)
				o.rotation.x = 0.1 + sin(t * 1.3 + ph) * 0.035
				var lo: Node3D = o.get_child(o.get_child_count() - 1)
				var sp: Dictionary = o.get_meta("sp", {})
				lo.rotation.x = 0.06 + sin(t * 1.9 + 0.8 + ph) * 0.08 + sp.get("ax", 0.0) * 0.4
				lo.rotation.z = sin(t * 1.1 + ph) * 0.04 + sp.get("az", 0.0) * 0.3, 1.0)
			P(upper, KitGeo.sheet(0.24, 0.42, 0.52, 0.09), [0, 0, -0.01], [1, 1, 1], cloth)
			P(upper, KitGeo.sheet(0.22, 0.39, 0.5, 0.085), [0, -0.01, -0.002], [1, 1, 1], Mc(LookColors.mix(L["accent"], "#000000", 0.35)))
			var lower := pivot(upper, [0, -0.47, -0.01])
			P(lower, KitGeo.sheet(0.4, 0.56, 0.06 + 0.5 * cape_len, 0.11), [0, 0.06, 0], [1, 1, 1], cloth)
			P(lower, KitGeo.sheet(0.56, 0.57, 0.045, 0.11, 10, 1), [0, -0.5 * cape_len + 0.02, 0.003], [1.01, 1, 1.01], trim)
		if L.get("prop") == "bow":
			P(g, "rod", [0.06, -0.05, -0.2], [0.12, 0.46, 0.12], C.leather, [0, 0, -25])
			for i in 3:
				P(g, "cone", [0.15 + i * 0.03, 0.2 + i * 0.01, -0.2 + (i - 1) * 0.03], [0.04, 0.08, 0.015], C.acc, [0, 0, -25])
		if K.get("back") == "shovel":
			P(g, "rod", [0, 0.0, z - 0.04], [0.04, 1.0, 0.04], C.wood, [0, 0, 35])
			P(g, "box", [0.3, -0.42, z - 0.04], [0.2, 0.26, 0.02], C.iron, [0, 0, 35])
			P(g, "box", [-0.28, 0.4, z - 0.04], [0.14, 0.04, 0.04], C.wood, [0, 0, 35])
		elif K.get("back") == "wings":
			var feather := M("#fbf6ea")
			var tip := Mg(LookColors.mix(L["accent"], "#ffffff", 0.3))
			for s in [-1, 1]:
				var w := live_group(g, [s * 0.07, 0.12, z - 0.03], func(o: Node3D, t: float) -> void:
					o.rotation.y = s * (0.55 + sin(t * 1.4) * 0.1)
					o.rotation.z = s * sin(t * 1.4) * 0.05)
				for i in 4:
					P(w, "sphere", [s * (0.06 + i * 0.07), 0.2 + i * 0.03, -0.04], [0.13, 0.1, 0.035], feather, [0, 0, s * -20])
				for i in 7:
					var ang := (10 + i * 13) * D2R
					var ln := 0.48 - absi(i - 3) * 0.04
					P(w, "sphere", [s * (0.24 + cos(ang) * ln / 2), 0.28 - sin(ang) * ln / 2, -0.05 - i * 0.004], [0.12, ln, 0.025], tip if i < 2 else feather, [0, 0, s * (90 - (10 + i * 13))])
		elif K.get("back") == "sunburst":
			var hp := pivot(g, [0, 0.3, z - 0.08])
			P(hp, "ring", [0, 0, 0], [0.46, 0.46, 0.5], C.gold)
			for i in 12:
				var a := i * 30 * D2R
				P(hp, "cone", [cos(a) * 0.3, sin(a) * 0.3, 0], [0.05, 0.14 if i % 2 else 0.22, 0.02], C.gold, [0, 0, i * 30 - 90])
			live_mesh(g, "sphere", [0, 0.3, z - 0.1], [0.5, 0.5, 0.1], C.glowH, pulse))
	if K.get("shards") and has_bone("Hips"):
		attach("Sh", socket_m("Hips", [0, 0.35, 0]), func(g: Node3D) -> void:
			var ring := live_group(g, [0, 0, 0], func(o: Node3D, t: float) -> void:
				o.rotation.y = t * 0.9
				o.position.y = sin(t * 1.3) * 0.04)
			for i in 5:
				var a := i * 72 * D2R
				P(ring, "octa", [cos(a) * 0.45, sin(i * 2.1) * 0.12, sin(a) * 0.45], [0.06, 0.13, 0.02], C.accG if i % 2 else M("#cfe4ff", 0.6), [0, -i * 72 + 90, 20]))


# ---- boots gear: shin greaves (or toe caps under a long robe) ----
func _boots() -> void:
	var gb = gear.get("boots") if gear else null
	if not gb:
		return
	var long_robe: bool = K.get("robe") == "long"
	var gbr: int = gb["r"]
	for trip in [[1, "LowerLegL", "FootL"], [-1, "LowerLegR", "FootR"]]:
		var side: int = trip[0]
		var metal := M(gmetal(gbr), 0.0 if gbr == 1 else 0.55)
		var trim := gtrim(gbr)
		var gem := Gl(SET_GLOW.get(gb.get("s", ""), L["accent"]))
		if not long_robe and has_bone(trip[1]):
			attach("gL%d" % side, socket_m(trip[1]), func(g: Node3D) -> void:
				if gbr == 1:
					P(g, "rod", [0, -0.24, 0.0], [0.125, 0.22, 0.125], M("#5a4232"))
					P(g, "rod", [0, -0.16, 0], [0.13, 0.02, 0.13], M("#3a2a20"))
					return
				P(g, "taper", [0, -0.21, 0.005], [0.13, 0.3, 0.13], metal)
				P(g, "rod", [0, -0.355, 0.005], [0.115, 0.025, 0.115], trim)
				P(g, "box", [0, -0.2, 0.06], [0.03, 0.26, 0.02], trim)
				if gbr >= 3:
					P(g, "sphere", [0, -0.03, 0.035], [0.11, 0.1, 0.09], metal)
				if gbr >= 4:
					P(g, "cone", [side * 0.065, -0.04, 0.0], [0.03, 0.12 + (gbr - 4) * 0.06, 0.03], trim, [0, 0, side * -70])
				if gbr >= 5:
					P(g, "octa", [0, -0.03, 0.085], [0.035, 0.045, 0.02], gem)
					live_mesh(g, "sphere", [0, -0.03, 0.08], 0.1, Gl(SET_GLOW.get(gb.get("s", ""), L["accent"]), 0.35), pulse), gear_sig(gb))
		if has_bone(trip[2]):
			attach("gF%d" % side, socket_m(trip[2]), func(g: Node3D) -> void:
				if gbr == 1:
					P(g, "rod", [0, 0.06, -0.01], [0.12, 0.05, 0.14], M("#5a4232"))
					return
				P(g, "dome", [0, 0.02, 0.06], [0.12, 0.08, 0.14], metal)
				P(g, "rod", [0, 0.08, -0.01], [0.13, 0.035, 0.14], trim)
				if long_robe and gbr >= 3:
					P(g, "octa", [0, 0.06, 0.12], [0.03, 0.04, 0.02], gem), gear_sig(gb) + ("r" if long_robe else ""))
