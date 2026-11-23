## The 3D battle screen: arena, heroes on the rune-circle rim, the "Three-quarter" camera fitted
## to the frame, simple cast/hit VFX, floating numbers, HUD tags, manual skill buttons, Auto and
## 1x/2x/3x speed. The turn loop makes the same engine calls, in the same order, as loop() /
## doAct() / endTurnAndCheck() in public/game/ui.js, with presentation awaited in between.
class_name BattleView
extends Control

signal finished(result: String)
signal quit_requested

const PANEL_H := 196.0

var battle: SbBattle
var stage_idx := 0
var auto := false
var speed := 1

var _vp: SubViewport
var _world: Node3D
var _cam: Camera3D
var _hud: Control
var _views := {} # key -> {u, root, model, home, idle_rot, tag, aura}
var _actor: SbUnit
var _awaiting := false
var _selected := ""
var _alive := true
var _skill_btns: Array = []
var _auto_btn: Button
var _speed_btns: Array = []
var _info: Label
var _wave_lbl: Label
var _banner: Label
var _vfx: Vfx
var _float_stack := {} # key -> [count, last spawn time] so texts on one unit don't overlap
var _dot_tex: GradientTexture2D

signal _chosen(choice: Dictionary)


func setup(b: SbBattle, idx: int) -> void:
	battle = b
	stage_idx = idx
	auto = bool(Game.g["settings"].get("auto", false))
	speed = clampi(int(Game.g["settings"].get("speed", 1)), 1, 3)


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	var svc := SubViewportContainer.new()
	svc.stretch = true
	svc.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	svc.offset_bottom = -PANEL_H
	svc.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(svc)
	var qp := Quality.preset()
	Quality.apply_globals()
	_vp = SubViewport.new()
	_vp.msaa_3d = qp["msaa"]
	_vp.scaling_3d_scale = qp["scale"]
	_vp.positional_shadow_atlas_size = 0
	_vp.own_world_3d = true
	svc.add_child(_vp)
	_world = Node3D.new()
	_vp.add_child(_world)
	var arena := Arena.new()
	_world.add_child(arena)
	arena.build(stage_idx / WebData.STAGES_PER_CHAPTER, qp["arena"])
	arena.env.glow_enabled = qp["glow"]
	var vig := _vignette()
	add_child(vig)
	vig.offset_bottom = -PANEL_H
	_cam = Camera3D.new()
	_cam.near = 0.1
	_cam.far = 150.0
	_world.add_child(_cam)
	_vfx = Vfx.new()
	_world.add_child(_vfx)
	_vfx.setup(_cam, qp["arena"])
	_hud = Control.new()
	_hud.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_hud.offset_bottom = -PANEL_H
	_hud.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_hud)
	_build_top()
	_build_panel()
	for i in battle.allies.size():
		_add_unit(battle.allies[i], i, battle.allies.size())
	for i in battle.enemies.size():
		_add_unit(battle.enemies[i], i, battle.enemies.size())
	resized.connect(_fit_camera)
	_fit_camera.call_deferred()
	Engine.time_scale = speed
	_loop.call_deferred()


func _exit_tree() -> void:
	_alive = false
	Engine.time_scale = 1.0


## A soft vignette over the 3D view (the web's post-processing vignette).
func _vignette() -> TextureRect:
	var g := Gradient.new()
	g.set_color(0, Color(0, 0, 0, 0))
	g.set_color(1, Color(0.02, 0.01, 0.04, 0.55))
	g.add_point(0.6, Color(0, 0, 0, 0))
	var t := GradientTexture2D.new()
	t.gradient = g
	t.fill = GradientTexture2D.FILL_RADIAL
	t.fill_from = Vector2(0.5, 0.45)
	t.fill_to = Vector2(1.15, 1.05)
	t.width = 128
	t.height = 256
	var r := TextureRect.new()
	r.texture = t
	r.stretch_mode = TextureRect.STRETCH_SCALE
	r.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return r


# ---------- camera ----------
func _fit_camera() -> void:
	var sz := Vector2(size.x, size.y - PANEL_H)
	if sz.x < 2 or sz.y < 2:
		return
	var aspect := sz.x / sz.y
	var c := BattleLayout.camera_preset(BattleLayout.DEFAULT_CAMERA)
	_cam.fov = BattleLayout.fov_for(aspect)
	var dist := BattleLayout.fit_distance(c, aspect, false)
	_cam.position = BattleLayout.camera_position(c, dist)
	_cam.look_at(BattleLayout.CAM_TARGET)


# ---------- units ----------
func _add_unit(u: SbUnit, i: int, n: int) -> void:
	var ally := u.side == "A"
	var root := Node3D.new()
	var model := HeroModel.new()
	model.build(u.template["id"], not ally, u.el, unit_gear(u))
	var scale := 1.3 if u.boss else 1.0
	model.scale = Vector3.ONE * scale
	root.add_child(model)
	var pos := BattleLayout.slot(ally, i, n)
	root.position = pos
	var idle_rot := BattleLayout.face_centre_yaw(pos)
	root.rotation.y = idle_rot
	var aura := MeshInstance3D.new()
	var disc := CylinderMesh.new()
	disc.top_radius = 0.62 * scale
	disc.bottom_radius = 0.62 * scale
	disc.height = 0.01
	aura.mesh = disc
	var aura_mat := _glow_mat(UiKit.element_color(u.el), 0.45)
	aura_mat.blend_mode = BaseMaterial3D.BLEND_MODE_MIX
	aura.material_override = aura_mat
	aura.position.y = 0.02
	aura.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	root.add_child(aura)
	_world.add_child(root)
	var tag := UnitTag.new(u)
	tag.tapped.connect(_on_tag_tapped)
	_hud.add_child(tag)
	_views[u.key] = {"u": u, "root": root, "model": model, "home": pos, "idle_rot": idle_rot, "tag": tag, "aura": aura, "top": model.height * scale}


## unitGear() in three3d.mjs: allies show their equipped items; Hollow enemies get plain gear
## that gets finer with their level.
static func unit_gear(u: SbUnit):
	if u.side == "A":
		var h := Rules.hero_by_uid(Game.g, u.hero_uid)
		return gear_look(Game.g, h)
	var lv := u.level
	if lv < 8:
		return null
	var r := mini(5, 1 + lv / 12)
	var gl := {"helm": {"r": r, "s": ""}, "chest": {"r": r, "s": ""}}
	if lv >= 20 or u.boss:
		gl["boots"] = {"r": r, "s": ""}
		gl["weapon"] = {"r": r, "s": ""}
	return gl


## art.js heroGearLook(): {slot: {r: rarity, s: set}} for the equipped pieces, or null.
static func gear_look(g: Dictionary, h: Dictionary):
	if h.is_empty():
		return null
	var out = null
	for slot in ["weapon", "helm", "chest", "boots"]:
		if h["gear"].has(slot):
			var gr := Rules.gear_by_uid(g, int(h["gear"][slot]))
			if not gr.is_empty():
				if out == null:
					out = {}
				out[slot] = {"r": int(gr["rarity"]), "s": gr["set"]}
	return out


func _remove_side(side: String) -> void:
	for k in _views.keys():
		var v: Dictionary = _views[k]
		if v["u"].side == side:
			v["root"].queue_free()
			v["tag"].queue_free()
			_views.erase(k)


func _process(_delta: float) -> void:
	if not _cam:
		return
	for k in _views:
		var v: Dictionary = _views[k]
		var tag: UnitTag = v["tag"]
		var p: Vector3 = v["root"].position + Vector3(0, v["top"] + 0.12, 0)
		if _cam.is_position_behind(p):
			tag.visible = false
			continue
		var s := _cam.unproject_position(p)
		tag.position = (s - Vector2(UnitTag.W / 2, 34)).clamp(Vector2(2, 2), _hud.size - Vector2(UnitTag.W + 2, 36))
		tag.visible = v["u"].alive
		tag.active = v["u"] == _actor
		tag.selected = k == _selected and _awaiting


func _screen(key: String, h: float = 1.2) -> Vector2:
	var v: Dictionary = _views.get(key, {})
	if v.is_empty():
		return Vector2.ZERO
	return _cam.unproject_position(v["root"].position + Vector3(0, h, 0))


# ---------- HUD ----------
func _build_top() -> void:
	var bar := UiKit.hbox(6)
	bar.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	bar.offset_left = 10
	bar.offset_right = -10
	bar.offset_top = 10
	add_child(bar)
	var back := UiKit.button("Retreat", func() -> void: quit_requested.emit(), true)
	back.custom_minimum_size = Vector2(0, 32)
	bar.add_child(back)
	_wave_lbl = UiKit.label("", 15, UiKit.GOLD, true)
	_wave_lbl.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_wave_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	_wave_lbl.add_theme_color_override("font_outline_color", Color.BLACK)
	_wave_lbl.add_theme_constant_override("outline_size", 4)
	bar.add_child(_wave_lbl)
	_banner = UiKit.label("", 22, Color("#ffe7a6"), true)
	_banner.set_anchors_and_offsets_preset(Control.PRESET_TOP_WIDE)
	_banner.offset_top = 56
	_banner.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_banner.add_theme_color_override("font_outline_color", Color("#1a1005"))
	_banner.add_theme_constant_override("outline_size", 6)
	_banner.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_banner)
	_update_wave()


func _update_wave() -> void:
	_wave_lbl.text = "Stage %s  ·  Wave %d/%d" % [Rules.stage_label(stage_idx), battle.wave + 1, battle.waves.size()]


func _build_panel() -> void:
	var panel := GildedPanel.new()
	panel.set_anchors_and_offsets_preset(Control.PRESET_BOTTOM_WIDE)
	panel.offset_top = -PANEL_H
	var sb: StyleBoxFlat = UiKit.theme.get_stylebox("panel", "PanelContainer").duplicate()
	sb.set_corner_radius_all(0)
	sb.corner_radius_top_left = 16
	sb.corner_radius_top_right = 16
	sb.bg_color = Color("#17121f")
	panel.add_theme_stylebox_override("panel", sb)
	add_child(panel)
	var col := UiKit.vbox(8)
	panel.add_child(col)
	_info = UiKit.label("", 14, UiKit.DIM)
	_info.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_info.custom_minimum_size.y = 20
	col.add_child(_info)
	var skills := UiKit.hbox(6)
	col.add_child(skills)
	for i in 3:
		var b := Button.new()
		b.focus_mode = Control.FOCUS_NONE
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		b.custom_minimum_size = Vector2(0, 62)
		b.clip_text = true
		b.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		b.add_theme_font_size_override("font_size", 13)
		b.pressed.connect(_on_skill.bind(i))
		skills.add_child(b)
		_skill_btns.append(b)
	var row := UiKit.hbox(6)
	col.add_child(row)
	_auto_btn = UiKit.button("Auto", _on_auto, true)
	_auto_btn.toggle_mode = true
	_auto_btn.button_pressed = auto
	_auto_btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(_auto_btn)
	for s in [1, 2, 3]:
		var b := UiKit.button("%dx" % s, _on_speed.bind(s), true)
		b.toggle_mode = true
		b.button_pressed = s == speed
		b.custom_minimum_size.x = 56
		row.add_child(b)
		_speed_btns.append(b)
	_refresh_skills()


func _refresh_skills() -> void:
	var u := _actor
	var manual := _awaiting and u != null and u.side == "A"
	for i in 3:
		var b: Button = _skill_btns[i]
		if manual and i < u.skills().size():
			var sk: Dictionary = u.skills()[i]
			var cd: int = u.cds[i]
			b.visible = true
			b.text = sk["name"] + ("\n(%d turn%s)" % [cd, "" if cd == 1 else "s"] if cd > 0 else "\n" + _target_hint(sk))
			b.disabled = cd > 0
		elif manual:
			b.visible = false
		else:
			b.visible = true
			b.text = "—" if i != 1 else ("Auto battle" if auto else "Enemy turn" if u and u.side == "B" else "…")
			b.disabled = true


static func _target_hint(sk: Dictionary) -> String:
	match sk["target"]:
		"enemy": return "one enemy"
		"allEnemies": return "all enemies"
		"ally": return "one ally"
		"allAllies": return "all allies"
	return "self"


func _on_skill(i: int) -> void:
	if not _awaiting:
		return
	var tgt := _selected
	var valid := battle.valid_targets(_actor, i)
	var ok := false
	for t in valid:
		if t.key == tgt:
			ok = true
	if not ok:
		tgt = _default_target(_actor, i, valid)
	_chosen.emit({"skill": i, "target": tgt})


## The AI's target rule (aiChoose) for a skill the player picked without tapping a target.
func _default_target(u: SbUnit, i: int, valid: Array) -> String:
	if valid.is_empty():
		return ""
	var by_elem: bool = u.skills()[i]["target"] == "enemy"
	var best: SbUnit = valid[0]
	for k in range(1, valid.size()):
		if battle._before(u, valid[k], best, by_elem):
			best = valid[k]
	return best.key


func _on_tag_tapped(key: String) -> void:
	_selected = key


func _on_auto() -> void:
	auto = _auto_btn.button_pressed
	Game.g["settings"]["auto"] = auto
	Game.save()
	if auto and _awaiting:
		_chosen.emit(battle.ai_choose(_actor))
	_refresh_skills()


func _on_speed(s: int) -> void:
	speed = s
	for i in 3:
		_speed_btns[i].button_pressed = (i + 1) == s
	Engine.time_scale = s
	Game.g["settings"]["speed"] = s
	Game.save()


# ---------- turn loop ----------
func _wait(t: float) -> void:
	await get_tree().create_timer(t).timeout


func _loop() -> void:
	await _wait(0.4)
	while _alive and not battle.over:
		var u := battle.next_actor()
		if u == null:
			break
		_actor = u
		_info.text = "%s's turn" % Rules.short_name(u.name) if u.side == "A" else "%s acts" % u.name
		var begin := battle.begin_turn(u)
		if not begin["events"].is_empty():
			await _show_events(begin["events"], "")
			await _wait(0.25)
		if not _alive:
			return
		if begin["skip"]:
			await _wait(0.4)
			if await _end_turn(u):
				return
			continue
		var choice: Dictionary
		if u.side == "A" and not auto:
			_awaiting = true
			_refresh_skills()
			choice = await _chosen
			_awaiting = false
			if not _alive:
				return
		else:
			_refresh_skills()
			choice = battle.ai_choose(u)
		await _do_act(u, choice)
		if not _alive:
			return
		if await _end_turn(u):
			return
	if _alive:
		await _finish()


func _do_act(u: SbUnit, choice: Dictionary) -> void:
	var sk: Dictionary = u.skills()[choice["skill"]]
	_banner.text = sk["name"]
	_refresh_skills()
	await _wait(0.25)
	if not _alive:
		return
	var color := UiKit.element_color(u.el)
	var events := battle.act(u, choice["skill"], choice["target"])
	var hit_keys := []
	for e in events:
		if e["type"] == "dmg" and e["key"] != u.key and not hit_keys.has(e["key"]):
			hit_keys.append(e["key"])
	var v: Dictionary = _views[u.key]
	var model: HeroModel = v["model"]
	var root: Node3D = v["root"]
	var home: Vector3 = v["home"]
	var aim_key: String = hit_keys[0] if not hit_keys.is_empty() else choice["target"]
	var aim: Vector3 = _views[aim_key]["root"].position if _views.has(aim_key) and aim_key != u.key else BattleLayout.CENTRE
	var dir := (aim - home)
	dir.y = 0
	var turn_to := atan2(dir.x, dir.z)
	var melee: bool = sk["mult"] > 0 and sk["target"] == "enemy" and not model.is_caster()
	var tw := create_tween()
	if melee:
		var to := home + dir * maxf(0.35, 1.0 - 1.15 / maxf(1.2, dir.length()))
		model.run(true)
		tw.set_parallel(true)
		tw.tween_property(root, "position", to, 0.26).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
		tw.tween_property(root, "rotation:y", root.rotation.y + angle_difference(root.rotation.y, turn_to), 0.16)
		await tw.finished
		model.strike()
		await _wait(0.35)
		for k in hit_keys:
			_vfx.slash(_views[k]["root"].position + Vector3(0, 1.1, 0), u.el)
	else:
		root.rotation.y = root.rotation.y + angle_difference(root.rotation.y, turn_to)
		model.cast(sk["mult"] == 0)
		# castCircle + charge under the caster (vfx.mjs), then a projectile per target
		_vfx.cast_circle(home, u.el)
		_vfx.charge(home, u.el, 0.5)
		tw.tween_property(root, "position", home + dir.normalized() * 0.2 if dir.length() > 0.01 else home, 0.2)
		await _wait(0.45)
		if not hit_keys.is_empty():
			var prop: String = LooksData.LOOKS.get(u.template["id"], {}).get("prop", "")
			var kind := "arrow" if prop == "bow" else ("bolt" if prop in ["mirror", "staff"] else ("orb" if prop in ["orb", "candle"] else "shard"))
			for k in hit_keys:
				_vfx.projectile(root.position + Vector3(0, 1.3, 0), _views[k]["root"].position + Vector3(0, 1.1, 0), u.el, kind)
			await _wait(0.32)
	if not _alive:
		return
	await _show_events(events, color)
	# recover: back home, facing the centre again
	var far := root.position.distance_to(home) > 0.6
	if far:
		model.play("Run_Back", true, 0.15, 1.5)
	var back := create_tween().set_parallel(true)
	back.tween_property(root, "position", home, 0.25).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_IN_OUT)
	back.tween_property(root, "rotation:y", root.rotation.y + angle_difference(root.rotation.y, v["idle_rot"]), 0.25)
	await back.finished
	if far:
		model.idle()
	_banner.text = ""
	await _wait(0.25)


func _end_turn(u: SbUnit) -> bool:
	var r := battle.end_turn(u)
	if r == "wave":
		await _wait(0.5)
		_remove_side("B")
		for i in battle.enemies.size():
			_add_unit(battle.enemies[i], i, battle.enemies.size())
		_update_wave()
		_banner.text = "Wave %d!" % (battle.wave + 1)
		await _wait(0.9)
		_banner.text = ""
	if not _alive:
		return true
	if battle.over:
		await _finish()
		return true
	return false


func _finish() -> void:
	_actor = null
	_refresh_skills()
	_info.text = ""
	if battle.result == "victory":
		for k in _views:
			var v: Dictionary = _views[k]
			if v["u"].side == "A" and v["u"].alive:
				var root: Node3D = v["root"]
				create_tween().tween_property(root, "rotation:y", root.rotation.y + angle_difference(root.rotation.y, -0.25 + v["home"].x * 0.08), 0.35)
				v["model"].win()
		_banner.text = "Victory"
	else:
		_banner.text = "Defeat"
	await _wait(1.4)
	Engine.time_scale = 1.0
	if _alive:
		finished.emit(battle.result)


# ---------- events & VFX ----------
func _show_events(events: Array, color: Variant) -> void:
	for e in events:
		var key: String = e.get("key", "")
		if not _views.has(key):
			continue
		var v: Dictionary = _views[key]
		var u: SbUnit = v["u"]
		match e["type"]:
			"dmg":
				var amt: float = e["amount"]
				var crit: bool = e.get("crit", false)
				var big := crit or amt / maxf(1.0, u.max_hp) > 0.14
				if e.get("dot", false):
					_float(key, "-%d" % amt, Color("#9be36a"), 18)
					_vfx.debuff_tick(v["root"].position)
				else:
					var col := Color("#ffd23a") if crit else (Color("#ffb0a0") if e.get("em", 1.0) > 1 else Color.WHITE)
					_float(key, ("%d!" if crit else "%d") % amt, col, 30 if crit else 22)
					_vfx.impact(v["root"].position, _actor.el if _actor else "physical", crit)
					if crit:
						_crit_flash()
					if u.alive:
						v["model"].hit(big)
						var back: Vector3 = (v["root"].position - BattleLayout.CENTRE).normalized() * (0.34 if big else 0.2)
						var root: Node3D = v["root"]
						var home: Vector3 = root.position
						var tw := create_tween()
						tw.tween_property(root, "position", home + back, 0.09)
						tw.tween_property(root, "position", home, 0.12)
			"absorb":
				_float(key, "Absorbed %d" % e["amount"], Color("#9fd8ff"), 16)
			"heal":
				_float(key, "+%d" % e["amount"], Color("#7dff7a"), 22)
				_vfx.heal(v["root"].position)
			"fx":
				var meta: Dictionary = WebData.EFFECTS[e["t"]]
				var buff: bool = meta["buff"]
				_float(key, meta["name"], Color("#ffd56a") if buff else Color("#c07aff"), 15)
				if e["t"] == "shield":
					_vfx.shield(v["root"].position)
				else:
					_vfx.buff(v["root"].position, buff)
			"text":
				_float(key, e["text"], Color("#f6e6c2"), 15)
			"death":
				_die(key)
	await _wait(0.17)


func _die(key: String) -> void:
	var v: Dictionary = _views[key]
	var model: HeroModel = v["model"]
	model.die()
	_vfx.death_fade(v["root"].position, v["u"].el)
	var root: Node3D = v["root"]
	var tw := create_tween()
	tw.tween_interval(1.6)
	tw.tween_property(root, "position:y", -0.9, 0.8)
	tw.tween_callback(func() -> void:
		if is_instance_valid(v["aura"]):
			v["aura"].visible = false)


## critFlash(): a brief full-screen tint in the hit's element colour.
func _crit_flash() -> void:
	var r := ColorRect.new()
	r.color = Color(1, 0.95, 0.8, 0.22)
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	r.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	r.offset_bottom = -PANEL_H
	add_child(r)
	var tw := create_tween()
	tw.tween_property(r, "color:a", 0.0, 0.18)
	tw.tween_callback(r.queue_free)


func _float(key: String, text: String, col: Color, fs: int) -> void:
	var l := UiKit.label(text, fs, col, true)
	l.autowrap_mode = TextServer.AUTOWRAP_OFF
	l.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.9))
	l.add_theme_constant_override("outline_size", 5)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_hud.add_child(l)
	var now := Time.get_ticks_msec() / 1000.0 * Engine.time_scale
	var st: Array = _float_stack.get(key, [0, -10.0])
	var n: int = st[0] + 1 if now - st[1] < 0.5 else 0
	_float_stack[key] = [n, now]
	var p := _screen(key, 1.6) + Vector2(randf_range(-10, 10), -20.0 * n)
	l.reset_size()
	l.position = p - l.size / 2
	var tw := create_tween().set_parallel(true)
	tw.tween_property(l, "position:y", l.position.y - 46, 0.9).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	tw.tween_property(l, "modulate:a", 0.0, 0.4).set_delay(0.5)
	tw.chain().tween_callback(l.queue_free)


func _glow_mat(c: Color, alpha: float = 1.0) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.blend_mode = BaseMaterial3D.BLEND_MODE_ADD
	m.albedo_color = Color(c, alpha)
	m.no_depth_test = false
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	return m
