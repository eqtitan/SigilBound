## Autoload "Updater": the in-game update check, like the web app's update.js.
## On launch, every few minutes while open, and whenever the app comes back to the foreground,
## it reads the "godot-latest" GitHub release. If that build is newer than this one, `changed`
## fires and Home shows a banner whose button downloads the new APK (Android then installs it).
## Does nothing in local "dev" builds or scripted test runs.
extends Node

signal changed

const RELEASE_API := "https://api.github.com/repos/eqtitan/SigilBound/releases/tags/godot-latest"
const APK_URL := "https://github.com/eqtitan/SigilBound/releases/download/godot-latest/Sigilbound-Godot.apk"
const POLL_SECONDS := 300.0
const MIN_GAP_MS := 2 * 60 * 1000

var latest_version := ""
var latest_build := 0 # 0 = nothing newer
var _http: HTTPRequest
var _last_check := -MIN_GAP_MS
var _busy := false


func my_build() -> int:
	var m := RegEx.create_from_string("build (\\d+)").search(Game.BUILD)
	return int(m.get_string(1)) if m else 0


func enabled() -> bool:
	return my_build() > 0 and not Game.smoke and not Game.scroll_test and Game.shots_dir == ""


func _ready() -> void:
	if not enabled():
		return
	_http = HTTPRequest.new()
	_http.timeout = 15.0
	add_child(_http)
	_http.request_completed.connect(_on_done)
	var t := Timer.new()
	t.wait_time = POLL_SECONDS
	t.autostart = true
	t.timeout.connect(check.bind(true))
	add_child(t)
	get_tree().create_timer(2.5).timeout.connect(check.bind(true))


func _notification(what: int) -> void:
	if what == NOTIFICATION_APPLICATION_FOCUS_IN or what == NOTIFICATION_APPLICATION_RESUMED:
		check()


func check(force: bool = false) -> void:
	if _http == null or _busy:
		return
	if not force and Time.get_ticks_msec() - _last_check < MIN_GAP_MS:
		return
	_last_check = Time.get_ticks_msec()
	_busy = true
	var err := _http.request(RELEASE_API, ["Accept: application/vnd.github+json", "User-Agent: Sigilbound-Godot"])
	if err != OK:
		_busy = false


func _on_done(result: int, code: int, _headers: PackedStringArray, body: PackedByteArray) -> void:
	_busy = false
	if result != HTTPRequest.RESULT_SUCCESS or code != 200:
		return # offline or rate-limited: try again later
	var rel = JSON.parse_string(body.get_string_from_utf8())
	if typeof(rel) != TYPE_DICTIONARY:
		return
	var m := RegEx.create_from_string("v(\\d+\\.\\d+\\.\\d+)\\s*\\(build\\s*(\\d+)\\)").search(str(rel.get("name", "")))
	if m == null:
		return
	var build := int(m.get_string(2))
	var before := latest_build
	if build > my_build():
		latest_build = build
		latest_version = m.get_string(1)
	else:
		latest_build = 0
	if latest_build != before:
		changed.emit()


func download() -> void:
	OS.shell_open(APK_URL)
