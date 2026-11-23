## 3D hero portraits for the menus (the web renders each hero's bust for its cards): a hidden
## SubViewport renders one hero at a time, head and shoulders, on a transparent background, and
## the image is cached. Cards ask with request(id, callback) and redraw when it arrives.
class_name Portraits
extends Node

const SIZE := 192

static var _cache := {}
static var _instance: Portraits

var _queue: Array = [] # [id, [callbacks]]
var _busy := false
var _vp: SubViewport


static func get_tex(id: String) -> Texture2D:
	return _cache.get(id)


## Calls cb(texture) when the portrait is ready (immediately if cached).
static func request(id: String, cb: Callable) -> void:
	if _cache.has(id):
		cb.call(_cache[id])
		return
	if DisplayServer.get_name() == "headless":
		return
	if _instance == null:
		_instance = Portraits.new()
		(Engine.get_main_loop() as SceneTree).root.add_child.call_deferred(_instance)
	for q in _instance._queue:
		if q[0] == id:
			q[1].append(cb)
			return
	_instance._queue.append([id, [cb]])
	_instance._pump.call_deferred()


func _ready() -> void:
	_vp = SubViewport.new()
	_vp.size = Vector2i(SIZE, SIZE)
	_vp.transparent_bg = true
	_vp.own_world_3d = true
	_vp.msaa_3d = Viewport.MSAA_4X
	_vp.render_target_update_mode = SubViewport.UPDATE_DISABLED
	add_child(_vp)
	var env := Environment.new()
	env.background_mode = Environment.BG_CLEAR_COLOR
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color("#c8b8a8")
	env.ambient_light_energy = 0.9
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.25
	var we := WorldEnvironment.new()
	we.environment = env
	_vp.add_child(we)
	var key := DirectionalLight3D.new()
	key.light_energy = 2.0
	key.light_color = Color("#fff1dd")
	_vp.add_child(key)
	key.look_at_from_position(Vector3(-2, 3, 4), Vector3(0, 1.4, 0))
	var rim := DirectionalLight3D.new()
	rim.light_energy = 1.2
	rim.light_color = Color("#99aaff")
	_vp.add_child(rim)
	rim.look_at_from_position(Vector3(3, 2, -3), Vector3(0, 1.4, 0))
	var cam := Camera3D.new()
	cam.fov = 19
	_vp.add_child(cam)
	cam.position = Vector3(0.3, 1.95, 2.9)
	cam.look_at(Vector3(0, 1.72, 0))


func _pump() -> void:
	if _busy or _queue.is_empty() or not is_inside_tree():
		if not is_inside_tree():
			_pump.call_deferred()
		return
	_busy = true
	var item: Array = _queue.pop_front()
	var id: String = item[0]
	var m := HeroModel.new()
	m.build(id, false, Rules.hero(id)["el"], null)
	m.rotation.y = 0.32
	_vp.add_child(m)
	for i in 3:
		await get_tree().process_frame
	_vp.render_target_update_mode = SubViewport.UPDATE_ONCE
	await RenderingServer.frame_post_draw
	var img := _vp.get_texture().get_image()
	if img:
		var tex := ImageTexture.create_from_image(img)
		_cache[id] = tex
		for cb in item[1]:
			if cb.is_valid():
				cb.call(tex)
	m.queue_free()
	_busy = false
	_pump.call_deferred()
