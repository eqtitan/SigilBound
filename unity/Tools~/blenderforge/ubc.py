"""High-detail hero builder (docs/sigilbound/PLAN.md phase F, step 57).

Builds one hero from a design (concepts.json / new_heroes.json format) on the CC0 Quaternius Universal
Base Characters + Fantasy outfit parts in godot/models/ubc (ubc_parts.py picks and tints the parts),
adds kit pieces bound rigidly to bones, merges everything onto ONE 65-joint skeleton, decimates to a
triangle budget and bakes one atlas (albedo, tangent-space normal, glow).

    bpyenv/bin/python ubc.py -- --design concepts.json --hero brannoc --out DIR [--tex 1024] [--tris 25000] [--render]

Writes SB_Hero_<id>_HD.glb (rest pose, skinned, no clips, placeholder material) with
SB_Hero_<id>_HD_albedo/_normal/_glow PNGs, and SB_Hero_<id>_HD_LOD1.glb (same atlas, ~1/3 triangles).
Weapons, off-hand weapons, shields and focus items are modelled in kit_props.py (procedural bmesh).
"""
import bpy, bmesh, sys, os, json, math, re, argparse
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ubc_parts as P  # noqa: E402
import kit_props as KP  # noqa: E402  (modelled weapons, shields and focus items)
import kit_cloth as KC  # noqa: E402
import kit_head as KH  # noqa: E402  (modelled head pieces and species features)
import kit_acc as KA  # noqa: E402  (modelled accessories)

ELEM = P.ELEM

def has(txt, *ks):
    return any(re.search(r"\b" + k + r"(e?s)?\b", txt) for k in ks)

def bind(obj, arm, groups):
    """Skin obj to arm: groups = {bone: weight} for every vertex (rigid), or a callable(vertex_world)->{bone: w}."""
    for b in (groups if isinstance(groups, dict) else {}):
        obj.vertex_groups.new(name=b)
    if isinstance(groups, dict):
        idx = list(range(len(obj.data.vertices)))
        for b, w in groups.items(): obj.vertex_groups[b].add(idx, w, "REPLACE")
    else:
        mw = obj.matrix_world
        for v in obj.data.vertices:
            for b, w in groups(mw @ v.co).items():
                vg = obj.vertex_groups.get(b) or obj.vertex_groups.new(name=b)
                vg.add([v.index], w, "REPLACE")
    md = obj.modifiers.new("Armature", "ARMATURE"); md.object = arm
    return obj

def rest_bone(arm, name):
    b = arm.data.bones[name]
    return arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local

def prim(kind, mat, **kw):
    getattr(bpy.ops.mesh, kind)(**kw)
    o = bpy.context.object
    o.data.materials.append(mat)
    return o

def apply_xform(o):
    P_ = bpy.context
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o

GRIP_CURL = {"01": 62, "02": 78, "03": 55}   # degrees per finger joint for a closed grip (thumb uses 0.6x)

def grip_frame(arm, side):
    """World matrix of the grip channel in the hand, at rest: a shaft held in a fist runs ACROSS the palm
    along the knuckle line (index -> little finger), so Z = from the little-finger knuckle toward the
    index knuckle (the weapon's 'up'), and the origin sits in the palm, just inside the curled fingers."""
    h, t = rest_bone(arm, "hand_" + side)
    i1, _ = rest_bone(arm, "index_01_" + side); p1, _ = rest_bone(arm, "pinky_01_" + side)
    m1, m2 = rest_bone(arm, "middle_01_" + side)
    z = (i1 - p1).normalized()
    along = (m1 - h).normalized()                       # wrist -> knuckles
    palm = z.cross(along).normalized()                  # palm normal (sign fixed below)
    th, _ = rest_bone(arm, "thumb_02_" + side)
    if palm.dot(th - h) < 0: palm = -palm               # the palm side is where the thumb wraps
    x = palm; y = z.cross(x).normalized()
    m = Matrix((x, y, z)).transposed().to_4x4()
    m.translation = m1 + palm * 0.028 - along * 0.012   # in the fist, under the middle knuckle
    return m

def grip_pose(arm, sides=("r",)):
    """Curl the fingers around the grip (pose only: for previews and the game's HandGrip, which
    applies the same angles after each animation frame)."""
    bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode="POSE")
    for side in sides:
        g = grip_frame(arm, side); axis_w = g.to_3x3().col[2]          # fingers wrap around the shaft
        for f in ("index", "middle", "ring", "pinky", "thumb"):
            for j in ("01", "02", "03"):
                pb = arm.pose.bones.get(f"{f}_{j}_{side}")
                if pb is None: continue
                rest = (arm.matrix_world.to_3x3() @ pb.bone.matrix_local.to_3x3())
                ax = (rest.inverted() @ axis_w).normalized()
                ang = math.radians(GRIP_CURL[j] * (0.6 if f == "thumb" else 1.0))
                # wrap toward the palm: test the sign on the bone's tail
                pb.rotation_mode = "AXIS_ANGLE"; pb.rotation_axis_angle = (ang, *ax)
                bpy.context.view_layer.update()
                tail = arm.matrix_world @ pb.tail
                palm = g.to_3x3().col[0]
                pb.rotation_axis_angle = (-ang, *ax); bpy.context.view_layer.update()
                if (arm.matrix_world @ pb.tail - g.translation).dot(palm) > (tail - g.translation).dot(palm):
                    pb.rotation_axis_angle = (ang, *ax)
    bpy.ops.object.mode_set(mode="OBJECT")

def weapon_side(c):
    """The weapon hand: bows and crossbows are held in the left (bow) hand, everything else in the right."""
    return "l" if has(str(c.get("weapon", "")).lower(), "bow", "crossbow", "longbow", "shortbow") else "r"

def make_bow(mat, string_mat, cross=False):
    """A bow along local +Z through the grip: two curved limbs (thicker at the grip), tips swept back
    toward the archer (+X, the palm side), and a straight string between the tips. A crossbow lays the
    limbs across the stock."""
    pts = []
    for i in range(13):
        t = i / 12 * 2 - 1                    # -1 .. 1 along the bow
        z = t * 0.62; x = 0.11 * (t * t) - 0.02 * (1 - t * t)
        pts.append((x, 0.0, z))
    cu = bpy.data.curves.new("BowLimb", "CURVE"); cu.dimensions = "3D"; cu.bevel_depth = 0.016; cu.bevel_resolution = 2
    sp = cu.splines.new("POLY"); sp.points.add(len(pts) - 1)
    for p, (x, y, z) in zip(sp.points, pts): p.co = (x, y, z, 1)
    limb = bpy.data.objects.new("BowLimb", cu); bpy.context.scene.collection.objects.link(limb)
    for x in bpy.context.selected_objects: x.select_set(False)
    limb.select_set(True); bpy.context.view_layer.objects.active = limb; bpy.ops.object.convert(target="MESH")
    limb = bpy.context.object; limb.data.materials.append(mat)
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.0035, depth=1.24, location=(0.11, 0, 0))
    st = bpy.context.object; st.data.materials.append(string_mat)
    bpy.ops.mesh.primitive_cylinder_add(vertices=8, radius=0.024, depth=0.12, location=(0, 0, 0))   # leather grip
    gr = bpy.context.object; gr.data.materials.append(string_mat)
    parts = [limb, st, gr]
    if cross:
        for o in parts: o.rotation_euler = (0, math.radians(90), 0)
        bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0)); stock = bpy.context.object
        stock.scale = (0.05, 0.05, 0.62); stock.location = (0, 0, 0.18); stock.data.materials.append(mat); parts.append(stock)
    return parts

def weapon(c, arm, mats):
    """The design's main-hand prop (kit_props: modelled weapons and focus items) along local +Z at the
    grip, rigid to the weapon hand. Bare-handed designs get none."""
    o = KP.build_weapon(c, mats)
    if o is None: return []
    o.name = "Kit_Weapon"
    side = weapon_side(c)
    o.matrix_world = grip_frame(arm, side) @ o.matrix_world
    apply_xform(o)
    return [bind(o, arm, {"hand_" + side: 1.0})]

def make_robe(ph, topz, hemz, mat, trim_mat):
    """A cloth skirt: snug at the waist, flaring to the ankles, with soft vertical folds that deepen
    toward a gently scalloped hem, plus a sash at the waist."""
    import bmesh
    N, R = 40, 14
    verts, faces = [], []
    for r in range(R + 1):
        t = r / R; z = topz + (hemz - topz) * t
        base = 0.2 + (0.43 - 0.2) * (t ** 1.25)
        for i in range(N):
            a = i / N * 2 * math.pi
            fold = 1 + 0.07 * t * math.sin(a * 9) + 0.03 * t * math.sin(a * 4 + 1.3)
            zz = z + (0.025 * math.sin(a * 9) * t * t if r == R else 0)
            verts.append((ph.x + math.cos(a) * base * fold, ph.y + 0.01 + math.sin(a) * base * fold * 0.82, zz))
    for r in range(R):
        for i in range(N):
            a, b = r * N + i, r * N + (i + 1) % N
            faces.append((a, b, b + N, a + N))
    me = bpy.data.meshes.new("Kit_Robe"); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new("Kit_Robe", me); bpy.context.scene.collection.objects.link(o); o.data.materials.append(mat)
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    sol = o.modifiers.new("S", "SOLIDIFY"); sol.thickness = 0.012
    bpy.ops.object.modifier_apply(modifier="S"); bpy.ops.object.shade_smooth()
    sash = prim("primitive_torus_add", trim_mat, major_segments=40, minor_segments=6, major_radius=0.205, minor_radius=0.02, location=(ph.x, ph.y + 0.01, topz - 0.02))
    sash.scale = (1, 0.82, 1); apply_xform(sash)
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True); sash.select_set(True); bpy.context.view_layer.objects.active = o; bpy.ops.object.join()
    return bpy.context.object

def make_wings(arm, mat, trim_mat):
    """A pair of feathered wings from the shoulder blades: a short arm bone of each wing with seven
    long feathers fanned back and up (span about 0.65 m a side), rigid to the upper back."""
    s3, _ = rest_bone(arm, "spine_03"); out = []
    for sx in (-1, 1):
        root = s3 + Vector((0.07 * sx, 0.12, 0.08))
        pieces = []
        for k in range(7):
            u = k / 6
            ang = math.radians(15 + 70 * u)                       # from nearly sideways to steeply up
            L = 0.62 - 0.32 * u
            d = Vector((math.cos(ang) * sx, 0.28, math.sin(ang))).normalized()
            mid = root + Vector((0.1 * sx, 0.05, 0.02)) + d * (L / 2)
            f = prim("primitive_cube_add", mat if k % 2 == 0 else trim_mat, size=1, location=mid)
            f.scale = (0.05, 0.006, L)
            f.rotation_mode = "QUATERNION"; f.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d)
            apply_xform(f); pieces.append(f)
        for x in bpy.context.selected_objects: x.select_set(False)
        for q in pieces: q.select_set(True)
        bpy.context.view_layer.objects.active = pieces[0]; bpy.ops.object.join(); o = bpy.context.object; o.name = "Kit_Wing"
        out.append(bind(o, arm, {"spine_03": 1.0}))
    return out

def head_box(arm):
    """The head as modelled (skin + hair vertices bound mostly to the Head bone): top, centre, half
    width, eye height and face front. Head kit is placed from this, not from the bone (whose tail sits
    well inside the skull, which put crowns over the eyes and ears at the collar)."""
    # Everything above the neck joint and within a head's width of the spine is the head (skin, hair,
    # helm): bone weights alone miss most of the full-body skin's head.
    nh, nt = rest_bone(arm, "neck_01"); hh, _ = rest_bone(arm, "Head")
    zcut = (nh.z + hh.z) / 2 + 0.03
    pts = []
    for o in bpy.context.scene.objects:
        if o.type != "MESH" or o.name.startswith("Kit_"): continue
        mw = o.matrix_world
        for v in o.data.vertices:
            p = mw @ v.co
            if p.z > zcut and abs(p.x - hh.x) < 0.17 and abs(p.y - hh.y) < 0.2: pts.append(p)
    if not pts:
        hh, ht = rest_bone(arm, "Head"); return {"top": ht.z + 0.1, "cx": hh.x, "cy": hh.y, "hw": 0.1, "eye": hh.z + 0.08, "front": hh.y - 0.1, "base": hh.z}
    xs = [p.x for p in pts]; ys = [p.y for p in pts]; zs = [p.z for p in pts]
    top, base = max(zs), min(zs)
    return {"top": top, "base": base, "cx": (min(xs) + max(xs)) / 2, "cy": (min(ys) + max(ys)) / 2, "hw": (max(xs) - min(xs)) / 2,
            "eye": base + (top - base) * 0.58, "front": min(ys)}

def body_bvh(include=()):
    """BVH of the dressed body (every non-kit mesh, evaluated in the rest pose) for fitting kit to it;
    `include` adds kit pieces by name prefix (a cape hangs over the gown)."""
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get()
    verts, polys = [], []
    for o in bpy.context.scene.objects:
        if o.type != "MESH" or (o.name.startswith("Kit_") and not (include and o.name.startswith(tuple(include)))) or o.name.startswith("Icosphere"): continue
        me = o.evaluated_get(dg).to_mesh(); mw = o.matrix_world; base = len(verts)
        verts += [mw @ v.co for v in me.vertices]; polys += [[base + i for i in p.vertices] for p in me.polygons]
        o.evaluated_get(dg).to_mesh_clear()
    return BVHTree.FromPolygons(verts, polys)

def make_cape(arm, mat, clasp_mat, long_=True, train=False, ember=None, ragged=False):
    """A cape fitted to this body: the top drapes OVER the shoulders and fastens with two clasps at the
    collarbones; the sheet follows the back's curve (ray-cast onto the dressed body), keeps clear of
    the shoulder blades, hips and calves, and flares toward an open front. Weights: the top row follows
    the clavicles and upper chest, blending to the pelvis and thighs toward the hem, so it moves with the
    shoulders instead of hanging like a board."""
    bvh = body_bvh(include=("Kit_Robe", "Kit_Belt"))   # over the gown, never through it
    cl, _ = rest_bone(arm, "clavicle_l"); cr, _ = rest_bone(arm, "clavicle_r")
    ul, _ = rest_bone(arm, "upperarm_l"); ur, _ = rest_bone(arm, "upperarm_r")
    nh, _ = rest_bone(arm, "neck_01"); ph, _ = rest_bone(arm, "pelvis"); kl, _ = rest_bone(arm, "calf_l")
    back = 1.0 if (rest_bone(arm, "spine_03")[0].y < 0.5) else 1.0   # +y is the back on this rig (the face looks to -y)
    top_z = nh.z - 0.03
    hem_z = 0.05 if train else (kl.z - 0.12) if long_ else (ph.z + 0.02)
    half_top = abs(ul.x - ur.x) * 0.5 * 0.92
    rows, cols = 22, 17
    gap = 0.035
    verts, weights = [], []
    def surface_y(x, z):
        """How far back the body reaches at (x, z): the first hit of a ray cast from behind."""
        hit = bvh.ray_cast(Vector((x, 2.0, z)), Vector((0, -1, 0)), 4.0)
        return hit[0].y if hit[0] is not None else None
    max_back = -9.0
    for r in range(rows + 1):
        t = r / rows
        z = top_z + (hem_z - top_z) * t
        half = half_top * (1.0 + 0.55 * t)                    # flare toward the hem
        row = []
        for c in range(cols + 1):
            u = c / cols * 2 - 1                              # -1 (right) .. 1 (left)
            x = (ul.x + ur.x) / 2 + u * half
            sy = surface_y(x * (0.9 if t < 0.15 else 1.0), z)
            if sy is not None: max_back = max(max_back, sy) if t < 0.55 else max_back
            y = (sy if sy is not None else nh.y) + gap
            if t > 0.4: y = max(y, max_back + gap * (1 + 2.5 * (t - 0.4)))  # hang free below the shoulder blades / seat
            # wrap the sides forward a little (the cape curves around the body) and let the edges fall open
            y -= (abs(u) ** 2) * (0.10 + 0.06 * t)
            if sy is not None and t > 0.3: y = max(y, sy + gap * (1 + 1.8 * t))   # the wrapped edges stay outside the body/gown
            row.append(Vector((x, y, z)))
        verts.append(row)
    # Yoke: the top row rolls forward over the shoulders to the collarbones (where the clasps sit).
    yoke = []
    for c in range(cols + 1):
        u = c / cols * 2 - 1
        x = (ul.x + ur.x) / 2 + u * half_top * 0.9
        hit = bvh.ray_cast(Vector((x, nh.y - 0.02, top_z + 0.12)), Vector((0, 0, -1)), 0.5)
        z = (hit[0].z + gap * 0.6) if hit[0] is not None else top_z + 0.02
        z = min(z, top_z + 0.03)   # never ride up the neck or into the hair: the yoke sits on the shoulders
        yoke.append(Vector((x, nh.y - 0.06 - 0.05 * (1 - abs(u)), z)))
    if ragged:   # a torn hem: tongues of cloth of uneven length
        for c in range(cols + 1):
            k = (c * 7) % 5 / 4.0
            for r_, f in ((-1, 1.0), (-2, 0.5)): verts[r_][c].z += (0.02 + 0.08 * k) * f
    hem_pts = [Vector(v) for v in verts[-1]]
    verts.insert(0, yoke)
    flat_v = [v for row in verts for v in row]
    faces = []
    W = cols + 1
    for r in range(len(verts) - 1):
        for c in range(cols):
            a = r * W + c; faces.append((a, a + 1, a + W + 1, a + W))
    me = bpy.data.meshes.new("Kit_Cape"); me.from_pydata(flat_v, [], faces); me.update()
    o = bpy.data.objects.new("Kit_Cape", me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    sol = o.modifiers.new("S", "SOLIDIFY"); sol.thickness = 0.012; sol.offset = 0
    sub = o.modifiers.new("Sub", "SUBSURF"); sub.levels = 1
    bpy.ops.object.modifier_apply(modifier="S"); bpy.ops.object.modifier_apply(modifier="Sub")
    bpy.ops.object.shade_smooth()
    tl, _ = rest_bone(arm, "thigh_l"); tr, _ = rest_bone(arm, "thigh_r")
    def cape_w(p):
        t = max(0.0, min(1.0, (top_z - p.z) / max(1e-6, top_z - hem_z)))
        side = max(0.0, min(1.0, (p.x - ur.x) / max(1e-6, ul.x - ur.x)))
        if p.z > top_z - 0.02:   # the collar: half follows the neck, so it turns with the head
            return {"neck_01": 0.5, "clavicle_l": 0.2 * side, "clavicle_r": 0.2 * (1 - side), "spine_03": 0.3}
        if t < 0.08:   # yoke and top: shoulders and upper chest
            return {"clavicle_l": 0.45 * side, "clavicle_r": 0.45 * (1 - side), "spine_03": 0.55}
        w = {"spine_03": max(0.0, 1 - t * 1.6), "spine_01": min(1.0, t * 1.2) * (1 - t)}
        low = max(0.0, (t - 0.45) / 0.55)
        w["pelvis"] = max(0.0, 1 - sum(w.values()) - low * 0.6)
        w["thigh_l"] = low * 0.3 * side; w["thigh_r"] = low * 0.3 * (1 - side)
        tot = sum(w.values()); return {k: v / tot for k, v in w.items() if v > 1e-4}
    out = [bind(o, arm, cape_w)]
    if ember is not None:   # burning edge: glowing tongues licking up from the torn hem
        import kit_geo as G_
        bits = []
        for i, p in enumerate(hem_pts):
            h = 0.05 + 0.05 * ((i * 5) % 3) / 2
            bits.append(G_.sweep([p + Vector((0, 0.008, -0.004)), p + Vector((0, 0.01, h * 0.5)), p + Vector((0, 0.012, h))], [0.022, 0.012, 0.0008], ember, segs=6))
            bits.append(G_.sweep([p + Vector((0, -0.006, -0.004)), p + Vector((0, -0.008, h * 0.4))], [0.016, 0.0008], ember, segs=5))
        eo = G_.join(bits, "Kit_Cape")
        out.append(bind(eo, arm, cape_w))
    for sx, c_ in ((1, cl), (-1, cr)):   # small clasps on the collarbones, on the body's surface
        cz = top_z + 0.005; cx = nh.x + 0.075 * sx
        hit = bvh.ray_cast(Vector((cx, nh.y - 0.6, cz)), Vector((0, 1, 0)), 1.0)[0]
        cy = (hit.y - 0.006) if hit is not None else nh.y - 0.1
        bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.017, depth=0.01, location=(cx, cy, cz), rotation=(math.radians(90), 0, 0))
        k = bpy.context.object; k.data.materials.append(clasp_mat); k.name = "Kit_Clasp"
        out.append(bind(k, arm, {"clavicle_l" if sx > 0 else "clavicle_r": 1.0}))
    return out

def kit(c, arm):
    pal = c.get("palette", {}); el = P.hexc(ELEM.get(c.get("element"), pal.get("glow")))
    mats = {"metal": P.flat("K_Metal", P.hexc(pal.get("metal", "#aab0b8")), metal=0.9, rough=0.3),
            "acc": P.flat("K_Accent", P.hexc(pal.get("accent")), metal=0.3, rough=0.4),
            "main": P.flat("K_Main", P.hexc(pal.get("main")), rough=0.6),
            "glow": P.flat("K_Glow", el, emit=4),
            "second": P.flat("K_Second", P.hexc(pal.get("secondary", pal.get("main"))), rough=0.6),
            "white": P.flat("K_White", P.hexc("#eeeae2"), rough=0.6),
            "leather": P.flat("K_Leather", P.hexc("#5a3b26"), rough=0.75),
            "frost": P.flat("K_Frost", P.hexc("#cfeaff"), emit=0.6, rough=0.3)}
    # sash cloth: the accent dyed halfway toward the gown, so the waist doesn't read as a glowing ring
    _a, _m = P.hexc(pal.get("accent")), P.hexc(pal.get("main"))
    mats["sash"] = P.flat("K_Sash", tuple(_a[i] * 0.55 + _m[i] * 0.45 for i in range(3)) + (1,), rough=0.55)
    text = " ".join([str(c.get("weapon", ""))] + [str(k) for k in c.get("kit", [])] + [str(x) for x in (c.get("outfit", {}).get("acc") or [])]
                    + [str(c.get("outfit", {}).get("head", ""))]).lower()
    out = weapon(c, arm, mats)
    w = str(c.get("weapon", "")).lower()
    if weapon_side(c) == "r" and KP.offhand(c) is not None:
        # the off-hand weapon (twin daggers, hatchets, sabres, ribbons): a second copy in the left fist
        o = KP.build_offhand(c, mats)
        if o is not None:
            o.matrix_world = grip_frame(arm, "l") @ o.matrix_world; apply_xform(o); o.name = "Kit_Offhand"; out.append(bind(o, arm, {"hand_l": 1.0}))
    if KP.shield_type(c) is not None:
        # strapped flat to the OUTSIDE of the left forearm, centred along it
        lh, lt = rest_bone(arm, "lowerarm_l"); hl, _ = rest_bone(arm, "hand_l")
        # the outside of the forearm = the back of the hand, which faces UP in the rest T-pose (palm down)
        along = (hl - lh).normalized(); out_dir = Vector((0, 0, 1))
        out_dir = (out_dir - along * out_dir.dot(along)).normalized()
        o = KP.build_shield(c, mats)                                # face in XY, local Y along the forearm, +Z out
        y = along; z = out_dir; xax = y.cross(z).normalized()
        M = Matrix((xax, y, z)).transposed().to_4x4(); M.translation = lh + (hl - lh) * 0.5 + out_dir * 0.06
        o.matrix_world = M @ o.matrix_world; apply_xform(o); o.name = "Kit_Shield"; out.append(bind(o, arm, {"lowerarm_l": 1.0}))
    hb = head_box(arm); top = Vector((hb["cx"], hb["cy"], hb["top"]))
    # Crowns, circlets, halos, horns and headwear, modelled and fitted to the head (kit_head.py)
    out += KH.head(c, arm, mats, sys.modules[__name__])
    nh, _ = rest_bone(arm, "neck_01")
    # Gowns and robes first (cloth-simulated, kit_cloth.py), so a cape is fitted over them.
    if KC.needs_gown(c, text):
        out += KC.make_gown(c, arm, mats, bind)
    if has(text, "cape", "cloak", "mantle"):
        out += make_cape(arm, mats["second"] if has(text, "crimson cape", "red cape") else mats["main"], mats["metal"], long_=not has(text, "short cape", "half cape", "shoulder cape", "mantle", "half-cape", "capelet"),
                         train=has(text, "train"), ember=mats["glow"] if has(text, "ember edge", "burning edge", "burning ember") else None,
                         ragged=has(text, "ragged cape", "tattered cape", "ragged black cape", "torn cape"))
    out += KC.make_wings(c, arm, mats, bind, hb)
    out += KH.species(c, arm, mats, sys.modules[__name__])   # ears, beaks, tails, shells, golem stone (kit_head.py)
    out += KA.accessories(c, arm, mats, sys.modules[__name__])   # belts, sashes, pauldrons, back items... (kit_acc.py)
    return out

def species(c, arm, mats):
    """Non-human heroes (phase H designs): ears, beak, horns or antlers on the head, a tail on the hips,
    a shell or fin on the back, stone plates for golems, a fading lower body for wraiths. Simple shapes
    bound rigidly to their bones; steps 70-74 replace them with modelled pieces where needed."""
    sp = str(c.get("species", "human")).lower()
    if sp == "human": return []
    pal = c.get("palette", {}); out = []
    skin = P.flat("K_Skin", P.hexc(c.get("skin") if str(c.get("skin", "")).startswith("#") else P.SKIN.get(c.get("skin"), "#c8915f")), rough=0.7)
    fur = P.flat("K_Fur", P.hexc((c.get("hair") or {}).get("colour", pal.get("secondary"))), rough=0.8)
    hb = head_box(arm); hh = Vector((hb["cx"], hb["cy"], hb["base"])); top = Vector((hb["cx"], hb["cy"], hb["top"])); ear_z = hb["eye"]
    def piece(kind, mat, bone, **kw):
        o = prim(kind, mat, **kw); o.name = "Kit_Species"; apply_xform(o); out.append(bind(o, arm, {bone: 1.0})); return o
    if any(k in sp for k in ("elf", "sylph", "sprite", "dryad", "gnome", "fae")):
        for sx in (-1, 1): piece("primitive_cone_add", skin, "Head", vertices=8, radius1=0.022, depth=0.1, location=(hb["cx"] + hb["hw"] * 1.02 * sx, hb["cy"] + 0.01, ear_z), rotation=(0, math.radians(-70 * sx), 0))
    if any(k in sp for k in ("fox", "cat", "lion", "wolf", "bear", "hare", "rabbit", "goat")):
        long_ = "hare" in sp or "rabbit" in sp
        for sx in (-1, 1): piece("primitive_cone_add", fur, "Head", vertices=8, radius1=0.04, depth=0.17 if long_ else 0.08, location=(hb["cx"] + hb["hw"] * 0.6 * sx, hb["cy"] + 0.01, top.z + (0.06 if long_ else 0.02)), rotation=(0, math.radians(12 * sx), 0))
    if "raven" in sp or "owl" in sp:
        piece("primitive_cone_add", mats["acc"], "Head", vertices=8, radius1=0.028, depth=0.07, location=(hb["cx"], hb["front"] - 0.03, ear_z - 0.04), rotation=(math.radians(-90), 0, 0))
    if any(k in sp for k in ("fox", "cat", "lion", "wolf", "hare", "lizard", "shark")):
        ph, _ = rest_bone(arm, "pelvis")
        piece("primitive_cylinder_add", fur if "shark" not in sp and "lizard" not in sp else skin, "pelvis", vertices=10, radius=0.045, depth=0.42, location=(ph.x, ph.y + 0.24, ph.z - 0.12), rotation=(math.radians(55), 0, 0))
    if "turtle" in sp:
        s3, _ = rest_bone(arm, "spine_03")
        o = piece("primitive_uv_sphere_add", fur, "spine_03", segments=16, ring_count=8, radius=0.3, location=(s3.x, s3.y + 0.16, s3.z - 0.08)); 
    if "shark" in sp:
        s3, _ = rest_bone(arm, "spine_03")
        piece("primitive_cone_add", skin, "spine_03", vertices=4, radius1=0.12, depth=0.22, location=(s3.x, s3.y + 0.2, s3.z), rotation=(math.radians(-20), 0, 0))
    if any(k in sp for k in ("goat", "stag", "dryad")):
        for sx in (-1, 1): piece("primitive_cone_add", mats["acc"], "Head", vertices=8, radius1=0.028, depth=0.16, location=(hb["cx"] + hb["hw"] * 0.65 * sx, hb["cy"] + 0.02, top.z - 0.01), rotation=(0, math.radians(30 * sx), 0))
    if "golem" in sp:
        # stone armour: pauldron plates over each shoulder, a breastplate and a back slab (bevelled blocks)
        stone = P.flat("K_Stone", P.hexc("#4e4a44" if "titan" in sp else "#7a746a"), rough=0.95)
        def slab(bone, loc, size, rot=(0, 0, 0)):
            o = prim("primitive_cube_add", stone, size=1, location=loc, rotation=rot); o.scale = size
            bv = o.modifiers.new("B", "BEVEL"); bv.width = 0.02; bv.segments = 2
            for x in bpy.context.selected_objects: x.select_set(False)
            o.select_set(True); bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier="B")
            o.name = "Kit_Species"; apply_xform(o); out.append(bind(o, arm, {bone: 1.0}))
        for sx, side in ((1, "l"), (-1, "r")):
            ua, _ = rest_bone(arm, "upperarm_" + side)
            slab("upperarm_" + side, ua + Vector((0.05 * sx, 0, 0.11)), (0.2, 0.2, 0.06), (0, math.radians(-14 * sx), 0))
        s2, _ = rest_bone(arm, "spine_03"); nk, _ = rest_bone(arm, "neck_01"); s2 = Vector((s2.x, s2.y, nk.z - 0.2))
        # breast and back plates sit ON the chest: measured from the body surface, not a fixed offset
        bvh = body_bvh(); cz = s2.z
        fr = bvh.ray_cast(Vector((s2.x, s2.y - 1.0, cz)), Vector((0, 1, 0)), 2.0)[0]
        bk = bvh.ray_cast(Vector((s2.x, s2.y + 1.0, cz)), Vector((0, -1, 0)), 2.0)[0]
        fy = (fr.y if fr is not None else s2.y - 0.12) - 0.03; by = (bk.y if bk is not None else s2.y + 0.12) + 0.035
        if "titan" not in sp: slab("spine_03", Vector((s2.x, fy, cz)), (0.28, 0.05, 0.24), (math.radians(-6), 0, 0))
        else: fy = (fr.y if fr is not None else s2.y - 0.12) + 0.022   # runes go straight onto the armour
        slab("spine_03", Vector((s2.x, by, cz + 0.02)), (0.3, 0.06, 0.28), (math.radians(8), 0, 0))
        if "titan" in sp:
            # glowing sigil runes cut into the breastplate, and a burning visor slit (the game tints the
            # glow with the Titan's current element through the glow mask)
            glow = mats["glow"]
            for k, (dx, dz, w_, h_) in enumerate(((0, 0.07, 0.16, 0.018), (0, -0.04, 0.12, 0.018), (-0.05, 0.015, 0.018, 0.1), (0.05, 0.015, 0.018, 0.1))):
                o = prim("primitive_cube_add", glow, size=1, location=(s2.x + dx, fy - 0.027, cz + dz)); o.scale = (w_, 0.01, h_)
                o.name = "Kit_Species"; apply_xform(o); out.append(bind(o, arm, {"spine_03": 1.0}))
            eye = hb["base"] + (hb["top"] - hb["base"]) * 0.47   # the helm raises the head's top
            vis = bvh.ray_cast(Vector((hb["cx"], hb["front"] - 0.5, eye)), Vector((0, 1, 0)), 1.0)[0]
            vy = (vis.y if vis is not None else hb["front"]) - 0.004
            o = prim("primitive_cube_add", glow, size=1, location=(hb["cx"], vy, eye)); o.scale = (hb["hw"] * 1.1, 0.012, 0.016)
            o.name = "Kit_Species"; apply_xform(o); out.append(bind(o, arm, {"Head": 1.0}))
    if "wraith" in sp or "spirit" in sp:
        ph, _ = rest_bone(arm, "pelvis"); tl, _ = rest_bone(arm, "thigh_l"); tr, _ = rest_bone(arm, "thigh_r")
        bpy.ops.mesh.primitive_cone_add(vertices=20, radius1=0.12, radius2=0.32, depth=0.95, location=(ph.x, ph.y, ph.z - 0.45), end_fill_type="NOTHING")
        o = bpy.context.object; o.data.materials.append(mats["glow"]); o.name = "Kit_Species"; apply_xform(o)
        out.append(bind(o, arm, {"pelvis": 0.6, "thigh_l": 0.2, "thigh_r": 0.2}))
    return out

def decimate_to(obj, tris):
    n = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    if n > tris:
        d = obj.modifiers.new("Dec", "DECIMATE"); d.ratio = tris / n
        for x in bpy.context.selected_objects: x.select_set(False)
        obj.select_set(True); bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_move_to_index(modifier="Dec", index=0)
        bpy.ops.object.modifier_apply(modifier="Dec")
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)

def bake(obj, size, out_dir, hid):
    """One atlas from the tinted part materials: a new UV map (smart project) is the bake target; the
    textures keep sampling the parts' own UVs."""
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"; sc.cycles.samples = 4; sc.cycles.device = "CPU"
    src_name = obj.data.uv_layers[0].name  # keep the name: layer references go stale when layers are added
    obj.data.uv_layers.new(name="Atlas")
    obj.data.uv_layers[src_name].active_render = True
    atlas = obj.data.uv_layers["Atlas"]
    obj.data.uv_layers.active = atlas
    for x in bpy.context.selected_objects: x.select_set(False)
    obj.select_set(True); bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.002)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.002)
    bpy.ops.object.mode_set(mode="OBJECT")
    imgs = {k: bpy.data.images.new(f"T_{k}", size if k != "glow" else size // 2, size if k != "glow" else size // 2, alpha=False) for k in ("albedo", "normal", "glow")}
    imgs["normal"].colorspace_settings.name = "Non-Color"
    def target(img):
        for m in obj.data.materials:
            if m is None or not m.use_nodes: continue
            nt = m.node_tree
            for n in [n for n in nt.nodes if n.name == "BakeTarget"]: nt.nodes.remove(n)
            t = nt.nodes.new("ShaderNodeTexImage"); t.name = "BakeTarget"; t.image = img
            uvn = nt.nodes.new("ShaderNodeUVMap"); uvn.uv_map = "Atlas"; nt.links.new(uvn.outputs[0], t.inputs[0])
            nt.nodes.active = t
    bk = sc.render.bake; bk.margin = 4
    bk.use_pass_direct = False; bk.use_pass_indirect = False; bk.use_pass_color = True
    target(imgs["albedo"]); bpy.ops.object.bake(type="DIFFUSE")
    import kit_paint; kit_paint.painted_pass(obj, imgs["albedo"], size)   # phase E 51-52: crevices, worn edges, top light
    target(imgs["normal"]); bk.normal_space = "TANGENT"; bpy.ops.object.bake(type="NORMAL", uv_layer="Atlas")
    target(imgs["glow"]); bpy.ops.object.bake(type="EMIT")
    paths = {}
    for k, img in imgs.items():
        p = os.path.join(out_dir, f"SB_Hero_{hid}_HD_{k}.png"); img.filepath_raw = p; img.file_format = "PNG"; img.save(); paths[k] = p
    # Keep only the atlas UVs, and one placeholder material (the game applies the PNGs).
    for name in [l.name for l in obj.data.uv_layers if l.name != "Atlas"]: obj.data.uv_layers.remove(obj.data.uv_layers[name])
    obj.data.uv_layers["Atlas"].active_render = True
    m = bpy.data.materials.new("M_Hero_HD"); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes["Principled BSDF"]
    ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = imgs["albedo"]; nt.links.new(ta.outputs[0], bsdf.inputs["Base Color"])
    tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = imgs["normal"]; nm = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(tn.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], bsdf.inputs["Normal"])
    tg = nt.nodes.new("ShaderNodeTexImage"); tg.image = imgs["glow"]; nt.links.new(tg.outputs[0], bsdf.inputs["Emission Color"]); bsdf.inputs["Emission Strength"].default_value = 1.5
    obj.data.materials.clear(); obj.data.materials.append(m)
    for poly in obj.data.polygons: poly.material_index = 0
    for ca in list(obj.data.color_attributes): obj.data.color_attributes.remove(ca)  # the parts' vertex colours aren't used
    return paths

def export(arm, obj, path):
    for x in bpy.context.selected_objects: x.select_set(False)
    arm.select_set(True); obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_skins=True, export_animations=False,
                              export_apply=False, export_yup=True, export_materials="PLACEHOLDER")
    return os.path.getsize(path) // 1024

def normalise_parts(c):
    """Outfit part names are file names (Male_Knight_Body_Armor). The phase H designs wrote them as
    Pack/Part ("Female_Ranger/Legs"): nothing matched, and every such slot silently fell back to the
    peasant part. Rewrite Pack/Part to Pack_Part everywhere a part is named, keeping any note after it."""
    o = c.get("outfit") or {}
    fix = lambda v: re.sub(r"\b((?:Male|Female)_[A-Za-z]+)/([A-Za-z_]+)", r"\1_\2", v) if isinstance(v, str) else v
    for k in ("arms", "body", "legs", "feet", "head"):
        if k in o: o[k] = fix(o[k])
    if o.get("acc"): o["acc"] = [fix(x) for x in o["acc"]]
    return c

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--design", required=True); ap.add_argument("--hero", required=True); ap.add_argument("--out", required=True)
    ap.add_argument("--tex", type=int, default=1024); ap.add_argument("--tris", type=int, default=25000); ap.add_argument("--render", action="store_true")
    ap.add_argument("--lod1", action="store_true", help="also export a ~1/3-triangle LOD1 (not shipped by default)")
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:])
    os.makedirs(a.out, exist_ok=True)
    c = normalise_parts({d["id"]: d for d in json.load(open(a.design))}[a.hero])
    arm = P.build(c, pose=False)
    pieces = kit(c, arm)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for x in bpy.context.selected_objects: x.select_set(False)
    for o in meshes: o.select_set(True)
    body = next((o for o in meshes if not o.name.startswith("Kit_")), meshes[0])
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.join()
    hero = bpy.context.view_layer.objects.active; hero.name = f"SB_Hero_{a.hero}"
    for md in [m for m in hero.modifiers if m.type == "ARMATURE"][1:]: hero.modifiers.remove(md)
    for o in [o for o in bpy.context.scene.objects if o.type == "MESH" and o != hero]:
        bpy.data.objects.remove(o, do_unlink=True)  # leftovers (e.g. a helper Icosphere) must not ship
    raw = sum(len(p.vertices) - 2 for p in hero.data.polygons)
    tris = decimate_to(hero, a.tris)
    paths = bake(hero, a.tex, a.out, a.hero)
    kb = export(arm, hero, os.path.join(a.out, f"SB_Hero_{a.hero}_HD.glb"))
    t1 = kb1 = 0
    if a.lod1:
        t1 = decimate_to(hero, max(4000, tris // 3))
        kb1 = export(arm, hero, os.path.join(a.out, f"SB_Hero_{a.hero}_HD_LOD1.glb"))
    # Phones: JPEG albedo and normal (the game reads .jpg or .png); glow stays a small PNG.
    try:
        from PIL import Image
        for k, q in (("albedo", 88), ("normal", 92)):
            p = paths[k]; Image.open(p).convert("RGB").save(p[:-4] + ".jpg", quality=q, optimize=True); os.remove(p)
    except ImportError:
        print("[ubc] Pillow not available: textures stay PNG")
    print(f"[ubc] {a.hero}: {raw} -> {tris} tris (LOD1 {t1}), {len(arm.data.bones)} bones, {kb} KB + LOD1 {kb1} KB, textures {a.tex}")

if __name__ == "__main__":
    main()
