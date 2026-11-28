"""Creature hero builder (non-humanoid lower bodies): the UBC upper body from ubc.py, a creature body, its own
baked clips (creature_gait.py legs + upper-body clips from models/ubc_anims.glb), HD paint pass and export.

    bpyenv/bin/python creature.py -- --design ../../../docs/sigilbound/concepts/creatures.json --hero vessa --out DIR
                                     [--tex 1024] [--tris 26000] [--review DIR]

Writes SB_Hero_<id>_HD.glb (skinned, WITH its clips: the game plays them through Core/Kit/Creatures.cs and
Client/CreatureRig.cs) plus the _albedo.jpg / _normal.jpg / _glow.png atlas, like ubc.py.
Shared pieces for other creatures: humanoid_upper, cut_lower, add_bones, tube/egg/ellip part builders,
bind_rigid, upper_source (sample a UAL clip by bone name) and bake_clips (gait frames -> FK keys).
Rig "spider" (Vessa): spider_body under root, the UBC pelvis under it; abdomen, abdomen_02; eight legs
leg_<L1..R4>_coxa/_femur/_tibia/_tarsus and a _tip marker at each foot.
"""
import bpy, bmesh, sys, os, json, math, argparse
from mathutils import Vector, Matrix, Quaternion, Euler

HERE = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, HERE)
import ubc, ubc_parts as P  # noqa: E402
import creature_gait as G  # noqa: E402
ANIMS = os.path.normpath(os.path.join(HERE, "..", "models", "ubc_anims.glb"))

# ---------------- humanoid upper body (ubc.py) ----------------
def humanoid_upper(c):
    # Built as an Epic: the kit gives bare-headed Legendaries an automatic halo, which a creature design doesn't want.
    c = dict(c, rarity="Epic" if c.get("rarity") == "Legendary" else c.get("rarity"))
    arm = P.build(c, pose=False); ubc.kit(c, arm)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for x in bpy.context.selected_objects: x.select_set(False)
    for o in meshes: o.select_set(True)
    body = next((o for o in meshes if not o.name.startswith("Kit_")), meshes[0])
    bpy.context.view_layer.objects.active = body; bpy.ops.object.join()
    hero = bpy.context.view_layer.objects.active; hero.name = f"SB_Hero_{c['id']}"
    for md in [m for m in hero.modifiers if m.type == "ARMATURE"][1:]: hero.modifiers.remove(md)
    for o in [o for o in bpy.context.scene.objects if o.type == "MESH" and o != hero]: bpy.data.objects.remove(o, do_unlink=True)
    return arm, hero

def cut_lower(hero, arm, z_cut, drop):
    """Delete the humanoid legs: vertices below z_cut, and any whose strongest weight is a dropped bone."""
    gi = {g.index: g.name for g in hero.vertex_groups}; mw = hero.matrix_world
    bm = bmesh.new(); bm.from_mesh(hero.data); dl = bm.verts.layers.deform.active; kill = []
    for v in bm.verts:
        w = v[dl] if dl else {}
        top = max(w.items(), key=lambda kv: kv[1])[0] if w else None
        if (mw @ v.co).z < z_cut or (top is not None and gi.get(top) in drop): kill.append(v)
    bmesh.ops.delete(bm, geom=kill, context="VERTS"); bm.to_mesh(hero.data); bm.free()
    for g in [g for g in hero.vertex_groups if g.name in drop]: hero.vertex_groups.remove(g)
    return len(kill)

def add_bones(arm, specs, reparent=(), remove=()):
    """specs: [(name, head, tail, parent, roll_hint, deform)], then reparent [(child, new_parent)], remove [names]."""
    for x in bpy.context.selected_objects: x.select_set(False)
    arm.select_set(True); bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode="EDIT")
    eb = arm.data.edit_bones
    for n, h, t, par, hint, deform in specs:
        b = eb.new(n); b.head = Vector(h); b.tail = Vector(t); b.align_roll(Vector(hint)); b.use_deform = deform
        if par: b.parent = eb[par]
    for ch, par in reparent: eb[ch].use_connect = False; eb[ch].parent = eb[par]
    for n in remove:
        if n in eb: eb.remove(eb[n])
    bpy.ops.object.mode_set(mode="OBJECT")

# ---------------- part builders ----------------
def _obj(name, bm, mat):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o); o.data.materials.append(mat); return o

def tube(name, pts_r, mat, segs=10, tip=False):
    """A smooth tube through (point, radius) samples; capped; tip=True closes the end to a point."""
    bm = bmesh.new(); rings = []
    for i, (p, r) in enumerate(pts_r):
        p = Vector(p); d = (Vector(pts_r[min(i + 1, len(pts_r) - 1)][0]) - Vector(pts_r[max(i - 1, 0)][0])).normalized()
        q = Vector((0, 0, 1)).rotation_difference(d); ring = []
        for k in range(segs):
            a = 2 * math.pi * k / segs; ring.append(bm.verts.new(p + q @ Vector((math.cos(a) * r, math.sin(a) * r, 0))))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(segs): bm.faces.new((r0[k], r0[(k + 1) % segs], r1[(k + 1) % segs], r1[k]))
    bm.faces.new(list(reversed(rings[0])))
    if tip:
        end = Vector(pts_r[-1][0]) + (Vector(pts_r[-1][0]) - Vector(pts_r[-2][0])).normalized() * pts_r[-1][1] * 3
        v = bm.verts.new(end)
        for k in range(segs): bm.faces.new((rings[-1][k], rings[-1][(k + 1) % segs], v))
    else: bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces); return _obj(name, bm, mat)

def ellip(name, centre, radii, mat, rot=None, u=24, v=14, shape=None):
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=1.0)
    for vt in bm.verts:
        co = vt.co.copy()
        if shape: co = shape(co)
        vt.co = (rot or Matrix()) @ Vector((co.x * radii[0], co.y * radii[1], co.z * radii[2])) + Vector(centre)
    return _obj(name, bm, mat)

def bind_rigid(o, arm, bone):
    ubc.bind(o, arm, {bone: 1.0}); o.parent = arm; return o

def mat(name, col, rough=0.5, metal=0.0, emit=None, strength=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True; p = m.node_tree.nodes["Principled BSDF"]
    p.inputs["Base Color"].default_value = (*col, 1); p.inputs["Roughness"].default_value = rough; p.inputs["Metallic"].default_value = metal
    if emit: p.inputs["Emission Color"].default_value = (*emit, 1); p.inputs["Emission Strength"].default_value = strength
    return m
def hexc(h): h = h.lstrip("#"); return tuple((int(h[i:i + 2], 16) / 255.0) ** 2.2 for i in (0, 2, 4))

# ---------------- spider ----------------
class SpiderMap:
    """Maps creature_gait's prototype metres onto this model: scale S, the body centre under the UBC pelvis."""
    def __init__(self, pelvis):
        self.S = pelvis.z / 1.02                     # gait waist 1.02 m -> the UBC pelvis height
        self.y0 = pelvis.y + 0.10 * self.S            # gait waist sits 0.10 m in front of the body centre
    def p(self, q): return Vector((q[0] * self.S, q[1] * self.S + self.y0, q[2] * self.S))

LEG_BONES = ("coxa", "femur", "tibia", "tarsus")
def spider_rig(arm, M):
    pel = arm.data.bones["pelvis"].head_local
    bz = G.BODY_Z
    specs = [("spider_body", M.p((0, 0.12, bz)), M.p((0, -0.14, bz)), "root", (0, 0, 1), True),
             ("abdomen", M.p((0, 0.12, 0.92)), M.p((0, 0.55, 0.80)), "spider_body", (0, 0, 1), True),
             ("abdomen_02", M.p((0, 0.55, 0.80)), M.p((0, 1.05, 0.68)), "abdomen", (0, 0, 1), True)]
    for leg in G.LEGS:
        pts = [M.p(x) for x in G.solve_leg(leg, G.ATTACH[leg], G.REST[leg])]
        o = (pts[4] - pts[0]); o.z = 0; o.normalize(); n = Vector((-o.y, o.x, 0)); par = "spider_body"
        for i, b in enumerate(LEG_BONES):
            specs.append((f"leg_{leg}_{b}", pts[i], pts[i + 1], par, n, True)); par = f"leg_{leg}_{b}"
        specs.append((f"leg_{leg}_tip", pts[4], pts[4] + Vector((0, 0, -0.04)), par, n, False))
    # the long braid: three bones from the nape to the waist, swayed by the clips
    hb = ubc.head_box(arm); nape = Vector((0, hb["cy"] + 0.085, hb["eye"] - 0.02)); s3 = arm.data.bones["spine_03"].head_local; s1 = arm.data.bones["spine_01"].head_local
    bpts = [nape, Vector((0, s3.y + 0.12, s3.z + 0.04)), Vector((0, s3.y + 0.13, (s3.z + s1.z) / 2)), Vector((0, s1.y + 0.13, s1.z - 0.06))]
    par = "Head"
    for k in range(3):
        specs.append((f"braid_0{k + 1}", bpts[k], bpts[k + 1], par, (0, -1, 0), True)); par = f"braid_0{k + 1}"
    drop = [b.name for b in arm.data.bones if any(b.name.startswith(k) for k in ("thigh", "calf", "foot", "ball"))]
    add_bones(arm, specs, reparent=[("pelvis", "spider_body")], remove=drop)
    return drop

def spider_parts(c, arm, M):
    pal = c.get("palette", {}); S = M.S
    chitin = mat("Chitin", hexc("#120e18"), 0.32); chitin2 = mat("Chitin_Joint", hexc("#1e1828"), 0.4)
    band = mat("Band", hexc("#6a2ad0"), 0.35, 0, hexc(pal.get("glow", "#a04aff")), 2.5)
    leather = mat("Leather", hexc(pal.get("main", "#141218")), 0.55); silver = mat("Silver", hexc(pal.get("metal", "#b8bccc")), 0.3, 1.0)
    glow = mat("Silk", hexc("#c080ff"), 0.3, 0, hexc(pal.get("glow", "#a04aff")), 6.0)
    eye = mat("Eye", hexc("#b070ff"), 0.2, 0, hexc("#c890ff"), 5.0)
    out = []
    B = arm.data.bones; bc = M.p((0, 0, G.BODY_Z))
    # cephalothorax: a domed carapace with a front ridge, under the waist
    def carapace(co):
        z = co.z * (1.0 if co.z > 0 else 0.65); y = co.y * (1.0 + 0.12 * max(0.0, co.y)); return Vector((co.x * (1 - 0.15 * max(0.0, -co.y)), y, z))
    out.append(bind_rigid(ellip("Spider_Carapace", bc + Vector((0, 0.02 * S, -0.02 * S)), (0.22 * S, 0.29 * S, 0.17 * S), chitin, shape=carapace), arm, "spider_body"))
    # egg abdomen: wide in front, tapering to the spinnerets; bands of darker chitin; front half on abdomen, back on abdomen_02
    ac = M.p((0, 0.68, 0.86))
    def egg(co):
        t = co.y; s = 1.0 - 0.45 * max(0.0, t) ** 1.3 + 0.06 * max(0.0, -t); r = (co.x * s, t * (1.0 + 0.12 * t), co.z * s)
        return Vector(r)
    ab = ellip("Spider_Abdomen", ac, (0.34 * S, 0.62 * S, 0.37 * S), chitin, Matrix.Rotation(math.radians(-10), 4, "X"), u=28, v=16, shape=egg)
    split = ac.y
    ubc.bind(ab, arm, lambda p: ({"abdomen": 1.0} if p.y < split - 0.1 * S else {"abdomen_02": 1.0} if p.y > split + 0.15 * S
                                 else {"abdomen": 1 - (p.y - split + 0.1 * S) / (0.25 * S), "abdomen_02": (p.y - split + 0.1 * S) / (0.25 * S)}))
    ab.parent = arm; out.append(ab)
    sp = tube("Spider_Spinneret", [(M.p((0, 1.22, 0.74)), 0.05 * S), (M.p((0, 1.32, 0.70)), 0.03 * S)], chitin2, 10, tip=True); out.append(bind_rigid(sp, arm, "abdomen_02"))
    # waist: a tattered leather skirt over the junction, silver web trim
    pz = B["pelvis"].head_local.z; py = B["pelvis"].head_local.y
    bm = bmesh.new(); top, bot = [], []; N = 24
    for k in range(N):
        a = 2 * math.pi * k / N; jag = 0.05 * S * (1 if k % 2 else 0.2)
        top.append(bm.verts.new((math.cos(a) * 0.15 * S, py + math.sin(a) * 0.12 * S, pz + 0.05 * S)))
        bot.append(bm.verts.new((math.cos(a) * 0.24 * S, py + 0.02 * S + math.sin(a) * 0.20 * S, pz - 0.12 * S - jag)))
    for k in range(N): bm.faces.new((top[k], top[(k + 1) % N], bot[(k + 1) % N], bot[k]))
    sk = _obj("Spider_Skirt", bm, leather); sk.modifiers.new("Solid", "SOLIDIFY").thickness = 0.008
    bpy.context.view_layer.objects.active = sk; sk.select_set(True); bpy.ops.object.modifier_apply(modifier="Solid"); sk.select_set(False)
    out.append(bind_rigid(sk, arm, "pelvis"))
    trim = tube("Spider_SkirtTrim", [(Vector((math.cos(2 * math.pi * k / 20) * 0.155 * S, py + math.sin(2 * math.pi * k / 20) * 0.125 * S, pz + 0.05 * S)), 0.008 * S) for k in range(21)], silver, 6)
    out.append(bind_rigid(trim, arm, "pelvis"))
    # legs: long thin chitin segments with knuckles, violet bands at every joint, pointed tips
    for leg in G.LEGS:
        nm = lambda b: f"leg_{leg}_{b}"; j = [B[nm(b)].head_local.copy() for b in LEG_BONES] + [B[nm("tarsus")].tail_local.copy()]
        out.append(bind_rigid(ellip(f"Coxa_{leg}", j[0].lerp(j[1], 0.5), (0.04 * S,) * 3, chitin2, u=12, v=8), arm, nm("coxa")))
        for i, (b, r0, r1) in enumerate((("femur", 0.032, 0.026), ("tibia", 0.026, 0.019), ("tarsus", 0.019, 0.003))):
            a, z = j[i + 1], j[i + 2]; pts = []
            for s_ in range(7):
                u = s_ / 6; r = (r0 + (r1 - r0) * u) * (1 + 0.18 * math.sin(math.pi * u)) * S
                pts.append((a.lerp(z, u), r))
            out.append(bind_rigid(tube(f"Leg_{leg}_{b}", pts, chitin, 10, tip=(b == "tarsus")), arm, nm(b)))
            d = (z - a).normalized(); rb = r0 * 1.55 * S
            out.append(bind_rigid(tube(f"Band_{leg}_{b}", [(a + d * 0.012 * S, rb), (a + d * 0.05 * S, rb * 0.96)], band, 12), arm, nm(b)))
    # the long thick braid to the waist (on the braid bones, so it sways) and two side braids framing the face
    hair = mat("Braid", hexc((c.get("hair") or {}).get("colour", "#0e0c12")), 0.45); tie = mat("BraidTie", hexc("#c8cad8"), 0.35, 1.0)
    hb0 = ubc.head_box(arm)
    for k in range(3):
        bn = f"braid_0{k + 1}"; h0, t0 = B[bn].head_local, B[bn].tail_local; n_ = 5
        for j in range(n_):
            u = (j + 0.5) / n_; p_ = h0.lerp(t0, u); r = 0.046 - 0.013 * ((k * n_ + j) / (3 * n_))
            out.append(bind_rigid(ellip(f"Braid_{k}{j}", p_, (r, r * 0.85, r * 0.95), hair, Matrix.Rotation(math.radians(25 * (1 if j % 2 else -1)), 4, "Y"), u=12, v=8), arm, bn))
    end = B["braid_03"].tail_local
    out.append(bind_rigid(tube("Braid_Tie", [(end + Vector((0, 0, 0.012)), 0.024), (end - Vector((0, 0, 0.012)), 0.022)], tie, 10), arm, "braid_03"))
    out.append(bind_rigid(tube("Braid_Tail", [(end - Vector((0, 0, 0.01)), 0.022), (end - Vector((0, 0, 0.07)), 0.012)], hair, 10, tip=True), arm, "braid_03"))
    for sx in (-1, 1):
        top = Vector((hb0["cx"] + sx * hb0["hw"] * 0.72, hb0["front"] + 0.045, hb0["eye"] + 0.03)); bot = Vector((hb0["cx"] + sx * hb0["hw"] * 0.95, hb0["front"] + 0.07, hb0["base"] - 0.07))
        for j in range(9):
            u = j / 8; p_ = top.lerp(bot, u) + Vector((sx * 0.012 * math.sin(math.pi * u), 0, 0)); r = 0.017 - 0.006 * u
            out.append(bind_rigid(ellip(f"SideBraid_{sx}{j}", p_, (r, r, r * 1.2), hair, u=10, v=6), arm, "Head"))
        side = "l" if sx > 0 else "r"; ua = B[f"upperarm_{side}"].head_local
        for k, (dx, dz, ln) in enumerate(((0.0, 0.0, 0.09), (0.035, -0.01, 0.075), (-0.03, -0.005, 0.07))):
            base = ua + Vector((sx * (0.02 + dx), 0.0 + 0.015 * k - 0.015, 0.055 + dz))
            tipp = base + Vector((sx * 0.35, 0.05 * (k - 1), 1.0)).normalized() * ln
            out.append(bind_rigid(tube(f"Spike_{side}{k}", [(base, 0.016), (base.lerp(tipp, 0.5), 0.010), (tipp, 0.003)], tie, 8, tip=True), arm, f"upperarm_{side}"))
    # four small violet eyes above her own two, and silk at her left hand
    hb = ubc.head_box(arm)
    for sx in (-1, 1):
        for k, (dx, dz) in enumerate(((0.018, 0.035), (0.034, 0.052))):
            out.append(bind_rigid(ellip(f"Eye_{sx}{k}", (hb["cx"] + sx * dx, hb["front"] - 0.004, hb["eye"] + dz), (0.0065, 0.004, 0.0065), eye, u=10, v=6), arm, "Head"))
    hl = B["hand_l"]; hp = hl.head_local.lerp(hl.tail_local, 0.9)
    out.append(bind_rigid(ellip("Silk_Orb", hp + Vector((0, -0.03, 0)), (0.03, 0.03, 0.03), glow, u=12, v=8), arm, "hand_l"))
    for k in range(3):
        pts = [(hp + Vector((0.04 * i * (1 if k != 1 else 0.3), -0.04 * i + 0.01 * k, -0.06 * i - 0.02 * k * i + 0.02 * math.sin(i + k))), 0.0035) for i in range(6)]
        out.append(bind_rigid(tube(f"Silk_{k}", pts, glow, 5), arm, "hand_l"))
    return out

# ---------------- clips ----------------
UPPER_SKIP = {"root", "spider_body", "abdomen", "abdomen_02"}
def upper_bones(arm):
    return [b.name for b in arm.data.bones if not b.name.startswith("leg_") and b.name not in UPPER_SKIP]

class Source:
    """UAL clips (models/ubc_anims.glb), sampled by bone name: local rotation per bone per frame (30 fps)."""
    def __init__(self, names):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=ANIMS)
        self.objs = [o for o in bpy.data.objects if o not in before]
        self.arm = next(o for o in self.objs if o.type == "ARMATURE")
        for o in self.objs: o.hide_render = True; o.hide_set(True)
        self.acts = {a.name.split("|")[-1]: a for a in bpy.data.actions}; self.names = names; self.cache = {}
    def length(self, clip):
        a = self.acts[clip]; f0, f1 = a.frame_range; return (f1 - f0) / bpy.context.scene.render.fps, f0
    def sample(self, clip, t):
        L, f0 = self.length(clip); f = f0 + max(0.0, min(L, t)) * bpy.context.scene.render.fps
        key = (clip, round(f, 2))
        if key not in self.cache:
            self.arm.animation_data_create(); self.arm.animation_data.action = self.acts[clip]
            sc = bpy.context.scene; sc.frame_set(int(math.floor(f)), subframe=f - math.floor(f))
            self.cache[key] = {n: self.arm.pose.bones[n].rotation_quaternion.copy() for n in self.names if n in self.arm.pose.bones}
        return self.cache[key]

SRC = {  # clip -> (source UAL clip, time mapping)
    "Idle_Loop": ("Sword_Idle", "loop"), "Walk_Loop": ("Sword_Idle", "hold"), "Turn": ("Sword_Idle", "loop"),
    "Attack": ("Sword_Attack", ("impact", 0.38)), "Attack2": ("Sword_Attack", ("impact", 0.38)),
    "Cast": ("Spell_Simple_Shoot", "fit"), "Ult_Pounce": ("Ult_LeapSlam", "fit"), "Hit": ("Hit_Chest", "fit"),
    "Death": ("Hit_Head", ("hold_after", 0.5)), "Win": ("Interact", "fit"), "Stun_Loop": ("Sword_Idle", "loop"),
    "WalkIn": ("Sword_Idle", "loop"),
}
def src_time(mode, t, L, srcL, clip):
    if mode == "hold": return 0.0
    if mode == "loop": k = max(1, round(L / srcL)); return (t / L * k * srcL) % srcL
    if mode == "fit": return t / L * srcL
    if mode[0] == "impact":
        I = G.IMPACT.get(clip, 0.3) * L; si = mode[1] * srcL
        return t / I * si if t < I else si + (t - I) / max(1e-6, L - I) * (srcL - si)
    if mode[0] == "hold_after": return min(t / mode[1], 1.0) * srcL
    return t

def frame_from(head, tail, n):
    Y = (tail - head).normalized(); Z = (n - Y * n.dot(Y)).normalized(); X = Y.cross(Z).normalized(); Z = X.cross(Y)
    m = Matrix((X, Y, Z)).transposed().to_4x4(); m.translation = head; return m

def bake_clips(arm, M, src, clips, fps=30):
    """creature_gait frames -> FK keys on every bone, one action per clip, as NLA tracks (for the exporter)."""
    B = arm.data.bones; PB = arm.pose.bones
    for pb in PB: pb.rotation_mode = "QUATERNION"
    order = sorted(B, key=lambda b: len(b.parent_recursive))
    upper = set(upper_bones(arm)); spine = ["spine_01", "spine_02", "spine_03"]
    rest = {b.name: b.matrix_local for b in B}
    rel = {b.name: (b.parent.matrix_local.inverted() @ b.matrix_local) if b.parent else b.matrix_local for b in B}
    actions = {}
    arm.animation_data_create()
    for name, frames in clips.items():
        act = bpy.data.actions.new(name); arm.animation_data.action = act
        clipname, mode = SRC[name]; srcL, _ = src.length(clipname); L = (len(frames) - 1) / fps if name != "Walk_Loop" else len(frames) / fps
        for fi, fr in enumerate(frames):
            x, y, z, yaw, pitch, roll, pivot = fr["body"]
            pv = M.p(pivot)
            D = (Matrix.Translation(Vector((x * M.S, y * M.S, z * M.S)) + pv) @ Matrix.Rotation(yaw, 4, "Z") @ Matrix.Rotation(-pitch, 4, "X")
                 @ Matrix.Rotation(roll, 4, "Y") @ Matrix.Translation(-pv))
            pose = {}; basis = {}
            s = src.sample(clipname, src_time(mode, fi / fps, L, srcL, name))
            ax = Matrix.Rotation(yaw, 3, "Z") @ Vector((1, 0, 0)); ay = Matrix.Rotation(yaw, 3, "Z") @ Vector((0, 1, 0))
            off = fr["upper"]
            def world_rot(bend, side, twist, comp=(0, 0)):
                return (Matrix.Rotation(math.radians(twist), 4, Vector((0, 0, 1))) @ Matrix.Rotation(math.radians(bend) + comp[0], 4, ax)
                        @ Matrix.Rotation(math.radians(side) + comp[1], 4, ay))
            legpts = {}
            for leg in G.LEGS:
                hip = D @ M.p(G.ATTACH[leg])
                # solve in gait units: map the hip back
                hip_g = ((hip.x) / M.S, (hip.y - M.y0) / M.S, hip.z / M.S)
                legpts[leg] = [M.p(q) for q in G.solve_leg(leg, hip_g, fr["feet"][leg])]
            for b in order:
                n = b.name; par = b.parent.name if b.parent else None; Mp = pose[par] if par else Matrix()
                if n == "spider_body": Mw = D @ rest[n]
                elif n == "abdomen":
                    ayw, ap, br = fr["abd"]; Mw = Mp @ rel[n] @ (Matrix.Rotation(ayw, 4, "Z") @ Matrix.Rotation(ap, 4, "X") @ Matrix.Diagonal((br, 1.0, br, 1)))
                elif n == "abdomen_02": Mw = Mp @ rel[n] @ Matrix.Rotation(fr["abd"][0] * 0.6, 4, "Z")
                elif n.startswith("leg_"):
                    _, leg, part = n.split("_")
                    if part == "tip": Mw = Mp @ rel[n]
                    else:
                        i = LEG_BONES.index(part); pts = legpts[leg]
                        head = Mp @ rel[n] @ Vector((0, 0, 0, 1)) if False else (pose[par] @ rel[n]).translation if par != "spider_body" else (D @ rest[n]).translation
                        o = pts[4] - pts[0]; o.z = 0; o = o.normalized() if o.length > 1e-6 else Vector((1, 0, 0)); nrm = Vector((-o.y, o.x, 0))
                        d = (pts[i + 1] - head).normalized(); Mw = frame_from(head, head + d * B[n].length, nrm)
                elif n.startswith("braid_"):
                    k = int(n[-1]); t_ = fi / fps; ph = 2 * math.pi * t_ / max(L, 1e-3)
                    cyc = max(1, round(L / 1.6)) if name in G.LOOPS else 1.0 / max(L, 1e-3) * 1.6
                    sway = (3.0 + 1.5 * k) * math.sin(cyc * ph + k * 0.7) + 25 * fr["abd"][0] * k / 3
                    swing = (2.0 + k) * math.sin(cyc * ph * 0.5 + k) - 6 * pitch * k
                    Mw = Mp @ rel[n] @ Euler((math.radians(swing), 0, math.radians(sway))).to_matrix().to_4x4()
                elif n in upper:
                    q = s.get(n, Quaternion())
                    if n == "pelvis": q = Quaternion().slerp(q, 0.5)
                    Mw = Mp @ rel[n] @ q.to_matrix().to_4x4()
                    h = Mw.translation.copy(); R = None
                    if n == "pelvis": R = world_rot(0, 0, 0, (pitch * 0.75, -roll * 0.5))
                    elif n in spine: bnd, sd, tw = off["spine"]; R = world_rot(bnd / 3, sd / 3, tw / 3)
                    elif n == "Head": bnd, sd, tw = off["head"]; R = world_rot(bnd, sd, tw)
                    if R is not None: Mw = Matrix.Translation(h) @ R @ Matrix.Translation(-h) @ Mw
                else: Mw = Mp @ rel[n]
                pose[n] = Mw
                bas = rel[n].inverted() @ Mp.inverted() @ Mw if par else rest[n].inverted() @ Mw
                PB[n].matrix_basis = bas
            for pb in PB:
                for path in ("location", "rotation_quaternion", "scale"): pb.keyframe_insert(path, frame=fi + 1)
        arm.animation_data.action = None; actions[name] = (act, len(frames))
    for name, (act, n) in actions.items():
        tr = arm.animation_data.nla_tracks.new(); tr.name = name; tr.strips.new(name, 1, act)
    for pb in PB: pb.matrix_basis = Matrix()
    return actions

GAME_CLIPS = ["Idle_Loop", "Walk_Loop", "Turn", "Attack", "Attack2", "Cast", "Ult_Pounce", "Hit", "Death", "Win", "Stun_Loop"]

def export(arm, hero, path):
    for x in bpy.context.selected_objects: x.select_set(False)
    arm.select_set(True); hero.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=True, export_skins=True, export_animations=True,
                              export_animation_mode="NLA_TRACKS", export_force_sampling=True, export_apply=False, export_yup=True,
                              export_materials="PLACEHOLDER")
    return os.path.getsize(path) // 1024

def _m(nt, op, a, b=None, x=0, y=0):
    n = nt.nodes.new("ShaderNodeMath"); n.operation = op; n.location = (x, y)
    for i, v in enumerate((a, b)):
        if v is None: continue
        if isinstance(v, (int, float)): n.inputs[i].default_value = v
        else: nt.links.new(v, n.inputs[i])
    return n.outputs[0]

def paint_details(hero, arm, hb, c):
    """Painted before the bake (the albedo bake reads base colours): a silver spider-web filigree on the leather
    bodice and bracers (spokes and rings round the chest, a lattice on the arms), and darker lips. Both are masks
    in object space (the hero mesh sits at the origin), so no UVs are needed."""
    silver = (0.62, 0.63, 0.70, 1.0); lips = (0.20, 0.07, 0.17, 1.0); chest = arm.data.bones["spine_03"].head_local
    def base_in(m):
        nt = m.node_tree; bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None: return None, None, None
        inp = bsdf.inputs["Base Color"]
        if inp.links: src = inp.links[0].from_socket
        else:
            rgb = nt.nodes.new("ShaderNodeRGB"); rgb.outputs[0].default_value = inp.default_value; src = rgb.outputs[0]
        return nt, inp, src
    for m in hero.data.materials:
        if m is None or not m.use_nodes: continue
        is_leather = m.name.startswith("MI_Ranger"); is_skin = m.name.startswith("MI_Regular")
        if not (is_leather or is_skin): continue
        nt, inp, src = base_in(m)
        if nt is None: continue
        tc = nt.nodes.new("ShaderNodeTexCoord"); sep = nt.nodes.new("ShaderNodeSeparateXYZ"); nt.links.new(tc.outputs["Object"], sep.inputs[0])
        X, Y, Z = sep.outputs[0], sep.outputs[1], sep.outputs[2]
        mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; nt.links.new(src, mix.inputs[6])
        if is_leather:
            dz = _m(nt, "SUBTRACT", Z, chest.z + 0.06); r = _m(nt, "SQRT", _m(nt, "ADD", _m(nt, "MULTIPLY", X, X), _m(nt, "MULTIPLY", dz, dz)))
            ang = _m(nt, "ARCTAN2", X, dz); N = 14.0
            f = _m(nt, "FRACT", _m(nt, "MULTIPLY", ang, N / (2 * math.pi))); fd = _m(nt, "MINIMUM", f, _m(nt, "SUBTRACT", 1.0, f))
            spoke = _m(nt, "LESS_THAN", _m(nt, "MULTIPLY", fd, _m(nt, "MULTIPLY", r, 2 * math.pi / N)), 0.0022)
            g = _m(nt, "FRACT", _m(nt, "MULTIPLY", r, 1 / 0.032)); gd = _m(nt, "MINIMUM", g, _m(nt, "SUBTRACT", 1.0, g))
            ring = _m(nt, "LESS_THAN", _m(nt, "MULTIPLY", gd, 0.032), 0.0016)
            web = _m(nt, "MAXIMUM", spoke, ring)
            torso = _m(nt, "LESS_THAN", _m(nt, "ABSOLUTE", X), 0.17)
            # arms: a diagonal lattice along the bracers
            l1 = _m(nt, "LESS_THAN", _m(nt, "ABSOLUTE", _m(nt, "SINE", _m(nt, "MULTIPLY", _m(nt, "ADD", X, Z), 90.0))), 0.12)
            l2 = _m(nt, "LESS_THAN", _m(nt, "ABSOLUTE", _m(nt, "SINE", _m(nt, "MULTIPLY", _m(nt, "SUBTRACT", X, Z), 90.0))), 0.12)
            arms = _m(nt, "MAXIMUM", l1, l2)
            fac = _m(nt, "ADD", _m(nt, "MULTIPLY", web, torso), _m(nt, "MULTIPLY", arms, _m(nt, "SUBTRACT", 1.0, torso)))
            mix.inputs[7].default_value = silver
        else:
            zm = hb["eye"] - 0.058; front = hb["front"]
            ex = _m(nt, "DIVIDE", X, 0.017); ez = _m(nt, "DIVIDE", _m(nt, "SUBTRACT", Z, zm), 0.0065)
            ell = _m(nt, "LESS_THAN", _m(nt, "ADD", _m(nt, "MULTIPLY", ex, ex), _m(nt, "MULTIPLY", ez, ez)), 1.0)
            fac = _m(nt, "MULTIPLY", ell, _m(nt, "LESS_THAN", Y, front + 0.045))
            mix.inputs[7].default_value = lips
        nt.links.new(fac, mix.inputs[0]); nt.links.new(mix.outputs[2], inp)

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--design", required=True); ap.add_argument("--hero", required=True); ap.add_argument("--out", required=True)
    ap.add_argument("--tex", type=int, default=1024); ap.add_argument("--tris", type=int, default=26000); ap.add_argument("--review", default="")
    ap.add_argument("--skip-bake", action="store_true")
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:])
    os.makedirs(a.out, exist_ok=True); bpy.context.scene.render.fps = G.FPS
    c = ubc.normalise_parts({d["id"]: d for d in json.load(open(a.design))}[a.hero])
    arm, hero = humanoid_upper(c)
    pel = arm.data.bones["pelvis"].head_local.copy(); M = SpiderMap(pel)
    drop = spider_rig(arm, M)
    cut = cut_lower(hero, arm, pel.z - 0.06 * M.S, set(drop))
    parts = spider_parts(c, arm, M)
    for x in bpy.context.selected_objects: x.select_set(False)
    for o in parts + [hero]: o.select_set(True)
    bpy.context.view_layer.objects.active = hero; bpy.ops.object.join(); hero = bpy.context.view_layer.objects.active
    for md in [m for m in hero.modifiers if m.type == "ARMATURE"][1:]: hero.modifiers.remove(md)
    hero.parent = arm
    paint_details(hero, arm, ubc.head_box(arm), c)
    raw = sum(len(p.vertices) - 2 for p in hero.data.polygons)
    tris = ubc.decimate_to(hero, a.tris)
    paths = {} if a.skip_bake else ubc.bake(hero, a.tex, a.out, a.hero)
    src = Source(upper_bones(arm))
    clips = {n: G.clip(n) for n in GAME_CLIPS}
    acts = bake_clips(arm, M, src, clips)
    for o in src.objs: bpy.data.objects.remove(o, do_unlink=True)
    kb = export(arm, hero, os.path.join(a.out, f"SB_Hero_{a.hero}_HD.glb"))
    for k, q in (("albedo", 88), ("normal", 92)):   # phones: JPEG albedo and normal, as ubc.py (saved by Blender: no Pillow needed)
        if k in paths:
            p = paths[k]; img = bpy.data.images.load(p); sc = bpy.context.scene; ims = sc.render.image_settings
            ims.file_format = "JPEG"; ims.quality = q; ims.color_mode = "RGB"
            img.save_render(p[:-4] + ".jpg", scene=sc); ims.file_format = "PNG"
            for im in bpy.data.images:   # the saved .blend (review renders) then reads the JPEG
                if im.filepath and os.path.abspath(bpy.path.abspath(im.filepath)) == os.path.abspath(p): im.filepath = p[:-4] + ".jpg"; im.reload()
            os.remove(p)
    print(f"[creature] {a.hero}: cut {cut} verts, {raw} -> {tris} tris, {len(arm.data.bones)} bones, clips {[(n, v[1]) for n, v in acts.items()]}, {kb} KB", flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(a.out, f"{a.hero}_creature.blend"))

if __name__ == "__main__":
    main()
