# Hero recipes for blenderforge: each returns the joints, the meshes, which meshes are rigid (bone
# name) and render settings. Colours come from public/game/art.js LOOKS and the hero sheets.
import math
from mathutils import Vector, Euler, Matrix
import forge as F

def lathe_between(name, a, b, prof, seg=20, sy=1.0):
    """Lathe along the segment a->b; prof = [(radius, t 0..1)]."""
    a, b = Vector(a), Vector(b); L = (b - a).length
    o = F.lathe(name, [(r, t * L) for r, t in prof], seg=seg, sy=sy, cap_top=False, cap_bottom=False)
    q = Vector((0, 0, 1)).rotation_difference((b - a).normalized())
    o.rotation_mode = "QUATERNION"; o.rotation_quaternion = q; o.location = a
    F.select_only([o]); import bpy; bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return o

def split_body(obj, rules, mats):
    """Give each face a material by the first rule its centre matches: rules = [(mat_index, fn(centre))]."""
    obj.data.materials.clear()
    for m in mats: obj.data.materials.append(m)
    for p in obj.data.polygons:
        c = p.center
        for idx, fn in rules:
            if fn(c): p.material_index = idx; break

def finish(o, mat, sub=1, bev=0.0, smooth=True):
    if bev: F.bevel(o, bev)
    if sub: F.subsurf(o, sub)
    F.apply_mods(o)
    if smooth: F.smooth(o, 50)
    return F.assign(o, mat)

def scalp_cap(c, sx, sy, sz, cut_front, cut_back, mat, name="HairCap"):
    o = F.ellipsoid(name, c, (sx, sy, sz), 32, 20)
    import bmesh, bpy
    bm = bmesh.new(); bm.from_mesh(o.data)
    kill = [v for v in bm.verts if (v.co.z < (cut_front if v.co.y < -0.3 * sy else cut_back) * sz)]
    bmesh.ops.delete(bm, geom=kill, context="VERTS"); bm.to_mesh(o.data); bm.free()
    m = o.modifiers.new("Solid", "SOLIDIFY"); m.thickness = 0.012
    return finish(o, mat, sub=1)

# ------------------------------------------------------------------------------------- Ysolde
def ysolde():
    J = F.skeleton(h=1.0, sh=0.92, hip=0.95, arm_down=42)
    skin = F.painted("Skin", "#f0c4a4", shade="#c98a70", light="#ffe2cc", strokes=0.06, edge=0.08, ao=0.4)
    suit = F.painted("Suit", "#3e1414", shade="#200808", light="#6a2420", strokes=0.2, edge=0.12)
    leather = F.painted("Leather", "#6a1c1c", shade="#3a0c0c", light="#a8342a", strokes=0.22, edge=0.35, rough=0.5)
    trim = F.painted("Trim", "#ff7a3a", shade="#b0401a", light="#ffc080", strokes=0.1, edge=0.4, metal=0.2, rough=0.4)
    gold = F.painted("Gold", "#e0a640", shade="#8a5a18", light="#fff0a0", metal=0.9, rough=0.3, edge=0.5, strokes=0.08)
    hair = F.painted("Hair", "#c8281e", shade="#6a0e0e", light="#ff6a3a", strokes=0.35, edge=0.3, rough=0.45)
    belt = F.painted("Belt", "#3a1a10", shade="#1a0a06", light="#6a3a20", strokes=0.2, edge=0.3)
    ember = F.painted("Ember", "#ffb347", glow="#ffb347", glow_strength=6, strokes=0.0, edge=0)
    wrap = F.painted("Wrap", "#d8c0a0", shade="#8a7058", light="#fff0dc", strokes=0.3, edge=0.2)

    r = {"Hips": (0.13, 0.095), "Spine": (0.1, 0.074), "Spine1": (0.112, 0.08), "Spine2": (0.124, 0.084), "Neck": (0.046, 0.046), "Head": (0.044, 0.044),
         "Shoulder": (0.056, 0.056), "Arm": (0.046, 0.046), "ForeArm": (0.037, 0.037), "Hand": (0.03, 0.02), "HandEnd": (0.022, 0.014),
         "UpLeg": (0.082, 0.082), "Leg": (0.052, 0.052), "Foot": (0.04, 0.042), "ToeBase": (0.034, 0.03)}
    b = F.body(J, r)
    hand = lambda c: min((c - J["LeftHand"]).length, (c - J["RightHand"]).length) < 0.075 or min((c - J["LeftHandEnd"]).length, (c - J["RightHandEnd"]).length) < 0.06
    split_body(b, [(0, lambda c: c.z > 1.455), (0, hand), (1, lambda c: True)], [skin, suit])
    meshes = [b]; rigid = {}
    for s_ in ("Left", "Right"):
        for p in F.hand(J, s_, skin): meshes.append(p); rigid[p] = s_ + "Hand"

    head_parts, hc = F.head(J, 1.0, skin, eye_glow="#ffa030", iris="#ffa030", brow_angle=16, brow_col="#7a1a10")
    for p in head_parts: meshes.append(p); rigid[p] = "Head"

    # cuirass, belt, flared tassets, chest edge
    cu = F.lathe("Cuirass", [(0.132, 0.95), (0.118, 1.03), (0.106, 1.12), (0.118, 1.23), (0.137, 1.33), (0.128, 1.41), (0.075, 1.47), (0.058, 1.52), (0.06, 1.545)], seg=28, sy=0.8, cap_top=False, cap_bottom=False)
    meshes.append(finish(cu, leather, sub=2, bev=0.0))
    for z, rr, mn, m, bone in ((1.0, 0.124, 0.016, belt, "Hips"), (1.405, 0.124, 0.009, trim, "Spine2"), (1.18, 0.112, 0.006, trim, "Spine1")):
        t = F.torus("Band", (0, 0, z), rr, mn, sy=0.8); meshes.append(finish(t, m, sub=1)); rigid[t] = bone
    for k, ang in enumerate((-115, -70, 70, 115, 160, -160)):
        rad = math.radians(ang)
        cx, cy = math.sin(rad) * 0.13, -math.cos(rad) * 0.1
        pl = F.box("Tasset", (cx * 1.15, cy * 1.15, 0.86), (0.075, 0.012, 0.15), (math.radians(-10) * math.cos(rad), math.radians(12) * math.sin(rad), -rad))
        for v in pl.data.vertices:
            if v.co.z < 0: v.co.x *= 1.25
        meshes.append(finish(pl, leather, sub=1, bev=0.01)); rigid[pl] = "Hips"
        ed = F.box("TassetTrim", (cx * 1.2, cy * 1.2, 0.715), (0.09, 0.014, 0.012), (0, 0, -rad)); meshes.append(finish(ed, trim, sub=1, bev=0.004)); rigid[ed] = "Hips"
    buckle = F.box("Buckle", (0, -0.104, 1.0), (0.05, 0.02, 0.04)); meshes.append(finish(buckle, gold, sub=1, bev=0.004)); rigid[buckle] = "Hips"
    # front sash
    sash = F.flat_strand("Sash", [(0.0, -0.112, 0.99), (0.006, -0.128, 0.86), (-0.004, -0.122, 0.74)], 0.05, 0.04, 0.18)
    meshes.append(finish(sash, leather, sub=0)); rigid[sash] = "Hips"

    # boots with trim cuffs, wrapped wrists, one shoulder guard
    for s_ in ("Left", "Right"):
        top = J[s_ + "Leg"] + Vector((0, 0, 0.05)); bot = J[s_ + "Foot"] + Vector((0, 0, -0.06))
        bo = lathe_between("Boot", bot, top, [(0.05, 0.0), (0.05, 0.12), (0.058, 0.35), (0.064, 0.55), (0.058, 0.8), (0.066, 0.92), (0.078, 1.0)], seg=20)
        meshes.append(finish(bo, belt, sub=1)); rigid[bo] = s_ + "Leg"
        cuff = lathe_between("Cuff", top - Vector((0, 0, 0.03)), top + Vector((0, 0, 0.02)), [(0.073, 0), (0.075, 1)], seg=20)
        meshes.append(finish(cuff, trim, sub=1)); rigid[cuff] = s_ + "Leg"
        toe = F.ellipsoid("Toe", J[s_ + "ToeBase"] + Vector((0, 0.02, 0.0)), (0.045, 0.085, 0.04)); meshes.append(finish(toe, belt, sub=0)); rigid[toe] = s_ + "Foot"
        wr = lathe_between("Wrap", J[s_ + "ForeArm"] + (J[s_ + "Hand"] - J[s_ + "ForeArm"]) * 0.25, J[s_ + "Hand"], [(0.036, 0), (0.035, 0.5), (0.033, 1)], seg=16)
        meshes.append(finish(wr, wrap, sub=1)); rigid[wr] = s_ + "ForeArm"
    sg = F.ellipsoid("ShoulderGuard", J["LeftArm"] + Vector((0.01, 0, 0.03)), (0.085, 0.08, 0.06), 20, 12)
    import bmesh as _b
    bm = _b.new(); bm.from_mesh(sg.data); _b.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.01], context="VERTS"); bm.to_mesh(sg.data); bm.free()
    sg.modifiers.new("Solid", "SOLIDIFY").thickness = 0.01
    sg.rotation_euler = Euler((0, math.radians(-28), 0))
    meshes.append(finish(sg, leather, sub=1)); rigid[sg] = "LeftArm"
    sg2 = F.torus("GuardTrim", J["LeftArm"] + Vector((0.025, 0, 0.015)), 0.078, 0.007, rot=(0, math.radians(-28), 0), sy=0.95)
    meshes.append(finish(sg2, trim, sub=1)); rigid[sg2] = "LeftArm"

    # circlet with an ember gem
    ci = F.torus("Circlet", hc + Vector((0, -0.004, 0.035)), 0.112, 0.0055, rot=(math.radians(-10), 0, 0), sy=1.07)
    meshes.append(finish(ci, gold, sub=1)); rigid[ci] = "Head"
    gem = F.ellipsoid("Gem", hc + Vector((0, -0.122, 0.052)), (0.012, 0.007, 0.016), 12, 8); meshes.append(F.assign(gem, ember)); rigid[gem] = "Head"

    # hair: cap, long back locks, side locks, swept bangs
    cap = scalp_cap(hc + Vector((0, 0.01, 0.018)), 0.121, 0.133, 0.146, 0.18, -0.6, hair)
    meshes.append(cap); rigid[cap] = "Head"
    locks = []
    for i in range(13):
        a = math.radians(-105 + i * 17.5)  # around the back of the head
        x0, y0 = math.sin(a) * 0.1, math.cos(a) * 0.1 + 0.01
        sway = 0.03 * math.sin(i * 1.7)
        end = 1.12 + 0.05 * ((i * 7) % 3)
        flick = 1.0 + 0.5 * (1 if x0 > 0 else -1) * (abs(x0) > 0.05)
        pts = [(x0, y0, hc.z + 0.08), (x0 * 1.3, y0 + 0.045, hc.z - 0.06), (x0 * 1.45 + sway, y0 + 0.075, 1.4), (x0 * 1.3 - sway, y0 + 0.085, 1.24), (x0 * 1.35 * flick, y0 + 0.11, end)]
        lk = F.flat_strand("Lock", pts, 0.085, 0.012, 0.026); locks.append(finish(lk, hair, sub=0))
    for x in (1, -1):
        pts = [(x * 0.095, -0.06, hc.z + 0.06), (x * 0.12, -0.06, hc.z - 0.06), (x * 0.115, -0.03, 1.48), (x * 0.1, -0.01, 1.36)]
        locks.append(finish(F.flat_strand("SideLock", pts, 0.05, 0.015, 0.02), hair, sub=0))
    for k, (x0, x1) in enumerate(((0.06, -0.03), (0.0, -0.075), (-0.06, -0.1))):
        pts = [(x0 * 0.5, -0.07, hc.z + 0.13), (x0, -0.115, hc.z + 0.08), (x1, -0.122, hc.z + 0.045)]
        locks.append(finish(F.flat_strand("Bang", pts, 0.05, 0.012, 0.018), hair, sub=0))
    for lk in locks: meshes.append(lk); rigid[lk] = "Head"

    # whip: handle in the right hand, lash trailing on the ground with an ember tip; coil at the hip
    hR = J["RightHand"]; hE = J["RightHandEnd"]
    handle = lathe_between("WhipHandle", hR + (hE - hR) * 0.2 + Vector((0, -0.04, 0.06)), hR + (hE - hR) * 0.2 + Vector((0, -0.06, -0.14)), [(0.017, 0), (0.016, 0.8), (0.024, 1)], seg=12)
    meshes.append(finish(handle, belt, sub=1)); rigid[handle] = "RightHand"
    cap2 = F.ellipsoid("Pommel", hR + (hE - hR) * 0.2 + Vector((0, -0.04, 0.07)), (0.024, 0.024, 0.018)); meshes.append(F.assign(cap2, gold)); rigid[cap2] = "RightHand"
    start = hR + (hE - hR) * 0.2 + Vector((0, -0.06, -0.14))
    lash = F.strand("Lash", [tuple(start), (start.x - 0.06, start.y - 0.1, 0.5), (start.x - 0.14, start.y - 0.22, 0.12), (start.x - 0.1, start.y - 0.45, 0.05), (start.x + 0.12, start.y - 0.62, 0.05), (start.x + 0.25, start.y - 0.75, 0.05)], 0.012, 0.005)
    meshes.append(F.assign(lash, leather)); rigid[lash] = "RightHand"
    tip = F.ellipsoid("EmberTip", (start.x + 0.27, start.y - 0.76, 0.05), (0.025, 0.035, 0.025)); meshes.append(F.assign(tip, ember)); rigid[tip] = "RightHand"
    for k in range(3):
        co = F.torus("Coil", (-0.15, 0.02, 0.9 - k * 0.018), 0.06 - k * 0.004, 0.009, rot=(0, math.radians(80), 0)); meshes.append(finish(co, leather, sub=0)); rigid[co] = "Hips"

    return {"J": J, "meshes": meshes, "rigid": rigid, "center": 1.0, "rim": "#ff7a3a",
            "pose": {"Hips": ("Z", -8), "Spine1": ("Z", 10), "Head": [("Z", -6), ("X", -4)], "LeftArm": [("Y", 26), ("X", 8)], "LeftForeArm": ("X", 25),
                     "RightArm": [("Y", -18), ("X", 14)], "RightForeArm": ("X", 30)}}

# ------------------------------------------------------------------------------------- Gorrum
def cone(name, base, tip, r, mat, seg=12):
    o = lathe_between(name, base, tip, [(r, 0), (r * 0.55, 0.5), (0.002, 1)], seg=seg)
    return finish(o, mat, sub=0)

def gorrum():
    J = F.skeleton(h=1.08, sh=1.45, hip=1.25, arm_down=38)
    skin = F.painted("Skin", "#6b412b", shade="#3e2214", light="#9a6544", strokes=0.08, edge=0.1, ao=0.45)
    soot = F.painted("Soot", "#2e2420", shade="#141010", light="#5a4a40", metal=0.6, rough=0.45, strokes=0.25, edge=0.45)
    dark = F.painted("DarkPlate", "#2a2222", shade="#100c0c", light="#4a3a36", metal=0.7, rough=0.4, strokes=0.2, edge=0.5)
    cloth = F.painted("Cloth", "#3a2a22", shade="#1a120e", light="#5a4232", strokes=0.3, edge=0.1)
    bone = F.painted("Bone", "#e6dcc4", shade="#8a7a60", light="#fff8e8", strokes=0.2, edge=0.3)
    beard = F.painted("Beard", "#1a1410", shade="#050404", light="#3a3028", strokes=0.4, edge=0.2)
    molten = F.painted("Molten", "#ff5a1a", glow="#ff5a1a", glow_strength=3, strokes=0.0, edge=0)
    iron = F.painted("Iron", "#4a4440", shade="#1e1a18", light="#8a8078", metal=0.8, rough=0.35, strokes=0.15, edge=0.55)
    wood = F.painted("Haft", "#4a3020", shade="#24160c", light="#7a5638", strokes=0.35, edge=0.2)

    r = {"Hips": (0.19, 0.14), "Spine": (0.18, 0.13), "Spine1": (0.2, 0.15), "Spine2": (0.23, 0.16), "Neck": (0.08, 0.08), "Head": (0.06, 0.06),
         "Shoulder": (0.1, 0.1), "Arm": (0.085, 0.085), "ForeArm": (0.07, 0.07), "Hand": (0.05, 0.035), "HandEnd": (0.035, 0.025),
         "UpLeg": (0.12, 0.12), "Leg": (0.08, 0.08), "Foot": (0.06, 0.065), "ToeBase": (0.05, 0.045)}
    b = F.body(J, r)
    hand = lambda c: min((c - J["LeftHand"]).length, (c - J["RightHand"]).length) < 0.1
    split_body(b, [(0, lambda c: c.z > 1.6), (0, hand), (1, lambda c: True)], [skin, cloth])
    meshes = [b]; rigid = {}
    for s_ in ("Left", "Right"):
        for p in F.hand(J, s_, skin, size=1.5): meshes.append(p); rigid[p] = s_ + "Hand"
    head_parts, hc = F.head(J, 0.98, skin, eye_glow="#ff7a2a", iris="#ff7a2a", brow_angle=22, brow_col="#140c08", jaw=0.6)
    for p in head_parts: meshes.append(p); rigid[p] = "Head"
    # beard: a big mass with hanging clumps
    bd = F.ellipsoid("Beard", hc + Vector((0, -0.07, -0.1)), (0.1, 0.07, 0.11), 20, 14); meshes.append(finish(bd, beard, sub=1)); rigid[bd] = "Head"
    for k, x in enumerate((-0.06, -0.02, 0.02, 0.06)):
        pts = [(x, -0.12 + hc.y, hc.z - 0.08), (x * 1.1, -0.15 + hc.y, hc.z - 0.2), (x * 0.8, -0.14 + hc.y, hc.z - 0.3 - 0.02 * (k % 2))]
        cl = F.flat_strand("BeardClump", pts, 0.05, 0.012, 0.03); meshes.append(finish(cl, beard, sub=0)); rigid[cl] = "Head"
    mus = F.flat_strand("Moustache", [(-0.06, hc.y - 0.12, hc.z - 0.04), (0, hc.y - 0.128, hc.z - 0.03), (0.06, hc.y - 0.12, hc.z - 0.04)], 0.03, 0.03, 0.02)
    meshes.append(finish(mus, beard, sub=0)); rigid[mus] = "Head"
    # iron head band with curved bone horns
    band = F.torus("HornBand", hc + Vector((0, 0, 0.05)), 0.108, 0.014, rot=(math.radians(-6), 0, 0), sy=1.08); meshes.append(finish(band, iron, sub=1)); rigid[band] = "Head"
    for x in (1, -1):
        base = hc + Vector((x * 0.1, -0.01, 0.07))
        hn = F.strand("Horn", [tuple(base), tuple(base + Vector((x * 0.08, -0.02, 0.06))), tuple(base + Vector((x * 0.11, -0.06, 0.15))), tuple(base + Vector((x * 0.08, -0.1, 0.21)))], 0.032, 0.003)
        meshes.append(F.assign(hn, bone)); rigid[hn] = "Head"
    # soot-black cuirass with glowing seams
    cu = F.lathe("Cuirass", [(0.2, 0.96), (0.19, 1.06), (0.2, 1.18), (0.235, 1.32), (0.25, 1.45), (0.21, 1.56), (0.11, 1.62)], seg=28, sy=0.75, cap_top=False, cap_bottom=False)
    cu.scale = (1.0, 1.0, 1.08); meshes.append(finish(cu, soot, sub=1, bev=0.0))
    import random
    rnd = random.Random(7)
    for k in range(7):  # jagged molten cracks across the plate (seams in the glow mask)
        ang = math.radians(-60 + k * 20 + rnd.uniform(-6, 6)); z0 = rnd.uniform(1.12, 1.5)
        pts = []
        for j in range(4):
            a2 = ang + rnd.uniform(-0.12, 0.12); z = z0 + (j - 1.5) * 0.06 + rnd.uniform(-0.02, 0.02)
            rr = 0.205 + 0.045 * (z - 1.0) / 0.5
            pts.append((math.sin(a2) * rr * 1.03, -math.cos(a2) * rr * 0.79, z * 1.08))
        sm = F.strand("Crack", pts, 0.0055, 0.0025); meshes.append(F.assign(sm, molten)); rigid[sm] = "Spine1"
    # spiked pauldrons
    for s_, x in (("Left", 1), ("Right", -1)):
        c = J[s_ + "Arm"] + Vector((x * 0.02, 0, 0.06))
        pd = F.ellipsoid("Pauldron", c, (0.16, 0.15, 0.11), 20, 12)
        import bmesh
        bm = bmesh.new(); bm.from_mesh(pd.data); bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -0.02], context="VERTS"); bm.to_mesh(pd.data); bm.free()
        pd.modifiers.new("Solid", "SOLIDIFY").thickness = 0.02
        pd.rotation_euler = Euler((0, x * math.radians(-22), 0))
        meshes.append(finish(pd, dark, sub=1)); rigid[pd] = s_ + "Arm"
        for k in range(3):
            base = c + Vector((x * (0.04 + 0.05 * k), -0.04 + 0.04 * k, 0.07 - 0.015 * k))
            sp = cone("Spike", base, base + Vector((x * 0.09, 0, 0.13)), 0.03, dark); meshes.append(sp); rigid[sp] = s_ + "Arm"
        # gauntlets and boots
        ga = lathe_between("Gauntlet", J[s_ + "ForeArm"] + (J[s_ + "Hand"] - J[s_ + "ForeArm"]) * 0.15, J[s_ + "Hand"], [(0.08, 0), (0.09, 0.6), (0.085, 1)], seg=18)
        meshes.append(finish(ga, dark, sub=1, bev=0.01)); rigid[ga] = s_ + "ForeArm"
        top = J[s_ + "Leg"] + Vector((0, 0, 0.08)); bot = J[s_ + "Foot"] + Vector((0, 0, -0.08))
        bo = lathe_between("Boot", bot, top, [(0.085, 0), (0.088, 0.3), (0.095, 0.7), (0.115, 1)], seg=20); meshes.append(finish(bo, dark, sub=1)); rigid[bo] = s_ + "Leg"
        kn = F.ellipsoid("Knee", J[s_ + "Leg"] + Vector((0, -0.07, 0.03)), (0.07, 0.05, 0.075), 14, 10); meshes.append(finish(kn, iron, sub=0)); rigid[kn] = s_ + "Leg"
        toe = F.ellipsoid("Toe", J[s_ + "ToeBase"] + Vector((0, 0.01, 0.01)), (0.07, 0.12, 0.06)); meshes.append(finish(toe, dark, sub=0)); rigid[toe] = s_ + "Foot"
    # chain apron and belt
    belt = F.torus("Belt", (0, 0, 1.0), 0.205, 0.025, sy=0.76); meshes.append(finish(belt, iron, sub=1)); rigid[belt] = "Hips"
    ap = F.box("Apron", (0, -0.16, 0.8), (0.24, 0.02, 0.36)); meshes.append(finish(ap, iron, sub=1, bev=0.01)); rigid[ap] = "Hips"
    for k in range(5):
        ln = F.box("ApronRing", (0, -0.175, 0.95 - k * 0.075), (0.25, 0.012, 0.01)); meshes.append(finish(ln, dark, sub=0)); rigid[ln] = "Hips"
    # the upright forge maul in the right hand
    hR = J["RightHand"]
    haft = lathe_between("Haft", hR + Vector((0, -0.05, -0.75)), hR + Vector((0, -0.05, 0.35)), [(0.03, 0), (0.028, 0.5), (0.032, 1)], seg=12)
    meshes.append(finish(haft, wood, sub=0)); rigid[haft] = "RightHand"
    head = F.box("MaulHead", hR + Vector((0, -0.05, 0.47)), (0.36, 0.22, 0.22)); meshes.append(finish(head, iron, sub=1, bev=0.025)); rigid[head] = "RightHand"
    for z in (-0.06, 0.0, 0.06):
        cr = F.box("MaulCrack", hR + Vector((0.03 * z * 10, -0.162, 0.47 + z)), (0.2, 0.01, 0.012), (0, math.radians(18 * (1 if z > 0 else -1)), 0)); meshes.append(F.assign(cr, molten)); rigid[cr] = "RightHand"
    return {"J": J, "meshes": meshes, "rigid": rigid, "center": 1.1, "rim": "#ff5a1a", "height": 2.1,
            "pose": {"Spine1": ("Z", 8), "Head": ("X", -6), "LeftArm": [("Y", 18), ("X", 10)], "LeftForeArm": ("X", 20), "RightArm": ("Y", -10)}}
