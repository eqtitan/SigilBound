## Checks the update banner logic headless: a newer release makes Home show "Update available",
## an older or equal one doesn't. Run: godot --headless --path . --script res://tests/update_check.gd
extends SceneTree

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	var main = load("res://scripts/main.gd").new()
	root.add_child(main)
	await process_frame
	var up = root.get_node("Updater")
	var mine: int = up.my_build()
	var ok := true
	for case in [[mine + 5, true], [mine, false]]:
		var body := JSON.stringify({"name": "Sigilbound Godot demo v0.2.0 (build %d)" % case[0]}).to_utf8_buffer()
		up._on_done(HTTPRequest.RESULT_SUCCESS, 200, PackedStringArray(), body)
		main.show_home()
		await process_frame
		var found := false
		for l in main.screen.find_children("*", "Label", true, false):
			if l.text == "Update available":
				found = true
		print("release build %d vs mine %d -> banner %s" % [case[0], mine, found])
		if found != case[1]:
			ok = false
	print("UPDATE OK" if ok else "UPDATE FAILED")
	quit(0 if ok else 1)
