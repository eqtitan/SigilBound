"""Cloth and wing pieces for the high-detail heroes (blenderforge/ubc.py kit()): gowns and robes draped
with Blender's cloth simulation, sashes and belts, bell sleeves, and layered wings (feathered, insect,
membrane, glider and helm wings). Replaces the old generated bell skirt and slat wings (owner review
2026-11-26: "identify any remaining placeholders and fix them").

Gowns are simulated in the rest pose: a tube pinned at the waist falls under gravity onto the hips and
legs (collision with the dressed body) and settles into folds; the result is skinned from the nearest
body surface (pelvis near the waist, blending to the thighs toward the hem) so it swings with the legs.
The design text picks length, flare, layers, slits, a train and the hem treatment (tendrils, bells,
fin tail, trim band). Deterministic: no randomness beyond a hash of the hero id.
"""
import bpy, bmesh, math, re
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree


def _has(txt, *ks):
    return any(re.search(r"\b" + k + r"(e?s)?\b", txt) for k in ks)


def _select(*objs):
    for x in bpy.context.selected_objects: x.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def _apply_all(o):
    _select(o)
    for md in list(o.modifiers):
        if md.type == "ARMATURE": continue
        bpy.ops.object.modifier_apply(modifier=md.name)


def _hash(s):
    h = 17
    for ch in s: h = (h * 31 + ord(ch)) & 0xFFFFFFFF
    return h


def _body_meshes():
    return [o for o in bpy.context.scene.objects if o.type == "MESH" and not o.name.startswith(("Kit_", "Icosphere"))
            and len(o.data.polygons) > 200 and not any(m and "Eye" in m.name for m in o.data.materials)]


def _rest(arm, name):
    b = arm.data.bones[name]
    return arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local


# ---------------------------------------------------------------- gown design from the text

def gown_spec(c):
    """Length, flare, layers, openings and hem from the design (body text + kit + accessories)."""
    o = c.get("outfit", {}) or {}
    txt = " ".join([str(o.get("body", ""))] + [str(k) for k in c.get("kit", [])] + [str(x) for x in (o.get("acc") or [])]).lower()
    h = _hash(c.get("id", ""))
    s = {"length": "floor", "flare": 0.55 + (h % 7) * 0.04, "layers": 1, "slit": False, "open": None, "train": 0.0,
         "hem": "trim", "sleeves": False, "belt": "sash", "stiff": False, "tails": False}
    if _has(txt, "tabard"): s.update(length="knee", flare=0.25, open="sides", hem="trim")
    if _has(txt, "apron", "apron-skirt"): s.update(length="knee", flare=0.2, open="back", hem="plain", belt="leather", stiff=True)
    if _has(txt, "dancer", "wrap"): s.update(length="calf", flare=1.0, slit=True)
    if _has(txt, "armour dress", "scale-armour", "plate"): s.update(length="calf" if "dress" in txt else "floor", flare=0.4, stiff=True)
    if re.search(r"layered (\w+ )?(\w+ )?(robe|gown)", txt) or "layered robe" in txt: s["layers"] = 2   # not "layered sleeves"
    if _has(txt, "train"): s["train"] = 0.45
    if _has(txt, "tendril"): s["hem"] = "tendrils"
    if _has(txt, "bell") and _has(txt, "fringe", "fringed"): s.update(hem="bells", flare=1.05, stiff=True)
    if _has(txt, "fin", "fin-tail"): s.update(hem="fin", train=max(s["train"], 0.3))
    if _has(txt, "frost", "frosted"): s["hem"] = "frost"
    if _has(txt, "sleeve") or "layered sleeves" in txt or "wizard_body robe" in txt: s["sleeves"] = True
    if _has(txt, "rope belt", "rope"): s["belt"] = "rope"
    if _has(txt, "sash"): s.update(belt="sash", tails=True)
    if _has(txt, "habit", "monk"): s.update(flare=0.45, tails=s["belt"] == "sash")
    return s


def needs_gown(c, text):
    o = c.get("outfit", {}) or {}
    return _has(text, "robe", "skirt", "dress", "gown", "habit", "tabard") or str(o.get("legs", "")).startswith("none")


# ---------------------------------------------------------------- weights from the body

class _BodyWeights:
    """Nearest-surface skin weights from the dressed body, blended toward the pelvis near the waist."""

    def __init__(self):
        self.verts, self.polys, self.src = [], [], []
        for o in _body_meshes():
            mw = o.matrix_world; base = len(self.verts)
            names = {g.index: g.name for g in o.vertex_groups}
            vw = []
            for v in o.data.vertices:
                vw.append({names[g.group]: g.weight for g in v.groups if g.weight > 0.01 and g.group in names})
            self.verts += [mw @ v.co for v in o.data.vertices]
            for p in o.data.polygons:
                self.polys.append([base + i for i in p.vertices]); self.src.append([vw[i] for i in p.vertices])
        self.bvh = BVHTree.FromPolygons(self.verts, self.polys)

    def near(self, p):
        hit = self.bvh.find_nearest(p)
        if hit[0] is None: return {"pelvis": 1.0}
        acc = {}
        for w in self.src[hit[2]]:
            for k, v in w.items(): acc[k] = acc.get(k, 0) + v
        tot = sum(acc.values()) or 1
        return {k: v / tot for k, v in acc.items()}


LEG_MAP = {"calf_l": "thigh_l", "foot_l": "thigh_l", "ball_l": "thigh_l", "calf_r": "thigh_r", "foot_r": "thigh_r", "ball_r": "thigh_r",
           "thigh_twist_01_l": "thigh_l", "thigh_twist_01_r": "thigh_r", "calf_twist_01_l": "thigh_l", "calf_twist_01_r": "thigh_r",
           "calf_twist_02_l": "thigh_l", "calf_twist_02_r": "thigh_r", "thigh_twist_02_l": "thigh_l", "thigh_twist_02_r": "thigh_r"}


def skirt_weights(bw, topz, kneez, leg_share=0.45, arm=None):
    """Callable for ubc.bind: pelvis at the waist, blending toward the thighs below (at most leg_share,
    reached at the knee), split between the legs by a smooth left/right ramp across the middle so the
    front and back never tear open between the legs, and a skirt sways rather than kicking up with the
    knee. (Nearest-surface weights flipped abruptly at the midline: tested on Jog_Fwd_Loop.)"""
    import kit_cloth as _k  # noqa
    xl = xr = py = None
    if arm is not None:
        xl = _rest(arm, "thigh_l")[0].x; xr = _rest(arm, "thigh_r")[0].x; py = _rest(arm, "pelvis")[0].y
    def w(p):
        a = max(0.0, min(1.0, (topz - p.z) / max(1e-6, topz - kneez))); a = a * a * (3 - 2 * a)
        # the front panel rides the knees more than the back (a raised knee mustn't poke through)
        if py is not None:
            fy = max(0.0, min(1.0, (py + 0.06 - p.y) / 0.2)); a *= leg_share * (0.85 + 0.5 * fy)
        else: a *= leg_share
        if xl is None:
            near = bw.near(p); l = sum(v for k, v in near.items() if LEG_MAP.get(k, k) == "thigh_l"); r = sum(v for k, v in near.items() if LEG_MAP.get(k, k) == "thigh_r")
            sl = l / (l + r) if l + r > 0 else 0.5
        else:
            u = (p.x - (xl + xr) / 2) / max(1e-6, (xl - xr) * 1.6) + 0.5   # wide ramp: 50/50 at the centre
            u = max(0.0, min(1.0, u)); sl = u * u * (3 - 2 * u)
        out = {"pelvis": 1.0 - a, "thigh_l": a * sl, "thigh_r": a * (1 - sl)}
        return {k: v for k, v in out.items() if v > 1e-3}
    return w


# ---------------------------------------------------------------- the cloth simulation

def _tube(name, ring_pts, rows, hem_z_of, cols, mat, top_inset=0.0):
    """A tube from the waist ring (list of (angle, Vector)) down to hem_z_of(angle), flaring linearly."""
    verts, faces = [], []
    for r in range(rows + 1):
        t = r / rows
        for i in range(cols):
            a, top = ring_pts[i]
            hz = hem_z_of(a)
            z = top.z + (hz - top.z) * t
            verts.append(Vector((top.x, top.y, z)))
    for r in range(rows):
        for i in range(cols):
            a_, b_ = r * cols + i, r * cols + (i + 1) % cols
            faces.append((a_, a_ + cols, b_ + cols, b_))   # outward normals
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); o.data.materials.append(mat)
    return o


def _waist_ring(cx, cy, z, cols, bvh, pad):
    """Points hugging the body at height z (ray-cast inward around the body)."""
    pts = []
    for i in range(cols):
        a = i / cols * 2 * math.pi
        d = Vector((math.cos(a), math.sin(a), 0))
        hit = bvh.ray_cast(Vector((cx, cy, z)) + d * 0.6, -d, 0.6)
        r = ((hit[0] - Vector((cx, cy, z))).length if hit[0] is not None else 0.17) + pad
        pts.append((a, Vector((cx + d.x * r, cy + d.y * r, z))))
    return pts


def simulate_gown(c, arm, mat, trim_mat, spec=None, bvh=None, colliders_extra=(), radius_pad=0.014, length_scale=1.0, name="Kit_Robe"):
    """One draped skirt layer. Returns the mesh object (not yet skinned)."""
    spec = spec or gown_spec(c)
    from ubc import body_bvh  # noqa
    bvh = bvh or body_bvh()
    ph, _ = _rest(arm, "pelvis"); kl, _ = _rest(arm, "calf_l"); fl, _ = _rest(arm, "foot_l")
    topz = ph.z + 0.07
    ground = 0.0
    hem = {"floor": ground + 0.045, "ankle": fl.z + 0.06, "calf": (kl.z + fl.z) * 0.5 + 0.05, "knee": kl.z - 0.04}[spec["length"]]
    hem = topz - (topz - hem) * length_scale
    cols, rows = 56, 26
    ring = _waist_ring(ph.x, ph.y + 0.01, topz, cols, bvh, radius_pad)
    back = math.pi / 2  # +Y is the back on this rig
    def hem_z(a):
        tr = spec["train"] * max(0.0, math.cos(a - back)) ** 2 if length_scale >= 0.99 else 0.0
        return hem - tr
    o = _tube(name, ring, rows, hem_z, cols, mat)
    # flare the initial shape outward with depth so gravity gathers it into folds; an invisible A-line
    # petticoat of the same oval section (smaller flare, always inside the cloth) gives the gown its
    # volume so it can't collapse between the legs (removed after the simulation).
    flare = spec["flare"]
    pet_me = o.data.copy(); pet = bpy.data.objects.new("SBPetticoat", pet_me); bpy.context.scene.collection.objects.link(pet)
    for v, pv in zip(o.data.vertices, pet_me.vertices):
        t = max(0.0, min(1.0, (topz - v.co.z) / max(1e-6, topz - hem)))
        d = Vector((v.co.x - ph.x, v.co.y - ph.y - 0.01, 0))
        if d.length > 1e-6:
            base = d.length; u = d / base
            v.co.x = ph.x + u.x * (base + flare * 0.42 * t ** 1.1); v.co.y = ph.y + 0.01 + u.y * (base + flare * 0.42 * t ** 1.1)
            rp = base - 0.016 + flare * 0.26 * t ** 1.3
            pv.co.x = ph.x + u.x * rp; pv.co.y = ph.y + 0.01 + u.y * rp; pv.co.z = v.co.z - 0.02 * t
    # openings
    bm = bmesh.new(); bm.from_mesh(o.data)
    bm.faces.ensure_lookup_table(); bm.verts.ensure_lookup_table()
    dead = set()
    front = -math.pi / 2
    def ang(f):
        cc = f.calc_center_median(); return math.atan2(cc.y - ph.y, cc.x - ph.x)
    def tdepth(f):
        return (topz - f.calc_center_median().z) / max(1e-6, topz - hem)
    def angdist(a, b):
        return abs((a - b + math.pi) % (2 * math.pi) - math.pi)
    for f in bm.faces:
        a, t = ang(f), tdepth(f)
        if spec["slit"] and t > 0.35 and angdist(a, front + 0.35) < 0.12: dead.add(f)
        if spec["open"] == "sides" and t > 0.12 and (angdist(a, 0) < 0.35 or angdist(a, math.pi) < 0.35): dead.add(f)
        if spec["open"] == "back" and angdist(a, back) < 1.25: dead.add(f)
        if spec["hem"] == "tendrils" and t > 0.62:
            seg = (math.degrees(a) % 360) / 360 * 14
            k = seg - math.floor(seg)                      # 0..1 across one tendril sector
            width = 0.5 * (1.0 - (t - 0.62) / 0.38) + 0.06  # tapering to a point
            if abs(k - 0.5) > width: dead.add(f)
        if spec["hem"] == "fin" and t > 0.8 and angdist(a, back) < 0.25 and t < 1.2: dead.add(f)  # split fin tail
    bmesh.ops.delete(bm, geom=list(dead), context="FACES")
    # trim band on the bottom two rows (accent), skipped for plain hems
    o.data.materials.append(trim_mat)
    for f in bm.faces:
        t = tdepth(f)
        if spec["hem"] in ("trim", "frost", "bells", "fin") and t > (1.0 - 2.2 / rows): f.material_index = 1
    bm.to_mesh(o.data); bm.free()
    # pin group: the waist ring
    pin = o.vertex_groups.new(name="pin")
    pin.add([v.index for v in o.data.vertices if v.co.z > topz - 0.005], 1.0, "REPLACE")
    # colliders: the dressed body (and lower layers), plus the floor
    cols_objs = _body_meshes() + list(colliders_extra)
    for b in cols_objs:
        if not any(m.type == "COLLISION" for m in b.modifiers):
            cm = b.modifiers.new("SBCollision", "COLLISION")
        b.collision.thickness_outer = 0.008; b.collision.cloth_friction = 6
    pet.modifiers.new("SBCollision", "COLLISION"); pet.collision.thickness_outer = 0.01; pet.collision.cloth_friction = 2
    cols_objs.append(pet)
    bpy.ops.mesh.primitive_plane_add(size=4, location=(0, 0, ground))
    floor = bpy.context.object; floor.name = "SBFloor"; floor.modifiers.new("SBCollision", "COLLISION")
    cl = o.modifiers.new("Cloth", "CLOTH"); st = cl.settings
    st.quality = 6; st.mass = 0.25; st.air_damping = 2.0
    st.tension_stiffness = st.compression_stiffness = 15 if not spec["stiff"] else 30
    st.shear_stiffness = 6; st.bending_stiffness = 0.4 if not spec["stiff"] else 4.0
    st.vertex_group_mass = "pin"; st.pin_stiffness = 1.0
    cl.collision_settings.distance_min = 0.006; cl.collision_settings.use_self_collision = False
    cl.collision_settings.collision_quality = 3
    sc = bpy.context.scene; frames = 48
    cl.point_cache.frame_start = 1; cl.point_cache.frame_end = frames
    for f in range(1, frames + 1): sc.frame_set(f)
    _select(o)
    bpy.ops.object.modifier_apply(modifier="Cloth")
    sc.frame_set(1)
    for b in cols_objs:
        for m in [m for m in b.modifiers if m.name == "SBCollision"]: b.modifiers.remove(m)
    bpy.data.objects.remove(floor, do_unlink=True); bpy.data.objects.remove(pet, do_unlink=True)
    if o.vertex_groups.get("pin"): o.vertex_groups.remove(o.vertex_groups["pin"])
    o["topz"] = topz; o["hemz"] = hem
    return o


def _thicken(o, t=0.006):
    _select(o)
    sol = o.modifiers.new("S", "SOLIDIFY"); sol.thickness = t; sol.offset = 1.0
    bpy.ops.object.modifier_apply(modifier="S"); bpy.ops.object.shade_smooth()


def _lathe(profile, segs, mat, center, axis_z=True):
    """A surface of revolution about +Z from (radius, z) pairs, at `center`."""
    verts, faces = [], []
    for (r, z) in profile:
        for i in range(segs):
            a = i / segs * 2 * math.pi
            verts.append(Vector((center.x + math.cos(a) * r, center.y + math.sin(a) * r, center.z + z)))
    for j in range(len(profile) - 1):
        for i in range(segs):
            a_, b_ = j * segs + i, j * segs + (i + 1) % segs
            faces.append((a_, b_, b_ + segs, a_ + segs))
    me = bpy.data.meshes.new("Kit_Lathe"); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new("Kit_Lathe", me); bpy.context.scene.collection.objects.link(o); o.data.materials.append(mat)
    return o


def _belt(arm, gown, mat, kind, tails, rope_mat=None):
    """A sash, leather belt or rope cord hugging the gown's waistline, with hanging tails for sashes and
    ropes. Returns [(obj, weightsfn-or-dict)]."""
    from ubc import body_bvh  # noqa
    bvh = body_bvh(); ph, _ = _rest(arm, "pelvis"); topz = gown["topz"]
    out = []
    if kind == "rope":
        ring = _waist_ring(ph.x, ph.y + 0.01, topz - 0.02, 40, bvh, 0.03)
        cu = bpy.data.curves.new("Rope", "CURVE"); cu.dimensions = "3D"; cu.bevel_depth = 0.009; cu.bevel_resolution = 2
        sp = cu.splines.new("POLY"); sp.points.add(len(ring) - 1); sp.use_cyclic_u = True
        for p, (_, v) in zip(sp.points, ring): p.co = (v.x, v.y, v.z, 1)
        # a knotted end hanging at the front-left
        sp2 = cu.splines.new("POLY"); hang = [Vector((ph.x + 0.07, ph.y - 0.15 - 0.01 * k, topz - 0.02 - 0.06 * k)) for k in range(7)]
        sp2.points.add(len(hang) - 1)
        for p, v in zip(sp2.points, hang): p.co = (v.x, v.y, v.z, 1)
        ob = bpy.data.objects.new("Kit_Belt", cu); bpy.context.scene.collection.objects.link(ob)
        _select(ob); bpy.ops.object.convert(target="MESH"); ob = bpy.context.object; ob.data.materials.append(mat)
        out.append(ob)
    else:
        h = 0.06 if kind == "sash" else 0.045
        ring_hi = _waist_ring(ph.x, ph.y + 0.01, topz + h * 0.4, 48, bvh, 0.018)
        ring_lo = _waist_ring(ph.x, ph.y + 0.01, topz - h * 0.6, 48, bvh, 0.026)
        verts = [p for _, p in ring_hi] + [p for _, p in ring_lo]; n = 48
        faces = [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
        me = bpy.data.meshes.new("Kit_Belt"); me.from_pydata(verts, [], faces); me.update()
        ob = bpy.data.objects.new("Kit_Belt", me); bpy.context.scene.collection.objects.link(ob); ob.data.materials.append(mat)
        _thicken(ob, 0.006); out.append(ob)
        if kind == "leather":   # a buckle
            bpy.ops.mesh.primitive_torus_add(major_segments=4, minor_segments=4, major_radius=0.03, minor_radius=0.006,
                                             location=(ph.x, ring_hi[int(n * 0.75)][1].y - 0.004, topz - h * 0.1), rotation=(math.radians(90), 0, math.radians(45)))
            bk = bpy.context.object; bk.data.materials.append(rope_mat or mat); out.append(bk)
        if tails:   # two sash ends knotted at the left hip, hanging to mid-thigh
            for k, (dx, ln) in enumerate(((0.0, 0.34), (0.035, 0.27))):
                x0 = ph.x + 0.09 + dx; y0 = ring_lo[int(n * 0.8)][1].y - 0.01
                verts = []; rows = 8
                for r in range(rows + 1):
                    t = r / rows; w = 0.05 * (1 - 0.3 * t)
                    zz = topz - 0.03 - ln * t; yy = y0 - 0.02 * t
                    verts += [Vector((x0 - w / 2 + 0.03 * t, yy, zz)), Vector((x0 + w / 2 + 0.03 * t, yy, zz))]
                faces = [(2 * r, 2 * r + 1, 2 * r + 3, 2 * r + 2) for r in range(rows)]
                me = bpy.data.meshes.new("Kit_Belt"); me.from_pydata(verts, [], faces); me.update()
                tb = bpy.data.objects.new("Kit_Belt", me); bpy.context.scene.collection.objects.link(tb); tb.data.materials.append(mat)
                _thicken(tb, 0.004); out.append(tb)
    return out


def _bells(arm, gown_obj, mat, n=18):
    """Small bells hung around the hem (Seraphine's bell fringe)."""
    vs = [gown_obj.matrix_world @ v.co for v in gown_obj.data.vertices]
    ph, _ = _rest(arm, "pelvis")
    low = min(v.z for v in vs)
    ring = [v for v in vs if v.z < low + 0.03]
    out, used = [], []
    for k in range(n):
        a = k / n * 2 * math.pi
        best = max(ring, key=lambda v: (v - ph).normalized().dot(Vector((math.cos(a), math.sin(a), 0))) if (v - ph).length > 0 else -9)
        if any((best - u).length < 0.03 for u in used): continue
        used.append(best)
        b = _lathe([(0.001, 0.0), (0.012, -0.004), (0.016, -0.018), (0.024, -0.032), (0.024, -0.036)], 10, mat, best + Vector((0, 0, -0.004)))
        b.name = "Kit_Bell"; out.append(b)
    return out


def _sleeves(arm, mat, trim_mat):
    """Bell sleeves: a flared cuff over each forearm, two layers (the lower one in the trim colour)."""
    out = []
    for side in ("l", "r"):
        e, _ = _rest(arm, "lowerarm_" + side); hnd, _ = _rest(arm, "hand_" + side)
        axis = (hnd - e).normalized(); L = (hnd - e).length
        # wide sleeves from above the elbow to past the wrist: an inner layer in the trim colour shows
        # below the outer one (the "layered sleeves")
        for layer, (m, r0, r1, extra) in enumerate(((trim_mat, 0.052, 0.078, 0.07), (mat, 0.058, 0.074, 0.0))):
            prof = [(r0, 0.0), (r0 + 0.004, -(L * 0.45)), (r0 + (r1 - r0) * 0.6, -(L * 0.85)), (r1, -(L + extra))]
            o = _lathe([(r, z) for r, z in prof], 20, m, Vector((0, 0, 0)))
            # the lathe runs down -Z; turn -Z onto the forearm axis, start at the elbow
            q = Vector((0, 0, -1)).rotation_difference(axis)
            o.matrix_world = Matrix.Translation(e - axis * 0.06) @ q.to_matrix().to_4x4()
            _select(o); bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            _thicken(o, 0.004); o.name = "Kit_Sleeve"
            out.append((o, {"lowerarm_" + side: 0.85, "upperarm_" + side: 0.15}))
    return out


def neckline_of(c):
    """high | off | v | square | scoop, from the design text (else picked by the hero id)."""
    o = c.get("outfit", {}) or {}
    t = " ".join([str(o.get("body", ""))] + [str(k) for k in c.get("kit", [])] + [str(x) for x in (o.get("acc") or [])]).lower()
    if _has(t, "high collar", "high-necked", "stand collar", "collar", "wimple", "habit"): return "high"
    if _has(t, "off-shoulder", "bare shoulders", "strapless", "bare-shouldered"): return "off"
    if _has(t, "v-neck", "plunging", "wrap", "dancer"): return "v"
    if _has(t, "square", "armour", "scale", "plate"): return "square"
    return ("square", "scoop", "high")[_hash(c.get("id", "")) % 3]


def _torso_target(arm):
    """A temporary copy of the dressed body with the arms cut away (beyond the shoulder joints), so the
    bodice wraps the torso and neck only and never webs across to the upper arms."""
    cx = _rest(arm, "pelvis")[0].x
    ux = min(abs(_rest(arm, "upperarm_l")[0].x - cx), abs(_rest(arm, "upperarm_r")[0].x - cx))
    dg = bpy.context.evaluated_depsgraph_get()
    bm = bmesh.new()
    for o in _body_meshes():
        me = o.evaluated_get(dg).to_mesh(); tmp = bmesh.new(); tmp.from_mesh(me); tmp.transform(o.matrix_world)
        mm = bpy.data.meshes.new("SBTorsoTmp"); tmp.to_mesh(mm); tmp.free(); bm.from_mesh(mm); bpy.data.meshes.remove(mm)
        o.evaluated_get(dg).to_mesh_clear()
    nz = _rest(arm, "neck_01")[0].z
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if abs(v.co.x - cx) > ux - 0.015 or v.co.z > nz + 0.06], context="VERTS")  # no arms, no head
    me = bpy.data.meshes.new("SBTorso"); bm.to_mesh(me); bm.free()
    t = bpy.data.objects.new("SBTorso", me); bpy.context.scene.collection.objects.link(t)
    return t, cx, ux


def make_bodice(c, arm, mats, topz):
    """A fitted bodice over the torso (owner polish pass): a cylinder projected onto the dressed torso (so it
    sits over the fallback top, 9 mm out), cut to the design's neckline and armholes, with the cut edges in
    the trim colour. Top and skirt then read as one garment. Returns the mesh (not skinned)."""
    neck = neckline_of(c)
    tgt, cx, ux = _torso_target(arm)
    cy = _rest(arm, "spine_03")[0].y
    nz = _rest(arm, "neck_01")[0].z; chest = (_rest(arm, "spine_03")[0].z + 2 * nz) / 3        # upper chest
    uz = (_rest(arm, "upperarm_l")[0].z + _rest(arm, "upperarm_r")[0].z) / 2
    z0, z1 = topz - 0.035, nz + (0.07 if neck == "high" else 0.0)
    cols, rows = 56, 26
    verts, faces = [], []
    for r in range(rows + 1):
        z = z0 + (z1 - z0) * r / rows
        for i in range(cols):
            a = i / cols * 2 * math.pi
            verts.append(Vector((cx + math.cos(a) * 0.34, cy + math.sin(a) * 0.3, z)))
    for r in range(rows):
        for i in range(cols):
            a_, b_ = r * cols + i, r * cols + (i + 1) % cols
            faces.append((a_, a_ + cols, b_ + cols, b_))
    me = bpy.data.meshes.new("Kit_Bodice"); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new("Kit_Bodice", me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(mats["main"]); o.data.materials.append(mats["acc"])
    sw = o.modifiers.new("SW", "SHRINKWRAP"); sw.target = tgt; sw.wrap_method = "NEAREST_SURFACEPOINT"; sw.offset = 0.009
    _select(o); bpy.ops.object.modifier_apply(modifier="SW")
    bpy.data.objects.remove(tgt, do_unlink=True)
    bm = bmesh.new(); bm.from_mesh(o.data)
    dead = set()
    for f in bm.faces:
        cc = f.calc_center_median(); x = abs(cc.x - cx); front = cc.y < cy
        if x > ux - 0.004 and cc.z > uz - 0.075: dead.add(f)                     # armholes (above the armpit; the shoulders stay covered)
        if neck == "high":
            if cc.z > nz + 0.055: dead.add(f)
            continue
        cyn = _rest(arm, "neck_01")[0].y
        if cc.z > nz + 0.035 or (cc.z > nz - 0.03 and math.hypot(cc.x - cx, cc.y - cyn) < 0.078): dead.add(f)   # the neck stays bare (shoulders kept)
        if neck == "off" and cc.z > uz - 0.055: dead.add(f)
        if neck == "square" and front and cc.z > chest and x < 0.058: dead.add(f)
        if neck == "scoop" and front and cc.z > chest and x < 0.062 * math.sqrt(max(0.0, min(1.0, (cc.z - chest) / max(1e-6, nz - chest)))) + 0.015: dead.add(f)
        if neck == "v" and front and cc.z > chest - 0.06 + 1.4 * x: dead.add(f)
    bmesh.ops.delete(bm, geom=list(dead), context="FACES")
    # drop small islands left by the cuts
    bm.faces.ensure_lookup_table()
    seen, islands = set(), []
    for f in bm.faces:
        if f in seen: continue
        stack, isl = [f], []
        seen.add(f)
        while stack:
            g = stack.pop(); isl.append(g)
            for e in g.edges:
                for h in e.link_faces:
                    if h not in seen: seen.add(h); stack.append(h)
        islands.append(isl)
    if islands:
        keep = max(islands, key=len)
        bmesh.ops.delete(bm, geom=[f for isl in islands if isl is not keep for f in isl], context="FACES")
    for _ in range(3): bmesh.ops.smooth_vert(bm, verts=bm.verts[:], factor=0.5, use_axis_x=True, use_axis_y=True, use_axis_z=True)
    for f in bm.faces:                                                            # trim: faces on a cut edge (not the waist)
        if any(e.is_boundary for e in f.edges) and f.calc_center_median().z > topz + 0.02: f.material_index = 1
    bm.to_mesh(o.data); bm.free()
    _thicken(o, 0.004)
    o["neckline"] = neck
    return o


def _cloth_sleeves(arm, mat, trim_mat, bw):
    """Flowing sleeves (owner polish pass): a flared tube from above the elbow to past the wrist, pinned at
    the top and draped by the cloth simulation in the rest pose so it hangs below the arm, then skinned
    from the nearest arm surface (upper arm / forearm / hand) so it bends at the elbow."""
    out = []
    colliders = _body_meshes()
    for b in colliders:
        if not any(m.type == "COLLISION" for m in b.modifiers): b.modifiers.new("SBCollisionS", "COLLISION")
        b.collision.thickness_outer = 0.006; b.collision.cloth_friction = 6
    for side in ("l", "r"):
        e, _ = _rest(arm, "lowerarm_" + side); hnd, _ = _rest(arm, "hand_" + side)
        axis = (hnd - e).normalized(); L = (hnd - e).length
        q = Vector((0, 0, -1)).rotation_difference(axis)
        cols, rows = 22, 12
        verts, faces = [], []
        for r in range(rows + 1):
            t = r / rows; d = -0.05 + (L + 0.13) * t; rad = 0.05 + 0.075 * t ** 1.3
            for i in range(cols):
                a = i / cols * 2 * math.pi
                verts.append(e + q @ Vector((math.cos(a) * rad, math.sin(a) * rad, -d)))
        for r in range(rows):
            for i in range(cols):
                a_, b_ = r * cols + i, r * cols + (i + 1) % cols
                faces.append((a_, b_, b_ + cols, a_ + cols))
        me = bpy.data.meshes.new("Kit_Sleeve"); me.from_pydata(verts, [], faces); me.update()
        o = bpy.data.objects.new("Kit_Sleeve", me); bpy.context.scene.collection.objects.link(o)
        o.data.materials.append(mat); o.data.materials.append(trim_mat)
        for p_ in o.data.polygons:
            if p_.index // cols >= rows - 1: p_.material_index = 1               # trim band at the cuff
        pin = o.vertex_groups.new(name="pin"); pin.add(list(range(cols * 2)), 1.0, "REPLACE")
        cl = o.modifiers.new("Cloth", "CLOTH"); st = cl.settings
        st.quality = 5; st.mass = 0.12; st.air_damping = 2.0; st.tension_stiffness = st.compression_stiffness = 12
        st.shear_stiffness = 5; st.bending_stiffness = 0.25; st.vertex_group_mass = "pin"; st.pin_stiffness = 1.0
        cl.collision_settings.distance_min = 0.005; cl.collision_settings.collision_quality = 3
        sc = bpy.context.scene; frames = 30
        cl.point_cache.frame_start = 1; cl.point_cache.frame_end = frames
        for f in range(1, frames + 1): sc.frame_set(f)
        _select(o); bpy.ops.object.modifier_apply(modifier="Cloth"); sc.frame_set(1)
        o.vertex_groups.remove(o.vertex_groups["pin"])
        _thicken(o, 0.003); o.name = "Kit_Sleeve"
        def w(p, side=side):
            near = {k: v for k, v in bw.near(p).items() if k.startswith(("upperarm_" + side, "lowerarm_" + side, "hand_" + side)) or k.endswith("_" + side) and ("arm" in k or "hand" in k)}
            if not near: return {"lowerarm_" + side: 1.0}
            tot = sum(near.values()); return {k: v / tot for k, v in near.items()}
        out.append((o, w))
    for b in colliders:
        for m in [m for m in b.modifiers if m.name == "SBCollisionS"]: b.modifiers.remove(m)
    return out


def trim_fallback_skirt(topz):
    """The Female_Peasant top is a dress with a knee skirt; under a gown that skirt poked out through
    the cloth. Cut it off below the waist (the sash covers the seam). Knight cloth tabards likewise."""
    for o in bpy.context.scene.objects:
        if o.type != "MESH" or not o.name.startswith(("Female_Peasant_Body", "Male_Knight_Body_Cloth")): continue
        bm = bmesh.new(); bm.from_mesh(o.data); mw = o.matrix_world
        dead = [f for f in bm.faces if all((mw @ v.co).z < topz - 0.02 for v in f.verts)]
        bmesh.ops.delete(bm, geom=dead, context="FACES"); bm.to_mesh(o.data); bm.free()


def slim_leggings(arm):
    """Leggings under a gown keep only what hugs the legs: pouches, belts and flaps that stand off the
    leg (they poked through the cloth at the hip) are cut away."""
    segs = []
    for side in ("l", "r"):
        t0, t1 = _rest(arm, "thigh_" + side); c0, c1 = _rest(arm, "calf_" + side); segs += [(t0, t1), (c0, c1)]
    ph, _ = _rest(arm, "pelvis")
    def dist(p):
        best = 9
        for a, b in segs:
            ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared)); best = min(best, (a + ab * t - p).length)
        return best
    for o in bpy.context.scene.objects:
        if o.type != "MESH" or "_Legs" not in o.name or o.name.startswith("Kit_"): continue
        bm = bmesh.new(); bm.from_mesh(o.data); mw = o.matrix_world
        dead = [f for f in bm.faces if dist(mw @ f.calc_center_median()) > 0.095 or (mw @ f.calc_center_median()).z > ph.z + 0.02]
        bmesh.ops.delete(bm, geom=dead, context="FACES"); bm.to_mesh(o.data); bm.free()


def make_gown(c, arm, mats, bind):
    """The whole gown: draped layer(s), hem details, belt and sleeves, each skinned. Returns objects."""
    spec = gown_spec(c)
    from ubc import body_bvh  # noqa
    ph, _ = _rest(arm, "pelvis"); kl, _ = _rest(arm, "calf_l")
    trim_fallback_skirt(ph.z + 0.07)
    if spec["open"] is None and not spec["slit"] and spec["length"] in ("floor", "ankle", "calf"): slim_leggings(arm)
    bvh = body_bvh()
    trim = mats["acc"] if spec["hem"] != "frost" else mats.get("frost", mats["acc"])
    out = []
    inner = simulate_gown(c, arm, mats["main"], trim, spec, bvh)
    layers = [inner]
    if spec["layers"] > 1:
        # The overskirt follows the draped skirt's own folds, a little outside it and shorter, with a
        # trim band at its hem: it can never cut into the layer beneath.
        o2 = inner.copy(); o2.data = inner.data.copy(); bpy.context.scene.collection.objects.link(o2)
        topz, hemz = inner["topz"], inner["hemz"]; cut = topz - (topz - hemz) * 0.62
        bm = bmesh.new(); bm.from_mesh(o2.data)
        for v in bm.verts:
            d = Vector((v.co.x - ph.x, v.co.y - ph.y - 0.01, 0)); t = max(0.0, (topz - v.co.z) / max(1e-6, topz - hemz))
            if d.length > 1e-6:
                k = 1.0 + 0.07 * t; off = 0.012 + 0.02 * t
                v.co.x = ph.x + d.x * k + d.normalized().x * off; v.co.y = ph.y + 0.01 + d.y * k + d.normalized().y * off
        bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().z < cut], context="FACES")
        for f in bm.faces:
            f.material_index = 1 if f.calc_center_median().z < cut + 0.045 else 0
        bm.to_mesh(o2.data); bm.free()
        o2.data.materials[0] = mats.get("second", mats["main"])
        layers.append(o2)
    for lay in layers: _thicken(lay)
    bw = _BodyWeights()
    topz = inner["topz"]
    gown_info = {"topz": topz}
    for lay in layers:
        lay.name = "Kit_Robe"; out.append(bind(lay, arm, skirt_weights(bw, topz, kl.z, arm=arm)))
    if spec["hem"] == "bells":
        for b in _bells(arm, inner, mats["acc"]): out.append(bind(b, arm, skirt_weights(bw, topz, kl.z, arm=arm)))
    belt_mat = mats.get("sash", mats["acc"]) if spec["belt"] != "leather" else mats.get("leather", mats["acc"])
    for b in _belt(arm, gown_info, belt_mat, spec["belt"], spec["tails"], mats.get("metal")):
        b.name = "Kit_Belt"; out.append(bind(b, arm, skirt_weights(bw, topz, kl.z, 0.3, arm=arm)))
    o_ = c.get("outfit", {}) or {}
    if spec["open"] is None and (str(o_.get("body", "")).startswith("custom") or not o_.get("body")):
        bod = make_bodice(c, arm, mats, topz)
        out.append(bind(bod, arm, lambda p: bw.near(p)))
    if spec["sleeves"]:
        for s_, w in _cloth_sleeves(arm, mats["main"], mats["acc"], bw): out.append(bind(s_, arm, w))
    return out


# ---------------------------------------------------------------- wings

def wing_kinds(c):
    """[(kind, phrase)] for each wing the design asks for: head (helm/hat wings), feather, folded,
    insect (moth/petal), membrane (ragged/bat/dragon/wraith), glider (canvas/sailcloth on ribs)."""
    o = c.get("outfit", {}) or {}
    phrases = [str(k) for k in c.get("kit", [])] + [str(x) for x in (o.get("acc") or [])] + [str(o.get("head", ""))]
    kinds = []
    for p in phrases:
        t = p.lower()
        if not re.search(r"\bwing", t): continue   # not "glowing"
        if _has(t, "helm", "hat", "visor", "cap", "helmet") or "armet" in t: k = "head"
        elif _has(t, "glider", "kite", "sailcloth"): k = "glider"
        elif _has(t, "moth", "petal", "butterfly", "insect", "translucent", "fae"): k = "insect"
        elif _has(t, "ragged", "wraith", "bat", "dragon", "demon", "membrane"): k = "membrane"
        elif _has(t, "capelet", "folded", "folds") and not _has(t, "glider"): k = "folded"
        else: k = "feather"
        if k not in [x[0] for x in kinds]: kinds.append((k, t))
    # one back wing type only (the most specific), head wings may add to it
    back = [x for x in kinds if x[0] != "head"]
    head = [x for x in kinds if x[0] == "head"]
    return head + back[:1]


def _feather(L, W, ridge=0.006, curl=0.06, segs=6, tip_round=True):
    """One feather along local +Z: a vane narrowing to a rounded tip with a raised quill ridge, curled
    slightly toward -Y. Returns (verts, faces) in local space."""
    verts, faces = [], []
    for i in range(segs + 1):
        t = i / segs
        w = W * (math.sin(math.pi * min(1.0, t * 0.9 + 0.08)) ** 0.6 if tip_round else (1 - t)) * (0.55 + 0.45 * min(1, t * 4))
        y = -curl * t * t
        z = L * t
        verts += [Vector((-w / 2, y + ridge * 0.2, z)), Vector((0, y - ridge, z)), Vector((w / 2, y + ridge * 0.2, z))]
    for i in range(segs):
        a = 3 * i
        faces += [(a, a + 1, a + 4, a + 3), (a + 1, a + 2, a + 5, a + 4)]
    return verts, faces


def _assemble(name, pieces, mats_idx, mats_list):
    """pieces: [(verts_world, faces, mat_index)] -> one mesh object."""
    V, F, M = [], [], []
    for vs, fs, mi in pieces:
        b = len(V); V += vs; F += [tuple(b + i for i in f) for f in fs]; M += [mi] * len(fs)
    me = bpy.data.meshes.new(name); me.from_pydata(V, [], F); me.update()
    for m in mats_list: me.materials.append(m)
    for p, mi in zip(me.polygons, M): p.material_index = mi
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    return o


def _frame(direction, normal):
    """Rotation taking local +Z to `direction` and local -Y to `normal` (the vane faces the normal)."""
    z = direction.normalized(); y = -(normal - z * normal.dot(z)).normalized(); x = y.cross(z).normalized()
    return Matrix((x, y, z)).transposed()


def _polyline_at(pts, s):
    """Point and tangent at parameter s (0..1) along a polyline."""
    lens = [(pts[i + 1] - pts[i]).length for i in range(len(pts) - 1)]; tot = sum(lens); d = s * tot
    for i, l in enumerate(lens):
        if d <= l or i == len(lens) - 1:
            u = d / l if l > 0 else 0
            return pts[i].lerp(pts[i + 1], min(1, u)), (pts[i + 1] - pts[i]).normalized()
        d -= l


def feathered_wing(root, sx, mats_list, scale=1.0, folded=False, glow=False):
    """Primaries, secondaries and two rows of coverts along the wing's leading edge (shoulder, elbow,
    wrist, tip), half-open and swept back. sx = +1 left / -1 right."""
    S = scale
    if not folded:
        lead = [root, root + Vector((0.17 * sx, 0.07, 0.15)) * S, root + Vector((0.37 * sx, 0.12, 0.2)) * S, root + Vector((0.55 * sx, 0.18, 0.15)) * S]
        normal = Vector((0, 1, 0.15)).normalized()
    else:
        lead = [root, root + Vector((0.13 * sx, 0.08, 0.03)) * S, root + Vector((0.2 * sx, 0.13, -0.12)) * S, root + Vector((0.22 * sx, 0.16, -0.3)) * S]
        normal = Vector((0.6 * sx, 1, 0)).normalized()
    pieces = []
    def put(s, length, width, out_bias, lift, mi, curl=0.05):
        p, tan = _polyline_at(lead, s)
        down = Vector((0, 0.35, -1)).normalized()
        outward = (lead[-1] - lead[-2]).normalized()
        d = down.lerp(outward, out_bias).normalized()
        R = _frame(d, normal)
        vs, fs = _feather(length * S, width * S, curl=curl * S)
        pieces.append(([p + normal * lift * S + R @ v for v in vs], fs, mi))
    m_main, m_trim = 0, 1
    for k in range(9):    # primaries: from the wrist out to the tip, longest near the wrist-tip middle
        s = 0.62 + 0.38 * k / 8
        put(s, 0.36 + 0.14 * math.sin(math.pi * (0.25 + 0.6 * k / 8)), 0.075, 0.25 + 0.7 * (k / 8) ** 1.5, 0.004 * k, m_main)
    for k in range(9):    # secondaries along the arm
        s = 0.06 + 0.55 * k / 8
        put(s, 0.30 - 0.05 * (1 - k / 8), 0.085, 0.08 + 0.12 * k / 8, 0.012, m_main)
    for k in range(10):   # greater coverts
        s = 0.05 + 0.85 * k / 9
        put(s, 0.17, 0.07, 0.1 + 0.4 * (k / 9) ** 2, 0.025, m_trim, curl=0.025)
    for k in range(8):    # lesser coverts along the leading edge
        s = 0.04 + 0.8 * k / 7
        put(s, 0.09, 0.06, 0.1 + 0.3 * (k / 7) ** 2, 0.035, m_trim, curl=0.015)
    # the wing's bony leading edge as a soft tube
    cu = bpy.data.curves.new("WingArm", "CURVE"); cu.dimensions = "3D"; cu.bevel_depth = 0.02 * S; cu.bevel_resolution = 2
    sp = cu.splines.new("POLY"); sp.points.add(len(lead) - 1)
    for p, v in zip(sp.points, lead): p.co = (v.x, v.y, v.z + 0.005, 1)
    sp.points[-1].radius = 0.3
    arm_o = bpy.data.objects.new("WingArm", cu); bpy.context.scene.collection.objects.link(arm_o)
    _select(arm_o); bpy.ops.object.convert(target="MESH"); arm_o = bpy.context.object
    o = _assemble("Kit_Wing", pieces, None, mats_list)
    arm_o.data.materials.clear(); arm_o.data.materials.append(mats_list[1])
    _thicken(o, 0.003)
    _select(o, arm_o); bpy.ops.object.join(); o = bpy.context.object
    return o


def insect_wing(root, sx, mats_list, petal=False, spots=True):
    """Moth or petal wings: a large rounded forewing and a smaller hindwing, flat with raised veins,
    and an eye spot on each forewing (moth)."""
    pieces = []
    normal = Vector((0, 1, 0))
    def lobe(direction, L, W, skew, mi):
        R = _frame(direction, normal); n = 22; vs = [Vector((0, 0, 0))]
        for i in range(n + 1):
            t = i / n; a = math.pi * t
            r = L * (math.sin(a) ** (0.7 if not petal else 1.4))
            x = W * math.cos(a) * (1 + skew * (1 - t)) * (0.6 if petal else 1.0)
            vs.append(Vector((x * 0.5, 0, r * 0.95 + L * 0.05 * math.sin(2 * a))))
        fs = [(0, i, i + 1) for i in range(1, n + 1)]
        pieces.append(([root + R @ v for v in vs], fs, mi))
        return R
    s = 0.6 if petal else 1.0
    Rf = lobe(Vector((0.8 * sx, 0.25, 0.55)).normalized(), 0.5 * s, 0.42 * s, 0.4, 0)
    lobe(Vector((0.7 * sx, 0.3, -0.6)).normalized(), 0.33 * s, 0.32 * s, -0.2, 0)
    if spots and not petal:   # eye spot: a ring and a dot on the forewing, slightly in front of it
        c0 = root + Rf @ Vector((0, -0.004, 0.3 * s))
        for (r_, mi) in ((0.06, 1), (0.03, 0)):
            n = 16; vs = [c0 - normal * 0.002 * (2 - mi)] + [c0 - normal * 0.002 * (2 - mi) + Rf @ Vector((math.cos(i / n * 2 * math.pi) * r_, 0, math.sin(i / n * 2 * math.pi) * r_)) for i in range(n)]
            pieces.append((vs, [(0, i, i % n + 1) for i in range(1, n + 1)], mi))
    o = _assemble("Kit_Wing", pieces, None, mats_list)
    _thicken(o, 0.003)
    return o


def membrane_wing(root, sx, mats_list, ragged=False, glider=False, scale=1.0):
    """Bat/dragon membrane (fingers fanning from the wrist, scalloped edge; ragged tears for wraiths) or
    a glider (straight ribs from a back hub with taut sailcloth panels)."""
    S = scale
    if glider:
        hub = root
        tips = [hub + Vector((0.78 * sx, 0.15, 0.32)) * S, hub + Vector((0.8 * sx, 0.2, -0.05)) * S, hub + Vector((0.55 * sx, 0.24, -0.45)) * S, hub + Vector((0.12 * sx, 0.2, -0.55)) * S]
        base = [hub] * len(tips)
    else:
        elbow = root + Vector((0.2 * sx, 0.08, 0.18)) * S; wrist = elbow + Vector((0.22 * sx, 0.06, 0.08)) * S
        tips = [wrist + Vector((0.32 * sx, 0.06, 0.14)) * S, wrist + Vector((0.3 * sx, 0.1, -0.12)) * S, wrist + Vector((0.18 * sx, 0.12, -0.36)) * S, root + Vector((0.08 * sx, 0.12, -0.5)) * S]
        base = [wrist, wrist, wrist, root + Vector((0, 0.02, -0.05))]
    pieces = []; rows = 6
    edges = list(zip(base, tips))
    for j in range(len(edges) - 1):
        (b0, t0), (b1, t1) = edges[j], edges[j + 1]
        vs, fs = [], []
        for r in range(rows + 1):
            u = r / rows
            a = b0.lerp(t0, u); b = b1.lerp(t1, u)
            for k in range(5):
                v = k / 4
                p = a.lerp(b, v)
                if r == rows and not glider:        # scallop: the free edge bows in toward the wrist
                    p = p.lerp(b0.lerp(b1, v), 0.22 * math.sin(math.pi * v))
                if glider: p = p + Vector((0, 0.025 * math.sin(math.pi * v) * u, 0))   # slight billow
                vs.append(p)
        for r in range(rows):
            for k in range(4):
                a_ = r * 5 + k
                if ragged and r >= rows - 2 and ((j * 7 + k * 3 + r) % 5 == 0): continue   # tears
                fs.append((a_, a_ + 1, a_ + 6, a_ + 5))
        pieces.append((vs, fs, 0))
    o = _assemble("Kit_Wing", pieces, None, mats_list)
    _thicken(o, 0.003)
    # bones / ribs
    cu = bpy.data.curves.new("WingBones", "CURVE"); cu.dimensions = "3D"; cu.bevel_depth = (0.012 if glider else 0.014) * S; cu.bevel_resolution = 1
    chain = [] if glider else [root, base[0]]
    if chain:
        sp = cu.splines.new("POLY"); sp.points.add(len(chain) - 1)
        for p, v in zip(sp.points, chain): p.co = (v.x, v.y, v.z, 1)
    for b, t in edges[:-1] if not glider else edges:
        sp = cu.splines.new("POLY"); sp.points.add(1)
        sp.points[0].co = (b.x, b.y, b.z, 1); sp.points[1].co = (t.x, t.y, t.z, 1); sp.points[1].radius = 0.35
    bo = bpy.data.objects.new("WingBones", cu); bpy.context.scene.collection.objects.link(bo)
    _select(bo); bpy.ops.object.convert(target="MESH"); bo = bpy.context.object
    bo.data.materials.clear(); bo.data.materials.append(mats_list[1])
    _select(o, bo); bpy.ops.object.join()
    return bpy.context.object


def make_wings(c, arm, mats, bind, head_box):
    """Every wing the design asks for, bound to its bone (spine_03 for back wings, Head for helm wings)."""
    out = []
    kinds = wing_kinds(c)
    if not kinds: return out
    el = c.get("element")
    s3, _ = _rest(arm, "spine_03")
    for kind, phrase in kinds:
        glow = _has(phrase, "flame", "fire", "light", "radiant", "gold light")
        main = mats["glow"] if glow else mats["main"]
        trim = mats["acc"]
        if kind == "head":
            hb = head_box
            metal = _has(phrase, "helm", "visor", "armet", "storm")
            m = mats["metal"] if metal else mats.get("white", mats["main"])
            from ubc import body_bvh  # noqa
            hbvh = body_bvh()
            for sx in (1, -1):
                # rooted ON the side of the helm/hat (ray-cast in from the side), swept back and up
                z0 = hb["eye"] + 0.02
                hit = hbvh.ray_cast(Vector((hb["cx"] + 0.5 * sx, hb["cy"], z0)), Vector((-sx, 0, 0)), 0.6)[0]
                root = (hit + Vector((0.004 * sx, 0, 0))) if hit is not None else Vector((hb["cx"] + hb["hw"] * sx, hb["cy"], z0))
                pieces = []
                lead = [root, root + Vector((0.02 * sx, 0.06, 0.05)), root + Vector((0.035 * sx, 0.14, 0.09))]
                normal = Vector((sx, 0.0, 0.0))
                for k in range(7):
                    p, _t = _polyline_at(lead, 0.05 + 0.95 * k / 6)
                    d = Vector((0.08 * sx, 0.55 + 0.45 * k / 6, -0.35 + 0.9 * (1 - k / 6))).normalized()
                    vs, fs = _feather(0.12 + 0.09 * math.sin(math.pi * (0.25 + 0.6 * k / 6)), 0.055, curl=0.015, segs=4)
                    R = _frame(d, normal)
                    pieces.append(([p + normal * 0.002 * k + R @ v for v in vs], fs, 0))
                o = _assemble("Kit_Wing", pieces, None, [m, m]); _thicken(o, 0.004)
                out.append(bind(o, arm, {"Head": 1.0}))
            continue
        for sx in (1, -1):
            root = s3 + Vector((0.07 * sx, 0.13, 0.07))
            if kind in ("feather", "folded"):
                o = feathered_wing(root, sx, [main, trim], folded=kind == "folded", glow=glow)
            elif kind == "insect":
                o = insect_wing(root, sx, [main, trim], petal=_has(phrase, "petal"), spots=_has(phrase, "moth", "eye"))
            elif kind == "membrane":
                o = membrane_wing(root, sx, [main, trim], ragged=_has(phrase, "ragged", "wraith", "tattered"),
                                  scale=1.15 if _has(phrase, "large") else 1.0)
            else:
                o = membrane_wing(root - Vector((0.07 * sx, 0, 0)), sx, [main, trim], glider=True,
                                  scale=1.25 if _has(phrase, "twice", "wide", "big") or "width" in phrase else 1.0)
            o.name = "Kit_Wing"
            out.append(bind(o, arm, {"spine_03": 1.0}))
    return out
