## Hero model colours, ported from game3d/three3d.mjs roleColors() with three.js r149 colour
## management (hex colours are converted to linear, mixed there, and written back the way
## Color.getHexString() does), so a hero's armour, cloth, hair and glow match the web exactly.
class_name LookColors

const HOLLOW_TINT := "#2e1f33"


static func _to_linear(c: float) -> float:
	return c * 0.0773993808 if c < 0.04045 else pow(c * 0.9478672986 + 0.0521327014, 2.4)


static func _to_srgb(c: float) -> float:
	return c * 12.92 if c < 0.0031308 else 1.055 * pow(c, 0.41666) - 0.055


static func _lin(hex: String) -> Array:
	var v := hex.trim_prefix("#").hex_to_int()
	return [_to_linear(((v >> 16) & 255) / 255.0), _to_linear(((v >> 8) & 255) / 255.0), _to_linear((v & 255) / 255.0)]


static func _hex(lin: Array) -> String:
	var out := "#"
	for c in lin:
		# getHex(): clamp(c * 255, 0, 255) << ... truncates
		out += "%02x" % int(clampf(_to_srgb(c) * 255.0, 0.0, 255.0))
	return out


static func mix(a: String, b: String, k: float) -> String:
	var x := _lin(a)
	var y := _lin(b)
	return _hex([x[0] + (y[0] - x[0]) * k, x[1] + (y[1] - x[1]) * k, x[2] + (y[2] - x[2]) * k])


static func mul(a: String, k: float) -> String:
	var x := _lin(a)
	return _hex([x[0] * k, x[1] * k, x[2] * k])


## roleColors(L, hollow): material role -> "#rrggbb". Hollow (enemy) copies are tinted violet-grey.
static func role_colors(L: Dictionary, hollow: bool) -> Dictionary:
	var tint := func(c: String) -> String: return mix(c, HOLLOW_TINT, 0.5) if hollow else c
	var cloth = L.get("cloth")
	var armor: String = L["armor"]
	var accent: String = L["accent"]
	var hair: String = L["hair"]
	var pants: String = L["pants"] if L.get("pants") else mix(cloth if cloth else armor, "#2a2522", 0.35 if cloth else 0.6)
	return {
		"armor": tint.call(armor), "cloth": tint.call(cloth if cloth else mul(armor, 0.62)), "pants": tint.call(pants), "accent": tint.call(accent),
		"trim": tint.call(L["trim"] if L.get("trim") else mix(accent, "#c8ccd4", 0.4)), "blade": tint.call("#c8d0dc"), "wood": tint.call("#6b4a29"),
		"boot": tint.call(L["boot"] if L.get("boot") else "#2a1f1a"), "hair": tint.call(hair), "brow": tint.call(mul(hair, 0.8)),
		"hood": tint.call(L["hood"] if L.get("hood") else armor), "skin": tint.call(LooksData.SKIN[L["skin"]]), "crown": tint.call(accent),
		"eye": "#ff2a2a" if hollow else (L["glow"] if L.get("glow") else "#1a141f"),
	}


## The colour role of a model material ("head:Black" keys apply to the head mesh only).
static func role_of(base: String, kind: String, material: String) -> String:
	var map: Dictionary = LooksData.ROLES.get(base, {})
	if kind != "" and map.has(kind + ":" + material):
		return map[kind + ":" + material]
	return map.get(material, "")
