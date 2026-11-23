## Autoload "Game": the save state (the web's `G`), saved locally as JSON in user://, plus the
## player actions the demo offers. Rules live in Rules / SbBattle; this file only wires them up
## the way public/game/ui.js does (pickStarter, finishBattle, summon, train, team edits).
extends Node

const VERSION := "0.1.0"
## Stamped by CI (.github/workflows/sigilbound-godot.yml): "build <run> · <commit>". "dev" locally.
const BUILD := "dev"
const SAVE_PATH := "user://sigilbound_godot_save.json"

var g: Dictionary = Rules.new_state()
var save_path := SAVE_PATH
var smoke := false
var shots_dir := ""


func _ready() -> void:
	var args := OS.get_cmdline_user_args()
	smoke = args.has("--smoke")
	for a in args:
		if a.begins_with("--shots="):
			shots_dir = a.substr(8)
	if smoke or shots_dir != "":
		# Scripted runs never touch the player's real save.
		save_path = "user://sigilbound_godot_smoke.json"
		g = Rules.new_state()
		save()
	else:
		load_save()
	SbRng.use_system()
	apply_fps()


func version_label() -> String:
	return "v%s · %s · Godot %s" % [VERSION, BUILD, Engine.get_version_info()["string"]]


func load_save() -> void:
	g = Rules.new_state()
	if not FileAccess.file_exists(save_path):
		return
	var data = JSON.parse_string(FileAccess.get_file_as_string(save_path))
	if typeof(data) == TYPE_DICTIONARY and data.get("heroes") is Array:
		g = Rules.normalize(data)


func save() -> void:
	g["gameVersion"] = VERSION
	var f := FileAccess.open(save_path, FileAccess.WRITE)
	if f:
		f.store_string(JSON.stringify(g))


func reset_save() -> void:
	g = Rules.new_state()
	save()


func apply_fps() -> void:
	Engine.max_fps = 30 if int(g["settings"].get("fpsCap", 60)) == 30 else 60


# ---------- actions (ui.js) ----------
func pick_starter(id: String) -> void:
	var a := Rules.add_hero(g, id)
	var b := Rules.add_hero(g, "pell")
	var c := Rules.add_hero(g, "odo")
	g["team"] = [a["uid"], b["uid"], c["uid"]]
	g["started"] = true
	for h in [a, b, c]:
		Rules.equip(g, h, Rules.make_gear(g, 1, "weapon"))
	save()


func team() -> Array:
	return Rules.team_heroes(g)


func toggle_team(u: int) -> String:
	var t: Array = g["team"]
	if t.has(u):
		if t.size() <= 1:
			return "Keep at least one hero in the team."
		t.erase(u)
	elif t.size() < Rules.MAX_TEAM:
		t.append(u)
	else:
		return "Team is full (4). Tap a team member to remove them."
	save()
	return ""


func next_stage() -> int:
	return mini(int(g["cleared"]), WebData.STAGES_PER_CHAPTER - 1)


## Builds the battle for a campaign stage (startBattle in ui.js).
func make_battle(idx: int) -> SbBattle:
	var allies := []
	for h in team():
		allies.append(SbBattle.unit_from_hero(g, h))
	var waves := []
	for w in Rules.stage_waves(idx):
		var row := []
		for e in w:
			row.append(SbBattle.unit_from_enemy(e))
		waves.append(row)
	return SbBattle.new(allies, waves)


## finishBattle() for a stage: rewards, XP, gear drop, progress. Returns a summary for the result screen.
func finish_stage(idx: int, result: String) -> Dictionary:
	var out := {"result": result, "idx": idx}
	if result != "victory":
		g["settings"]["autoReplay"] = false
		save()
		return out
	var first := idx == int(g["cleared"])
	var r := Rules.stage_rewards(idx, first)
	Rules.grant(g, r)
	var ups := []
	for h in team():
		var n := Rules.give_xp(h, int(r["xp"]))
		if n:
			ups.append("%s → Lv %d" % [Rules.short_name(Rules.hero(h["id"])["name"]), h["level"]])
	var gear := Rules.roll_stage_gear(g, idx)
	if first:
		g["cleared"] = int(g["cleared"]) + 1
	g["stats"]["wins"] = int(g["stats"]["wins"]) + 1
	save()
	out.merge({"rewards": r, "first": first, "ups": ups, "gear": gear})
	return out


func summon(shard: String) -> Dictionary:
	var got := Rules.summon(g, shard, 1)
	save()
	return got[0] if not got.is_empty() else {}


func train(h: Dictionary) -> bool:
	var ok := Rules.train(g, h)
	if ok:
		save()
	return ok


func auto_equip_team() -> void:
	for h in team():
		Rules.auto_equip(g, h)
	save()
