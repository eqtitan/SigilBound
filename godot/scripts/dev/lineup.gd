## Dev tool: renders every hero close up in the battle lighting, six per image (plus their Hollow
## versions), to compare the kit and shading against the web game's hero viewer.
##   godot --rendering-driver opengl3 --resolution 900x700 --path godot -- --lineup=DIR [--gear=R]
extends Node3D

var out_dir := ""
var gear_r := 0


func _ready() -> void:
	var arena := Arena.new()
	add_child(arena)
	arena.build(0, "high")
	var cam := Camera3D.new()
	cam.fov = 30
	add_child(cam)
	cam.position = Vector3(0, 1.45, 9.2)
	cam.look_at(Vector3(0, 1.05, 0))
	_run.call_deferred()


func _run() -> void:
	var ids := []
	for h in WebData.HEROES:
		ids.append(h["id"])
	var gear = null
	if gear_r > 0:
		gear = {"weapon": {"r": gear_r, "s": "fury"}, "helm": {"r": gear_r, "s": "keen"}, "chest": {"r": gear_r, "s": "vigor"}, "boots": {"r": gear_r, "s": "haste"}}
	for page in 4:
		for hollow in [false, true]:
			var row := Node3D.new()
			add_child(row)
			for i in 6:
				var id: String = ids[page * 6 + i]
				var m := HeroModel.new()
				m.build(id, hollow, Rules.hero(id)["el"], gear)
				m.position = Vector3((i - 2.5) * 1.35, 0, 0)
				m.rotation.y = 0.35
				row.add_child(m)
			for f in 20:
				await get_tree().process_frame
			await RenderingServer.frame_post_draw
			var img := get_viewport().get_texture().get_image()
			var name := "lineup-%d%s%s.png" % [page + 1, "-hollow" if hollow else "", "-gear%d" % gear_r if gear_r else ""]
			img.save_png(out_dir.path_join(name))
			print("shot ", name)
			row.queue_free()
			await get_tree().process_frame
	get_tree().quit(0)
