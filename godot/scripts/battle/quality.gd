## Graphics quality tiers, after game3d/post.mjs: Auto picks Ultra on flagship GPUs (Adreno 730+,
## Mali-G715+ / Immortalis, Xclipse 940+) and High elsewhere (Low on software renderers); the
## player can force a tier in Settings. The frame cap (60, or 30 with "Cooler") applies on top.
## Godot renders directional shadows every frame, so heat is kept down with a 60/30 cap, a short
## shadow distance, and no shadow-casting scenery below High.
class_name Quality

const TIERS := ["Ultra", "High", "Medium", "Low"]
const PRESETS := {
	"Ultra": {"msaa": Viewport.MSAA_4X, "scale": 1.0, "shadow": 4096, "soft": RenderingServer.SHADOW_QUALITY_SOFT_HIGH, "arena": "ultra", "glow": true},
	"High": {"msaa": Viewport.MSAA_4X, "scale": 1.0, "shadow": 2048, "soft": RenderingServer.SHADOW_QUALITY_SOFT_MEDIUM, "arena": "high", "glow": true},
	"Medium": {"msaa": Viewport.MSAA_2X, "scale": 0.85, "shadow": 1024, "soft": RenderingServer.SHADOW_QUALITY_SOFT_LOW, "arena": "medium", "glow": true},
	"Low": {"msaa": Viewport.MSAA_DISABLED, "scale": 0.75, "shadow": 512, "soft": RenderingServer.SHADOW_QUALITY_HARD, "arena": "low", "glow": false},
}


## tierForGpu() in post.mjs, reduced to the demo's rule: flagship GPUs get Ultra, others High.
static func tier_for_gpu(s: String) -> String:
	if s == "":
		return "High"
	var re := RegEx.new()
	if RegEx.create_from_string("(?i)swiftshader|llvmpipe|softpipe|software|microsoft basic").search(s):
		return "Low"
	re.compile("(?i)Adreno\\D{0,8}(\\d{3})")
	var m := re.search(s)
	if m:
		return "Ultra" if int(m.get_string(1)) >= 730 else "High"
	re.compile("(?i)Mali-?G(\\d{2,3})")
	m = re.search(s)
	if m:
		var g := m.get_string(1)
		return "Ultra" if g.length() == 3 and int(g) >= 715 else "High"
	if RegEx.create_from_string("(?i)Immortalis|Xclipse\\s*9[4-9]\\d|Xclipse\\s*\\d{4}").search(s):
		return "Ultra"
	return "High"


static func gpu_name() -> String:
	return RenderingServer.get_video_adapter_name()


## The tier in use: the player's choice, or Auto's pick for this GPU.
static func current() -> String:
	var game = (Engine.get_main_loop() as SceneTree).root.get_node_or_null("Game")
	var choice := String(game.g["settings"].get("quality", "auto")) if game else "auto"
	return choice if PRESETS.has(choice) else tier_for_gpu(gpu_name())


static func preset() -> Dictionary:
	return PRESETS[current()]


## Applies the tier's global settings (shadow atlas size and filter).
static func apply_globals() -> void:
	var p := preset()
	RenderingServer.directional_shadow_atlas_set_size(p["shadow"], true)
	RenderingServer.directional_soft_shadow_filter_set_quality(p["soft"])
