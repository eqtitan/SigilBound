## Progression rules ported from public/game/state.js (and the helpers in data.js) for the subset
## the demo uses: the save shape, heroes, stats with gear, XP, gear drops, summoning and the
## campaign's stage waves and rewards. Functions take the save state `g` (the web's `G`), a
## Dictionary with the same fields and names, so it stays readable next to the JavaScript.
## Numbers follow JavaScript: Math.round is SbRng.js_round, divisions are float divisions.
class_name Rules

const TOTAL_STAGES := 60 # WebData.CHAPTERS.size() * STAGES_PER_CHAPTER
const TRAIN_XP_PER_GOLD := 2
const MAX_TEAM := 4

static var _hero_by_id := {}


static func hero(id: String) -> Dictionary:
	if _hero_by_id.is_empty():
		for h in WebData.HEROES:
			_hero_by_id[h["id"]] = h
	return _hero_by_id.get(id, {})


static func elem_mult(att: String, def: String) -> float:
	if WebData.ELEMENTS[att]["strong"] == def:
		return 1.3
	if WebData.ELEMENTS[def]["strong"] == att:
		return 0.8
	return 1.0


static func xp_to_next(level: int) -> int:
	return 50 + level * level * 12


static func max_level_for(stars: int) -> int:
	return stars * 10


static func short_name(full: String) -> String:
	# name.split(/[ ,]/)[0]
	var cut := full.length()
	for sep in [" ", ","]:
		var i := full.find(sep)
		if i >= 0 and i < cut:
			cut = i
	return full.substr(0, cut)


# ---------- save state ----------
static func new_state() -> Dictionary:
	return {
		"v": 1, "started": false, "nextUid": 1,
		"gold": 5000, "essence": 0,
		"shards": {"common": 5, "mystic": 3, "radiant": 0},
		"heroes": [], "gear": [], "team": [],
		"cleared": 0,
		"pity": {"mystic": 0, "radiant": 0},
		"login": {"last": "", "day": 0},
		"daily": null,
		"settings": {"speed": 1, "auto": false, "autoReplay": false, "fpsCap": 60},
		"stats": {"wins": 0, "summons": 0, "titanBest": 0},
	}


## Fills in missing fields (Object.assign(newState(), saved)) and turns JSON floats back into ints.
static func normalize(saved: Dictionary) -> Dictionary:
	var g := new_state()
	for k in saved:
		g[k] = saved[k]
	for k in ["nextUid", "gold", "essence", "cleared"]:
		g[k] = int(g[k])
	for k in g["shards"]:
		g["shards"][k] = int(g["shards"][k])
	for k in g["pity"]:
		g["pity"][k] = int(g["pity"][k])
	for k in g["stats"]:
		g["stats"][k] = int(g["stats"][k])
	var team := []
	for u in g["team"]:
		team.append(int(u))
	g["team"] = team
	for h in g["heroes"]:
		for k in ["uid", "level", "xp", "stars"]:
			h[k] = int(h[k])
		for s in h["gear"]:
			h["gear"][s] = int(h["gear"][s])
	for gr in g["gear"]:
		for k in ["uid", "rarity", "level"]:
			gr[k] = int(gr[k])
		for sub in gr["subs"]:
			sub["val"] = int(sub["val"])
	var defaults: Dictionary = new_state()["settings"]
	for k in defaults:
		if not g["settings"].has(k):
			g["settings"][k] = defaults[k]
	return g


static func uid(g: Dictionary) -> int:
	var u: int = g["nextUid"]
	g["nextUid"] = u + 1
	return u


static func hero_by_uid(g: Dictionary, u: int) -> Dictionary:
	for h in g["heroes"]:
		if int(h["uid"]) == u:
			return h
	return {}


static func gear_by_uid(g: Dictionary, u: int) -> Dictionary:
	for x in g["gear"]:
		if int(x["uid"]) == u:
			return x
	return {}


static func team_heroes(g: Dictionary) -> Array:
	var out := []
	for u in g["team"]:
		var h := hero_by_uid(g, int(u))
		if not h.is_empty():
			out.append(h)
	return out


static func grant(g: Dictionary, r: Dictionary) -> void:
	if r.get("gold", 0):
		g["gold"] += int(r["gold"])
	if r.get("essence", 0):
		g["essence"] += int(r["essence"])
	for k in ["common", "mystic", "radiant"]:
		if r.get(k, 0):
			g["shards"][k] += int(r[k])


static func reward_text(r: Dictionary) -> String:
	var parts := []
	if r.get("gold", 0):
		parts.append("%s gold" % fmt(r["gold"]))
	if r.get("essence", 0):
		parts.append("%d essence" % int(r["essence"]))
	for k in ["common", "mystic", "radiant"]:
		if r.get(k, 0):
			parts.append("%s x%d" % [WebData.SHARDS[k]["name"], int(r[k])])
	return "   ".join(parts)


static func fmt(n: float) -> String:
	if n >= 1e6:
		return "%.2fM" % (n / 1e6)
	if n >= 1e4:
		return "%.1fK" % (n / 1e3)
	var s := str(int(SbRng.js_round(n)))
	var out := ""
	while s.length() > 3:
		out = "," + s.substr(s.length() - 3) + out
		s = s.substr(0, s.length() - 3)
	return s + out


# ---------- heroes ----------
static func add_hero(g: Dictionary, id: String) -> Dictionary:
	var t := hero(id)
	var h := {"uid": uid(g), "id": id, "level": 1, "xp": 0, "stars": int(WebData.RARITY[t["rarity"]]["stars"]), "gear": {}, "locked": false}
	g["heroes"].append(h)
	return h


static func base_stats(template: Dictionary, level: float, stars: float, el: String = "") -> Dictionary:
	var r: Dictionary = WebData.RARITY[template["rarity"]]
	var role: Dictionary = WebData.ROLES[template["role"]]
	var tier := WebData.RARITY_KEYS.find(template["rarity"])
	var grow: float = (1.0 + (level - 1) * 0.05) * (1.0 + (stars - 1) * 0.15) * r["mult"]
	return {
		"hp": 1000 * role["hp"] * grow, "atk": 100 * role["atk"] * grow, "def": 90 * role["def"] * grow,
		"spd": 95 + tier * 2 + role["spd"] + (stars - 1), "crit": 15.0, "cdmg": 50.0,
		"el": el if el != "" else template["el"],
	}


static func gear_main_value(gr: Dictionary) -> float:
	return SbRng.js_round(WebData.GEAR_SLOTS[gr["slot"]]["base"] * (1 + gr["rarity"] * 0.25) * (1 + gr["level"] * 0.15))


static func hero_stats(g: Dictionary, h: Dictionary) -> Dictionary:
	var s := base_stats(hero(h["id"]), h["level"], h["stars"])
	var pct := {"atkPct": 0.0, "hpPct": 0.0, "defPct": 0.0, "spdPct": 0.0}
	var flat := {"atk": 0.0, "hp": 0.0, "def": 0.0, "spd": 0.0, "crit": 0.0, "cdmg": 0.0}
	var set_count := {}
	var set_order := []
	for slot in WebData.GEAR_SLOT_KEYS:
		if not h["gear"].has(slot):
			continue
		var gr := gear_by_uid(g, int(h["gear"][slot]))
		if gr.is_empty():
			continue
		flat[WebData.GEAR_SLOTS[slot]["stat"]] += gear_main_value(gr)
		for sub in gr["subs"]:
			if pct.has(sub["stat"]):
				pct[sub["stat"]] += sub["val"]
			else:
				flat[sub["stat"]] += sub["val"]
		if not set_count.has(gr["set"]):
			set_count[gr["set"]] = 0
			set_order.append(gr["set"])
		set_count[gr["set"]] += 1
	var sets := []
	for set_key in set_order:
		for i in int(set_count[set_key] / 2):
			var d: Dictionary = WebData.GEAR_SETS[set_key]
			if pct.has(d["stat"]):
				pct[d["stat"]] += d["val"]
			else:
				flat[d["stat"]] += d["val"]
			sets.append(set_key)
	return {
		"el": s["el"],
		"hp": SbRng.js_round((s["hp"] + flat["hp"]) * (1 + pct["hpPct"] / 100.0)),
		"atk": SbRng.js_round((s["atk"] + flat["atk"]) * (1 + pct["atkPct"] / 100.0)),
		"def": SbRng.js_round((s["def"] + flat["def"]) * (1 + pct["defPct"] / 100.0)),
		"spd": SbRng.js_round((s["spd"] + flat["spd"]) * (1 + pct["spdPct"] / 100.0)),
		"crit": minf(100.0, s["crit"] + flat["crit"]), "cdmg": s["cdmg"] + flat["cdmg"], "sets": sets,
	}


static func power(st: Dictionary) -> float:
	return SbRng.js_round(st["hp"] / 10.0 + st["atk"] * 2 + st["def"] * 1.5 + st["spd"] * 4 + st["crit"] * 3 + st["cdmg"])


static func team_power(g: Dictionary) -> float:
	var p := 0.0
	for h in team_heroes(g):
		p += power(hero_stats(g, h))
	return p


## Returns the number of level-ups.
static func give_xp(h: Dictionary, amount: int) -> int:
	var cap := max_level_for(int(h["stars"]))
	var ups := 0
	if int(h["level"]) >= cap:
		return 0
	h["xp"] = int(h["xp"]) + amount
	while int(h["level"]) < cap and int(h["xp"]) >= xp_to_next(int(h["level"])):
		h["xp"] = int(h["xp"]) - xp_to_next(int(h["level"]))
		h["level"] = int(h["level"]) + 1
		ups += 1
	if int(h["level"]) >= cap:
		h["xp"] = 0
	return ups


static func xp_to_cap(h: Dictionary) -> int:
	var need := 0
	for l in range(int(h["level"]), max_level_for(int(h["stars"]))):
		need += xp_to_next(l)
	return maxi(0, need - int(h["xp"]))


## The web's "Train" (gold for XP up to the next level). Returns false if it can't.
static func train(g: Dictionary, h: Dictionary) -> bool:
	if int(h["level"]) >= max_level_for(int(h["stars"])):
		return false
	var xp := xp_to_next(int(h["level"])) - int(h["xp"])
	var cost := ceili(xp / float(TRAIN_XP_PER_GOLD))
	if g["gold"] < cost:
		return false
	g["gold"] -= cost
	give_xp(h, xp)
	return true


static func train_cost(h: Dictionary) -> int:
	return ceili((xp_to_next(int(h["level"])) - int(h["xp"])) / float(TRAIN_XP_PER_GOLD))


# ---------- gear ----------
static func make_gear(g: Dictionary, rarity: int, slot: String = "") -> Dictionary:
	if slot == "":
		slot = SbRng.pick(WebData.GEAR_SLOT_KEYS)
	var subs_pool: Array = WebData.SUBSTATS.keys()
	var subs := []
	for i in mini(3, rarity - 1):
		var stat: String = subs_pool.pop_at(SbRng.rand_int(0, subs_pool.size() - 1))
		var roll: Array = WebData.SUBSTATS[stat]["roll"]
		subs.append({"stat": stat, "val": SbRng.rand_int(roll[0], roll[1])})
	var gr := {"uid": uid(g), "slot": slot, "rarity": rarity, "set": SbRng.pick(WebData.GEAR_SET_KEYS), "level": 0, "subs": subs}
	g["gear"].append(gr)
	return gr


static func gear_owner(g: Dictionary, gr: Dictionary) -> Dictionary:
	for h in g["heroes"]:
		if h["gear"].has(gr["slot"]) and int(h["gear"][gr["slot"]]) == int(gr["uid"]):
			return h
	return {}


static func equip(g: Dictionary, h: Dictionary, gr: Dictionary) -> void:
	var prev := gear_owner(g, gr)
	if not prev.is_empty():
		prev["gear"].erase(gr["slot"])
	h["gear"][gr["slot"]] = int(gr["uid"])


static func gear_score(gr: Dictionary) -> float:
	var s := 0.0
	for x in gr["subs"]:
		s += x["val"]
	return gear_main_value(gr) / WebData.GEAR_SLOTS[gr["slot"]]["base"] * 10 + s


static func auto_equip(g: Dictionary, h: Dictionary) -> void:
	for slot in WebData.GEAR_SLOT_KEYS:
		var best := {}
		for gr in g["gear"]:
			if gr["slot"] != slot:
				continue
			var owner := gear_owner(g, gr)
			if not owner.is_empty() and int(owner["uid"]) != int(h["uid"]):
				continue
			if best.is_empty() or gear_score(gr) > gear_score(best):
				best = gr
		if not best.is_empty():
			equip(g, h, best)


# ---------- summoning ----------
static func roll_rarity(g: Dictionary, shard_key: String) -> String:
	var d: Dictionary = WebData.SHARDS[shard_key]
	if d.has("pity") and int(g["pity"][shard_key]) + 1 >= int(d["pity"]):
		return "legendary"
	var r := SbRng.random() * 100
	for k in WebData.RARITY_KEYS:
		r -= d["rates"][k]
		if r < 0:
			return k
	for k in WebData.RARITY_KEYS:
		if d["rates"][k] > 0:
			return k
	return "common"


static func summon(g: Dictionary, shard_key: String, count: int) -> Array:
	if int(g["shards"][shard_key]) < count:
		return []
	g["shards"][shard_key] -= count
	var results := []
	for i in count:
		var rarity := roll_rarity(g, shard_key)
		if WebData.SHARDS[shard_key].has("pity"):
			g["pity"][shard_key] = 0 if rarity == "legendary" else int(g["pity"][shard_key]) + 1
		var pool := []
		for x in WebData.HEROES:
			if x["rarity"] == rarity:
				pool.append(x)
		results.append(add_hero(g, SbRng.pick(pool)["id"]))
	g["stats"]["summons"] += count
	return results


# ---------- campaign ----------
static func stage_label(idx: int) -> String:
	return "%d-%d" % [idx / WebData.STAGES_PER_CHAPTER + 1, idx % WebData.STAGES_PER_CHAPTER + 1]


static func is_boss_stage(idx: int) -> bool:
	return idx % WebData.STAGES_PER_CHAPTER == WebData.STAGES_PER_CHAPTER - 1


static func stage_level(idx: int) -> int:
	return int(minf(60, SbRng.js_round(2 + idx * 0.95)))


## state.js seeded(): deterministic per-stage enemy picks (integers stay exact in both languages).
class Seeded:
	var s: int
	func _init(seed: int) -> void:
		s = seed * 9301 + 49297
	func next() -> float:
		s = (s * 9301 + 49297) % 233280
		return s / 233280.0


static func enemy_grade(idx: int) -> Dictionary:
	var level := mini(50, stage_level(idx))
	var stars := minf(5, 1 + (level - 1) / 10.0)
	var t := idx / float(TOTAL_STAGES - 1)
	var curve: Dictionary = WebData.ENEMY_CURVE
	var chapters: Array = curve["chapter"]
	var ci := idx / WebData.STAGES_PER_CHAPTER
	var ch_adj: float = chapters[ci] if ci < chapters.size() and chapters[ci] else 1.0
	var mul: float = ch_adj * (curve["start"] + (curve["end"] - curve["start"]) * pow(t, curve["shape"]))
	return {"level": level, "stars": stars, "mul": mul, "spd": SbRng.js_round(idx * curve["spdPerStage"])}


static func stage_waves(idx: int) -> Array:
	var rng := Seeded.new(idx + 7)
	var gr := enemy_grade(idx)
	var chapter_el: String = WebData.ELEMENT_KEYS[(idx / WebData.STAGES_PER_CHAPTER) % WebData.ELEMENT_KEYS.size()]
	var waves := []
	var wave_count := 3 if is_boss_stage(idx) else 2
	var n_heroes := WebData.HEROES.size()
	for w in wave_count:
		var wave := []
		var n := 2 if idx < 2 else 3
		for i in n:
			var t: Dictionary = WebData.HEROES[int(floorf(rng.next() * n_heroes))]
			var e := gr.duplicate()
			e["template"] = t
			e["shownLevel"] = stage_level(idx)
			e["el"] = chapter_el if rng.next() < 0.5 else t["el"]
			wave.append(e)
		if is_boss_stage(idx) and w == wave_count - 1:
			var t: Dictionary = WebData.HEROES[int(floorf(rng.next() * n_heroes))]
			var e := gr.duplicate()
			e["template"] = t
			e["shownLevel"] = stage_level(idx) + 2
			e["el"] = chapter_el
			e["boss"] = true
			wave[1] = e
		waves.append(wave)
	return waves


static func stage_rewards(idx: int, first_clear: bool) -> Dictionary:
	var level := stage_level(idx)
	var r := {"gold": SbRng.js_round(200 + level * 60), "xp": SbRng.js_round(60 + level * level * 3.2)}
	if first_clear:
		r["common"] = 1
		if is_boss_stage(idx):
			r["mystic"] = 1
			r["gold"] *= 3
		if is_boss_stage(idx) and (idx + 1) % (WebData.STAGES_PER_CHAPTER * 3) == 0:
			r["radiant"] = 1
	return r


## Returns the new gear piece, or {} when nothing dropped.
static func roll_stage_gear(g: Dictionary, idx: int) -> Dictionary:
	var chance := 0.9 if is_boss_stage(idx) else 0.45
	if SbRng.random() > chance:
		return {}
	var chapter := idx / WebData.STAGES_PER_CHAPTER
	var top := mini(5, 1 + chapter / 2 + (1 if is_boss_stage(idx) else 0))
	var a := 1 if SbRng.random() < 0.55 else 0
	var b := 1 if SbRng.random() < 0.2 else 0
	var rarity := maxi(1, top - a - b)
	return make_gear(g, rarity)
