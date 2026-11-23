## The "Gilded Sigil" look of the web game (public/game/style.css) for Godot Controls: dark arcane
## panels with burnished gold trim and corner filigree, gilded buttons, serif (Cinzel, OFL)
## display type. Everything is built in code; no image assets.
class_name UiKit

const BG := Color("#0f0c15")
const PANEL := Color("#1d1728")
const PANEL2 := Color("#251e31")
const LINE := Color("#5c4a30")
const TEXT := Color("#f1e9da")
const DIM := Color("#b0a48f")
const GOLD := Color("#f2c45c")
const TRIM := Color("#b88a3e")
const TRIM_HI := Color("#ffe2a0")
const TRIM_LO := Color("#6b4b1c")
const GOOD := Color("#5bd17a")
const BAD := Color("#ff5a5a")

static var display_font: Font
static var display_regular: Font
static var theme: Theme


static func setup() -> Theme:
	if theme:
		return theme
	display_font = load("res://fonts/Cinzel-Bold.woff2")
	display_regular = load("res://fonts/Cinzel-Regular.woff2")
	theme = Theme.new()
	theme.default_font_size = 15
	theme.set_color("font_color", "Label", TEXT)
	# Buttons: gilded plates.
	var gold := _box(Color("#d9a64a"), Color("#4a3110"), 10, 1)
	gold.shadow_color = Color(0, 0, 0, 0.5)
	gold.shadow_size = 3
	gold.shadow_offset = Vector2(0, 2)
	gold.content_margin_left = 14
	gold.content_margin_right = 14
	gold.content_margin_top = 10
	gold.content_margin_bottom = 10
	var gold_hi: StyleBoxFlat = gold.duplicate()
	gold_hi.bg_color = Color("#ecc06a")
	var gold_down: StyleBoxFlat = gold.duplicate()
	gold_down.bg_color = Color("#b98330")
	gold_down.shadow_size = 0
	var gold_off: StyleBoxFlat = gold.duplicate()
	gold_off.bg_color = Color("#5a4a30")
	gold_off.border_color = Color("#2a2010")
	theme.set_stylebox("normal", "Button", gold)
	theme.set_stylebox("hover", "Button", gold_hi)
	theme.set_stylebox("pressed", "Button", gold_down)
	theme.set_stylebox("disabled", "Button", gold_off)
	theme.set_stylebox("focus", "Button", StyleBoxEmpty.new())
	for k in ["font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color"]:
		theme.set_color(k, "Button", Color("#2a1a05"))
	theme.set_color("font_disabled_color", "Button", Color("#2a1a05", 0.6))
	theme.set_font("font", "Button", display_font)
	theme.set_font_size("font_size", "Button", 16)
	# Secondary buttons: dark stone with a gold edge.
	theme.add_type("SecButton")
	theme.set_type_variation("SecButton", "Button")
	var sec := _box(Color("#2c2337"), Color("#6b5432"), 10, 1)
	sec.content_margin_left = 12
	sec.content_margin_right = 12
	sec.content_margin_top = 8
	sec.content_margin_bottom = 8
	var sec_hi: StyleBoxFlat = sec.duplicate()
	sec_hi.bg_color = Color("#3a2f45")
	var sec_on: StyleBoxFlat = sec.duplicate()
	sec_on.bg_color = Color("#3d3020")
	sec_on.border_color = GOLD
	var sec_off: StyleBoxFlat = sec.duplicate()
	sec_off.bg_color = Color("#1a1520")
	theme.set_stylebox("normal", "SecButton", sec)
	theme.set_stylebox("hover", "SecButton", sec_hi)
	theme.set_stylebox("pressed", "SecButton", sec_on)
	theme.set_stylebox("hover_pressed", "SecButton", sec_on)
	theme.set_stylebox("disabled", "SecButton", sec_off)
	for k in ["font_color", "font_hover_color", "font_focus_color"]:
		theme.set_color(k, "SecButton", Color("#f3e3c3"))
	theme.set_color("font_pressed_color", "SecButton", GOLD)
	theme.set_color("font_hover_pressed_color", "SecButton", GOLD)
	theme.set_color("font_disabled_color", "SecButton", Color("#f3e3c3", 0.35))
	theme.set_font_size("font_size", "SecButton", 14)
	# Panels.
	var card := _box(PANEL, LINE, 14, 1)
	card.shadow_color = Color(0, 0, 0, 0.55)
	card.shadow_size = 8
	card.shadow_offset = Vector2(0, 4)
	for side in [SIDE_LEFT, SIDE_RIGHT, SIDE_TOP, SIDE_BOTTOM]:
		card.set_content_margin(side, 14)
	theme.set_stylebox("panel", "PanelContainer", card)
	theme.set_stylebox("panel", "Panel", card)
	# Progress bars: gold in a recessed groove.
	var groove := _box(Color("#07050a"), Color("#3a2f22"), 3, 1)
	var fill := _box(Color("#e0a63a"), Color("#e0a63a"), 3, 0)
	theme.set_stylebox("background", "ProgressBar", groove)
	theme.set_stylebox("fill", "ProgressBar", fill)
	theme.set_constant("separation", "VBoxContainer", 10)
	theme.set_constant("separation", "HBoxContainer", 8)
	var sb := StyleBoxEmpty.new()
	theme.set_stylebox("panel", "ScrollContainer", sb)
	return theme


static func _box(bg: Color, border: Color, radius: int, bw: int) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.border_color = border
	s.set_border_width_all(bw)
	s.set_corner_radius_all(radius)
	s.anti_aliasing = true
	return s


static func label(text: String, size: int = 15, color: Color = TEXT, display: bool = false) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size)
	l.add_theme_color_override("font_color", color)
	if display:
		l.add_theme_font_override("font", display_font)
	# Only paragraphs wrap: a wrapping label in a row has no minimum width and collapses.
	if text.length() > 34 or text.contains("\n"):
		l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	return l


static func heading(text: String, size: int = 20) -> Label:
	var l := label(text, size, Color("#f6e6c2"), true)
	l.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.8))
	l.add_theme_constant_override("shadow_offset_y", 2)
	return l


## The gold "SIGILBOUND" logo with an engraved shadow.
static func logo(size: int = 40, spaced: bool = false) -> Label:
	var l := label("SIGILBOUND", size, GOLD, true)
	if spaced:
		# letter-spaced like the web's logo (letter-spacing: 5px)
		var fv := FontVariation.new()
		fv.base_font = display_font
		fv.spacing_glyph = int(size * 0.16)
		l.add_theme_font_override("font", fv)
	l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	l.add_theme_color_override("font_shadow_color", Color("#3a2508"))
	l.add_theme_constant_override("shadow_offset_y", 3)
	l.add_theme_color_override("font_outline_color", Color("#1a1005"))
	l.add_theme_constant_override("outline_size", 4)
	return l


static func button(text: String, cb: Callable, secondary: bool = false) -> Button:
	var b := Button.new()
	b.text = text
	if secondary:
		b.theme_type_variation = "SecButton"
	b.pressed.connect(cb)
	b.focus_mode = Control.FOCUS_NONE
	b.custom_minimum_size.y = 44 if not secondary else 36
	return b


static func card() -> GildedPanel:
	var p := GildedPanel.new()
	return p


static func vbox(sep: int = 10) -> VBoxContainer:
	var v := VBoxContainer.new()
	v.add_theme_constant_override("separation", sep)
	return v


static func hbox(sep: int = 8) -> HBoxContainer:
	var h := HBoxContainer.new()
	h.add_theme_constant_override("separation", sep)
	return h


static func element_color(el: String) -> Color:
	return Color(WebData.ELEMENTS[el]["color"])


static func rarity_color(r: String) -> Color:
	return Color(WebData.RARITY[r]["color"])


static func stars(n: float) -> String:
	return "★".repeat(int(n))
