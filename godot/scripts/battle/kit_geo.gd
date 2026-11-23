## Unit-size primitive meshes for the code-built hero kit, matching the three.js geometries in
## game3d/three3d.mjs geos(): same proportions and axes (cylinders along +Y, tori in the XY
## plane, lathes around +Y), so kit recipes carry over number for number.
class_name KitGeo

static var _cache := {}


static func get_mesh(kind: String) -> ArrayMesh:
	if _cache.has(kind):
		return _cache[kind]
	var m: ArrayMesh
	match kind:
		"box", "bbox": m = _box()
		"sphere": m = _sphere(0.5, 18, 12, PI)
		"dome": m = _sphere(0.5, 20, 8, PI / 2)
		"capsule": m = _capsule()
		"cyl": m = _cyl(0.5, 0.5, 2.0, 18)
		"rod": m = _cyl(0.5, 0.5, 1.0, 14)
		"disc": m = _cyl(0.5, 0.5, 1.0, 28)
		"cone": m = _cyl(0.0, 0.5, 1.0, 14)
		"taper": m = _cyl(0.5, 0.32, 1.0, 28)
		"bell": m = _cyl(0.22, 0.5, 1.0, 14)
		"half": m = _cyl(0.5, 0.5, 1.0, 12, PI)
		"ring": m = _torus(0.5, 0.07, 8, 36, TAU)
		"hook": m = _torus(0.5, 0.18, 6, 14, PI * 1.25)
		"crook": m = _torus(0.5, 0.2, 6, 12, PI)
		"octa", "gem": m = _octa()
		"helm": m = _lathe([[0.53, -0.04], [0.55, 0.0], [0.52, 0.04], [0.5, 0.12], [0.48, 0.3], [0.42, 0.5], [0.33, 0.66], [0.2, 0.79], [0.07, 0.86], [0, 0.875]], 28)
		"shell": m = _lathe([[0.54, -0.02], [0.53, 0.03], [0.5, 0.06], [0.47, 0.18], [0.4, 0.36], [0.3, 0.5], [0.18, 0.58], [0.1, 0.63], [0, 0.64]], 24)
		"boss": m = _lathe([[0.5, 0], [0.5, 0.1], [0.42, 0.18], [0.36, 0.42], [0.22, 0.62], [0.08, 0.72], [0, 0.74]], 24)
		"breast": m = _breast()
		_: m = _box()
	_cache[kind] = m
	return m


## Commits with every triangle wound so its front face (clockwise in Godot) faces along its
## vertex normals, whatever order the generator emitted it in.
static func _commit(st: SurfaceTool) -> ArrayMesh:
	var arr := st.commit_to_arrays()
	var v: PackedVector3Array = arr[Mesh.ARRAY_VERTEX]
	var n: PackedVector3Array = arr[Mesh.ARRAY_NORMAL]
	for i in range(0, v.size() - 2, 3):
		var face := (v[i + 1] - v[i]).cross(v[i + 2] - v[i])
		if face.dot(n[i] + n[i + 1] + n[i + 2]) > 0:
			var tv := v[i + 1]
			v[i + 1] = v[i + 2]
			v[i + 2] = tv
			var tn := n[i + 1]
			n[i + 1] = n[i + 2]
			n[i + 2] = tn
	arr[Mesh.ARRAY_VERTEX] = v
	arr[Mesh.ARRAY_NORMAL] = n
	var m := ArrayMesh.new()
	m.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arr)
	return m


static func _box() -> ArrayMesh:
	var b := BoxMesh.new()
	b.size = Vector3.ONE
	var m := ArrayMesh.new()
	m.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, b.get_mesh_arrays())
	return m


## three.js SphereGeometry: phi around Y, theta from the top (0) down to theta_len.
static func _sphere(r: float, ws: int, hs: int, theta_len: float) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var grid := []
	for iy in hs + 1:
		var row := []
		var v := float(iy) / hs
		for ix in ws + 1:
			var u := float(ix) / ws
			var p := Vector3(-r * cos(u * TAU) * sin(v * theta_len), r * cos(v * theta_len), r * sin(u * TAU) * sin(v * theta_len))
			row.append(p)
		grid.append(row)
	for iy in hs:
		for ix in ws:
			var a: Vector3 = grid[iy][ix + 1]
			var b: Vector3 = grid[iy][ix]
			var c: Vector3 = grid[iy + 1][ix]
			var d: Vector3 = grid[iy + 1][ix + 1]
			if iy != 0 or theta_len < 0:
				_tri_s(st, a, b, d, r)
			if iy != hs - 1 or theta_len < PI - 0.01:
				_tri_s(st, b, c, d, r)
	return _commit(st)


static func _tri_s(st: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, _r: float) -> void:
	# Godot front faces are clockwise when seen from outside; three.js is counter-clockwise.
	for p in [a, c, b]:
		st.set_normal(p.normalized() if p.length() > 0 else Vector3.UP)
		st.add_vertex(p)


static func _capsule() -> ArrayMesh:
	var c := CapsuleMesh.new()
	c.radius = 0.5
	c.height = 2.0
	c.radial_segments = 14
	c.rings = 6
	var m := ArrayMesh.new()
	m.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, c.get_mesh_arrays())
	return m


## three.js CylinderGeometry(radiusTop, radiusBottom, height, segments, 1, false, 0, theta_len), centred.
static func _cyl(rt: float, rb: float, h: float, seg: int, theta_len: float = TAU) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var slope := (rb - rt) / h
	var y0 := -h / 2.0
	var y1 := h / 2.0
	for i in seg:
		var t0 := float(i) / seg * theta_len
		var t1 := float(i + 1) / seg * theta_len
		var d0 := Vector3(sin(t0), 0, cos(t0))
		var d1 := Vector3(sin(t1), 0, cos(t1))
		var n0 := Vector3(d0.x, slope, d0.z).normalized()
		var n1 := Vector3(d1.x, slope, d1.z).normalized()
		var a := d0 * rt + Vector3(0, y1, 0)
		var b := d0 * rb + Vector3(0, y0, 0)
		var c := d1 * rb + Vector3(0, y0, 0)
		var d := d1 * rt + Vector3(0, y1, 0)
		_vx(st, a, n0); _vx(st, d, n1); _vx(st, b, n0)
		_vx(st, b, n0); _vx(st, d, n1); _vx(st, c, n1)
		if rt > 0:
			_vx(st, Vector3(0, y1, 0), Vector3.UP); _vx(st, d, Vector3.UP); _vx(st, a, Vector3.UP)
		if rb > 0:
			_vx(st, Vector3(0, y0, 0), Vector3.DOWN); _vx(st, b, Vector3.DOWN); _vx(st, c, Vector3.DOWN)
	if theta_len < TAU - 0.01:
		# close the open sides of a half cylinder
		for t in [0.0, theta_len]:
			var dd := Vector3(sin(t), 0, cos(t))
			var n := Vector3(cos(t), 0, -sin(t)) * (-1.0 if t == 0.0 else 1.0)
			var p0 := Vector3(0, y0, 0)
			var p1 := Vector3(0, y1, 0)
			var p2 := dd * rt + Vector3(0, y1, 0)
			var p3 := dd * rb + Vector3(0, y0, 0)
			_vx(st, p0, n); _vx(st, p1, n); _vx(st, p2, n)
			_vx(st, p0, n); _vx(st, p2, n); _vx(st, p3, n)
	return _commit(st)


static func _vx(st: SurfaceTool, p: Vector3, n: Vector3) -> void:
	st.set_normal(n)
	st.add_vertex(p)


## three.js TorusGeometry(R, r, radial, tubular, arc): in the XY plane, around +Z.
static func _torus(R: float, r: float, radial: int, tubular: int, arc: float) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var pts := []
	var nrm := []
	for j in radial + 1:
		var rp := []
		var rn := []
		for i in tubular + 1:
			var u := float(i) / tubular * arc
			var v := float(j) / radial * TAU
			var p := Vector3((R + r * cos(v)) * cos(u), (R + r * cos(v)) * sin(u), r * sin(v))
			var cen := Vector3(R * cos(u), R * sin(u), 0)
			rp.append(p)
			rn.append((p - cen).normalized())
		pts.append(rp)
		nrm.append(rn)
	for j in range(1, radial + 1):
		for i in range(1, tubular + 1):
			var a := [j, i - 1]
			var b := [j - 1, i - 1]
			var c := [j - 1, i]
			var d := [j, i]
			for tri in [[a, d, b], [b, d, c]]:
				for k in tri:
					_vx(st, pts[k[0]][k[1]], nrm[k[0]][k[1]])
	return _commit(st)


static func _octa() -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var v := [Vector3(0.5, 0, 0), Vector3(-0.5, 0, 0), Vector3(0, 0.5, 0), Vector3(0, -0.5, 0), Vector3(0, 0, 0.5), Vector3(0, 0, -0.5)]
	var faces := [[0, 2, 4], [0, 4, 3], [0, 3, 5], [0, 5, 2], [1, 2, 5], [1, 5, 3], [1, 3, 4], [1, 4, 2]]
	for f in faces:
		var a: Vector3 = v[f[0]]
		var b: Vector3 = v[f[1]]
		var c: Vector3 = v[f[2]]
		var n := (b - a).cross(c - a).normalized()
		if n.dot(a + b + c) < 0:
			n = -n
		# clockwise from outside for Godot
		if (b - a).cross(c - a).dot(n) > 0:
			_vx(st, a, n); _vx(st, c, n); _vx(st, b, n)
		else:
			_vx(st, a, n); _vx(st, b, n); _vx(st, c, n)
	return _commit(st)


## three.js LatheGeometry: profile points (x, y) swept around +Y, smooth normals.
static func _lathe(pts: Array, seg: int) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var n := pts.size()
	# profile normals (2D), averaged at joints
	var pn := []
	for k in n:
		var a: Array = pts[maxi(0, k - 1)]
		var b: Array = pts[mini(n - 1, k + 1)]
		var t := Vector2(b[0] - a[0], b[1] - a[1]).normalized()
		pn.append(Vector2(t.y, -t.x))
	for i in seg:
		var p0 := float(i) / seg * TAU
		var p1 := float(i + 1) / seg * TAU
		for k in n - 1:
			var q := []
			for pair in [[k, p0], [k, p1], [k + 1, p1], [k + 1, p0]]:
				var pt: Array = pts[pair[0]]
				var ph: float = pair[1]
				var nn: Vector2 = pn[pair[0]]
				q.append([Vector3(pt[0] * sin(ph), pt[1], pt[0] * cos(ph)), Vector3(nn.x * sin(ph), nn.y, nn.x * cos(ph)).normalized()])
			for tri in [[0, 2, 1], [0, 3, 2]]:
				for idx in tri:
					_vx(st, q[idx][0], q[idx][1])
	return _commit(st)


## A curved breastplate (roundedPlate 0.3 x 0.27, bowed back toward the chest), centred.
static func _breast() -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var w := 0.3
	var h := 0.27
	var th := 0.03
	var sx := 8
	var sy := 6
	var surf := func(u: float, v: float, back: bool) -> Vector3:
		var x := (u - 0.5) * w
		var y := (v - 0.5) * h
		var z := -0.17 * pow(x / (w / 2), 2) * 0.5 * w - 0.45 * pow(y / (h / 2), 2) * 0.03
		# round the corners in
		var cu := absf(u - 0.5) * 2
		var cv := absf(v - 0.5) * 2
		if cu > 0.7 and cv > 0.7:
			var k := (cu - 0.7) * (cv - 0.7) / 0.09
			x *= 1.0 - 0.12 * k
			y *= 1.0 - 0.12 * k
		return Vector3(x, y, z - (th if back else 0.0))
	for face in [false, true]:
		for j in sy:
			for i in sx:
				var a: Vector3 = surf.call(float(i) / sx, float(j) / sy, face)
				var b: Vector3 = surf.call(float(i + 1) / sx, float(j) / sy, face)
				var c: Vector3 = surf.call(float(i + 1) / sx, float(j + 1) / sy, face)
				var d: Vector3 = surf.call(float(i) / sx, float(j + 1) / sy, face)
				var n := (b - a).cross(d - a).normalized() * (-1.0 if face else 1.0)
				if face:
					_vx(st, a, n); _vx(st, b, n); _vx(st, c, n); _vx(st, a, n); _vx(st, c, n); _vx(st, d, n)
				else:
					_vx(st, a, n); _vx(st, c, n); _vx(st, b, n); _vx(st, a, n); _vx(st, d, n); _vx(st, c, n)
	return _commit(st)


static var _sheets := {}


## sheet(): a curved cloth panel hanging down from y = 0, width w0 at the top flaring to w1,
## bowed back (-Z) by `curve`; drawn double-sided by the cloth material.
static func sheet(w0: float, w1: float, h: float, curve: float, sx: int = 16, sy: int = 8) -> ArrayMesh:
	var key := "%s|%s|%s|%s|%s|%s" % [w0, w1, h, curve, sx, sy]
	if _sheets.has(key):
		return _sheets[key]
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	var g := []
	for j in sy + 1:
		var row := []
		for i in sx + 1:
			var u := float(i) / sx
			var v := float(j) / sy
			var w := w0 + (w1 - w0) * v
			var a := u * 2 - 1
			row.append(Vector3(a * w / 2, -v * h, -curve * (1 - a * a) * (0.7 + 0.3 * v) + sin(a * 3 + 1) * 0.012 * v))
		g.append(row)
	for j in sy:
		for i in sx:
			var a: Vector3 = g[j][i]
			var b: Vector3 = g[j][i + 1]
			var c: Vector3 = g[j + 1][i]
			var d: Vector3 = g[j + 1][i + 1]
			var n1 := (c - a).cross(b - a).normalized()
			var n2 := (c - b).cross(d - b).normalized()
			_vx(st, a, n1); _vx(st, b, n1); _vx(st, c, n1)
			_vx(st, b, n2); _vx(st, d, n2); _vx(st, c, n2)
	var m := _commit(st)
	_sheets[key] = m
	return m
