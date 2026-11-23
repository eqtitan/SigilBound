## A framed panel with gold corner filigree (the web's .card::before corner ornaments).
class_name GildedPanel
extends PanelContainer

var accent := Color("#d9ac5a")


func _draw() -> void:
	var s := size
	var r := 12.0
	for corner in 4:
		var flip_x := corner == 1 or corner == 3
		var flip_y := corner >= 2
		var o := Vector2(s.x - 4 if flip_x else 4.0, s.y - 4 if flip_y else 4.0)
		var dx := -1.0 if flip_x else 1.0
		var dy := -1.0 if flip_y else 1.0
		var pts := PackedVector2Array()
		pts.append(o + Vector2(0, 14 * dy))
		pts.append(o + Vector2(0, 5 * dy))
		pts.append(o + Vector2(2 * dx, 2 * dy))
		pts.append(o + Vector2(5 * dx, 0))
		pts.append(o + Vector2(14 * dx, 0))
		draw_polyline(pts, accent, 1.3, true)
		draw_circle(o + Vector2(3.5 * dx, 3.5 * dy), 1.8, Color("#ffe2a0"))
	draw_line(Vector2(r + 14, 1), Vector2(s.x - r - 14, 1), Color(1, 0.85, 0.5, 0.08), 1.0)


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		queue_redraw()
