## The HUD tag above a unit (the web's .unit.ov3d overlay): element dot, level and name, an HP bar
## with a shield overlay, and the turn-meter bar. Tapping it selects that unit as the target.
class_name UnitTag
extends Control

signal tapped(key: String)

const W := 96.0

var u: SbUnit
var selected := false
var active := false
var _hp_shown := 1.0


func _init(unit: SbUnit) -> void:
	u = unit
	custom_minimum_size = Vector2(W, 34)
	size = custom_minimum_size
	mouse_filter = Control.MOUSE_FILTER_STOP


func _gui_input(event: InputEvent) -> void:
	if (event is InputEventMouseButton and event.pressed) or (event is InputEventScreenTouch and event.pressed):
		tapped.emit(u.key)
		accept_event()


func _process(delta: float) -> void:
	var target := u.hp / u.max_hp if u.max_hp > 0 else 0.0
	_hp_shown = lerpf(_hp_shown, target, minf(1.0, delta * 8.0))
	queue_redraw()


func _draw() -> void:
	var font := UiKit.display_font
	var ally := u.side == "A"
	var nm := Rules.short_name(u.name) if ally else u.name.replace("Hollow ", "")
	var title := "%d %s" % [u.level, nm]
	var fs := 12
	var tw := font.get_string_size(title, HORIZONTAL_ALIGNMENT_LEFT, -1, fs).x
	var x0 := (W - tw - 12) / 2.0
	draw_circle(Vector2(x0 + 4, 9), 4.5, UiKit.element_color(u.el))
	draw_arc(Vector2(x0 + 4, 9), 4.5, 0, TAU, 16, Color("#ffe2a0", 0.7), 1.0, true)
	var name_col := Color("#ffd27a") if active else (Color("#ff9a8a") if u.boss else Color("#f6e6c2"))
	draw_string_outline(font, Vector2(x0 + 12, 13), title, HORIZONTAL_ALIGNMENT_LEFT, -1, fs, 3, Color(0, 0, 0, 0.85))
	draw_string(font, Vector2(x0 + 12, 13), title, HORIZONTAL_ALIGNMENT_LEFT, -1, fs, name_col)
	# HP bar
	var r := Rect2(4, 18, W - 8, 7)
	draw_rect(r.grow(1), Color("#000000", 0.85))
	draw_rect(r, Color("#2a0d10"))
	var hp_col := Color("#5bd17a") if ally else Color("#e2483a")
	draw_rect(Rect2(r.position, Vector2(r.size.x * clampf(_hp_shown, 0, 1), r.size.y)), hp_col)
	var shield := 0.0
	for e in u.effects:
		if e["t"] == "shield":
			shield = e["val"]
	if shield > 0:
		draw_rect(Rect2(r.position + Vector2(0, r.size.y - 2), Vector2(r.size.x * clampf(shield / u.max_hp, 0, 1), 2)), Color("#9fd8ff"))
	# Turn meter
	var t := Rect2(4, 27, W - 8, 3)
	draw_rect(t, Color("#000000", 0.7))
	draw_rect(Rect2(t.position, Vector2(t.size.x * clampf(u.tm / SbBattle.TM_FULL, 0, 1), t.size.y)), Color("#7fc8ff"))
	# Effect pips (buffs gold, debuffs violet)
	var px := 4.0
	for e in u.effects:
		var buff: bool = WebData.EFFECTS[e["t"]]["buff"]
		draw_rect(Rect2(px, 32, 6, 2), Color("#ffd56a") if buff else Color("#c07aff"))
		px += 8
	if selected:
		draw_rect(Rect2(0, 15, W, 18), Color("#f2c45c"), false, 1.5)
