## The menu backdrop: a violet-to-black gradient with the faint gold sigil (circles and two
## triangles) the web game draws behind its screens.
class_name SigilBackground
extends Control

var _tex: GradientTexture2D


func _ready() -> void:
	set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	var g := Gradient.new()
	g.set_color(0, Color("#2d2140"))
	g.set_color(1, Color("#0f0c15"))
	g.add_point(0.55, Color("#150f1d"))
	_tex = GradientTexture2D.new()
	_tex.gradient = g
	_tex.fill = GradientTexture2D.FILL_RADIAL
	_tex.fill_from = Vector2(0.5, 0.0)
	_tex.fill_to = Vector2(1.3, 0.9)
	_tex.width = 256
	_tex.height = 256


func _draw() -> void:
	draw_rect(Rect2(Vector2.ZERO, size), Color("#0f0c15"))
	draw_texture_rect(_tex, Rect2(Vector2.ZERO, size), false)
	var c := Vector2(size.x / 2, minf(300, size.y * 0.33))
	var r := minf(size.x * 0.48, 220)
	var col := Color("#c9a25a", 0.07)
	draw_arc(c, r, 0, TAU, 96, col, 1.2, true)
	draw_arc(c, r * 0.88, 0, TAU, 96, Color(col, 0.05), 1.0, true)
	draw_arc(c, r * 0.54, 0, TAU, 64, col, 1.0, true)
	for flip in [1.0, -1.0]:
		var pts := PackedVector2Array()
		for k in 4:
			var a := -PI / 2 + k * TAU / 3
			pts.append(c + Vector2(cos(a), sin(a) * flip) * r)
		draw_polyline(pts, col, 1.0, true)


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()
