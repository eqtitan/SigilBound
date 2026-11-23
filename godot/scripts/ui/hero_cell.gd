## A hero tile (the web's .hcell / .por): a rounded portrait card in the hero's element colour
## with the rarity frame and a gilt bezel, the 3D bust (Portraits), an element pip, the level
## badge, stars and the short name. Emits `tapped` with the uid.
class_name HeroCell
extends Control

signal tapped(uid: int)

const CARD := 72.0

var h: Dictionary
var selected := false
var _tex: Texture2D


func _init(hero: Dictionary, is_selected: bool = false) -> void:
	h = hero
	selected = is_selected
	custom_minimum_size = Vector2(84, 112)
	mouse_filter = Control.MOUSE_FILTER_STOP


func _ready() -> void:
	Portraits.request(h["id"], func(t: Texture2D) -> void:
		if is_instance_valid(self):
			_tex = t
			queue_redraw())


func _gui_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.pressed and event.button_index == MOUSE_BUTTON_LEFT:
		tapped.emit(int(h["uid"]))
		accept_event()


func _rounded(r: Rect2, rad: float, col: Color) -> void:
	var sb := StyleBoxFlat.new()
	sb.bg_color = col
	sb.set_corner_radius_all(int(rad))
	sb.anti_aliasing = true
	draw_style_box(sb, r)


func _draw() -> void:
	var t := Rules.hero(h["id"])
	var rc := UiKit.rarity_color(t["rarity"])
	var ec := UiKit.element_color(t["el"])
	var card := Rect2((size.x - CARD) / 2, 6, CARD, CARD)
	# gilt bezel, rarity frame, element-tinted radial background
	_rounded(card.grow(4), 16, Color("#000000"))
	_rounded(card.grow(3), 15, UiKit.GOLD if selected else Color("#8a6a36"))
	_rounded(card.grow(1), 14, Color("#2b2116"))
	_rounded(card, 13, rc)
	_rounded(card.grow(-2), 11, ec.darkened(0.62))
	draw_circle(card.position + card.size * Vector2(0.32, 0.28), CARD * 0.42, Color(ec, 0.28))
	draw_circle(card.position + card.size * Vector2(0.32, 0.28), CARD * 0.22, Color(ec, 0.22))
	var font := UiKit.display_font
	if _tex:
		draw_texture_rect(_tex, card.grow(-2), false)
	else:
		var initial: String = t["name"].substr(0, 1)
		var iw := font.get_string_size(initial, HORIZONTAL_ALIGNMENT_LEFT, -1, 30).x
		var c := card.get_center()
		draw_string_outline(font, c + Vector2(-iw / 2, 11), initial, HORIZONTAL_ALIGNMENT_LEFT, -1, 30, 4, Color(0, 0, 0, 0.7))
		draw_string(font, c + Vector2(-iw / 2, 11), initial, HORIZONTAL_ALIGNMENT_LEFT, -1, 30, Color("#fff1d6"))
	# element pip (top-left), level badge (bottom-right)
	var pip := card.position + Vector2(2, 2)
	draw_circle(pip, 9, Color("#0f0c15"))
	draw_arc(pip, 9, 0, TAU, 20, UiKit.TRIM, 1.2, true)
	draw_circle(pip, 5.5, ec)
	var lv := "Lv%d" % int(h["level"])
	var lw := font.get_string_size(lv, HORIZONTAL_ALIGNMENT_LEFT, -1, 10).x
	var badge := Rect2(card.end.x - lw - 10, card.end.y - 13, lw + 8, 15)
	_rounded(badge.grow(1), 5, Color("#6b5432"))
	_rounded(badge, 4, Color("#120e19"))
	draw_string(font, badge.position + Vector2(4, 11.5), lv, HORIZONTAL_ALIGNMENT_LEFT, -1, 10, Color("#f6e6c2"))
	# stars and name
	var n := int(h["stars"])
	var sx := size.x / 2 - (n * 9) / 2.0 + 4.5
	for i in n:
		_star(Vector2(sx + i * 9, card.end.y + 11), 4.0, UiKit.GOLD)
	var nm := Rules.short_name(t["name"])
	var nw := font.get_string_size(nm, HORIZONTAL_ALIGNMENT_LEFT, -1, 12).x
	draw_string(font, Vector2(size.x / 2 - nw / 2, card.end.y + 29), nm, HORIZONTAL_ALIGNMENT_LEFT, -1, 12, Color("#ecdcbc"))


func _star(p: Vector2, r: float, col: Color) -> void:
	var pts := PackedVector2Array()
	for k in 10:
		var a := -PI / 2 + k * PI / 5
		var rr := r if k % 2 == 0 else r * 0.45
		pts.append(p + Vector2(cos(a), sin(a)) * rr)
	draw_colored_polygon(pts, col)
