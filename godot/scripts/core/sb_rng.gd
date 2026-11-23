## The one source of randomness for the game rules (the web game's Math.random()).
## In play it uses Godot's RNG; the parity tests seed it with the same Park-Miller generator
## the fixture generator (tests/make-fixtures.cjs) installs as Math.random, so a battle replays
## exactly as it did in the web code.
class_name SbRng

static var _seeded := false
static var _seed := 1.0
static var _rng := RandomNumberGenerator.new()


static func use_seed(s: int) -> void:
	_seeded = true
	_seed = float(s)


static func use_system() -> void:
	_seeded = false
	_rng.randomize()


## Math.random(): a float in [0, 1).
static func random() -> float:
	if _seeded:
		# seed * 16807 stays below 2^53, so this is exact in doubles (as in JavaScript).
		_seed = fmod(_seed * 16807.0, 2147483647.0)
		return _seed / 2147483647.0
	return _rng.randf()


## state.js rand(a, b)
static func rand(a: float, b: float) -> float:
	return a + random() * (b - a)


## state.js randInt(a, b)
static func rand_int(a: int, b: int) -> int:
	return int(floorf(rand(a, b + 1)))


## state.js pick(arr)
static func pick(arr: Array) -> Variant:
	return arr[int(floorf(random() * arr.size()))]


## JavaScript Math.round (halves round up, toward +infinity).
static func js_round(x: float) -> float:
	return floorf(x + 0.5)
