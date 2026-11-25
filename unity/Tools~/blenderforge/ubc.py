"""High-detail hero builder (docs/sigilbound/PLAN.md phase F, step 57).

Builds one hero from a design (concepts.json / new_heroes.json format) on the CC0 Quaternius Universal
Base Characters + Fantasy outfit parts in godot/models/ubc (ubc_parts.py picks and tints the parts),
adds kit pieces bound rigidly to bones, merges everything onto ONE 65-joint skeleton, decimates to a
triangle budget and bakes one atlas (albedo, tangent-space normal, glow).

    bpyenv/bin/python ubc.py -- --design concepts.json --hero brannoc --out DIR [--tex 1024] [--tris 25000] [--render]

Writes SB_Hero_<id>_HD.glb (rest pose, skinned, no clips, placeholder material) with
SB_Hero_<id>_HD_albedo/_normal/_glow PNGs, and SB_Hero_<id>_HD_LOD1.glb (same atlas, ~1/3 triangles).
Kit pieces are still simple shapes; steps 59-63 replace them with modelled ones.
"""
import bpy, bmesh, sys, os, json, math, re, argparse
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ubc_parts as P  # noqa: E402

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

def weapon(c, arm, mats):
    """A simple weapon along local +Z at the right-hand grip, rigid to hand_r."""
    w = str(c.get("weapon", "")).lower()
    metal, main, acc, glow = mats["metal"], mats["main"], mats["acc"], mats["glow"]
    parts = []
    def add(o): parts.append(o); return o
    if has(w, "spear", "lance", "glaive", "trident", "halberd", "scythe", "staff", "sceptre", "pike", "oar", "polearm", "quarterstaff"):
        add(prim("primitive_cylinder_add", main if has(w, "staff", "sceptre") else metal, vertices=10, radius=0.02, depth=2.0, location=(0, 0, 0.45)))
        if has(w, "staff", "sceptre"): add(prim("primitive_uv_sphere_add", glow, segments=16, ring_count=8, radius=0.08, location=(0, 0, 1.5)))
        else: add(prim("primitive_cone_add", metal, vertices=8, radius1=0.05, depth=0.32, location=(0, 0, 1.6)))
        if has(w, "trident"):
            for dx in (-0.08, 0.08): add(prim("primitive_cone_add", metal, vertices=6, radius1=0.025, depth=0.24, location=(dx, 0, 1.52)))
        if has(w, "scythe", "glaive", "halberd"):
            o = add(prim("primitive_cube_add", metal, size=1, location=(0.16, 0, 1.38))); o.scale = (0.32, 0.012, 0.09)
    elif has(w, "sword", "katana", "rapier", "blade", "sabre", "cutlass", "longsword", "greatsword"):
        o = add(prim("primitive_cube_add", metal, size=1, location=(0, 0, 0.55))); o.scale = (0.012, 0.05, 0.9)
        o = add(prim("primitive_cube_add", acc, size=1, location=(0, 0, 0.08))); o.scale = (0.03, 0.18, 0.03)
        add(prim("primitive_cylinder_add", main, vertices=8, radius=0.018, depth=0.18, location=(0, 0, -0.04)))
    elif has(w, "hammer", "maul", "mace", "club", "flail", "tankard", "pickaxe", "pick", "axe"):
        add(prim("primitive_cylinder_add", main, vertices=8, radius=0.022, depth=0.9, location=(0, 0, 0.3)))
        o = add(prim("primitive_cube_add", metal, size=1, location=(0, 0, 0.75))); o.scale = (0.26, 0.15, 0.16)
    elif has(w, "dagger", "knife", "sickle", "hook", "fan", "quill", "wand"):
        o = add(prim("primitive_cube_add", metal, size=1, location=(0, 0, 0.17))); o.scale = (0.01, 0.035, 0.3)
    elif has(w, "bow", "crossbow"):
        bpy.ops.mesh.primitive_torus_add(major_segments=24, minor_segments=6, major_radius=0.55, minor_radius=0.014, location=(0, 0, 0))
        o = bpy.context.object; o.data.materials.append(main); o.scale = (0.35, 1, 1); add(o)
    elif has(w, "orb", "lantern", "candle", "bell", "mirror", "book", "tome", "conch", "lamp", "harp", "lute", "lens"):
        add(prim("primitive_uv_sphere_add", glow, segments=16, ring_count=8, radius=0.08, location=(0, 0, 0.1)))
    else:
        add(prim("primitive_cylinder_add", main, vertices=8, radius=0.02, depth=1.6, location=(0, 0, 0.4)))
        add(prim("primitive_uv_sphere_add", glow, segments=12, ring_count=6, radius=0.06, location=(0, 0, 1.25)))
    if not parts: return []
    for x in bpy.context.selected_objects: x.select_set(False)
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]; bpy.ops.object.join()
    o = bpy.context.object; o.name = "Kit_Weapon"
    o.matrix_world = grip_frame(arm, "r") @ o.matrix_world
    apply_xform(o)
    return [bind(o, arm, {"hand_r": 1.0})]

def body_bvh():
    """BVH of the dressed body (every non-kit mesh, evaluated in the rest pose) for fitting kit to it."""
    from mathutils.bvhtree import BVHTree
    dg = bpy.context.evaluated_depsgraph_get()
    verts, polys = [], []
    for o in bpy.context.scene.objects:
        if o.type != "MESH" or o.name.startswith("Kit_"): continue
        me = o.evaluated_get(dg).to_mesh(); mw = o.matrix_world; base = len(verts)
        verts += [mw @ v.co for v in me.vertices]; polys += [[base + i for i in p.vertices] for p in me.polygons]
        o.evaluated_get(dg).to_mesh_clear()
    return BVHTree.FromPolygons(verts, polys)

def make_cape(arm, mat, clasp_mat, long_=True):
    """A cape fitted to this body: the top drapes OVER the shoulders and fastens with two clasps at the
    collarbones; the sheet follows the back's curve (ray-cast onto the dressed body), keeps clear of
    the shoulder blades, hips and calves, and flares toward an open front. Weights: the top row follows
    the clavicles and upper chest, blending to the pelvis and thighs toward the hem, so it moves with the
    shoulders instead of hanging like a board."""
    bvh = body_bvh()
    cl, _ = rest_bone(arm, "clavicle_l"); cr, _ = rest_bone(arm, "clavicle_r")
    ul, _ = rest_bone(arm, "upperarm_l"); ur, _ = rest_bone(arm, "upperarm_r")
    nh, _ = rest_bone(arm, "neck_01"); ph, _ = rest_bone(arm, "pelvis"); kl, _ = rest_bone(arm, "calf_l")
    back = 1.0 if (rest_bone(arm, "spine_03")[0].y < 0.5) else 1.0   # +y is the back on this rig (the face looks to -y)
    top_z = nh.z - 0.03
    hem_z = (kl.z - 0.12) if long_ else (ph.z + 0.02)
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
    for sx, c_ in ((1, cl), (-1, cr)):   # clasps at the collarbones
        bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=0.028, depth=0.012, location=(nh.x + 0.09 * sx, nh.y - 0.1, top_z - 0.03), rotation=(math.radians(90), 0, 0))
        k = bpy.context.object; k.data.materials.append(clasp_mat); k.name = "Kit_Clasp"
        out.append(bind(k, arm, {"clavicle_l" if sx > 0 else "clavicle_r": 1.0}))
    return out

def kit(c, arm):
    pal = c.get("palette", {}); el = P.hexc(ELEM.get(c.get("element"), pal.get("glow")))
    mats = {"metal": P.flat("K_Metal", P.hexc(pal.get("metal", "#aab0b8")), metal=0.9, rough=0.3),
            "acc": P.flat("K_Accent", P.hexc(pal.get("accent")), metal=0.3, rough=0.4),
            "main": P.flat("K_Main", P.hexc(pal.get("main")), rough=0.6),
            "glow": P.flat("K_Glow", el, emit=4)}
    text = " ".join([str(c.get("weapon", ""))] + [str(k) for k in c.get("kit", [])] + [str(x) for x in (c.get("outfit", {}).get("acc") or [])]
                    + [str(c.get("outfit", {}).get("head", ""))]).lower()
    out = weapon(c, arm, mats)
    if has(text, "shield"):
        m = grip_frame(arm, "l")
        o = prim("primitive_cylinder_add", mats["metal"], vertices=24, radius=0.26, depth=0.03, location=(0, 0, 0))
        o.scale = (1, 1.25 if has(text, "tower") else 1, 1)
        o.matrix_world = m @ Matrix.Translation((-0.09, 0, 0.0)) @ Matrix.Rotation(math.radians(90), 4, "Y") @ o.matrix_world  # on the back of the forearm
        apply_xform(o); o.name = "Kit_Shield"; out.append(bind(o, arm, {"lowerarm_l": 1.0}))
    hh, ht = rest_bone(arm, "Head"); top = ht + Vector((0, 0, 0.1))
    if has(text, "halo") or (c.get("rarity") == "Legendary" and not has(text, "crown", "helm", "hood")):
        bpy.ops.mesh.primitive_torus_add(major_segments=32, minor_segments=6, major_radius=0.14, minor_radius=0.012, location=top + Vector((0, 0, 0.12)))
        o = bpy.context.object; o.data.materials.append(mats["glow"]); o.name = "Kit_Halo"; out.append(bind(o, arm, {"Head": 1.0}))
    if has(text, "crown", "circlet", "diadem") and not str(c.get("outfit", {}).get("head", "")).startswith("Noble_Head_Crown"):
        o = prim("primitive_cylinder_add", mats["acc"], vertices=16, radius=0.105, depth=0.04, location=top - Vector((0, 0, 0.03)), end_fill_type="NOTHING")
        o.name = "Kit_Crown"; out.append(bind(o, arm, {"Head": 1.0}))
    if has(text, "horn", "antler"):
        for sx in (-1, 1):
            o = prim("primitive_cone_add", mats["acc"], vertices=8, radius1=0.035, depth=0.2, location=top + Vector((0.09 * sx, 0.02, -0.02)), rotation=(0, math.radians(25 * sx), 0))
            o.name = "Kit_Horn"; out.append(bind(o, arm, {"Head": 1.0}))
    nh, _ = rest_bone(arm, "neck_01")
    if has(text, "cape", "cloak", "mantle"):
        out += make_cape(arm, mats["main"], mats["acc"], long_=not has(text, "short cape", "half cape", "shoulder cape", "mantle"))
    if has(text, "robe", "skirt", "dress", "gown") or str(c.get("outfit", {}).get("legs", "")).startswith("none"):
        ph, _ = rest_bone(arm, "pelvis"); tl, _ = rest_bone(arm, "thigh_l"); tr, _ = rest_bone(arm, "thigh_r")
        bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=0.36, radius2=0.17, depth=0.95, location=ph + Vector((0, 0, -0.42)), end_fill_type="NOTHING")
        o = bpy.context.object; o.data.materials.append(mats["main"]); apply_xform(o)
        topz, hemz = ph.z + 0.05, ph.z - 0.9
        def robe_w(p):
            t = max(0.0, min(1.0, 1.8 * (topz - p.z) / (topz - hemz))) * 0.95
            sl = max(0.0, min(1.0, (p.x - tr.x) / max(1e-6, tl.x - tr.x))); sl = sl * sl * (3 - 2 * sl)
            return {"pelvis": 1 - t, "thigh_l": t * sl, "thigh_r": t * (1 - sl)}
        o.name = "Kit_Robe"; out.append(bind(o, arm, robe_w))
    if has(text, "wing"):
        for sx in (-1, 1):
            o = prim("primitive_cube_add", mats["glow"] if c.get("element") in ("Light", "Flame") else mats["main"], size=1,
                     location=nh + Vector((0.42 * sx, 0.22, -0.15)), rotation=(0, math.radians(-25 * sx), 0))
            o.scale = (0.8, 0.02, 0.44); apply_xform(o); o.name = "Kit_Wing"; out.append(bind(o, arm, {"spine_03": 1.0}))
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

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--design", required=True); ap.add_argument("--hero", required=True); ap.add_argument("--out", required=True)
    ap.add_argument("--tex", type=int, default=1024); ap.add_argument("--tris", type=int, default=25000); ap.add_argument("--render", action="store_true")
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:])
    os.makedirs(a.out, exist_ok=True)
    c = {d["id"]: d for d in json.load(open(a.design))}[a.hero]
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
    t1 = decimate_to(hero, max(4000, tris // 3))
    kb1 = export(arm, hero, os.path.join(a.out, f"SB_Hero_{a.hero}_HD_LOD1.glb"))
    print(f"[ubc] {a.hero}: {raw} -> {tris} tris (LOD1 {t1}), {len(arm.data.bones)} bones, {kb} KB + LOD1 {kb1} KB, textures {a.tex}")

if __name__ == "__main__":
    main()
