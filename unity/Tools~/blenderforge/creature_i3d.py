"""A creature hero from an image-to-3D mesh (TRELLIS via fal.ai, cleaned by i3d_prep.py), on the spider rig of
creature.py: the UBC upper body above the pelvis, spider_body, abdomen / abdomen_02, eight 4-bone legs ending in
_tip markers, and the model's own baked clips (creature_gait.py + the UAL upper-body clips).

    bpyenv/bin/python i3d_prep.py -- vessa.glb vessa DIR            # weld, drop debris, 1.85 m, feet at z 0
    bpyenv/bin/python creature_i3d.py -- --hero vessa --clean DIR/vessa_clean.blend --out OUT [--blend OUT/vessa_rig.blend]
    node compress-hd.mjs OUT && python3 compress-textures.py OUT     # then copy into Tools~/models/heroes_hd

How:
1. The leg layout is MEASURED from the mesh (LAYOUTS: per-leg hip, knee, ankle and foot, read off the cleaned
   mesh) and handed to creature_gait.configure, so the gait's rest pose is the mesh's own stance and its legs
   need no reshaping; the bones come from creature_gait.solve_leg as in creature.py.
2. Upper body as i3d_rig.py: the UBC joints moved to the mesh's landmarks (arms in the T-pose), the arms posed
   into the mesh's pose, the mesh skinned there with bone heat (upper-body bones only) and posed back to the
   T-pose; faces stretched > TEAR x by that move are dropped (generator fusions of arm and body).
3. Everything below the waist is rigid: each leg vertex goes wholly to the nearest segment of the nearest leg
   (no smearing between segments or legs), the cephalothorax to spider_body, the abdomen to abdomen.
4. Clips: creature.bake_clips with an identity-scale map (gait metres = model metres, origin at the legs'
   centre), so the game plays the same GAME_CLIPS as the procedural build.
No rest_fit marker: the clips are baked for this skeleton, so RigExtras must not re-express them.
"""
import bpy, bmesh, sys, os, math, argparse
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import ubc_parts as P  # noqa: E402
import creature_gait as G  # noqa: E402
import creature as C  # noqa: E402

NODEFORM = ("index", "middle", "ring", "pinky", "thumb", "_leaf", "root", "_tip")
LEG_BONES = ("coxa", "femur", "tibia", "tarsus")

# Landmarks on the cleaned mesh (1.85 m, feet at z 0, facing -Y, the character's left = +X); legs are given for the
# left side as (hip, knee, ankle, foot) and mirrored (the generated mesh is symmetric to ~1 cm).
LAYOUTS = {
    # fal.ai TRELLIS from the owner's image (2026-11-27): elf upper body on a spider body with eight legs
    "vessa": {
        "y0": -0.22,          # gait origin (between the hips) in model metres
        "coxa": 0.05,
        "legs": {
            "1": ((0.24, -0.55, 0.84), (0.34, -0.58, 0.99), (0.515, -0.80, 0.62), (0.57, -0.915, 0.0)),
            "2": ((0.27, -0.36, 0.85), (0.40, -0.36, 1.02), (0.645, -0.465, 0.62), (0.625, -0.46, 0.0)),
            "3": ((0.27, -0.10, 0.82), (0.42, -0.10, 0.98), (0.68, -0.09, 0.62), (0.626, -0.11, 0.0)),
            "4": ((0.26, 0.12, 0.84), (0.38, 0.13, 1.00), (0.595, 0.295, 0.62), (0.557, 0.335, 0.0)),
        },
        "joints": {
            "pelvis": (0, -0.20, 1.00), "spine_01": (0, -0.21, 1.10), "spine_02": (0, -0.22, 1.21), "spine_03": (0, -0.22, 1.33),
            "neck": (0, -0.24, 1.50), "head": (0, -0.25, 1.56), "head_top": (0, -0.25, 1.80),
            "shoulder_l": (0.19, -0.18, 1.44), "elbow_l": (0.37, -0.04, 1.40), "wrist_l": (0.33, -0.24, 1.16), "handend_l": (0.35, -0.33, 1.08),
        },
        "body": ((0.0, -0.10, 0.84), (0.0, -0.36, 0.84)),                    # spider_body head, tail
        "abdomen": ((0.0, -0.02, 0.86), (0.0, 0.45, 0.95), (0.0, 0.92, 0.82)),  # abdomen head, joint, abdomen_02 tail
        "ceph": ((0.0, -0.25, 0.78), (0.28, 0.33, 0.26)),                    # ellipsoids: centre, radii
        "abd": ((0.0, 0.47, 0.95), (0.38, 0.52, 0.36)),
        "tear": 2.5,
        "glow_hue": (190, 280),   # the blue leg bands and the abdomen's web markings
    },
}


def mirror(p): return (-p[0], p[1], p[2])


def seg_dist(p, a, b):
    ab = b - a; u = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (p - (a + ab * u)).length


def ell(p, c, r): return ((p.x - c[0]) / r[0]) ** 2 + ((p.y - c[1]) / r[1]) ** 2 + ((p.z - c[2]) / r[2]) ** 2


class Map:
    """creature.bake_clips' gait -> model map: metres unchanged, gait origin at y0."""
    def __init__(self, y0): self.S = 1.0; self.y0 = y0
    def p(self, q): return Vector((q[0], q[1] + self.y0, q[2]))
    def g(self, v): return (v[0], v[1] - self.y0, v[2])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--hero", required=True); ap.add_argument("--clean", required=True); ap.add_argument("--out", required=True)
    ap.add_argument("--blend", default=""); ap.add_argument("--tris", type=int, default=18000)
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:])
    L = LAYOUTS[a.hero]; M = Map(L["y0"])
    bpy.ops.wm.open_mainfile(filepath=a.clean); bpy.context.scene.render.fps = G.FPS
    body = bpy.data.objects["Body"]
    n = sum(len(p.vertices) - 2 for p in body.data.polygons)
    if n > a.tris:
        for o in bpy.context.selected_objects: o.select_set(False)
        body.select_set(True); bpy.context.view_layer.objects.active = body
        d = body.modifiers.new("Dec", "DECIMATE"); d.ratio = a.tris / n; bpy.ops.object.modifier_apply(modifier="Dec")

    # --- the leg layout from the mesh -> creature_gait ---
    legs_m = {}
    for k, pts in L["legs"].items():
        legs_m["L" + k] = [Vector(p) for p in pts]; legs_m["R" + k] = [Vector(mirror(p)) for p in pts]
    attach, rest, segs, leans, chains_m = {}, {}, {}, {}, {}
    for leg, (hip, knee, ankle, foot) in legs_m.items():
        o = Vector((foot.x - hip.x, foot.y - hip.y, 0)).normalized()
        cend = hip + o * L["coxa"]
        t = ankle - foot; lean = math.atan2(-(t.x * o.x + t.y * o.y), t.z)   # + = ankle inward of the foot
        attach[leg] = M.g(hip); rest[leg] = M.g(Vector((foot.x, foot.y, 0.0)))
        segs[leg] = (L["coxa"], (knee - cend).length, (ankle - knee).length, t.length); leans[leg] = lean
        chains_m[leg] = [hip, cend, knee, ankle, foot]
    G.configure(attach, rest, segs, leans, sum(v[2] for v in attach.values()) / len(attach))
    chains_r = {leg: [M.p(q) for q in G.solve_leg(leg, G.ATTACH[leg], G.REST[leg])] for leg in G.LEGS}
    res = max((chains_r[l][i] - chains_m[l][i]).length for l in G.LEGS for i in range(5))
    print(f"[i3d-creature] gait rest vs mesh joints: max {res:.3f} m")

    # --- UBC armature, joints on the landmarks ---
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=P.FILES["Regular_Male_FullBody"])
    new = [o for o in bpy.data.objects if o not in before]
    A1 = next(o for o in new if o.type == "ARMATURE")
    for o in new:
        if o.type == "MESH": bpy.data.objects.remove(o, do_unlink=True)
    A1.name = "Armature"; A1.data.name = "Armature"
    J = {k: Vector(v) for k, v in L["joints"].items()}
    for s in ("shoulder", "elbow", "wrist", "handend"): J[s + "_r"] = Vector(mirror(J[s + "_l"]))
    seg = lambda x, y: (J[y] - J[x]).length
    restp = {}
    for s, sg in (("l", 1), ("r", -1)):
        sh = J["shoulder_" + s]; e = sh + Vector((sg * seg("shoulder_" + s, "elbow_" + s), 0, 0))
        w = e + Vector((sg * seg("elbow_" + s, "wrist_" + s), 0, 0)); h = w + Vector((sg * max(0.06, seg("wrist_" + s, "handend_" + s)), 0, 0))
        restp.update({"upperarm_" + s: sh, "lowerarm_" + s: e, "hand_" + s: w, "handend_" + s: h})
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = A1; A1.select_set(True); bpy.ops.object.mode_set(mode="EDIT")
    eb = A1.data.edit_bones
    orig = {b.name: (b.head.copy(), b.tail.copy(), b.roll) for b in eb}
    def place(name, head, tail):
        b = eb[name]; roll = b.roll; b.head = head; b.tail = tail; b.roll = roll
    chain = ["pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "Head"]
    pts = [J["pelvis"], J["spine_01"], J["spine_02"], J["spine_03"], J["neck"], J["head"], J["head_top"]]
    for i, nm in enumerate(chain): place(nm, pts[i], pts[i + 1])
    for s, sg in (("l", 1), ("r", -1)):
        place("clavicle_" + s, J["spine_03"].lerp(J["neck"], 0.8) + Vector((sg * 0.03, 0, 0)), restp["upperarm_" + s])
        place("upperarm_" + s, restp["upperarm_" + s], restp["lowerarm_" + s])
        place("lowerarm_" + s, restp["lowerarm_" + s], restp["hand_" + s])
        place("hand_" + s, restp["hand_" + s], restp["handend_" + s])
        oh = orig["hand_" + s][0]; olen = (orig["hand_" + s][1] - oh).length
        k = max(0.6, min(2.5, (restp["handend_" + s] - restp["hand_" + s]).length / max(1e-4, olen) * 0.5 + 0.5))
        for b in eb:
            if b.name.endswith("_" + s) and any(b.name.startswith(f) for f in ("index", "middle", "ring", "pinky", "thumb")):
                h0, t0, r0 = orig[b.name]; b.head = restp["hand_" + s] + (h0 - oh) * k; b.tail = restp["hand_" + s] + (t0 - oh) * k; b.roll = r0
    # spider bones (as creature.spider_rig), the humanoid legs removed
    for nm in [b.name for b in eb if any(b.name.startswith(x) for x in ("thigh", "calf", "foot", "ball"))]: eb.remove(eb[nm])
    def add(nm, h, t, par, hint):
        b = eb.new(nm); b.head = Vector(h); b.tail = Vector(t); b.align_roll(Vector(hint)); b.parent = eb[par]; b.use_connect = False
    add("spider_body", L["body"][0], L["body"][1], "root", (0, 0, 1))
    ab = L["abdomen"]; add("abdomen", ab[0], ab[1], "spider_body", (0, 0, 1)); add("abdomen_02", ab[1], ab[2], "abdomen", (0, 0, 1))
    for leg in G.LEGS:
        p_ = chains_r[leg]; o = p_[4] - p_[0]; o.z = 0; o.normalize(); nrm = Vector((-o.y, o.x, 0)); par = "spider_body"
        for i, bn in enumerate(LEG_BONES):
            add(f"leg_{leg}_{bn}", p_[i], p_[i + 1], par, nrm); par = f"leg_{leg}_{bn}"
        add(f"leg_{leg}_tip", p_[4], p_[4] + Vector((0, 0, -0.04)), par, nrm)
    eb["pelvis"].use_connect = False; eb["pelvis"].parent = eb["spider_body"]
    bpy.ops.object.mode_set(mode="OBJECT")
    for b in A1.data.bones: b.use_deform = not any(k in b.name for k in NODEFORM)

    # --- A1 posed into the mesh's arm pose; A2 takes it as rest; heat-skin the upper body there ---
    bpy.ops.object.mode_set(mode="POSE")
    for s in ("l", "r"):
        for bone, tgt in (("upperarm_" + s, "elbow_" + s), ("lowerarm_" + s, "wrist_" + s), ("hand_" + s, "handend_" + s)):
            pb = A1.pose.bones[bone]; bpy.context.view_layer.update()
            head = A1.matrix_world @ pb.head; v = A1.matrix_world @ pb.tail - head; t = J[tgt] - head
            q = v.rotation_difference(t)
            pb.matrix = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ pb.matrix
    bpy.context.view_layer.update(); bpy.ops.object.mode_set(mode="OBJECT")
    A2 = A1.copy(); A2.data = A1.data.copy(); A2.name = "A2"; bpy.context.scene.collection.objects.link(A2)
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = A2; A2.select_set(True)
    bpy.ops.object.mode_set(mode="POSE"); bpy.ops.pose.select_all(action="SELECT"); bpy.ops.pose.armature_apply(selected=False)
    bpy.ops.object.mode_set(mode="OBJECT")
    lower = lambda nm: nm.startswith("leg_") or nm in ("spider_body", "abdomen", "abdomen_02")
    for b in A2.data.bones:
        if lower(b.name): b.use_deform = False
    for o in bpy.context.selected_objects: o.select_set(False)
    body.select_set(True); A2.select_set(True); bpy.context.view_layer.objects.active = A2
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    for b in A2.data.bones: b.use_deform = A1.data.bones[b.name].use_deform
    defb = [b.name for b in A2.data.bones if b.use_deform]
    vg = {nm: (body.vertex_groups.get(nm) or body.vertex_groups.new(name=nm)) for nm in defb}
    bseg = {nm: (A2.data.bones[nm].head_local, A2.data.bones[nm].tail_local) for nm in defb if not lower(nm)}

    # --- rigid lower body: classify every vertex in the mesh's pose ---
    arm_segs = [(J[x + s], J[y + s]) for s in ("_l", "_r") for x, y in (("shoulder", "elbow"), ("elbow", "wrist"), ("wrist", "handend"))]
    leg_segs = [(leg, LEG_BONES[i], chains_m[leg][i], chains_m[leg][i + 1]) for leg in G.LEGS for i in range(4)]
    (cc, cr), (ac, ar) = L["ceph"], L["abd"]
    count = {"upper": 0, "body": 0, "abdomen": 0, "legs": 0, "filled": 0}
    for v in body.data.vertices:
        p = body.matrix_world @ v.co
        dl, leg, part = min(((seg_dist(p, a_, b_), lg, pt_) for lg, pt_, a_, b_ in leg_segs), key=lambda x: x[0])
        da = min(seg_dist(p, a_, b_) for a_, b_ in arm_segs)
        in_abd = ell(p, ac, ar) < 1.08 and p.y > -0.02
        if p.z > 0.97 and not in_abd and ((abs(p.x) < 0.27 and p.y < 0.05) or da < min(dl, 0.12) or p.z > 1.3):
            tgt = None; count["upper"] += 1
            if sum(g.weight for g in v.groups) < 1e-3:   # heat missed it: nearest upper-body bone
                tgt = min(bseg, key=lambda nm: seg_dist(p, *bseg[nm])); count["filled"] += 1
        elif in_abd: tgt = "abdomen"; count["abdomen"] += 1
        elif ell(p, cc, cr) < 1.1 and dl > 0.03: tgt = "spider_body"; count["body"] += 1
        elif dl < 0.30: tgt = f"leg_{leg}_{part}"; count["legs"] += 1
        else: tgt = "spider_body" if ell(p, cc, cr) < ell(p, ac, ar) else "abdomen"; count["body"] += 1
        if tgt is not None:
            for gi in [g.group for g in v.groups]: body.vertex_groups[gi].remove([v.index])
            vg[tgt].add([v.index], 1.0, "REPLACE")
    print(f"[i3d-creature] {a.hero}: vertices {count}")
    # head lock (as i3d_rig.py): bone heat bled neck_01 and the clavicles into the face, which warped when the neck
    # turned. Inside the head's radius, above the jaw = Head alone; from the neck joint up to the jaw a blend.
    hn_, hh_, ht_ = J["neck"], J["head"], J["head_top"]
    H_ = max(0.08, ht_.z - hn_.z); jaw = hn_.z + 0.45 * (hh_.z - hn_.z); R_ = 0.62 * H_; nhl = 0
    hg = vg.get("Head")
    for v in body.data.vertices:
        p = body.matrix_world @ v.co
        if hg is None or p.z < hn_.z or math.hypot(p.x - hh_.x, p.y - hh_.y) > (R_ if p.z < hh_.z else 1.3 * H_): continue
        ws = {body.vertex_groups[g.group].name: g.weight for g in v.groups}
        if not ws or max(ws, key=ws.get).startswith(("clavicle", "upperarm", "lowerarm", "hand", "leg_")): continue
        t = 1.0 if p.z >= jaw else max(0.0, min(1.0, (p.z - hn_.z) / max(1e-6, jaw - hn_.z)))
        t = t * t * (3 - 2 * t); tot = sum(ws.values()) or 1.0
        for nm, w in ws.items(): body.vertex_groups[nm].add([v.index], w / tot * (1 - t), "REPLACE")
        hg.add([v.index], ws.get("Head", 0.0) / tot * (1 - t) + t, "REPLACE"); nhl += 1
    print(f"[i3d-creature] {a.hero}: head-locked {nhl}")

    # --- A2 back to A1's rest (arms to the T-pose), apply, tear stretched fusions ---
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = A2; A2.select_set(True); bpy.ops.object.mode_set(mode="POSE")
    for pb in sorted(A2.pose.bones, key=lambda p: len(p.bone.parent_recursive)):
        pb.matrix = A1.data.bones[pb.name].matrix_local.copy(); bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = body; body.select_set(True)
    md = next(m for m in body.modifiers if m.type == "ARMATURE")
    len0 = [(body.data.vertices[e.vertices[0]].co - body.data.vertices[e.vertices[1]].co).length for e in body.data.edges]
    bpy.ops.object.modifier_apply(modifier=md.name)
    bm = bmesh.new(); bm.from_mesh(body.data); bm.edges.ensure_lookup_table()
    torn = {e.index for e in bm.edges if e.calc_length() > max(0.08, len0[e.index] * L["tear"])}
    kill = [f for f in bm.faces if any(e.index in torn for e in f.edges)]
    bmesh.ops.delete(bm, geom=kill, context="FACES"); bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(body.data); bm.free()
    print(f"[i3d-creature] {a.hero}: tore {len(kill)} stretched faces")
    body.parent = None; bpy.data.objects.remove(A2, do_unlink=True)
    for pb in A1.pose.bones: pb.matrix_basis = Matrix.Identity(4)
    body.parent = A1; md = body.modifiers.new("Armature", "ARMATURE"); md.object = A1
    body.name = f"SB_Hero_{a.hero}"

    # --- textures (albedo from the generated texture, glow from its bright blue/violet texels) ---
    os.makedirs(a.out, exist_ok=True)
    img = next((i for i in bpy.data.images if i.size[0] > 0 and i.name != "Render Result"), None)
    from PIL import Image
    tmp = os.path.join(a.out, f"_tmp_{a.hero}.png"); img.filepath_raw = tmp; img.file_format = "PNG"; img.save()
    im = Image.open(tmp).convert("RGB"); os.remove(tmp)
    if im.width > 1024: im = im.resize((1024, 1024 * im.height // im.width), Image.LANCZOS)  # HD textures are 1024
    alb = os.path.join(a.out, f"SB_Hero_{a.hero}_HD_albedo.jpg"); im.save(alb, quality=88, optimize=True)
    g = im.resize((im.width // 2, im.height // 2)).convert("HSV"); lo, hi = L["glow_hue"]
    gp = Image.new("RGB", g.size); src_, dst = g.load(), gp.load(); ng = 0
    for y in range(g.height):
        for x in range(g.width):
            h, s_, v_ = src_[x, y]
            if s_ > 120 and v_ > 170 and lo <= h * 360 // 255 <= hi: dst[x, y] = (255, 255, 255); ng += 1
    if ng > 50: gp.save(os.path.join(a.out, f"SB_Hero_{a.hero}_HD_glow.png"))
    mat = bpy.data.materials.new("M_Hero_HD"); body.data.materials.clear(); body.data.materials.append(mat)

    # --- the creature's own clips, baked for this skeleton ---
    src = C.Source(C.upper_bones(A1))
    clips = {nm: G.clip(nm) for nm in C.GAME_CLIPS}
    acts = C.bake_clips(A1, M, src, clips)
    for o in src.objs: bpy.data.objects.remove(o, do_unlink=True)
    path = os.path.join(a.out, f"SB_Hero_{a.hero}_HD.glb")
    kb = C.export(A1, body, path)
    if a.blend:
        img2 = bpy.data.images.load(alb); mat.use_nodes = True; nt = mat.node_tree; t = nt.nodes.new("ShaderNodeTexImage"); t.image = img2
        nt.links.new(t.outputs[0], nt.nodes["Principled BSDF"].inputs["Base Color"])
        bpy.ops.wm.save_as_mainfile(filepath=a.blend)
    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    print(f"[i3d-creature] {a.hero}: {tris} tris, {len(A1.data.bones)} bones, glow px {ng}, clips {[(k, v[1]) for k, v in acts.items()]}, {kb} KB")


if __name__ == "__main__":
    main()
