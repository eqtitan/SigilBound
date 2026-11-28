"""Rig an image-to-3D hero mesh (TRELLIS / Hunyuan3D output, cleaned by i3d_prep) to the UBC skeleton.

    bpyenv/bin/python i3d_rig.py -- --hero talvo --clean DIR/talvo_clean.blend --texsrc DIR --out OUTDIR

How (so the shared UBC battle clips play on it):
1. A1 = the UBC armature (T-pose) with its joints moved to the mesh's landmarks (i3d_landmarks.py): limb bones keep
   their T-pose directions and rolls, only lengths and positions change. RigExtras (Unity) re-expresses the
   clips' positions for bones that moved, as for the kit creature rigs; the marker bone "rest_fit" turns it on.
2. A1 is posed into the mesh's pose (limbs aimed at the landmarks); a copy A2 takes that pose as its rest, and
   the mesh is skinned to A2 with bone-heat weights (fingers, face and leaf bones don't deform; held weapons,
   shields and other rigid pieces are overridden to one bone).
3. A2 is posed back to A1's rest and applied: the mesh is now in the UBC T-pose, skinned to A1, and exported in
   the HD layout (SB_Hero_<id>_HD.glb + _albedo.jpg [+ _glow.png]).
"""
import bpy, bmesh, sys, os, math, json, argparse
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ubc_parts as P  # noqa: E402
import importlib  # noqa: E402
# landmark tables: i3d_landmarks (the new heroes) and i3d_landmarks_portrait (roster heroes remodelled from
# their portraits), whichever this branch carries
LANDMARKS = {}
for _m in ("i3d_landmarks", "i3d_landmarks_portrait"):
    try: LANDMARKS.update(importlib.import_module(_m).LANDMARKS)
    except ModuleNotFoundError: pass

NODEFORM = ("index", "middle", "ring", "pinky", "thumb", "_leaf", "root", "rest_fit")


def ray_mid(bvh, x, z, y_hint=None):
    """Centre of the first solid layer hit by a ray from the front (-Y) at (x, z)."""
    if y_hint is not None:
        return y_hint
    o = Vector((x, -5.0, z)); d = Vector((0, 1, 0))
    hit = bvh.ray_cast(o, d)
    if hit[0] is None:
        return 0.0
    y1 = hit[0].y
    hit2 = bvh.ray_cast(hit[0] + d * 1e-3, d)
    y2 = hit2[0].y if hit2[0] is not None else y1 + 0.1
    if y2 - y1 > 0.5:
        y2 = y1 + 0.12
    return (y1 + y2) / 2


def pt(bvh, spec):
    if len(spec) == 3:
        return Vector(spec)
    x, z = spec
    return Vector((x, ray_mid(bvh, x, z), z))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--hero", required=True); ap.add_argument("--clean", required=True)
    ap.add_argument("--out", required=True); ap.add_argument("--blend", default="")
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:])
    L = LANDMARKS[a.hero]
    bpy.ops.wm.open_mainfile(filepath=a.clean)
    body = bpy.data.objects["Body"]
    # optional mesh trims (generator guesses to drop)
    if L.get("trim"):
        bm = bmesh.new(); bm.from_mesh(body.data)
        kill = [f for f in bm.faces if any(t(f.calc_center_median()) for t in L["trim"])]
        bmesh.ops.delete(bm, geom=kill, context="FACES")
        loose = [v for v in bm.verts if not v.link_faces]
        bmesh.ops.delete(bm, geom=loose, context="VERTS")
        bm.to_mesh(body.data); bm.free()
    # triangle cap (HD budget; the generator's meshes are dense where the texture has detail)
    cap = L.get("max_tris", 18000)
    n = sum(len(p.vertices) - 2 for p in body.data.polygons)
    if n > cap:
        for o in bpy.context.selected_objects: o.select_set(False)
        body.select_set(True); bpy.context.view_layer.objects.active = body
        d = body.modifiers.new("Dec", "DECIMATE"); d.ratio = cap / n
        bpy.ops.object.modifier_apply(modifier="Dec")
    # texture fixes for generator guesses (recolour masks are UV triangles of faces picked in the mesh's pose)
    recol = []
    if L.get("recolor"):
        uvl = body.data.uv_layers.active.data
        bm = bmesh.new(); bm.from_mesh(body.data); bm.faces.ensure_lookup_table()
        for rc in L["recolor"]:
            sel = [f for f in bm.faces if rc["faces"](f.calc_center_median(), f.normal)]
            ids = {f.index for f in sel}
            edge = [f.index for f in sel if any(e.is_boundary or any(g.index not in ids for g in e.link_faces) for e in f.edges)]
            tri = lambda fi: [tuple(uvl[l].uv) for l in body.data.polygons[fi].loop_indices]
            recol.append((rc, [tri(f.index) for f in sel], [tri(fi) for fi in edge]))
        bm.free()
    halo_uv = None
    if L.get("halo"):
        hc = L["halo"]
        bpy.ops.mesh.primitive_torus_add(major_radius=hc["radius"], minor_radius=hc["minor"], major_segments=48, minor_segments=8, generate_uvs=False,
                                         location=hc["center"], rotation=(math.radians(90), 0, 0))
        ring = bpy.context.object
        # every halo vertex samples one reserved gold texel block (found free in the UV layout at export)
        ring.data.uv_layers.new(name=body.data.uv_layers.active.name)
        for o in bpy.context.selected_objects: o.select_set(False)
        ring.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active = body
        bpy.ops.object.join()
        halo_uv = True
    dg = bpy.context.evaluated_depsgraph_get()
    bvh = BVHTree.FromObject(body, dg)

    # --- UBC armature ---
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=P.FILES["Regular_Male_FullBody"])
    new = [o for o in bpy.data.objects if o not in before]
    A1 = next(o for o in new if o.type == "ARMATURE")
    for o in new:
        if o.type == "MESH": bpy.data.objects.remove(o, do_unlink=True)
    A1.name = "Armature"; A1.data.name = "Armature"
    bpy.context.view_layer.update()

    J = {k: pt(bvh, v) for k, v in L["joints"].items()}
    # T-pose targets in A1 (rest): arms out along +-X from the shoulder, legs straight down from the hip.
    def seg(a_, b_): return (J[b_] - J[a_]).length
    rest = {}
    for s, sg in (("l", 1), ("r", -1)):
        sh = J["shoulder_" + s]
        e = sh + Vector((sg * seg("shoulder_" + s, "elbow_" + s), 0, 0))
        w = e + Vector((sg * seg("elbow_" + s, "wrist_" + s), 0, 0))
        h = w + Vector((sg * max(0.06, seg("wrist_" + s, "handend_" + s)), 0, 0))
        rest.update({"upperarm_" + s: sh, "lowerarm_" + s: e, "hand_" + s: w, "handend_" + s: h})
        hp = J["hip_" + s]
        k = hp + Vector((0, 0, -seg("hip_" + s, "knee_" + s)))
        an = k + Vector((0, 0.02, -seg("knee_" + s, "ankle_" + s)))
        toe = an + Vector((0, -max(0.12, seg("ankle_" + s, "toe_" + s)), -an.z + 0.02))
        rest.update({"thigh_" + s: hp, "calf_" + s: k, "foot_" + s: an, "ball_" + s: an + (toe - an) * 0.6, "toe_" + s: toe})

    bpy.context.view_layer.objects.active = A1
    for o in bpy.context.selected_objects: o.select_set(False)
    A1.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = A1.data.edit_bones
    orig = {b.name: (b.head.copy(), b.tail.copy(), b.roll) for b in eb}

    def place(name, head, tail):
        b = eb[name]; roll = b.roll
        b.head = head; b.tail = tail; b.roll = roll

    # spine chain straight from the landmarks (rest = the mesh's own posture)
    chain = ["pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "Head"]
    pts = [J["pelvis"], J["spine_01"], J["spine_02"], J["spine_03"], J["neck"], J["head"], J["head_top"]]
    for i, n in enumerate(chain):
        place(n, pts[i], pts[i + 1])
    for s, sg in (("l", 1), ("r", -1)):
        place("clavicle_" + s, J["spine_03"].lerp(J["neck"], 0.8) + Vector((sg * 0.03, 0, 0)), rest["upperarm_" + s])
        place("upperarm_" + s, rest["upperarm_" + s], rest["lowerarm_" + s])
        place("lowerarm_" + s, rest["lowerarm_" + s], rest["hand_" + s])
        place("hand_" + s, rest["hand_" + s], rest["handend_" + s])
        # fingers: keep their offsets from the hand, scaled by the hand length ratio
        oh = orig["hand_" + s][0]; olen = (orig["hand_" + s][1] - oh).length
        k = max(0.6, min(2.5, (rest["handend_" + s] - rest["hand_" + s]).length / max(1e-4, olen) * 0.5 + 0.5))
        for b in eb:
            if b.name.endswith("_" + s) and any(b.name.startswith(f) for f in ("index", "middle", "ring", "pinky", "thumb")):
                h0, t0, r0 = orig[b.name]
                b.head = rest["hand_" + s] + (h0 - oh) * k; b.tail = rest["hand_" + s] + (t0 - oh) * k; b.roll = r0
        place("thigh_" + s, rest["thigh_" + s], rest["calf_" + s])
        place("calf_" + s, rest["calf_" + s], rest["foot_" + s])
        place("foot_" + s, rest["foot_" + s], rest["ball_" + s])
        place("ball_" + s, rest["ball_" + s], rest["toe_" + s])
        lf = eb["ball_leaf_" + s]; lf.head = rest["toe_" + s]; lf.tail = rest["toe_" + s] + (rest["toe_" + s] - rest["ball_" + s]) * 0.8
    # tail chain (RigExtras sways bones named tail_01..)
    prev = eb["pelvis"]
    for i, p in enumerate(L.get("tail", [])[:-1]):
        b = eb.new(f"tail_{i + 1:02d}"); b.head = Vector(p); b.tail = Vector(L["tail"][i + 1]); b.parent = prev
        b.use_connect = False; prev = b
    # extra rigid bones (moth wings: wing_l / wing_r, which RigExtras flutters about their own axis, so the
    # bone runs up the wing's hinge); they take only the vertices a rigid test gives them
    for name, parent, h, tl in L.get("extra_bones", []):
        b = eb.new(name); b.head = Vector(h); b.tail = Vector(tl); b.parent = eb[parent]; b.use_connect = False
    m = eb.new("rest_fit"); m.head = Vector((0, 0, 0)); m.tail = Vector((0, 0.05, 0)); m.parent = eb["root"]
    bpy.ops.object.mode_set(mode="OBJECT")
    for b in A1.data.bones:
        b.use_deform = not any(k in b.name for k in NODEFORM)

    # --- pose A1 into the mesh's pose: aim each limb bone at its landmark ---
    bpy.ops.object.mode_set(mode="POSE")
    aims = []
    for s in ("l", "r"):
        aims += [("upperarm_" + s, "elbow_" + s), ("lowerarm_" + s, "wrist_" + s), ("hand_" + s, "handend_" + s),
                 ("thigh_" + s, "knee_" + s), ("calf_" + s, "ankle_" + s), ("foot_" + s, "toe_" + s)]
    for bone, tgt in aims:
        pb = A1.pose.bones[bone]
        bpy.context.view_layer.update()
        head = A1.matrix_world @ pb.head; tail = A1.matrix_world @ pb.tail
        v = tail - head; t = J[tgt] - head
        if bone.startswith("foot_"):
            t = Vector((t.x, t.y, 0)) if t.length > 0 else v  # feet stay flat
            v = Vector((v.x, v.y, 0))
        if v.length < 1e-5 or t.length < 1e-5: continue
        q = v.rotation_difference(t)
        pb.matrix = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ pb.matrix
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")

    # --- A2: the posed armature as rest; skin the mesh to it ---
    A2 = A1.copy(); A2.data = A1.data.copy(); A2.name = "A2"; bpy.context.scene.collection.objects.link(A2)
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = A2; A2.select_set(True)
    bpy.ops.object.mode_set(mode="POSE"); bpy.ops.pose.select_all(action="SELECT"); bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in bpy.context.selected_objects: o.select_set(False)
    body.select_set(True); A2.select_set(True); bpy.context.view_layer.objects.active = A2
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    defb = [b.name for b in A2.data.bones if b.use_deform]
    # weights: fill anything heat missed (nearest bone segment), then the rigid overrides
    vg = {n: (body.vertex_groups.get(n) or body.vertex_groups.new(name=n)) for n in defb}
    segs = {n: (A2.matrix_world @ A2.data.bones[n].head_local, A2.matrix_world @ A2.data.bones[n].tail_local) for n in defb}
    extra = {e[0] for e in L.get("extra_bones", [])}
    def nearest(p):
        best, bd = None, 1e9
        for n, (h, t) in segs.items():
            if n in extra: continue
            ab = t - h; u = max(0.0, min(1.0, (p - h).dot(ab) / max(ab.length_squared, 1e-9)))
            d = (p - (h + ab * u)).length
            if d < bd: best, bd = n, d
        return best
    mw = body.matrix_world
    over = L.get("rigid", [])
    for name, *_ in L.get("extra_bones", []):
        if name in body.vertex_groups:
            g = body.vertex_groups[name]; g.remove([v.index for v in body.data.vertices])
    filled = forced = 0; attached = set()
    for v in body.data.vertices:
        p = mw @ v.co
        tgt = next((bn for bn, test in over if test(p)), None)
        if tgt is None and sum(g.weight for g in v.groups) < 1e-3:
            tgt = nearest(p); filled += 1
        elif tgt is not None:
            forced += 1
            if not any(tgt.startswith(k) for k in ("thigh", "calf", "foot", "ball", "clavicle", "upperarm", "lowerarm", "hand")):
                attached.add(v.index)   # back gear, capes, halos: a rigid part on the trunk
        if tgt == "nearest_tail":
            tails = [n for n in defb if n.startswith("tail_")]
            tgt = min(tails, key=lambda n: min(((p - segs[n][0]).length, (p - segs[n][1]).length))) if tails else nearest(p)
        if tgt is not None:
            for gi in [g.group for g in v.groups]: body.vertex_groups[gi].remove([v.index])
            vg[tgt].add([v.index], 1.0, "REPLACE")
    # arm reach: arm-bone weight only within this distance of the arm in the mesh's pose, so bone heat can't drag
    # torso skin out with an arm folded against it into a curtain when the arm goes to the T-pose
    reach = L.get("arm_reach"); cut = 0
    if reach:
        def segd(p, a_, b_):
            ab = b_ - a_; u = max(0.0, min(1.0, (p - a_).dot(ab) / max(ab.length_squared, 1e-9))); return (p - (a_ + ab * u)).length
        for s in ("l", "r"):
            chain_ = [J["shoulder_" + s], J["elbow_" + s], J["wrist_" + s], J["handend_" + s]]
            names = {"upperarm_" + s, "lowerarm_" + s, "hand_" + s}
            gids = {body.vertex_groups[n_].index for n_ in names if n_ in body.vertex_groups}
            for v in body.data.vertices:
                if not any(g.group in gids and g.weight > 0 for g in v.groups): continue
                p = mw @ v.co
                if any(t_(p) for _, t_ in over): continue
                if min(segd(p, a_, b_) for a_, b_ in zip(chain_, chain_[1:])) <= reach: continue
                for gi in gids: body.vertex_groups[gi].remove([v.index])
                if sum(g.weight for g in v.groups) < 1e-3:
                    tgt = min((n_ for n_ in segs if n_ not in extra and not any(n_.startswith(k) for k in ("upperarm", "lowerarm", "hand"))),
                              key=lambda n_: segd(p, *segs[n_]))
                    vg[tgt].add([v.index], 1.0, "REPLACE")
                cut += 1
        print(f"[i3d] {a.hero}: arm weight cut from {cut} torso vertices")
    # soft blends: long robes and coats share part of their weight with a calmer bone (pelvis), so they don't
    # split with the legs: (bone, share, test)
    soft = 0
    for bn, share, test in L.get("blend", []):
        for v in body.data.vertices:
            if not test(mw @ v.co) or any(t_(mw @ v.co) for _, t_ in over): continue
            ws = [(body.vertex_groups[g.group].name, g.weight) for g in v.groups]
            tot = sum(w for _, w in ws) or 1.0
            for n_, w in ws: body.vertex_groups[n_].add([v.index], w / tot * (1 - share), "REPLACE")
            if bn not in [n_ for n_, _ in ws]: vg[bn].add([v.index], share, "REPLACE")
            else: body.vertex_groups[bn].add([v.index], dict(ws)[bn] / tot * (1 - share) + share, "REPLACE")
            soft += 1
    print(f"[i3d] {a.hero}: heat-filled {filled}, rigid {forced}, blended {soft}")

    # --- pose A2 back to A1's rest and apply: the mesh moves into the T-pose ---
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = A2; A2.select_set(True)
    bpy.ops.object.mode_set(mode="POSE")
    def depth(b): return 0 if b.parent is None else 1 + depth(b.parent)
    for pb in sorted(A2.pose.bones, key=lambda p: depth(p.bone)):
        pb.matrix = A1.data.bones[pb.name].matrix_local.copy()
        bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = body; body.select_set(True)
    md = next(m for m in body.modifiers if m.type == "ARMATURE")
    len0 = [(body.data.vertices[e.vertices[0]].co - body.data.vertices[e.vertices[1]].co).length for e in body.data.edges]
    bpy.ops.object.modifier_apply(modifier=md.name)
    # tear generator fusions: faces bridging two separately moving parts (a hammer head welded to a boot, a
    # sleeve to the robe) stretch into sheets once the mesh leaves its own pose; drop faces whose edges grew a lot
    bm = bmesh.new(); bm.from_mesh(body.data); bm.edges.ensure_lookup_table()
    torn_e = {e.index for e in bm.edges if e.calc_length() > max(0.08, len0[e.index] * L.get("tear", 2.5))}
    kill = [f for f in bm.faces if any(e.index in torn_e for e in f.edges)]
    if L.get("split_limbs"):
        # faces joining two different limbs (left leg to right leg, a hand's weapon to a calf), or a limb to a rigid
        # trunk part (a banner tip fused to a knee), become sheets in any stance: drop them (limb to trunk is kept)
        dl = bm.verts.layers.deform.active; gname = {g.index: g.name for g in body.vertex_groups}
        def limb(v):
            w = v[dl] if dl else {}
            if not w: return None
            n_ = gname[max(w.items(), key=lambda kv: kv[1])[0]]
            for pre, tag in (("thigh_", "leg"), ("calf_", "leg"), ("foot_", "leg"), ("ball_", "leg"), ("clavicle_", "arm"),
                             ("upperarm_", "arm"), ("lowerarm_", "arm"), ("hand_", "arm")):
                if n_.startswith(pre): return tag + n_[-2:]
            return None
        lim = {v.index: ("attached" if v.index in attached else limb(v)) for v in bm.verts}
        kill += [f for f in bm.faces if f not in kill and len({lim[v.index] for v in f.verts} - {None}) > 1]
        kill = list(set(kill))
    oid = bm.verts.layers.int.new("oid")
    for v in bm.verts: v[oid] = v.index
    open0 = {frozenset((e.verts[0][oid], e.verts[1][oid])) for e in bm.edges if e.is_boundary}
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    filled_n = 0
    if L.get("fill_holes"):
        # close the holes the tear opened (the generator never modelled what an arm hid): each loop of new
        # boundary edges gets a cap whose corners take the UVs of the faces beside them
        uv = bm.loops.layers.uv.active
        newb = [e for e in bm.edges if e.is_boundary and frozenset((e.verts[0][oid], e.verts[1][oid])) not in open0]
        # one loop at a time, small ones only (a long loop is an arm torn off a robe: a cap would be a sheet)
        left, keep = set(newb), []
        while left:
            st = [left.pop()]; comp = []
            while st:
                e = st.pop(); comp.append(e)
                for v in e.verts:
                    for e2 in v.link_edges:
                        if e2 in left: left.discard(e2); st.append(e2)
            if sum(e.calc_length() for e in comp) < L.get("fill_max", 0.6): keep += comp
        res = bmesh.ops.holes_fill(bm, edges=keep, sides=0)
        for f in res["faces"]:
            for lp in f.loops:
                src = next((l2 for l2 in lp.vert.link_loops if l2.face is not f and l2.face not in res["faces"]), None)
                if src is not None: lp[uv].uv = src[uv].uv.copy()
        if L.get("cap_sample") and res["faces"]:   # caps over dark interiors: one texel from a chosen surface
            cs = Vector(L["cap_sample"]); caps = set(res["faces"])
            fs = min((f for f in bm.faces if f not in caps), key=lambda f: (f.calc_center_median() - cs).length)
            uv0 = fs.loops[0][uv].uv.copy()
            for f in caps:
                for lp in f.loops: lp[uv].uv = uv0
        bmesh.ops.triangulate(bm, faces=res["faces"]); filled_n = len(res["faces"])
    bm.verts.layers.int.remove(oid)
    bm.to_mesh(body.data); bm.free()
    print(f"[i3d] {a.hero}: tore {len(kill)} stretched faces, capped {filled_n} holes")
    body.parent = None
    bpy.data.objects.remove(A2, do_unlink=True)
    # A1 back to its rest; bind
    for pb in A1.pose.bones: pb.matrix_basis = Matrix.Identity(4)
    body.parent = A1
    md = body.modifiers.new("Armature", "ARMATURE"); md.object = A1
    body.name = f"SB_Hero_{a.hero}"
    bpy.context.view_layer.update()

    # --- export (HD layout) ---
    os.makedirs(a.out, exist_ok=True)
    img = next((i for i in bpy.data.images if i.size[0] > 0 and i.name != "Render Result"), None)
    from PIL import Image
    alb_png = os.path.join(a.out, f"_tmp_{a.hero}.png")
    img.filepath_raw = alb_png; img.file_format = "PNG"; img.save()
    im = Image.open(alb_png).convert("RGB")
    if im.width > 1024: im = im.resize((1024, 1024 * im.height // im.width), Image.LANCZOS)  # HD textures are 1024
    from PIL import ImageDraw, ImageChops
    W_, H_ = im.size
    def mask_of(tris):
        mk = Image.new("L", (W_, H_), 0); dr = ImageDraw.Draw(mk)
        for t in tris:
            if len(t) >= 3: dr.polygon([(u * W_, (1 - v) * H_) for u, v in t], fill=255)
        return mk
    for rc, tris, etris in recol:
        mk = mask_of(tris); em = mask_of(etris) if rc.get("edge_color") else None
        px = im.load(); mp = mk.load(); ep = em.load() if em else None
        dark = rc.get("dark", 0.32); col = rc["color"]; ecol = rc.get("edge_color")
        for y in range(H_):
            for x in range(W_):
                if not mp[x, y]: continue
                r, g, b_ = px[x, y]; v = max(r, g, b_) / 255.0
                if v > dark: continue
                shade = 0.55 + 0.45 * (v / dark)
                c = ecol if (ep and ep[x, y]) else col
                px[x, y] = tuple(int(min(255, ch * shade)) for ch in c)
    glow_extra = None
    if halo_uv:
        cov = Image.new("L", (W_ // 8, H_ // 8), 0); dr = ImageDraw.Draw(cov)
        for poly in body.data.polygons:
            pts_ = [tuple(body.data.uv_layers.active.data[l].uv) for l in poly.loop_indices]
            if all(abs(u) < 1e-6 and abs(v) < 1e-6 for u, v in pts_): continue
            dr.polygon([(u * W_ / 8, (1 - v) * H_ / 8) for u, v in pts_], fill=255)
        cp = cov.load(); free = next(((x, y) for y in range(1, cov.height - 2) for x in range(1, cov.width - 2)
                                      if not any(cp[x + i, y + j] for i in range(-1, 3) for j in range(-1, 3))), (0, 0))
        bx, by = free[0] * 8, free[1] * 8
        ImageDraw.Draw(im).rectangle([bx, by, bx + 15, by + 15], fill=tuple(L["halo"]["color"]))
        uc, vc = (bx + 8) / W_, 1 - (by + 8) / H_
        for poly in body.data.polygons:
            if all(all(abs(x) < 1e-6 for x in body.data.uv_layers.active.data[l].uv) for l in poly.loop_indices):
                for l in poly.loop_indices: body.data.uv_layers.active.data[l].uv = (uc, vc)
        glow_extra = (bx, by)
    im.save(os.path.join(a.out, f"SB_Hero_{a.hero}_HD_albedo.jpg"), quality=88, optimize=True)
    # glow: bright saturated element-coloured pixels (lava, flames, runes), half size
    g = im.resize((im.width // 2, im.height // 2)).convert("HSV")
    hue_lo, hue_hi = L.get("glow_hue", (0, 40))
    gp = Image.new("RGB", g.size); src = g.load(); dst = gp.load()
    n = 0
    for y in range(g.height):
        for x in range(g.width):
            h, s, v = src[x, y]
            hd = h * 360 // 255
            if s > 120 and v > 200 and hue_lo <= hd <= hue_hi:
                dst[x, y] = (255, 255, 255); n += 1
    if glow_extra:
        ImageDraw.Draw(gp).rectangle([glow_extra[0] // 2, glow_extra[1] // 2, glow_extra[0] // 2 + 7, glow_extra[1] // 2 + 7], fill=(255, 255, 255)); n += 64
    if n > 50:
        gp.save(os.path.join(a.out, f"SB_Hero_{a.hero}_HD_glow.png"))
    os.remove(alb_png)
    # placeholder material (the game applies the textures)
    mat = bpy.data.materials.new("M_Hero_HD"); body.data.materials.clear(); body.data.materials.append(mat)
    for o in bpy.context.selected_objects: o.select_set(False)
    A1.select_set(True); body.select_set(True)
    path = os.path.join(a.out, f"SB_Hero_{a.hero}_HD.glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_skins=True, export_animations=False,
                              export_apply=False, export_yup=True, export_materials="PLACEHOLDER")
    if a.blend:
        img2 = bpy.data.images.load(os.path.join(a.out, f"SB_Hero_{a.hero}_HD_albedo.jpg"))
        mat.use_nodes = True; nt = mat.node_tree; t = nt.nodes.new("ShaderNodeTexImage"); t.image = img2
        nt.links.new(t.outputs[0], nt.nodes["Principled BSDF"].inputs["Base Color"])
        bpy.ops.wm.save_as_mainfile(filepath=a.blend)
    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    print(f"[i3d] {a.hero}: {tris} tris, {len(A1.data.bones)} bones, glow px {n}, {os.path.getsize(path) // 1024} KB")


if __name__ == "__main__":
    main()
