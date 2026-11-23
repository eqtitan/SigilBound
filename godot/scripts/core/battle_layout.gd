## Battle layout and camera framing from game3d/three3d.mjs (slot(), CAMS, resize()). Godot and
## three.js share the same axes (right-handed, +Y up, models face +Z at yaw 0), so the numbers
## carry over unchanged.
class_name BattleLayout

## The rune circle's rim (an ellipse): three.js RIM { cx 0, cz -0.6, rx 4.2, rz 3.95 }.
const RIM_CX := 0.0
const RIM_CZ := -0.6
const RIM_RX := 4.2
const RIM_RZ := 3.95
const SPACING := 2.2 # metres of arc between neighbours
const CENTRE := Vector3(0, 0, -0.6)
const CAM_TARGET := Vector3(0, 1.0, -0.5)

const CAMERAS := {
	"classic": {"name": "Classic", "yaw": 0, "pitch": 34},
	"high": {"name": "Overhead", "yaw": 0, "pitch": 60},
	"low": {"name": "Cinematic", "yaw": 0, "pitch": 16},
	"angle": {"name": "Three-quarter", "yaw": 38, "pitch": 30},
	"front": {"name": "Hero view", "yaw": 180, "pitch": 30},
}
const DEFAULT_CAMERA := "angle"


## Allies along the near arc, enemies along the far arc, ~2.2 m apart.
static func slot(ally: bool, i: int, n: int) -> Vector3:
	var step := SPACING / ((RIM_RX + RIM_RZ) / 2.0)
	var half := 0.0 if n <= 1 else minf(deg_to_rad(70.0), step * (n - 1) / 2.0)
	var t := 0.0 if n <= 1 else -half + i * (2.0 * half / (n - 1))
	var a := t if ally else PI - t # 0 = near side (camera), PI = far side
	return Vector3(RIM_CX + sin(a) * RIM_RX, 0, RIM_CZ + cos(a) * RIM_RZ)


## Yaw that turns a unit at p toward the circle's centre (models face +Z at yaw 0).
static func face_centre_yaw(p: Vector3) -> float:
	return atan2(CENTRE.x - p.x, CENTRE.z - p.z)


static func camera_preset(key: String) -> Dictionary:
	return CAMERAS.get(key, CAMERAS[DEFAULT_CAMERA])


## Portrait screens get a wider lens so a full row fits without pulling far back.
static func fov_for(aspect: float) -> float:
	return 62.0 if aspect < 0.75 else 50.0


static func camera_position(c: Dictionary, dist: float) -> Vector3:
	var pitch := deg_to_rad(float(c["pitch"]))
	var yaw := deg_to_rad(float(c["yaw"]))
	var h := cos(pitch) * dist
	return Vector3(CAM_TARGET.x + sin(yaw) * h, CAM_TARGET.y + sin(pitch) * dist, CAM_TARGET.z + cos(yaw) * h)


## Points that must stay on screen: the whole rim with head height and HUD-tag points above it.
static func frame_points(titan: bool) -> Array:
	var pts := []
	for sx in [-1, 1]:
		for k in 7:
			var ang := k / 6.0 * PI
			var x: float = sx * sin(ang) * (RIM_RX + 0.4)
			var z := RIM_CZ + cos(ang) * (RIM_RZ + 0.4)
			pts.append(Vector3(x, 0, z))
			pts.append(Vector3(x, 2.75 if z > 0 else 3.3, z))
	pts.append(Vector3(0, 3.75, -3.95)) # HUD tags above the back row
	if titan:
		pts.append_array([Vector3(0, 5.6, -4.0), Vector3(-2.3, 3.4, -4.0), Vector3(2.3, 3.4, -4.0)])
	return pts


## Normalized device coordinates of p for a camera at eye looking at CAM_TARGET, or null if behind.
static func project(eye: Vector3, fov_deg: float, aspect: float, p: Vector3) -> Variant:
	var f := (CAM_TARGET - eye).normalized()
	var r := f.cross(Vector3.UP).normalized()
	var u := r.cross(f)
	var d := p - eye
	var z := d.dot(f)
	if z <= 1e-6:
		return null
	var t := tan(deg_to_rad(fov_deg) / 2.0)
	return Vector2(d.dot(r) / (z * t * aspect), d.dot(u) / (z * t))


## The closest distance (binary search, as in three3d.mjs resize()) at which every frame point
## projects inside |x| <= 0.95 and |y| <= 0.84.
static func fit_distance(c: Dictionary, aspect: float, titan: bool) -> float:
	var pts := frame_points(titan)
	var fov := fov_for(aspect)
	var lo := 5.0
	var hi := 40.0
	for i in 22:
		var mid := (lo + hi) / 2.0
		var eye := camera_position(c, mid)
		var fits := true
		for p in pts:
			var q = project(eye, fov, aspect, p)
			if q == null or absf(q.x) > 0.95 or q.y > 0.84 or q.y < -0.84:
				fits = false
				break
		if fits:
			hi = mid
		else:
			lo = mid
	return hi
