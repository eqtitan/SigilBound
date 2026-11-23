## The turn-meter battle engine, ported line by line from public/game/battle.js. No presentation:
## every call returns the same event dictionaries the web UI reads ({type, key, amount, ...}).
## Randomness goes through SbRng in the same order as the web code's Math.random() calls.
class_name SbBattle
extends RefCounted

const TM_FULL := 1000.0

static var next_n := 0 # Battle._n

var allies: Array = []
var waves: Array = []
var wave := 0
var enemies: Array = []
var opts := {}
var over := false
var result := ""
var boss_turns := 0
var turn := 0
var boost := 1.0
var titan_damage := 0.0


static func make_unit(side: String, template: Dictionary, stats: Dictionary, extra: Dictionary = {}) -> SbUnit:
	var u := SbUnit.new()
	u.key = "%s%d" % [side, next_n]
	next_n += 1
	u.side = side
	u.template = template
	u.name = template["name"]
	u.el = stats["el"]
	u.max_hp = stats["hp"]
	u.hp = stats["hp"]
	u.atk = stats["atk"]
	u.def = stats["def"]
	u.spd = stats["spd"]
	u.crit = stats["crit"]
	u.cdmg = stats["cdmg"]
	u.tm = SbRng.random() * 100
	for s in template["skills"]:
		u.cds.append(0)
	for k in extra:
		u.set(k, extra[k])
	return u


static func unit_from_hero(g: Dictionary, h: Dictionary) -> SbUnit:
	return make_unit("A", Rules.hero(h["id"]), Rules.hero_stats(g, h),
		{"hero_uid": int(h["uid"]), "level": int(h["level"]), "stars": float(h["stars"]), "skill_lv": h.get("skillLv", [])})


static func unit_from_enemy(e: Dictionary) -> SbUnit:
	# Stats use a fixed baseline rarity; e.mul (the stage curve) sets the strength.
	var tpl: Dictionary = e["template"].duplicate()
	tpl["rarity"] = "common"
	var s := Rules.base_stats(tpl, e["level"], e["stars"], e["el"])
	var boss: bool = e.get("boss", false)
	var curve: Dictionary = WebData.ENEMY_CURVE
	var mul: float = e["mul"] * (curve["bossStat"] if boss else 1)
	var tname: String = e["template"]["name"]
	return make_unit("B", e["template"], {
		"el": s["el"], "hp": SbRng.js_round(s["hp"] * mul * (curve["bossHp"] if boss else 1)), "atk": SbRng.js_round(s["atk"] * mul),
		"def": SbRng.js_round(s["def"] * mul), "spd": s["spd"] + e["spd"] + (4 if boss else 0), "crit": s["crit"], "cdmg": s["cdmg"],
	}, {"level": int(e["shownLevel"]), "stars": float(e["stars"]), "boss": boss,
		"name": ("Warlord " if boss else "Hollow ") + Rules.short_name(tname)})


func _init(ally_units: Array, wave_list: Array, options: Dictionary = {}) -> void:
	allies = ally_units
	waves = wave_list
	enemies = waves[0]
	opts = options


func units() -> Array:
	return allies + enemies


func side_of(u: SbUnit, own: bool = true) -> Array:
	return allies if (u.side == "A") == own else enemies


func living(list: Array) -> Array:
	return list.filter(func(x: SbUnit) -> bool: return x.alive)


## Advance turn meters until a unit is ready; return it.
func next_actor() -> SbUnit:
	for guard in 10000:
		var ready := living(units()).filter(func(x: SbUnit) -> bool: return x.tm >= TM_FULL)
		if not ready.is_empty():
			# ready.sort((a, b) => b.tm - a.tm || speed(b) - speed(a))[0] with a stable sort.
			var best: SbUnit = ready[0]
			for i in range(1, ready.size()):
				var u: SbUnit = ready[i]
				var c := best.tm - u.tm
				if c == 0:
					c = speed(best) - speed(u)
				if c < 0:
					best = u
			return best
		for u in living(units()):
			u.tm += speed(u) * 0.07 * 10
	return null


func has(u: SbUnit, t: String) -> bool:
	for e in u.effects:
		if e["t"] == t:
			return true
	return false


func _find_effect(u: SbUnit, t: String) -> Variant:
	for e in u.effects:
		if e["t"] == t:
			return e
	return null


func speed(u: SbUnit) -> float:
	return u.spd * (1 + (0.3 if has(u, "spdUp") else 0.0) - (0.3 if has(u, "slow") else 0.0))


func atk_of(u: SbUnit) -> float:
	return u.atk * (1 + (0.4 if has(u, "atkUp") else 0.0) - (0.4 if has(u, "atkDown") else 0.0))


func def_of(u: SbUnit) -> float:
	return u.def * (1 + (0.5 if has(u, "defUp") else 0.0) - (0.5 if has(u, "defDown") else 0.0))


## Start-of-turn upkeep. Returns {events, skip} (skip = stunned or killed by poison).
func begin_turn(u: SbUnit) -> Dictionary:
	var ev := []
	turn += 1
	u.tm = 0
	for i in u.cds.size():
		u.cds[i] = maxi(0, u.cds[i] - 1)
	for e in u.effects:
		if e["t"] == "poison":
			var d := SbRng.js_round(minf(u.max_hp * 0.04, atk_of_strongest() * 3 if u.titan else INF))
			apply_damage(u, d, null, ev, {"dot": true})
		elif e["t"] == "regen":
			heal(u, u.max_hp * 0.08, ev)
	var skip := u.alive and has(u, "stun")
	if skip:
		ev.append({"type": "text", "key": u.key, "text": "Stunned!"})
	return {"events": ev, "skip": skip or not u.alive}


func atk_of_strongest() -> float:
	var m := -INF
	for a in allies:
		m = maxf(m, a.atk)
	return m


func end_turn(u: SbUnit) -> String:
	for e in u.effects:
		if e["fresh"]:
			e["fresh"] = false
		else:
			e["turns"] -= 1
	u.effects = u.effects.filter(func(e: Dictionary) -> bool: return e["turns"] > 0 and not (e["t"] == "shield" and e["val"] <= 0))
	if u.titan:
		boss_turns += 1
	return check_end()


func check_end() -> String:
	if living(allies).is_empty():
		over = true
		result = "defeat"
		return "defeat"
	if opts.get("titan", false) and boss_turns >= opts.get("turnLimit", 0):
		over = true
		result = "timeout"
		return "timeout"
	if living(enemies).is_empty():
		if wave < waves.size() - 1:
			wave += 1
			enemies = waves[wave]
			for a in allies:
				a.tm = minf(a.tm, TM_FULL * 0.6)
			return "wave"
		over = true
		result = "victory"
		return "victory"
	return ""


func usable(u: SbUnit) -> Array:
	var out := []
	for i in u.cds.size():
		if u.cds[i] == 0:
			out.append(i)
	return out


func valid_targets(u: SbUnit, skill_idx: int) -> Array:
	var sk: Dictionary = u.skills()[skill_idx]
	if sk["target"] == "enemy":
		var foes := living(side_of(u, false))
		var taunters := foes.filter(func(f: SbUnit) -> bool: return has(f, "taunt"))
		return taunters if not taunters.is_empty() else foes
	if sk["target"] == "ally":
		return living(side_of(u, true))
	return [] # AoE / self need no choice


static func _heals(sk: Dictionary) -> bool:
	for f in sk["fx"]:
		if f["t"] == "heal" or f["t"] == "regen":
			return true
	return false


static func _taunts(sk: Dictionary) -> bool:
	for f in sk["fx"]:
		if f["t"] == "taunt":
			return true
	return false


## AI: pick the strongest ready skill and a sensible target. Returns {skill, target} (target key or "").
func ai_choose(u: SbUnit) -> Dictionary:
	var ready := usable(u)
	var own := living(side_of(u, true))
	var hurt := own.filter(func(a: SbUnit) -> bool: return a.hp / a.max_hp < 0.65).size()
	var best := 0
	var best_score := -1
	for i in ready:
		var sk: Dictionary = u.skills()[i]
		var score: int = sk["cd"] * 10 + i
		if _heals(sk) and hurt == 0:
			score -= 100
		if _taunts(sk) and has(u, "taunt"):
			score -= 100
		if score > best_score:
			best_score = score
			best = i
	var targets := valid_targets(u, best)
	var target: SbUnit = null
	if not targets.is_empty():
		var by_elem: bool = u.skills()[best]["target"] == "enemy"
		# The first element after the web's stable sort: a later unit only wins if it sorts strictly first.
		target = targets[0]
		for k in range(1, targets.size()):
			if _before(u, targets[k], target, by_elem):
				target = targets[k]
	return {"skill": best, "target": target.key if target else ""}


## True when the stable sort in aiChoose puts `b` (later in the list) before `a`.
func _before(u: SbUnit, b: SbUnit, a: SbUnit, by_elem: bool) -> bool:
	# comparator(x, y) for the web's sort; x = b, y = a. b goes first only if cmp(b, a) < 0.
	var c := 0.0
	if by_elem:
		c = Rules.elem_mult(u.el, a.el) - Rules.elem_mult(u.el, b.el) # ey - ex with x=b, y=a
	if c == 0:
		c = b.hp / b.max_hp - a.hp / a.max_hp
	return c < 0


func find(key: String) -> SbUnit:
	for x in units():
		if x.key == key:
			return x
	return null


## Execute a skill. Returns events for the UI.
func act(u: SbUnit, skill_idx: int, target_key: String) -> Array:
	var ev := []
	var sk: Dictionary = u.skills()[skill_idx]
	# Skill upgrades (Skill Tomes): +8% damage/healing per level up to 3; level 4 cuts the cooldown by 1.
	var slv: int = int(u.skill_lv[skill_idx]) if skill_idx < u.skill_lv.size() and u.skill_lv[skill_idx] else 0
	u.cds[skill_idx] = maxi(0, int(sk["cd"]) - (1 if slv >= 4 and sk["cd"] > 1 else 0))
	boost = 1 + 0.08 * mini(slv, 3)
	ev.append({"type": "skill", "key": u.key, "name": sk["name"], "side": u.side})
	var own := living(side_of(u, true))
	var foes := living(side_of(u, false))
	var targets := []
	match sk["target"]:
		"enemy":
			var valid := valid_targets(u, skill_idx)
			var pick: SbUnit = null
			for t in valid:
				if t.key == target_key:
					pick = t
					break
			if pick == null and not valid.is_empty():
				pick = valid[0]
			if pick:
				targets = [pick]
		"allEnemies":
			targets = foes
		"ally":
			var pick: SbUnit = u
			for t in own:
				if t.key == target_key:
					pick = t
					break
			targets = [pick]
		"allAllies":
			targets = own
		_:
			targets = [u]
	if sk["mult"] > 0:
		for t in targets:
			var h := 0
			while h < int(sk.get("hits", 1)) and t.alive:
				hit(u, t, sk, ev)
				h += 1
	for f in sk["fx"]:
		var to: String = f.get("to", "")
		var recips := []
		if to == "self":
			recips = [u]
		elif to == "allies":
			recips = living(own)
		elif to == "enemies":
			recips = living(foes)
		else:
			recips = targets.filter(func(t: SbUnit) -> bool: return t.alive)
		for r in recips:
			apply_fx(u, r, f, ev)
	boost = 1
	return ev


func hit(u: SbUnit, t: SbUnit, sk: Dictionary, ev: Array) -> void:
	var scale: String = sk.get("scale", "atk")
	var stat := def_of(u) if scale == "def" else (u.max_hp if scale == "hp" else atk_of(u))
	var dmg: float = stat * sk["mult"] * boost
	var em := Rules.elem_mult(u.el, t.el)
	dmg *= em
	dmg *= 650 / (650 + def_of(t))
	var crit := SbRng.random() * 100 < u.crit and em >= 1
	if crit:
		dmg *= 1 + u.cdmg / 100
	dmg *= SbRng.rand(0.95, 1.05)
	apply_damage(t, maxf(1, SbRng.js_round(dmg)), u, ev, {"crit": crit, "em": em})


func apply_damage(t: SbUnit, dmg: float, src: SbUnit, ev: Array, info: Dictionary = {}) -> void:
	var remaining := dmg
	var shield = _find_effect(t, "shield")
	if shield != null and not info.get("dot", false):
		var absorbed := minf(shield["val"], remaining)
		shield["val"] -= absorbed
		remaining -= absorbed
		if absorbed:
			ev.append({"type": "absorb", "key": t.key, "amount": absorbed})
		if shield["val"] <= 0:
			t.effects = t.effects.filter(func(e: Dictionary) -> bool: return not is_same(e, shield))
	t.hp -= remaining
	if src:
		src.dmg_dealt += dmg
	if opts.get("titan", false) and t.titan:
		titan_damage += dmg
	var e := {"type": "dmg", "key": t.key, "amount": remaining}
	for k in ["crit", "em", "dot"]:
		if info.has(k):
			e[k] = info[k]
	ev.append(e)
	if t.hp <= 0:
		t.hp = 0
		t.alive = false
		t.effects = []
		ev.append({"type": "death", "key": t.key})


func heal(t: SbUnit, amount: float, ev: Array) -> void:
	if not t.alive:
		return
	var amt := SbRng.js_round(minf(amount, t.max_hp - t.hp))
	t.hp += amt
	if amt > 0:
		ev.append({"type": "heal", "key": t.key, "amount": amt})


func apply_fx(u: SbUnit, r: SbUnit, f: Dictionary, ev: Array) -> void:
	if f.has("chance") and SbRng.random() > f["chance"]:
		return
	var t: String = f["t"]
	var meta = WebData.EFFECTS.get(t)
	if r.titan and (t == "stun" or t == "tmDrain"):
		ev.append({"type": "text", "key": r.key, "text": "Immune"})
		return
	match t:
		"heal":
			heal(r, r.max_hp * f["pct"] * boost, ev)
			return
		"tm":
			r.tm += TM_FULL * f["val"]
			ev.append({"type": "text", "key": r.key, "text": "Turn Meter up"})
			return
		"tmDrain":
			r.tm = maxf(0, r.tm - TM_FULL * f["val"])
			ev.append({"type": "text", "key": r.key, "text": "Turn Meter down"})
			return
		"cleanse":
			var n := r.effects.size()
			r.effects = r.effects.filter(func(e: Dictionary) -> bool: return WebData.EFFECTS[e["t"]]["buff"])
			if r.effects.size() < n:
				ev.append({"type": "text", "key": r.key, "text": "Cleansed"})
			return
		"strip":
			var n := r.effects.size()
			r.effects = r.effects.filter(func(e: Dictionary) -> bool: return not WebData.EFFECTS[e["t"]]["buff"])
			if r.effects.size() < n:
				ev.append({"type": "text", "key": r.key, "text": "Buffs removed"})
			return
	if meta == null:
		return
	var self_cast := r == u
	var existing = _find_effect(r, t)
	if t == "shield":
		var val := SbRng.js_round(u.max_hp * f["pct"])
		if existing != null:
			existing["val"] = maxf(existing["val"], val)
			existing["turns"] = 2
		else:
			r.effects.append({"t": "shield", "val": val, "turns": 2, "fresh": self_cast})
	elif existing != null:
		existing["turns"] = maxi(existing["turns"], int(f["turns"]))
		if self_cast:
			existing["fresh"] = true
	else:
		r.effects.append({"t": t, "turns": int(f.get("turns", 1)) if f.get("turns", 0) else 1, "fresh": self_cast})
	ev.append({"type": "fx", "key": r.key, "t": t})
