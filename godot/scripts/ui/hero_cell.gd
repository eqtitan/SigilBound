## A hero tile (the web's .hcell): a gilt-bezelled portrait disc in the element colour with the
## rarity frame, the hero's initial, level, stars and short name. Emits `tapped` with the uid.
class_name HeroCell
extends Control

signal tapped(uid: int)

var h: Dictionary
var selected := false


func _init(hero: Dictionary, is_selected: bool = false) -> void:
	h = hero
	selected = is_selected
	custom_minimum_size = Vector2(80, 104)
	mouse_filter = Control.MOUSE_FILTER_STOP


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		tapped.emit(int(h["uid"]))
		accept_event()


func _draw() -> void:
	var t := Rules.hero(h["id"])
	var c := Vector2(size.x / 2, 36)
	var rc := UiKit.rarity_color(t["rarity"])
	var ec := UiKit.element_color(t["el"])
	draw_circle(c, 34, Color("#000000"))
	draw_circle(c, 33, Color("#8a6a36") if not selected else UiKit.GOLD)
	draw_circle(c, 31, Color("#2b2116"))
	draw_circle(c, 29, rc)
	draw_circle(c, 27, ec.darkened(0.55))
	draw_circle(c + Vector2(-6, -7), 16, Color(ec, 0.45))
	var font := UiKit.display_font
	var initial: String = t["name"].substr(0, 1)
	var iw := font.get_string_size(initial, HORIZONTAL_ALIGNMENT_LEFT, -1, 28).x
	draw_string_outline(font, c + Vector2(-iw / 2, 10), initial, HORIZONTAL_ALIGNMENT_LEFT, -1, 28, 4, Color(0, 0, 0, 0.7))
	draw_string(font, c + Vector2(-iw / 2, 10), initial, HORIZONTAL_ALIGNMENT_LEFT, -1, 28, Color("#fff1d6"))
	# level badge
	var lv := "Lv %d" % int(h["level"])
	var lw := font.get_string_size(lv, HORIZONTAL_ALIGNMENT_LEFT, -1, 10).x
	var badge := Rect2(c.x + 30 - lw - 8, c.y + 20, lw + 8, 15)
	draw_rect(badge, Color("#0d0a12"))
	draw_rect(badge, Color("#6b5432"), false, 1.0)
	draw_string(font, badge.position + Vector2(4, 11), lv, HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color("#f6e6c2"))
	# stars
	var n := int(h["stars"])
	var sx := c.x - (n * 9) / 2.0 + 4.5
	for i in n:
		_star(Vector2(sx + i * 9, 76), 4.0, UiKit.GOLD)
	var nm := Rules.short_name(t["name"])
	var nw := font.get_string_size(nm, HORIZONTAL_ALIGNMENT_LEFT, -1, 12).x
	draw_string(font, Vector2(size.x / 2 - nw / 2, 97), nm, HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color("#ecdcbc"))


func _star(p: Vector2, r: float, col: Color) -> void:
	var pts := PackedVector2Array()
	for k in 10:
		var a := -PI / 2 + k * PI / 5
		var rr := r if k % 2 == 0 else r * 0.45
		pts.append(p + Vector2(cos(a), sin(a)) * rr)
	draw_colored_polygon(pts, col)
