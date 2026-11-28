"""Digitigrade legs for image-to-3D heroes (owner report 2026-11-28: animal-folk knees bent like a human's).

Opt-in per hero in the landmark table (i3d_landmarks*.py):
    "legs": "digitigrade",
    joints "knee_l", "hock_l", "ball_l", "toe_l" (and _r) as (x, y, z): the true knee (bends forward), the hock
    (the reversed "ankle-knee" that points back), the ball of the foot (where the raised metatarsal meets the
    toes or hoof, on the ground) and the toe tip. "ankle_l"/"ankle_r" are not used for these legs.

Skeleton: the leg becomes thigh -> calf -> meta -> foot -> ball (femur, tibia, the long raised metatarsal, the
toes in two parts); meta_l / meta_r are new bones, foot_l / foot_r are re-parented under them.

Clips: the shared UBC battle clips are humanoid (plantigrade). For each clip this module plays it on a virtual
plantigrade leg of the hero's own proportions (the leg a normal i3d hero would have, so its hip motion and foot
plants are the clip's), then solves the digitigrade leg to it: the foot (ball and toes) stays where the
humanoid foot is (planted when it is planted), the hock rides the humanoid foot and folds down as the humanoid
ankle bends (crouch, kneel), lifts when the leg would over-reach, and the thigh and calf are a two-bone IK from
the clip's hip to the hock with the knee on the humanoid knee's side. The result is baked to FK keys (rotation
and translation of the ten leg bones) and written into the hero's GLB as one animation per shared clip, with
the same name and length. The game layers those over the shared clips on the legs only (Client/
DigitigradeLegs.cs), so clip names, aliases and battle code are unchanged and no IK runs on the phone.

Everything here is self-written (MIT-compatible project code); the approach (a reverse-knee chain with an
extra metatarsal segment driven by IK, baked to FK) is the standard one, as in Blender's Rigify "paw" legs,
whose GPL code is not used.
"""
import bpy, json, math, os, struct
from mathutils import Vector, Matrix, Quaternion

SIDES = (("l", 1), ("r", -1))
LEG = ("thigh", "calf", "meta", "foot", "ball")
PARENT = {"thigh": "pelvis", "calf": "thigh", "meta": "calf", "foot": "meta", "ball": "foot"}
HOCK_FLEX = 0.85      # metatarsal fold per radian of humanoid ankle bend (crouch, kneel)


def enabled(L):
    return L.get("legs") == "digitigrade"


def canon(J, L, orig):
    """Rest joints per side. The virtual humanoid leg the clips are played on is the UBC reference leg (whose
    proportions the clips' hip heights and foot plants were made for) scaled to the hero's hip height. The
    digitigrade leg is the mesh's own (hip, knee, hock, ball, toe), swung about the hip in the side view so its
    ball sits over the humanoid ball (a stepping leg in the mesh doesn't become the stance), toes laid flat."""
    out = {}
    for s, sg in SIDES:
        hip = J["hip_" + s]
        u0 = orig["thigh_" + s][0]; k = hip.z / u0.z
        def U(p): return hip + (p - u0) * k
        knee_h, ankle_h, ball_h, toe_h = U(orig["calf_" + s][0]), U(orig["foot_" + s][0]), U(orig["ball_" + s][0]), U(orig["ball_" + s][1])
        knee, hock, ball, toe = (J[n + "_" + s].copy() for n in ("knee", "hock", "ball", "toe"))
        v = ball - hip; r = math.hypot(v.y, v.z); lf = hip.y - ball_h.y
        cur = math.atan2(v.z, v.y); tgt = math.atan2(-math.sqrt(max(1e-6, r * r - lf * lf)), -lf)
        R = Matrix.Rotation(tgt - cur, 3, "X")
        knee, hock, ball, toe = (hip + R @ (p - hip) for p in (knee, hock, ball, toe))
        d = toe - ball; flat = Vector((d.x, d.y, 0)).normalized() * d.length
        toe = ball + flat; toe.z = max(0.015, min(toe.z, ball.z))
        out[s] = {"hip": hip, "knee": knee, "hock": hock, "ball": ball, "toe": toe, "mid": ball.lerp(toe, 0.5),
                  "knee_h": knee_h, "ankle_h": ankle_h, "ball_h": ball_h, "toe_h": toe_h}
    return out


def rest(eb, C):
    """Edit mode on A1: lay the digitigrade leg bones on the rest joints C (canon) and add meta_l / meta_r."""
    for s, sg in SIDES:
        c = C[s]
        def put(name, h, t, up):
            b = eb[name]; b.head = h; b.tail = t; b.align_roll(up)
        fwd = Vector((0, -1, 0))
        put("thigh_" + s, c["hip"], c["knee"], fwd)
        put("calf_" + s, c["knee"], c["hock"], fwd)
        m = eb.get("meta_" + s) or eb.new("meta_" + s)
        m.head = c["hock"]; m.tail = c["ball"]; m.parent = eb["calf_" + s]; m.use_connect = True; m.align_roll(fwd)
        f = eb["foot_" + s]; f.use_connect = False; f.parent = m; f.use_connect = True
        put("foot_" + s, c["ball"], c["mid"], Vector((0, 0, 1)))
        put("ball_" + s, c["mid"], c["toe"], Vector((0, 0, 1)))
        lf = eb["ball_leaf_" + s]; lf.head = c["toe"]; lf.tail = c["toe"] + (c["toe"] - c["mid"]) * 0.8


def aims(s):
    """The pose step's aims for a digitigrade leg (bone, mesh landmark): the mesh keeps its own leg pose."""
    return [("thigh_" + s, "knee_" + s), ("calf_" + s, "hock_" + s), ("meta_" + s, "ball_" + s), ("foot_" + s, "toe_" + s)]


# ---------------------------------------------------------------- baking

def _virtual(A1, C, orig):
    """A copy of A1 whose legs are the plantigrade humanoid legs a normal i3d hero would have (UBC bone rolls)."""
    H = A1.copy(); H.data = A1.data.copy(); H.name = "Hleg"; bpy.context.scene.collection.objects.link(H)
    for o in bpy.context.selected_objects: o.select_set(False)
    bpy.context.view_layer.objects.active = H; H.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    eb = H.data.edit_bones
    for s, sg in SIDES:
        c = C[s]
        for name, h, t in (("thigh_" + s, c["hip"], c["knee_h"]), ("calf_" + s, c["knee_h"], c["ankle_h"]),
                           ("foot_" + s, c["ankle_h"], c["ball_h"]), ("ball_" + s, c["ball_h"], c["toe_h"])):
            b = eb[name]; b.head = h; b.tail = t; b.roll = orig[name][2]
        f = eb["foot_" + s]; f.use_connect = False; f.parent = eb["calf_" + s]
        eb.remove(eb["meta_" + s])
    bpy.ops.object.mode_set(mode="OBJECT")
    return H


def _glb(path):
    b = open(path, "rb").read()
    n = struct.unpack("<I", b[12:16])[0]
    j = json.loads(b[20:20 + n])
    rest_ = b[20 + n:]
    bn = struct.unpack("<I", rest_[0:4])[0]
    return j, bytearray(rest_[8:8 + bn])


def _write_glb(path, j, binb):
    while len(binb) % 4: binb += b"\0"
    js = json.dumps(j, separators=(",", ":")).encode()
    while len(js) % 4: js += b" "
    out = struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(binb))
    out += struct.pack("<II", len(js), 0x4E4F534A) + js + struct.pack("<II", len(binb), 0x004E4942) + bytes(binb)
    open(path, "wb").write(out)


def _swing(R, d):
    """R with its Y axis swung onto direction d (shortest rotation)."""
    y = R @ Vector((0, 1, 0))
    return (y.rotation_difference(d).to_matrix() @ R)


def _rot_about(p, c, axis, ang):
    return c + Matrix.Rotation(ang, 3, axis) @ (p - c)


def _solve(Hpose, C, s, rest3):
    """World (armature-space) head and rotation of the five digitigrade bones for one side, from the posed
    virtual humanoid leg Hpose = {bone: Matrix 4x4}."""
    c = C[s]
    P = Hpose["thigh_" + s].translation; Kh = Hpose["calf_" + s].translation; Ah = Hpose["foot_" + s].translation
    Bh = Hpose["ball_" + s].translation
    Rf = Hpose["foot_" + s].to_3x3(); Rb = Hpose["ball_" + s].to_3x3(); Rc = Hpose["calf_" + s].to_3x3()
    Rf0, Rb0, Rc0 = rest3["foot_" + s], rest3["ball_" + s], rest3["calf_" + s]
    a = (c["knee"] - c["hip"]).length; b = (c["hock"] - c["knee"]).length
    # the digitigrade foot rides the humanoid foot; toes ride the humanoid toe bone
    Df = Rf @ Rf0.inverted(); Db = Rb @ Rb0.inverted()
    Bd = Ah + Df @ (c["ball"] - c["ankle_h"])
    Hk = Ah + Df @ (c["hock"] - c["ankle_h"])
    Td = Bd + Db @ (c["toe"] - c["ball"])
    # hock fold: the humanoid ankle's bend (shin over foot) folds the metatarsal down toward the ground
    def ang(Rc_, Rf_): return (Rc_ @ Vector((0, 1, 0))).angle(Rf_ @ Vector((0, 1, 0)))
    dA = ang(Rc0, Rf0) - ang(Rc, Rf)            # > 0: dorsiflexed (crouch), < 0: pointed
    axis = (Kh - Ah).cross(Bh - Ah)
    if axis.length < 1e-6: axis = Rf @ Vector((1, 0, 0))
    axis.normalize()
    if abs(dA) > 1e-4:
        cand = _rot_about(Hk, Bd, axis, HOCK_FLEX * dA)
        if (cand.z - Hk.z) * dA > 0: cand = _rot_about(Hk, Bd, axis, -HOCK_FLEX * dA)
        if cand.z > Bd.z + 0.22 * (Hk - Bd).length: Hk = cand
    # reach: a leg the clip stretches further than femur + tibia lifts the hock toward the hip (heel rise)
    reach = (a + b) * 0.995
    if (Hk - P).length > reach:
        sgn = 1 if (_rot_about(Hk, Bd, axis, 0.05) - P).length < (Hk - P).length else -1
        lo, hi = 0.0, math.radians(85)
        for _ in range(24):
            mid = (lo + hi) / 2
            if (_rot_about(Hk, Bd, axis, sgn * mid) - P).length > reach: lo = mid
            else: hi = mid
        Hk = _rot_about(Hk, Bd, axis, sgn * hi)
    d = Hk - P; dl = d.length
    dl_c = max(abs(a - b) + 1e-3, min(a + b - 1e-4, dl))
    dirv = d / max(dl, 1e-6)
    cosA = max(-1.0, min(1.0, (a * a + dl_c * dl_c - b * b) / (2 * a * dl_c)))
    n = dirv.cross(Kh - P)
    if n.length < 1e-6: n = Rc @ Vector((1, 0, 0))
    n.normalize(); u = n.cross(dirv).normalized()
    knee = P + dirv * a * cosA + u * a * math.sqrt(max(0.0, 1 - cosA * cosA))
    hock = knee + (Hk - knee).normalized() * b
    # bones keep their lengths: when the hock fell short of its target the foot follows the metatarsal
    cm = (c["ball"] - c["hock"]).length
    Bn = hock + (Bd - hock).normalized() * cm; slide = (Bn - Bd).length; Td = Td + (Bn - Bd); Bd = Bn
    return slide, {"thigh": (P, knee), "calf": (knee, hock), "meta": (hock, Bd), "foot": (Bd, Bd.lerp(Td, 0.5)), "ball": (Bd.lerp(Td, 0.5), Td)}


def bake(A1, C, orig, glb_path, anims_path, clip_filter=None):
    """Bake every shared clip onto the digitigrade legs and add the animations to the exported hero GLB."""
    sc = bpy.context.scene
    H = _virtual(A1, C, orig)
    # shared clips (actions keyed by bone name, applied rest-relative as the game's RigExtras does)
    before_o, before_a = set(bpy.data.objects), set(bpy.data.actions)
    bpy.ops.import_scene.gltf(filepath=anims_path)
    for o in [o for o in bpy.data.objects if o not in before_o]: bpy.data.objects.remove(o, do_unlink=True)
    acts = {a.name.split("|")[-1]: a for a in bpy.data.actions if a not in before_a}
    aj, _ = _glb(anims_path)
    lengths = {}
    for an in aj.get("animations", []):
        mx = 0.0
        for ch in an["channels"]:
            acc = aj["accessors"][an["samplers"][ch["sampler"]]["input"]]
            mx = max(mx, acc["max"][0])
        lengths[an["name"]] = mx
    fps = sc.render.fps / sc.render.fps_base
    names = [n for n in lengths if "TPose" not in n and (clip_filter is None or n in clip_filter)]
    bones = [f"{k}_{s}" for s, _ in SIDES for k in LEG]
    rest_arm = {b.name: b.matrix_local.copy() for b in A1.data.bones}
    rest3 = {b.name: b.matrix_local.to_3x3() for b in H.data.bones}
    def local(Mp, M): return Mp.inverted() @ M
    baked = {}
    for name in names:
        act = acts.get(name)
        if act is None: continue
        for ob in (A1, H):
            ob.animation_data_create(); ob.animation_data.action = act
            if hasattr(act, "slots") and act.slots: ob.animation_data.action_slot = act.slots[0]
        f0 = act.frame_range[0]; T = lengths[name]
        n = max(2, int(round(T * 30)) + 1)
        times = [T * i / (n - 1) for i in range(n)]
        tracks = {b: ([], []) for b in bones}
        worst = 0.0
        for t in times:
            fr = f0 + t * fps; sc.frame_set(int(math.floor(fr)), subframe=fr - math.floor(fr))
            Hpose = {pb.name: pb.matrix.copy() for pb in H.pose.bones if pb.name.startswith(("thigh_", "calf_", "foot_", "ball_"))}
            Mpel = A1.pose.bones["pelvis"].matrix.copy()
            for s, _ in SIDES:
                slide, seg = _solve(Hpose, C, s, rest3); worst = max(worst, slide)
                Mpar = Mpel; Rpar_rest = rest_arm["pelvis"].to_3x3()
                for k in LEG:
                    bn = f"{k}_{s}"; h, tl = seg[k]
                    Rinh = Mpar.to_3x3() @ Rpar_rest.inverted() @ rest_arm[bn].to_3x3()
                    R = _swing(Rinh, (tl - h).normalized())
                    M = Matrix.Translation(h) @ R.to_4x4()
                    Lm = local(Mpar, M)
                    loc, q, _sc = Lm.decompose()
                    tracks[bn][0].append(loc); tracks[bn][1].append(q)
                    Mpar = M; Rpar_rest = rest_arm[bn].to_3x3()
        baked[name] = (times, tracks)
        print(f"[legs] baked {name}: {n} keys, foot off the humanoid plant by up to {worst * 100:.1f} cm")
    for ob in (A1, H): ob.animation_data_clear()
    bpy.data.objects.remove(H, do_unlink=True)
    for a in list(acts.values()): bpy.data.actions.remove(a)
    _append(glb_path, baked, rest_arm)
    return len(baked)


def _append(glb_path, baked, rest_arm):
    """Write the baked leg keys into the GLB as animations (rotation + translation per leg bone)."""
    j, binb = _glb(glb_path)
    idx = {nd.get("name"): i for i, nd in enumerate(j["nodes"])}
    # check the convention: a bone node's local transform is the Blender armature-space parent^-1 @ bone
    for s, _ in SIDES:
        for k in LEG:
            bn = f"{k}_{s}"; nd = j["nodes"][idx[bn]]
            Lm = rest_arm[PARENT[k] if k == "thigh" else f"{PARENT[k]}_{s}"].inverted() @ rest_arm[bn]
            loc, q, _ = Lm.decompose(); gq = nd.get("rotation", [0, 0, 0, 1]); gt = nd.get("translation", [0, 0, 0])
            dq = abs(abs(q.w * gq[3] + q.x * gq[0] + q.y * gq[1] + q.z * gq[2]) - 1)
            assert dq < 1e-4 and (loc - Vector(gt)).length < 1e-4, f"bone convention mismatch on {bn}: {tuple(loc)} {tuple(q)} vs {gt} {gq}"
    j.setdefault("animations", [])
    def add(data, comp, typ, count, mn=None, mx=None):
        while len(binb) % 4: binb.append(0)
        off = len(binb); binb.extend(data)
        j["bufferViews"].append({"buffer": 0, "byteOffset": off, "byteLength": len(data)})
        acc = {"bufferView": len(j["bufferViews"]) - 1, "componentType": comp, "count": count, "type": typ}
        if mn is not None: acc["min"] = mn; acc["max"] = mx
        j["accessors"].append(acc); return len(j["accessors"]) - 1
    for name, (times, tracks) in baked.items():
        tin = add(struct.pack(f"<{len(times)}f", *times), 5126, "SCALAR", len(times), [times[0]], [times[-1]])
        # bone lengths never change, so translations are constant: two keys (they must still be keyed, so the
        # layered leg clip owns the leg bones' positions as well as their rotations)
        t2 = add(struct.pack("<2f", times[0], times[-1]), 5126, "SCALAR", 2, [times[0]], [times[-1]])
        an = {"name": name, "channels": [], "samplers": []}
        for bn, (locs, qs) in tracks.items():
            # keep quaternion signs continuous (no flips between keys)
            fq = []
            for q in qs:
                if fq and fq[-1].dot(q) < 0: q = -q
                fq.append(q)
            r = add(b"".join(struct.pack("<4f", q.x, q.y, q.z, q.w) for q in fq), 5126, "VEC4", len(fq))
            spread = max((v - locs[0]).length for v in locs)
            assert spread < 1e-3, f"{name} {bn}: leg translation moved {spread:.4f} m"
            tl = add(struct.pack("<3f", *locs[0]) * 2, 5126, "VEC3", 2)
            for path, inp, out in (("rotation", tin, r), ("translation", t2, tl)):
                an["samplers"].append({"input": inp, "output": out, "interpolation": "LINEAR"})
                an["channels"].append({"sampler": len(an["samplers"]) - 1, "target": {"node": idx[bn], "path": path}})
        j["animations"].append(an)
    j["buffers"][0]["byteLength"] = len(binb) + (-len(binb)) % 4
    _write_glb(glb_path, j, binb)
