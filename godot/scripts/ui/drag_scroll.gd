class_name DragScroll
extends ScrollContainer
## A ScrollContainer that scrolls with a finger drag wherever the drag starts, including on
## buttons and hero cards (which otherwise swallow the touch, so the page never moves).
## A touch counts as a scroll once it moves past a small threshold; from then on the
## release is eaten so the button under the finger does not fire. Lets go with momentum.

const THRESHOLD := 12.0 # px before a touch becomes a scroll
const FRICTION := 5.0   # momentum decay per second

var _touching := false
var _dragging := false
var _start := Vector2.ZERO
var _origin := 0.0
var _velocity := 0.0
var _eat_mouse_release := false
var _eat_touch_release := false


func _input(event: InputEvent) -> void:
	if not is_visible_in_tree():
		return
	if event is InputEventScreenTouch and event.index == 0:
		if event.pressed:
			_touching = get_global_rect().has_point(event.position)
			_dragging = false
			_eat_mouse_release = false
			_eat_touch_release = false
			_start = event.position
			_origin = scroll_vertical
			_velocity = 0.0
		else:
			_touching = false
			if _dragging or _eat_touch_release:
				_dragging = false
				_eat_touch_release = false
				get_viewport().set_input_as_handled()
	elif event is InputEventScreenDrag and event.index == 0 and _touching:
		var dy: float = event.position.y - _start.y
		if not _dragging and absf(dy) > THRESHOLD:
			_dragging = true
			_eat_mouse_release = true
			_eat_touch_release = true
			_start = event.position
			_origin = scroll_vertical
			dy = 0.0
		if _dragging:
			scroll_vertical = int(_origin - dy)
			var dt := maxf(get_process_delta_time(), 1.0 / 120.0)
			_velocity = lerpf(_velocity, -event.relative.y / dt, 0.5)
			get_viewport().set_input_as_handled()
	elif event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_LEFT and not event.pressed:
		# The release that Android also sends as a mouse click: drop it after a scroll.
		if _eat_mouse_release:
			_eat_mouse_release = false
			get_viewport().set_input_as_handled()


func _process(delta: float) -> void:
	if _touching or absf(_velocity) < 20.0:
		return
	var before := scroll_vertical
	scroll_vertical = int(scroll_vertical + _velocity * delta)
	_velocity *= maxf(0.0, 1.0 - FRICTION * delta)
	if scroll_vertical == before:
		_velocity = 0.0
