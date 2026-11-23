## Web-parity tests for the GDScript rules port. Fixtures come from the web game's own code
## (tests/make-fixtures.cjs). Run headless; exits 0 when everything matches, 1 otherwise:
##   godot --headless --path godot --script res://tests/run_tests.gd
extends SceneTree

var passed := 0
var failed := 0


func check(ok: bool, what: String) -> void:
	if ok:
		passed += 1
		print("  ok   ", what)
	else:
		failed += 1
		print("  FAIL ", what)


func _init() -> void:
	var text := FileAccess.get_file_as_string("res://tests/fixtures/parity.json")
	var fx = JSON.parse_string(text)
	if typeof(fx) != TYPE_DICTIONARY:
		print("could not read tests/fixtures/parity.json")
		quit(1)
		return
	print("Sigilbound Godot parity tests (fixtures from public/game/*.js)")
	test_data(fx["data"])
	test_hero_stats(fx["heroStats"])
	test_xp(fx["xp"])
	test_stages(fx["stages"])
	test_battles(fx["battles"])
	test_rules_misc()
	test_role_colors()
	test_quality_and_kit()
	print("%d passed, %d failed" % [passed, failed])
	quit(0 if failed == 0 else 1)


## Deep comparison of JSON-like values; numbers compare by value (JSON makes every number a float).
static func same(a: Variant, b: Variant, path: String = "") -> String:
	var ta := typeof(a)
	var tb := typeof(b)
	var num := [TYPE_INT, TYPE_FLOAT]
	if ta in num and tb in num:
		return "" if absf(float(a) - float(b)) <= 1e-9 * maxf(1.0, absf(float(a))) else "%s: %s vs %s" % [path, a, b]
	if ta == TYPE_DICTIONARY and tb == TYPE_DICTIONARY:
		var keys := {}
		for k in a:
			keys[k] = true
		for k in b:
			keys[k] = true
		for k in keys:
			if not a.has(k) or not b.has(k):
				return "%s.%s missing on one side" % [path, k]
			var d := same(a[k], b[k], "%s.%s" % [path, k])
			if d != "":
				return d
		return ""
	if ta == TYPE_ARRAY and tb == TYPE_ARRAY:
		if a.size() != b.size():
			return "%s: length %d vs %d" % [path, a.size(), b.size()]
		for i in a.size():
			var d := same(a[i], b[i], "%s[%d]" % [path, i])
			if d != "":
				return d
		return ""
	if ta == TYPE_STRING_NAME or tb == TYPE_STRING_NAME:
		return "" if str(a) == str(b) else "%s: %s vs %s" % [path, a, b]
	return "" if ta == tb and a == b else "%s: %s vs %s" % [path, a, b]


func test_data(d: Dictionary) -> void:
	print("Data")
	var heroes := []
	for h in WebData.HEROES:
		heroes.append({"id": h["id"], "name": h["name"], "rarity": h["rarity"], "el": h["el"], "role": h["role"], "face": h["face"], "skills": h["skills"]})
	var diff := same(heroes, d["heroes"], "heroes")
	check(diff == "", "all %d heroes and their skills match data.js %s" % [heroes.size(), diff])
	check(same(WebData.STARTERS, d["starters"]) == "" and same(WebData.ENEMY_CURVE, d["curve"]) == "" and same(WebData.CHAPTERS, d["chapters"]) == "",
		"starters, enemy curve and chapters match")
	check(same(WebData.ROLES, _drop(d["roles"], "icon")) == "" and same(WebData.RARITY, d["rarity"]) == "", "roles and rarity tables match")
	var els := {}
	for k in WebData.ELEMENTS:
		els[k] = {"name": WebData.ELEMENTS[k]["name"], "color": WebData.ELEMENTS[k]["color"], "strong": WebData.ELEMENTS[k]["strong"]}
	var effs := {}
	for k in WebData.EFFECTS:
		effs[k] = {"name": WebData.EFFECTS[k]["name"], "buff": WebData.EFFECTS[k]["buff"]}
	check(same(els, d["elements"]) == "" and same(effs, d["effects"]) == "", "elements and effects match")
	var slots := {}
	for k in WebData.GEAR_SLOTS:
		slots[k] = {"stat": WebData.GEAR_SLOTS[k]["stat"], "base": WebData.GEAR_SLOTS[k]["base"]}
	var sets := {}
	for k in WebData.GEAR_SETS:
		sets[k] = {"stat": WebData.GEAR_SETS[k]["stat"], "val": WebData.GEAR_SETS[k]["val"]}
	check(same(slots, d["gearSlots"]) == "" and same(sets, d["gearSets"]) == "", "gear slots and sets match")


static func _drop(o: Dictionary, key: String) -> Dictionary:
	var out := {}
	for k in o:
		var v: Dictionary = o[k].duplicate()
		v.erase(key)
		out[k] = v
	return out


func test_hero_stats(list: Array) -> void:
	print("Hero stats")
	var bad := []
	for row in list:
		var g := Rules.new_state()
		var gear := {}
		for gr in row["gear"]:
			var piece: Dictionary = gr.duplicate(true)
			g["gear"].append(piece)
			gear[piece["slot"]] = int(piece["uid"])
		var h := {"uid": 1, "id": row["id"], "level": int(row["level"]), "stars": int(row["stars"]), "gear": gear}
		var st := Rules.hero_stats(g, h)
		var want: Dictionary = row["stats"]
		var ok := true
		for k in ["hp", "atk", "def", "spd", "crit", "cdmg"]:
			if same(st[k], want[k]) != "":
				ok = false
		if same(st["sets"], want["sets"]) != "" or same(Rules.power(st), row["power"]) != "":
			ok = false
		if not ok:
			bad.append("%s: godot %s/%s/%s/%s p%s vs web %s/%s/%s/%s p%s" % [row["id"], st["hp"], st["atk"], st["def"], st["spd"], Rules.power(st), want["hp"], want["atk"], want["def"], want["spd"], row["power"]])
	for b in bad:
		print("     ", b)
	check(bad.is_empty(), "stats and power match the web for %d/%d heroes (levels, stars, gear, set bonuses)" % [list.size() - bad.size(), list.size()])


func test_xp(list: Array) -> void:
	print("XP")
	var ok := true
	for row in list:
		var h := {"level": int(row["level"]), "xp": 0, "stars": int(row["stars"])}
		var ups := Rules.give_xp(h, int(row["amount"]))
		if ups != int(row["ups"]) or h["level"] != int(row["after"]["level"]) or h["xp"] != int(row["after"]["xp"]):
			ok = false
			print("     Lv%d %d★ +%d: godot Lv%d xp%d (%d ups) vs web %s" % [row["level"], row["stars"], row["amount"], h["level"], h["xp"], ups, row["after"]])
	check(ok, "giveXp level-ups and caps match (%d cases)" % list.size())


func test_stages(list: Array) -> void:
	print("Stages")
	var bad := 0
	for st in list:
		var idx := int(st["idx"])
		var label := Rules.stage_label(idx)
		if Rules.stage_level(idx) != int(st["level"]):
			bad += 1
			print("     %s level differs" % label)
		var gr := Rules.enemy_grade(idx)
		var d := same(gr, st["grade"])
		if d != "":
			bad += 1
			print("     %s grade %s" % [label, d])
		for first in [true, false]:
			d = same(Rules.stage_rewards(idx, first), st["rewardsFirst" if first else "rewards"])
			if d != "":
				bad += 1
				print("     %s rewards %s" % [label, d])
		var waves := Rules.stage_waves(idx)
		var mine := []
		for w in waves:
			var row := []
			for e in w:
				var u := SbBattle.unit_from_enemy(e)
				row.append({"id": e["template"]["id"], "el": e["el"], "boss": e.get("boss", false), "shownLevel": e["shownLevel"], "name": u.name,
					"hp": u.max_hp, "atk": u.atk, "def": u.def, "spd": u.spd, "crit": u.crit, "cdmg": u.cdmg})
			mine.append(row)
		d = same(mine, st["waves"], label)
		if d != "":
			bad += 1
			print("     waves ", d)
	check(bad == 0, "stage levels, enemy grades, waves (heroes, elements, bosses, HP/ATK/DEF/SPD) and rewards match for %d stages" % list.size())


func test_battles(list: Array) -> void:
	print("Battles (seeded, turn by turn)")
	for bt in list:
		var g := Rules.new_state()
		for gr in bt["gear"]:
			g["gear"].append(gr.duplicate(true))
		var team := []
		for hh in bt["heroes"]:
			var h := {"uid": int(hh["uid"]), "id": hh["id"], "level": int(hh["level"]), "stars": int(hh["stars"]), "gear": {}}
			for s in hh["gear"]:
				h["gear"][s] = int(hh["gear"][s])
			g["heroes"].append(h)
			team.append(h)
		SbRng.use_seed(int(bt["seed"]))
		SbBattle.next_n = 0
		var allies := []
		for h in team:
			allies.append(SbBattle.unit_from_hero(g, h))
		var waves := []
		for w in Rules.stage_waves(int(bt["idx"])):
			var row := []
			for e in w:
				row.append(SbBattle.unit_from_enemy(e))
			waves.append(row)
		var b := SbBattle.new(allies, waves)
		var all := allies.duplicate()
		for w in waves:
			all.append_array(w)
		var start := []
		for u in all:
			start.append({"key": u.key, "tm": u.tm, "hp": u.max_hp})
		var what := "%s Lv%d at stage %s (seed %d)" % [bt["starter"], bt["level"], Rules.stage_label(int(bt["idx"])), bt["seed"]]
		var problem := same(start, bt["start"], "start")
		var n := 0
		if problem == "":
			for want in bt["turns"]:
				var u := b.next_actor()
				if u == null or u.key != want["actor"]:
					problem = "turn %d: actor %s vs %s" % [n, u.key if u else "none", want["actor"]]
					break
				var begin := b.begin_turn(u)
				problem = same(_events(begin["events"]), _events(want["begin"]), "turn %d begin" % n)
				if problem != "":
					break
				if begin["skip"]:
					if not want.get("skip", false):
						problem = "turn %d: skipped here only" % n
						break
				else:
					var choice := b.ai_choose(u)
					var wt = want.get("target")
					if choice["skill"] != int(want["skill"]) or choice["target"] != (wt if wt != null else ""):
						problem = "turn %d: %s chose skill %d -> %s vs %s -> %s" % [n, u.key, choice["skill"], choice["target"], want["skill"], wt]
						break
					var ev := b.act(u, choice["skill"], choice["target"])
					problem = same(_events(ev), _events(want["events"]), "turn %d events" % n)
					if problem != "":
						break
				var end := b.end_turn(u)
				if end != (want["end"] if want["end"] != null else ""):
					problem = "turn %d: end %s vs %s" % [n, end, want["end"]]
					break
				var hps := []
				for x in b.units():
					hps.append(x.hp)
				problem = same(hps, want["hp"], "turn %d hp" % n)
				if problem != "":
					break
				n += 1
		if problem == "" and (b.result != bt["result"] or b.wave != int(bt["wave"])):
			problem = "result %s (wave %d) vs %s (wave %s)" % [b.result, b.wave, bt["result"], bt["wave"]]
		if problem != "":
			print("     ", problem)
		check(problem == "", "%s: %s in %d turns, identical turn by turn" % [what, bt["result"], bt["turns"].size()])
	SbRng.use_system()


## Events as comparable dictionaries (the turn-meter texts carry an emoji on the web).
static func _events(list: Array) -> Array:
	var out := []
	for e in list:
		var c: Dictionary = e.duplicate()
		if c.has("text") and String(c["text"]).contains("Turn Meter"):
			c["text"] = "Turn Meter"
		out.append(c)
	return out


func test_rules_misc() -> void:
	print("Rules")
	var g := Rules.new_state()
	check(g["gold"] == 5000 and g["shards"]["mystic"] == 3 and g["shards"]["common"] == 5, "a new save starts with 5000 gold, 3 Mystic and 5 Worn shards")
	var h := Rules.add_hero(g, "ysolde")
	check(h["stars"] == 2 and h["uid"] == 1 and g["nextUid"] == 2, "addHero gives a Rare 2★ hero and the next uid")
	SbRng.use_seed(77)
	var got := Rules.summon(g, "mystic", 3)
	check(got.size() == 3 and g["shards"]["mystic"] == 0 and g["stats"]["summons"] == 3, "summoning spends shards and records summons")
	var none := Rules.summon(g, "mystic", 1)
	check(none.is_empty(), "no summon without shards")
	SbRng.use_system()
	var round_trip := Rules.normalize(JSON.parse_string(JSON.stringify(g)))
	check(same(round_trip, g) == "" and typeof(round_trip["nextUid"]) == TYPE_INT, "a save survives a JSON round trip with ints restored")
	check(Rules.stage_label(0) == "1-1" and Rules.stage_label(5) == "1-6" and Rules.is_boss_stage(5) and not Rules.is_boss_stage(4), "stage labels and the boss stage")
	check(BattleLayout.slot(true, 1, 3).distance_to(Vector3(0, 0, -0.6 + 3.95)) < 1e-6, "the middle ally stands on the near rim of the rune circle")
	var a := BattleLayout.slot(true, 0, 3)
	var b := BattleLayout.slot(true, 1, 3)
	var arc_step := 2.2 / ((4.2 + 3.95) / 2)
	check(absf(a.distance_to(b) - 2.0 * sin(arc_step / 2) * 4.0) < 0.15, "neighbours are about 2.2 m apart along the rim")
	var e := BattleLayout.slot(false, 1, 3)
	check(e.distance_to(Vector3(0, 0, -0.6 - 3.95)) < 1e-6, "the middle enemy stands on the far rim")
	var cam := BattleLayout.camera_preset("angle")
	check(cam["yaw"] == 38 and cam["pitch"] == 30, "the default camera is Three-quarter (yaw 38°, pitch 30°)")
	var dist := BattleLayout.fit_distance(cam, 412.0 / 915.0, false)
	check(dist > 5 and dist < 40, "the camera fits a portrait frame (%.2f m)" % dist)
	var c := LookColors.role_colors(LooksData.LOOKS["ysolde"], false)
	check(c["armor"] == "#5a1e1e" and c["eye"] == "#ffb347", "hero colours come from art.js LOOKS")


func test_role_colors() -> void:
	print("Hero looks")
	var want = JSON.parse_string(FileAccess.get_file_as_string("res://tests/fixtures/role-colors.json"))
	var bad := []
	for id in want:
		for hollow in [false, true]:
			var d := same(LookColors.role_colors(LooksData.LOOKS[id], hollow), want[id]["hollow" if hollow else "hero"], id)
			if d != "":
				bad.append(d)
	for b in bad.slice(0, 6):
		print("     ", b)
	check(bad.is_empty() and want.size() == 24, "model colours (24 heroes and their Hollow copies) match three.js roleColors()")


func test_quality_and_kit() -> void:
	print("Graphics tiers and hero kit")
	var cases := {"Adreno (TM) 830": "Ultra", "Adreno (TM) 740": "Ultra", "Adreno (TM) 730": "Ultra", "Adreno (TM) 650": "High",
		"Mali-G715 MC11": "Ultra", "Mali-G77 MC9": "High", "Immortalis-G720": "Ultra", "Samsung Xclipse 940": "Ultra",
		"Samsung Xclipse 920": "High", "llvmpipe (LLVM 20.1.2, 256 bits)": "Low", "": "High"}
	var bad := []
	for k in cases:
		if Quality.tier_for_gpu(k) != cases[k]:
			bad.append("%s -> %s" % [k, Quality.tier_for_gpu(k)])
	check(bad.is_empty(), "Auto picks Ultra on Adreno 730+, Mali-G715+/Immortalis and Xclipse 940+, else High %s" % [bad])
	var sockets := 0
	var heroes := 0
	var with_fx := 0
	var gear := {"weapon": {"r": 5, "s": "fury"}, "helm": {"r": 4, "s": "keen"}, "chest": {"r": 3, "s": "vigor"}, "boots": {"r": 2, "s": "haste"}}
	for h in WebData.HEROES:
		for hollow in [false, true]:
			var m := HeroModel.new()
			m.build(h["id"], hollow, h["el"], gear if not hollow else null)
			heroes += 1
			sockets += m.find_children("*", "BoneAttachment3D", true, false).size()
			if not m._fx.is_empty():
				with_fx += 1
			m.free()
	check(heroes == 48 and sockets >= 48 * 3, "all 24 heroes (and Hollow copies) build with their kit: %d bone sockets" % sockets)
	check(with_fx >= 10, "animated kit parts (capes, glows, tails) on %d builds" % with_fx)
