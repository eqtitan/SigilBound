## The demo's screens, built in code: title -> starter pick -> home (team, heroes, summoning,
## chapter 1 stages, Battle) -> 3D battle -> victory/defeat result -> back home.
## `-- --smoke` plays one auto battle headless and exits with a status code; `-- --shots=DIR`
## captures portrait screenshots of each screen (needs a renderer).
extends Node

var ui: CanvasLayer
var screen: Control
var battle_view: BattleView
var modal_layer: CanvasLayer
var stage_sel := -1
var _last_result := {}


func _ready() -> void:
	ui = CanvasLayer.new()
	add_child(ui)
	modal_layer = CanvasLayer.new()
	modal_layer.layer = 10
	add_child(modal_layer)
	var root := Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.theme = UiKit.setup()
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	ui.add_child(root)
	ui.set_meta("root", root)
	Updater.changed.connect(_on_update_changed)
	show_title()
	if Game.smoke:
		_smoke.call_deferred()
	elif Game.scroll_test:
		_scroll_test.call_deferred()
	elif Game.shots_dir != "":
		_shots.call_deferred()


func _root() -> Control:
	return ui.get_meta("root")


func _set_screen(c: Control) -> void:
	_close_modal()
	if screen:
		screen.queue_free()
	if battle_view:
		battle_view.queue_free()
		battle_view = null
	screen = c
	_root().add_child(c)


func _scroll_page() -> Array:
	var page := Control.new()
	page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	page.add_child(SigilBackground.new())
	var sc := DragScroll.new()
	sc.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	sc.horizontal_scroll_mode = ScrollContainer.SCROLL_MODE_DISABLED
	page.add_child(sc)
	var margin := MarginContainer.new()
	margin.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	for side in ["left", "right"]:
		margin.add_theme_constant_override("margin_" + side, 16)
	margin.add_theme_constant_override("margin_top", 28)
	margin.add_theme_constant_override("margin_bottom", 24)
	sc.add_child(margin)
	var col := UiKit.vbox(12)
	margin.add_child(col)
	return [page, col]


# ---------- title ----------
func show_title() -> void:
	var page := Control.new()
	page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	page.add_child(SigilBackground.new())
	var col := UiKit.vbox(14)
	col.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	col.offset_left = 24
	col.offset_right = -24
	col.alignment = BoxContainer.ALIGNMENT_CENTER
	page.add_child(col)
	var spacer := Control.new()
	spacer.custom_minimum_size.y = 120
	col.add_child(spacer)
	col.add_child(UiKit.logo(46))
	var sub := UiKit.label("Sigilbound — Godot demo", 18, Color("#f6e6c2"), true)
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(sub)
	col.add_child(_rule())
	var tag := UiKit.label("The Sigil has shattered. Its shards call heroes to your banner.", 15, UiKit.DIM)
	tag.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(tag)
	var grow := Control.new()
	grow.size_flags_vertical = Control.SIZE_EXPAND_FILL
	col.add_child(grow)
	var start := UiKit.button("Begin" if not Game.g["started"] else "Continue", func() -> void:
		if Game.g["started"]:
			show_home()
		else:
			show_starters())
	start.custom_minimum_size.y = 52
	col.add_child(start)
	var free := UiKit.label("100% free. No shop, no paid currency. Everything is earned by playing.", 12, UiKit.DIM)
	free.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(free)
	var ver := UiKit.label(Game.version_label(), 12, Color("#8a7f6c"))
	ver.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(ver)
	var pad := Control.new()
	pad.custom_minimum_size.y = 24
	col.add_child(pad)
	_set_screen(page)


func _rule() -> Control:
	var c := Control.new()
	c.custom_minimum_size = Vector2(0, 12)
	c.draw.connect(func() -> void:
		var w := 220.0
		var x0 := c.size.x / 2 - w / 2
		var steps := 24
		for i in steps:
			var t0 := float(i) / steps
			var a := 1.0 - absf(t0 - 0.5) * 2
			c.draw_line(Vector2(x0 + w * t0, 6), Vector2(x0 + w * (t0 + 1.0 / steps), 6), Color(UiKit.TRIM_HI, a), 1.0)
		c.draw_circle(Vector2(c.size.x / 2, 6), 3, UiKit.TRIM_HI))
	return c


# ---------- starter ----------
func show_starters() -> void:
	var pc := _scroll_page()
	var col: VBoxContainer = pc[1]
	col.add_child(UiKit.logo(34))
	var p := UiKit.label("Choose the first champion to answer.", 15, UiKit.DIM)
	p.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(p)
	for id in WebData.STARTERS:
		var t := Rules.hero(id)
		var card := UiKit.card()
		var row := UiKit.hbox(12)
		card.add_child(row)
		row.add_child(HeroCell.new({"uid": 0, "id": id, "level": 1, "stars": WebData.RARITY[t["rarity"]]["stars"]}))
		var info := UiKit.vbox(4)
		info.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		row.add_child(info)
		info.add_child(UiKit.heading(t["name"], 18))
		info.add_child(UiKit.label("%s · %s · %s" % [WebData.RARITY[t["rarity"]]["name"], WebData.ELEMENTS[t["el"]]["name"], WebData.ROLES[t["role"]]["name"]], 13, UiKit.DIM))
		var names := []
		for s in t["skills"]:
			names.append(s["name"])
		info.add_child(UiKit.label(" · ".join(names), 13))
		info.add_child(UiKit.button("Choose %s" % Rules.short_name(t["name"]), pick_starter.bind(id)))
		col.add_child(card)
	_set_screen(pc[0])


func pick_starter(id: String) -> void:
	Game.pick_starter(id)
	show_home()
	_modal("Welcome!", "%s answers your call, joined by Pell and Odo.\n\nYou also have %d Mystic and %d Worn shards: summon heroes on the home screen, then fight through Chapter 1." % [Rules.hero(id)["name"], Game.g["shards"]["mystic"], Game.g["shards"]["common"]], [["Let's go", _close_modal]])


# ---------- home ----------
func _update_banner() -> Control:
	var card := UiKit.card()
	var row := UiKit.hbox(8)
	card.add_child(row)
	var info := UiKit.vbox(2)
	info.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(info)
	info.add_child(UiKit.heading("Update available", 16))
	info.add_child(UiKit.label("v%s · build %d (you have build %d)" % [Updater.latest_version, Updater.latest_build, Updater.my_build()], 12, UiKit.DIM))
	var b := UiKit.button("Download", Updater.download)
	b.size_flags_vertical = Control.SIZE_SHRINK_CENTER
	row.add_child(b)
	return card


# A newer build showed up (or went away): redraw Home now, without a restart. Mid-battle the
# banner waits until the player is back on Home.
func _on_update_changed() -> void:
	if battle_view == null and screen and screen.get_meta("home", false):
		show_home()


func show_home() -> void:
	var g := Game.g
	var pc := _scroll_page()
	var col: VBoxContainer = pc[1]
	col.add_child(UiKit.logo(32))
	if Updater.latest_build > 0:
		col.add_child(_update_banner())
	var stats := UiKit.label("Gold %s   ·   Worn %d   ·   Mystic %d   ·   %d wins" % [Rules.fmt(g["gold"]), g["shards"]["common"], g["shards"]["mystic"], g["stats"]["wins"]], 13, UiKit.DIM)
	stats.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(stats)
	# Team
	var team_card := UiKit.card()
	var tc := UiKit.vbox(8)
	team_card.add_child(tc)
	var th := UiKit.hbox()
	tc.add_child(th)
	var tt := UiKit.heading("Your Team", 18)
	tt.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	th.add_child(tt)
	th.add_child(UiKit.label("Power %s" % Rules.fmt(Rules.team_power(g)), 13, UiKit.GOLD))
	var cells := UiKit.hbox(6)
	cells.alignment = BoxContainer.ALIGNMENT_CENTER
	tc.add_child(cells)
	for h in Game.team():
		var cell := HeroCell.new(h, true)
		cell.tapped.connect(show_hero)
		cells.add_child(cell)
	tc.add_child(UiKit.label("Tap a hero to train them or change the team (up to 4).", 12, UiKit.DIM))
	col.add_child(team_card)
	# Campaign
	var camp := UiKit.card()
	var cc := UiKit.vbox(8)
	camp.add_child(cc)
	cc.add_child(UiKit.heading("Chapter 1 · %s" % WebData.CHAPTERS[0], 18))
	var cleared := int(g["cleared"])
	if stage_sel < 0 or stage_sel > mini(cleared, 5):
		stage_sel = Game.next_stage()
	var grid := GridContainer.new()
	grid.columns = 3
	grid.add_theme_constant_override("h_separation", 8)
	grid.add_theme_constant_override("v_separation", 8)
	cc.add_child(grid)
	for idx in WebData.STAGES_PER_CHAPTER:
		var b := Button.new()
		b.focus_mode = Control.FOCUS_NONE
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		b.custom_minimum_size.y = 48
		var boss := Rules.is_boss_stage(idx)
		b.text = Rules.stage_label(idx) + ("  Boss" if boss else "") + ("\nCleared" if idx < cleared else ("\nNext" if idx == cleared else "\nLocked"))
		b.add_theme_font_size_override("font_size", 13)
		if idx != stage_sel:
			b.theme_type_variation = "SecButton"
		b.disabled = idx > cleared
		b.pressed.connect(func() -> void:
			stage_sel = idx
			show_home())
		grid.add_child(b)
	var lvl := Rules.stage_level(stage_sel)
	cc.add_child(UiKit.label("Stage %s · enemy level %d · %d waves%s" % [Rules.stage_label(stage_sel), lvl, 3 if Rules.is_boss_stage(stage_sel) else 2, " · Warlord boss" if Rules.is_boss_stage(stage_sel) else ""], 13, UiKit.DIM))
	var fight := UiKit.button("Battle %s" % Rules.stage_label(stage_sel), func() -> void: start_battle(stage_sel))
	fight.custom_minimum_size.y = 52
	cc.add_child(fight)
	if cleared >= WebData.STAGES_PER_CHAPTER:
		cc.add_child(UiKit.label("Chapter 1 complete! The full game continues in the web app.", 13, UiKit.GOOD))
	col.add_child(camp)
	# Heroes not in the team
	var others := []
	for h in g["heroes"]:
		if not g["team"].has(int(h["uid"])):
			others.append(h)
	if not others.is_empty():
		var rc := UiKit.card()
		var rcol := UiKit.vbox(8)
		rc.add_child(rcol)
		rcol.add_child(UiKit.heading("Heroes", 18))
		var flow := HFlowContainer.new()
		flow.add_theme_constant_override("h_separation", 6)
		flow.add_theme_constant_override("v_separation", 6)
		rcol.add_child(flow)
		for h in others:
			var cell := HeroCell.new(h)
			cell.tapped.connect(show_hero)
			flow.add_child(cell)
		col.add_child(rc)
	# Summon
	var sm := UiKit.card()
	var scol := UiKit.vbox(8)
	sm.add_child(scol)
	scol.add_child(UiKit.heading("Summon", 18))
	scol.add_child(UiKit.label("Shards come from first clears. Mystic shards guarantee a Legendary within 60 summons.", 12, UiKit.DIM))
	var srow := UiKit.hbox(8)
	scol.add_child(srow)
	for k in ["common", "mystic"]:
		var b := UiKit.button("%s (%d)" % [WebData.SHARDS[k]["name"], g["shards"][k]], do_summon.bind(k), k == "common")
		b.size_flags_horizontal = Control.SIZE_EXPAND_FILL
		b.disabled = int(g["shards"][k]) < 1
		srow.add_child(b)
	col.add_child(sm)
	# Settings
	var st := UiKit.card()
	var stc := UiKit.vbox(8)
	st.add_child(stc)
	stc.add_child(UiKit.heading("Settings", 18))
	var row := UiKit.hbox(8)
	stc.add_child(row)
	var cool := UiKit.button("Cooler (30 fps)", func() -> void: pass, true)
	cool.toggle_mode = true
	cool.button_pressed = int(g["settings"].get("fpsCap", 60)) == 30
	cool.toggled.connect(func(on: bool) -> void:
		g["settings"]["fpsCap"] = 30 if on else 60
		Game.apply_fps()
		Game.save())
	cool.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(cool)
	var eq := UiKit.button("Auto-equip gear", func() -> void:
		Game.auto_equip_team()
		show_home(), true)
	eq.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(eq)
	stc.add_child(UiKit.label("Gear: %d pieces. Equipped gear adds to hero stats exactly as in the web game." % g["gear"].size(), 12, UiKit.DIM))
	stc.add_child(UiKit.button("Reset demo save", func() -> void:
		_modal("Reset?", "Start the demo over from the starter pick?", [["Cancel", _close_modal], ["Reset", func() -> void:
			Game.reset_save()
			stage_sel = -1
			show_title()]]), true))
	col.add_child(st)
	var ver := UiKit.label("Sigilbound — Godot demo · " + Game.version_label(), 12, Color("#8a7f6c"))
	ver.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(ver)
	pc[0].set_meta("home", true)
	_set_screen(pc[0])


func show_hero(u: int) -> void:
	var g := Game.g
	var h := Rules.hero_by_uid(g, u)
	if h.is_empty():
		return
	var t := Rules.hero(h["id"])
	var st := Rules.hero_stats(g, h)
	var lines := []
	lines.append("%s · %s · %s · %s" % [WebData.RARITY[t["rarity"]]["name"], WebData.ELEMENTS[t["el"]]["name"], WebData.ROLES[t["role"]]["name"], UiKit.stars(h["stars"])])
	var cap := Rules.max_level_for(int(h["stars"]))
	lines.append("Level %d / %d   ·   XP %d / %d" % [h["level"], cap, h["xp"], Rules.xp_to_next(int(h["level"]))])
	lines.append("HP %d   ATK %d   DEF %d   SPD %d   Crit %d%%   Power %s" % [st["hp"], st["atk"], st["def"], st["spd"], st["crit"], Rules.fmt(Rules.power(st))])
	lines.append("")
	for s in t["skills"]:
		lines.append("%s  (%s%s)" % [s["name"], _target_hint(s["target"]), ", cooldown %d" % s["cd"] if s["cd"] > 0 else ""])
	var in_team: bool = g["team"].has(u)
	var buttons := []
	if int(h["level"]) < cap:
		buttons.append(["Train (%s gold)" % Rules.fmt(Rules.train_cost(h)), func() -> void:
			if Game.train(h):
				show_home()
				show_hero(u)
			else:
				_toast("Not enough gold.")])
	buttons.append(["Remove from team" if in_team else "Add to team", func() -> void:
		var msg := Game.toggle_team(u)
		show_home()
		if msg != "":
			_toast(msg)])
	buttons.append(["Close", _close_modal])
	_modal(t["name"], "\n".join(lines), buttons)


static func _target_hint(t: String) -> String:
	return {"enemy": "one enemy", "allEnemies": "all enemies", "ally": "one ally", "allAllies": "all allies"}.get(t, "self")


func do_summon(k: String) -> void:
	var h := Game.summon(k)
	if h.is_empty():
		return
	var t := Rules.hero(h["id"])
	show_home()
	_modal("Summoned!", "%s\n%s · %s · %s" % [t["name"], WebData.RARITY[t["rarity"]]["name"], WebData.ELEMENTS[t["el"]]["name"], WebData.ROLES[t["role"]]["name"]],
		[["Add to team", func() -> void:
			var msg := Game.toggle_team(int(h["uid"]))
			show_home()
			if msg != "":
				_toast(msg)], ["OK", _close_modal]])


# ---------- battle ----------
func start_battle(idx: int) -> void:
	if Game.team().is_empty():
		_toast("Choose at least one hero.")
		return
	var b := Game.make_battle(idx)
	var page := Control.new()
	page.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	_set_screen(page)
	battle_view = BattleView.new()
	battle_view.setup(b, idx)
	battle_view.finished.connect(_on_battle_finished.bind(idx))
	battle_view.quit_requested.connect(func() -> void:
		_modal("Retreat?", "Leave this battle? It counts as a defeat (no rewards).", [["Stay", _close_modal], ["Retreat", func() -> void: show_home()]]))
	page.add_child(battle_view)


func _on_battle_finished(result: String, idx: int) -> void:
	var r := Game.finish_stage(idx, result)
	_last_result = r
	if result == "victory":
		var rew: Dictionary = r["rewards"]
		var body := "%s\n+%d XP each%s" % [Rules.reward_text(rew), rew["xp"], " · First clear bonus!" if r["first"] else ""]
		if not r["ups"].is_empty():
			body += "\n\n" + "\n".join(r["ups"])
		var gear: Dictionary = r["gear"]
		if not gear.is_empty():
			body += "\n\nGear found: %s %s (%s set)" % [WebData.GEAR_RARITY[gear["rarity"]], WebData.GEAR_SLOTS[gear["slot"]]["name"], WebData.GEAR_SETS[gear["set"]]["name"]]
		var buttons := []
		if idx + 1 < WebData.STAGES_PER_CHAPTER and int(Game.g["cleared"]) > idx:
			buttons.append(["Next Stage", func() -> void:
				stage_sel = idx + 1
				start_battle(idx + 1)])
		buttons.append(["Replay", func() -> void: start_battle(idx)])
		buttons.append(["Home", func() -> void:
			stage_sel = mini(int(Game.g["cleared"]), 5)
			show_home()])
		_modal("VICTORY", body, buttons, UiKit.GOLD)
	else:
		_modal("DEFEAT", "Tips: train heroes with gold, summon new ones with your shards, equip gear, and bring elements that beat the enemy.",
			[["Retry", func() -> void: start_battle(idx)], ["Home", func() -> void: show_home()]], UiKit.BAD)


# ---------- modal & toast ----------
func _modal(title: String, body: String, buttons: Array, title_color: Color = Color("#f6e6c2")) -> void:
	_close_modal()
	var dim := ColorRect.new()
	dim.color = Color(0, 0, 0, 0.6)
	dim.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	dim.theme = UiKit.theme
	modal_layer.add_child(dim)
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	dim.add_child(center)
	var box := UiKit.card()
	var sb: StyleBoxFlat = UiKit.theme.get_stylebox("panel", "PanelContainer").duplicate()
	sb.bg_color = Color("#231b2f")
	sb.border_color = Color("#7a5f35")
	sb.set_border_width_all(2)
	sb.shadow_size = 24
	for side in [SIDE_LEFT, SIDE_RIGHT, SIDE_TOP, SIDE_BOTTOM]:
		sb.set_content_margin(side, 20)
	box.add_theme_stylebox_override("panel", sb)
	box.custom_minimum_size.x = 340
	center.add_child(box)
	var col := UiKit.vbox(12)
	box.add_child(col)
	var t := UiKit.heading(title, 26 if title.to_upper() == title else 20)
	t.add_theme_color_override("font_color", title_color)
	t.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(t)
	var b := UiKit.label(body, 14)
	b.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	b.custom_minimum_size.x = 300
	col.add_child(b)
	for i in buttons.size():
		col.add_child(UiKit.button(buttons[i][0], buttons[i][1], i > 0))


func _close_modal() -> void:
	for c in modal_layer.get_children():
		c.queue_free()


func _toast(text: String) -> void:
	var l := UiKit.label(text, 14, Color("#f6e6c2"))
	var p := PanelContainer.new()
	p.theme = UiKit.theme
	p.add_child(l)
	p.set_anchors_and_offsets_preset(Control.PRESET_CENTER_BOTTOM)
	p.position = Vector2(get_viewport().get_visible_rect().size.x / 2 - 150, get_viewport().get_visible_rect().size.y - 120)
	p.custom_minimum_size.x = 300
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	var layer := CanvasLayer.new()
	layer.layer = 20
	add_child(layer)
	layer.add_child(p)
	get_tree().create_timer(2.2).timeout.connect(layer.queue_free)


# ---------- scripted runs ----------
func _press(text_prefix: String) -> bool:
	for layer in [modal_layer, ui]:
		for b in layer.find_children("*", "Button", true, false):
			if b.is_visible_in_tree() and not b.disabled and b.text.begins_with(text_prefix):
				b.pressed.emit()
				return true
	return false


func _frames(n: int) -> void:
	for i in n:
		await get_tree().process_frame


func _smoke() -> void:
	var watchdog := get_tree().create_timer(600.0, true, false, true)
	watchdog.timeout.connect(func() -> void:
		print("SMOKE FAILED: timed out")
		get_tree().quit(1))
	await _frames(2)
	if not _press("Begin"):
		print("SMOKE FAILED: no Begin button")
		get_tree().quit(1)
		return
	await _frames(2)
	_press("Choose Ysolde")
	await _frames(2)
	_press("Let's go")
	await _frames(2)
	if Game.team().size() != 3:
		print("SMOKE FAILED: starter team not created")
		get_tree().quit(1)
		return
	Game.g["settings"]["auto"] = true
	Game.g["settings"]["speed"] = 3
	_press("Battle 1-1")
	await _frames(2)
	if battle_view == null:
		print("SMOKE FAILED: battle did not start")
		get_tree().quit(1)
		return
	var turns_before := 0
	await battle_view.finished
	var b := battle_view.battle
	turns_before = b.turn
	await _frames(3)
	var r := _last_result
	print("battle finished: %s after %d turns, %d hero models, cleared=%d gold=%d" % [b.result, turns_before, b.allies.size(), Game.g["cleared"], Game.g["gold"]])
	if not (b.over and r.get("result", "") == b.result):
		print("SMOKE FAILED: no result")
		get_tree().quit(1)
		return
	if not _press("Home"):
		print("SMOKE FAILED: no Home button on the result")
		get_tree().quit(1)
		return
	await _frames(2)
	print("SMOKE OK result=%s turns=%d" % [b.result, turns_before])
	get_tree().quit(0)


func _shot(name: String) -> void:
	await RenderingServer.frame_post_draw
	var img := get_viewport().get_texture().get_image()
	DirAccess.make_dir_recursive_absolute(Game.shots_dir)
	img.save_png(Game.shots_dir.path_join(name + ".png"))
	print("shot ", name)


func _shots() -> void:
	await _frames(10)
	await _shot("1-title")
	_press("Begin")
	await _frames(10)
	await _shot("2-starter")
	_press("Choose Hask")
	await _frames(10)
	await _shot("3-welcome")
	_press("Let's go")
	await _frames(10)
	await _shot("4-home")
	Game.g["settings"]["auto"] = false
	Game.g["settings"]["speed"] = 1
	_press("Battle 1-1")
	await get_tree().create_timer(2.5).timeout
	await _shot("5-battle-manual")
	battle_view._auto_btn.button_pressed = true
	battle_view._on_auto()
	await get_tree().create_timer(5.0).timeout
	await _shot("6-battle")
	await get_tree().create_timer(2.3).timeout
	await _shot("7-battle-b")
	battle_view._on_speed(3)
	await battle_view.finished
	await _frames(10)
	await _shot("8-result")
	get_tree().quit(0)


# Checks that a finger drag scrolls a page even when it starts on a button or a hero card.
func _scroll_test() -> void:
	await _frames(2)
	_press("Begin")
	await _frames(2)
	_press("Choose Ysolde")
	await _frames(2)
	_press("Let's go")
	await _frames(3)
	var sc: ScrollContainer = screen.find_children("*", "ScrollContainer", true, false)[0]
	var vp := get_viewport()
	var h := vp.get_visible_rect().size
	var start := Vector2(h.x * 0.5, h.y * 0.75)
	var xf := vp.get_final_transform()
	var target := vp.gui_get_focus_owner()
	var t := InputEventScreenTouch.new()
	t.index = 0; t.position = xf * start; t.pressed = true
	vp.push_input(t)
	await _frames(1)
	var pos := start
	for i in 20:
		pos.y -= 20.0
		var d := InputEventScreenDrag.new()
		d.index = 0; d.position = xf * pos; d.relative = Vector2(0, -20); d.screen_relative = Vector2(0, -20)
		vp.push_input(d)
		await _frames(1)
	var r := InputEventScreenTouch.new()
	r.index = 0; r.position = xf * pos; r.pressed = false
	vp.push_input(r)
	await _frames(3)
	print("SCROLL max=%d now=%d screen=%s" % [int(sc.get_v_scroll_bar().max_value - sc.size.y), sc.scroll_vertical, str(screen.get_class())])
	print("SCROLL OK" if sc.scroll_vertical > 0 else "SCROLL FAILED")
	get_tree().quit(0 if sc.scroll_vertical > 0 else 1)
