"""Crusk Brineclaw's own crab body and skeleton (owner direction 2026-11-27: custom creature bodies, like
Vessa). Not the UBC humanoid: a wide, low carapace on six crab legs, two huge claws as arms (the
trident gripped in the right pincer, the nautilus shell shield mounted on the left claw), eye stalks.

    bpyenv/bin/python crab.py -- --out DIR [--tex 1024] [--tris 25000] [--anims]

Bones (all under "root"): body (the shell; idles and bobs), Head (eye-stalk base, at eye height so the
game's height fitting works), eye_l/eye_r, claw_{l,r}_{1,2,3} (shoulder, forearm, hand) + claw_{l,r}_jaw
(the movable pincer finger), leg_{l,r}{1,2,3}_{a,b} (upper and lower segment of each walking leg).
The legs are meant to be posed procedurally (multi-leg IK: the shared creature-rig foundation); the
optional --anims keys a short idle, walk and attack for review previews only.
Writes SB_Hero_crusk_HD.glb and the _albedo/_normal/_glow textures (same bake as ubc.py).
"""
import bpy, bmesh, sys, os, math, argparse
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ubc_parts as P   # noqa: E402  (flat materials)
import kit_geo as G     # noqa: E402
import kit_props as KP  # noqa: E402
import ubc as U         # noqa: E402  (bind, decimate, bake, export)

PAL = {"shell": "#b8582a", "shell_dark": "#7a3418", "under": "#e8b080", "barnacle": "#d8d0c0", "weed": "#3a6a2a",
       "glow": "#5ae8ff", "teal": "#1e5a5a", "spike": "#e87a4a", "pearl": "#cfe8f0"}

BODY_Z = 0.8

def mat(k, **kw): return P.flat("C_" + k, P.hexc(PAL[k]), **kw)

def build_armature():
    bpy.ops.object.armature_add(location=(0, 0, 0)); arm = bpy.context.object; arm.name = "Armature"
    bpy.ops.object.mode_set(mode="EDIT"); eb = arm.data.edit_bones
    r = eb[0]; r.name = "root"; r.head = (0, 0, 0); r.tail = (0, 0.3, 0)
    def bone(name, h, t, parent):
        b = eb.new(name); b.head = h; b.tail = t; b.parent = eb[parent]; return b
    bone("body", (0, 0, BODY_Z - 0.1), (0, 0, BODY_Z + 0.2), "root")
    bone("Head", (0, -0.36, BODY_Z + 0.12), (0, -0.4, BODY_Z + 0.24), "body")
    for sx, s in ((1, "l"), (-1, "r")):
        bone("eye_" + s, (sx * 0.07, -0.42, BODY_Z + 0.16), (sx * 0.08, -0.46, BODY_Z + 0.27), "Head")
        sh = (sx * 0.3, -0.28, BODY_Z + 0.02); el = (sx * 0.52, -0.42, BODY_Z - 0.04); wr = (sx * 0.52, -0.7, BODY_Z + 0.02); tip = (sx * 0.5, -1.2, BODY_Z + 0.06)
        bone(f"claw_{s}_1", sh, el, "body"); bone(f"claw_{s}_2", el, wr, f"claw_{s}_1"); bone(f"claw_{s}_3", wr, tip, f"claw_{s}_2")
        bone(f"claw_{s}_jaw", (wr[0], wr[1] - 0.04, wr[2] - 0.05), (tip[0], tip[1] + 0.06, tip[2] - 0.09), f"claw_{s}_3")
        for k, y in enumerate((-0.12, 0.08, 0.27)):
            h0 = (sx * 0.34, y, BODY_Z - 0.08); kn = (sx * 0.66, y + 0.03 * (k - 1), BODY_Z + 0.06); ft = (sx * 0.86, y + 0.08 * (k - 1), 0.0)
            bone(f"leg_{s}{k + 1}_a", h0, kn, "body"); bone(f"leg_{s}{k + 1}_b", kn, ft, f"leg_{s}{k + 1}_a")
            bone(f"leg_{s}{k + 1}_tip", ft, (ft[0], ft[1] - 0.05, ft[2]), f"leg_{s}{k + 1}_b")   # foot marker for the shared leg planting (CreatureLegs)
    bpy.ops.object.mode_set(mode="OBJECT")
    return arm

def rb(arm, n):
    b = arm.data.bones[n]; return arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local

def build_meshes(arm, mats):
    out = []
    def add(o, w):
        o.name = "Kit_Crab"; out.append(U.bind(o, arm, w)); return o
    shell, dark, under = mats["shell"], mats["shell_dark"], mats["under"]
    C = Vector((0, 0.02, BODY_Z + 0.1))
    # carapace: a wide, low dome with a scalloped rim and a ridge, barnacles and seaweed
    prof = [(1.06, -0.05), (1.0, 0.0)] + [(math.cos(t * math.pi / 2), 0.32 * math.sin(t * math.pi / 2)) for t in [i / 10 for i in range(1, 11)]]
    dome = G.lathe(prof, shell, center=C, rx=0.6, ry=0.5, segs=40, rfun=lambda a, r, z: 1.0 + 0.05 * math.sin(a * 11))
    parts = [dome, G.sphere(C + Vector((0, 0, -0.06)), 1.0, under, segs=24, scale=(0.5, 0.42, 0.16))]
    rng = __import__("random").Random(7)
    for k in range(26):
        a = rng.uniform(0, 2 * math.pi); rr = rng.uniform(0.2, 0.9)
        p = C + Vector((math.cos(a) * rr * 0.58, math.sin(a) * rr * 0.48, 0.32 * math.sqrt(max(0, 1 - rr * rr)) - 0.005))
        parts.append(G.sphere(p, rng.uniform(0.014, 0.03), mats["barnacle"], segs=8, scale=(1, 1, 0.7)))
    for k in range(7):
        u = -0.75 + 0.25 * k; b0 = C + Vector((u * 0.55, 0.42, 0.12 - abs(u) * 0.1))
        parts.append(G.sweep(G.bezier(b0, b0 + Vector((0.02, 0.06, -0.15)), b0 + Vector((-0.02, 0.07, -0.34 - 0.05 * (k % 2))), n=8), [(0.02, 0.003)] * 9, mats["weed"], segs=5, side=Vector((1, 0, 0))))
    for k in range(9):   # glowing teal swirl studs across the shell
        a = -1.2 + k * 0.3; p = C + Vector((math.sin(a) * 0.42, -math.cos(a) * 0.3, 0.16))
        parts.append(G.sphere(p, 0.016, mats["glow"], segs=8, scale=(1, 1, 0.5)))
    for sx in (-1, 1):   # spiky front rim horns
        for k in range(3):
            b0 = C + Vector((sx * (0.25 + 0.12 * k), -0.42 + 0.05 * k, 0.04))
            parts.append(G.sweep([b0, b0 + Vector((sx * 0.04, -0.07, 0.04))], [0.022, 0.001], mats["spike"], segs=6))
    add(G.join(parts, "Kit_Crab"), {"body": 1.0})
    # face: mandibles under the front rim; eye stalks with glowing eyes
    hb, _ = rb(arm, "Head")
    face = [G.sphere(hb + Vector((0, 0.0, -0.02)), 1.0, dark, segs=16, scale=(0.14, 0.08, 0.08))]
    for sx in (-1, 1):
        m0 = hb + Vector((sx * 0.04, -0.06, -0.06))
        face.append(G.sweep(G.bezier(m0, m0 + Vector((sx * 0.01, -0.04, -0.02)), m0 + Vector((-sx * 0.02, -0.05, -0.05)), n=5), G.taper(6, 0.014, 0.003), under, segs=6))
    add(G.join(face, "Kit_Crab"), {"Head": 1.0})
    for s in ("l", "r"):
        e0, e1 = rb(arm, "eye_" + s)
        add(G.join([G.sweep([e0, e1], [0.018, 0.014], dark, segs=8), G.sphere(e1, 0.03, mats["glow"], segs=12)], "Kit_Crab"), {"eye_" + s: 1.0})
    # walking legs: two tapered, spiny segments each
    for s in ("l", "r"):
        for k in (1, 2, 3):
            for seg, r0, r1 in (("a", 0.075, 0.065), ("b", 0.065, 0.016)):
                h, t = rb(arm, f"leg_{s}{k}_{seg}")
                pts = [h.lerp(t, i / 6) for i in range(7)]
                o = G.sweep(pts, [(r0 + (r1 - r0) * i / 6) for i in range(7)], shell, segs=10, ridge=0.08, ridges=3)
                bits = [o]
                for j in ((2, 4) if seg == "a" else (2,)): bits.append(G.sweep([pts[j], pts[j] + Vector((0, 0, 0.05))], [0.016, 0.001], mats["spike"], segs=5))
                add(G.join(bits, "Kit_Crab"), {f"leg_{s}{k}_{seg}": 1.0})
    # claw arms: thick shell segments; the hand is a huge pincer (fixed finger) + the jaw finger
    for s, sx in (("l", 1), ("r", -1)):
        for i, (r0, r1) in enumerate(((0.1, 0.12), (0.12, 0.15))):
            h, t = rb(arm, f"claw_{s}_{i + 1}")
            pts = [h.lerp(t, j / 6) for j in range(7)]
            bits = [G.sweep(pts, [r0 + (r1 - r0) * j / 6 for j in range(7)], shell, segs=12, ridge=0.06, ridges=3)]
            for j in (2, 4): bits.append(G.sweep([pts[j], pts[j] + Vector((sx * 0.03, 0, 0.06))], [0.016, 0.001], mats["spike"], segs=5))
            add(G.join(bits, "Kit_Crab"), {f"claw_{s}_{i + 1}": 1.0})
        h, t = rb(arm, f"claw_{s}_3")
        d = (t - h).normalized()
        palm = G.sphere(h + d * 0.16, 1.0, shell, segs=20, scale=(0.22, 0.28, 0.2))
        fixed = G.sweep(G.bezier(h + d * 0.2 + Vector((0, 0, 0.04)), h + d * 0.34 + Vector((0, 0, 0.07)), t + Vector((0, 0, 0.01)), n=10),
                        [(0.14 * (1 - 0.9 * (i / 10) ** 1.3), 0.1 * (1 - 0.85 * i / 10)) for i in range(11)], shell, segs=12, side=Vector((1, 0, 0)))
        add(G.join([palm, fixed], "Kit_Crab"), {f"claw_{s}_3": 1.0})
        j0, j1 = rb(arm, f"claw_{s}_jaw")
        jaw = G.sweep(G.bezier(j0, j0.lerp(j1, 0.5) + Vector((0, 0, -0.06)), j1, n=8), [(0.1 * (1 - 0.9 * i / 8), 0.075 * (1 - 0.85 * i / 8)) for i in range(9)], under, segs=10, side=Vector((1, 0, 0)))
        add(jaw, {f"claw_{s}_jaw": 1.0})
    return out

def weapons(arm, mats):
    """The trident in the right pincer; the nautilus shell shield on the outside of the left claw."""
    out = []
    km = {"metal": P.flat("K_Metal", P.hexc("#b88a4a"), metal=0.9, rough=0.3), "acc": mats["teal"], "main": mats["shell"], "glow": mats["glow"],
          "second": mats["teal"], "white": mats["pearl"], "leather": P.flat("K_Leather", P.hexc("#5a3b26"), rough=0.75)}
    h, t = rb(arm, "claw_r_3")
    c = {"id": "crusk", "weapon": "short bronze trident", "kit": ["custom:short bronze trident"], "outfit": {}}
    o = KP.build_weapon(c, km) if hasattr(KP, "build_weapon") else None
    if o is None:
        sh = G.sweep([h + Vector((0, 0.05, -0.55)), h + Vector((0, -0.12, 0.9))], 0.016, km["metal"], segs=8)
        prongs = [G.sweep([h + Vector((dx, -0.12, 0.9)), h + Vector((dx * 1.4, -0.14, 1.1))], [0.01, 0.002], km["metal"], segs=6) for dx in (-0.05, 0, 0.05)]
        o = G.join([sh] + prongs, "Kit_Weapon")
    else:
        o.matrix_world = Matrix.Translation(h + (t - h) * 0.35 + Vector((0, 0, -0.1))) @ Matrix.Diagonal((0.75, 0.75, 0.75, 1)) @ o.matrix_world; U.apply_xform(o)
    o.name = "Kit_Weapon"; out.append(U.bind(o, arm, {"claw_r_3": 1.0}))
    h, t = rb(arm, "claw_l_2")
    sc = {"id": "crusk", "weapon": "spiral nautilus shell shield with glowing pearls", "kit": ["custom:spiral nautilus shell shield with glowing pearls"], "outfit": {}}
    s = KP.build_shield(sc, km)
    along = (t - h).normalized(); out_dir = Vector((1, 0, 0.3)).normalized()
    out_dir = (out_dir - along * out_dir.dot(along)).normalized(); xax = along.cross(out_dir).normalized()
    M = Matrix((xax, along, out_dir)).transposed().to_4x4(); M.translation = h + (t - h) * 0.5 + out_dir * 0.14
    s.matrix_world = M @ Matrix.Diagonal((1.35, 1.35, 1.35, 1)) @ s.matrix_world; U.apply_xform(s); s.name = "Kit_Shield"
    out.append(U.bind(s, arm, {"claw_l_2": 1.0}))
    return out

def key_anims(arm):
    """Review-only clips: Idle (bob, leg shuffle, claws flex), Walk (alternating tripod), Attack (trident
    thrust, left claw snap). The game drives the legs procedurally."""
    bpy.context.view_layer.objects.active = arm; bpy.ops.object.mode_set(mode="POSE")
    pb = arm.pose.bones
    for p in pb: p.rotation_mode = "XYZ"
    def clip(name, n, fn):
        act = bpy.data.actions.new(name); arm.animation_data_create(); arm.animation_data.action = act
        for f in range(n + 1):
            for p in pb: p.rotation_euler = (0, 0, 0); p.location = (0, 0, 0)
            fn(f / n)
            for p in pb:
                p.keyframe_insert("rotation_euler", frame=f); p.keyframe_insert("location", frame=f)
        return act
    TAU = 2 * math.pi
    def idle(u):
        pb["body"].location = (0, 0, 0.012 * math.sin(u * TAU)); pb["body"].rotation_euler = (0.03 * math.sin(u * TAU), 0, 0)
        for s, sx in (("l", 1), ("r", -1)):
            pb[f"claw_{s}_jaw"].rotation_euler = (0.25 + 0.2 * math.sin(u * TAU * 2 + sx), 0, 0)
            pb[f"claw_{s}_1"].rotation_euler = (0.05 * math.sin(u * TAU + sx), 0, 0)
            pb[f"eye_{s}"].rotation_euler = (0.15 * math.sin(u * TAU * 1.5 + sx), 0, 0.1 * math.sin(u * TAU))
    def walk(u):
        pb["body"].location = (0.015 * math.sin(u * TAU * 2), 0, 0.01 * math.sin(u * TAU * 4))
        for s, sx in (("l", 1), ("r", -1)):
            for k in (1, 2, 3):
                ph = u * TAU * 2 + (0 if (k + (s == "l")) % 2 else math.pi)
                pb[f"leg_{s}{k}_a"].rotation_euler = (0.3 * math.sin(ph), 0, -sx * 0.25 * max(0, math.cos(ph)))
                pb[f"leg_{s}{k}_b"].rotation_euler = (0.15 * math.sin(ph), 0, 0)
    def attack(u):
        e = math.sin(min(1, u * 1.6) * math.pi)
        pb["body"].rotation_euler = (-0.12 * e, 0, 0.1 * e)
        pb["claw_r_1"].rotation_euler = (-0.9 * e, 0, -0.3 * e); pb["claw_r_2"].rotation_euler = (0.5 * e, 0, 0)
        pb["claw_l_1"].rotation_euler = (-0.4 * e, 0, 0.2 * e); pb["claw_l_jaw"].rotation_euler = (0.7 * e, 0, 0)
    acts = [clip("Crab_Idle", 24, idle), clip("Crab_Walk", 24, walk), clip("Crab_Attack", 18, attack)]
    bpy.ops.object.mode_set(mode="OBJECT")
    return acts

def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--out", required=True); ap.add_argument("--tex", type=int, default=1024)
    ap.add_argument("--tris", type=int, default=25000); ap.add_argument("--anims", action="store_true")
    ap.add_argument("--preview", default="", help="render review frames (front, three-quarter, back + an idle/walk/attack strip) into this dir")
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:]); os.makedirs(a.out, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = {k: mat(k, rough=0.5) for k in PAL}
    mats["glow"] = P.flat("C_glow", P.hexc(PAL["glow"]), emit=4)
    arm = build_armature()
    pieces = build_meshes(arm, mats) + weapons(arm, mats)
    for x in bpy.context.selected_objects: x.select_set(False)
    for o in pieces: o.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]; bpy.ops.object.join()
    hero = bpy.context.view_layer.objects.active; hero.name = "SB_Hero_crusk"
    mw = hero.matrix_world.copy(); hero.parent = arm; hero.matrix_world = mw   # one root node (Armature): clip paths resolve from the holder
    for md in [m for m in hero.modifiers if m.type == "ARMATURE"][1:]: hero.modifiers.remove(md)
    raw = sum(len(p.vertices) - 2 for p in hero.data.polygons)
    tris = U.decimate_to(hero, a.tris)
    paths = U.bake(hero, a.tex, a.out, "crusk")
    kb = U.export(arm, hero, os.path.join(a.out, "SB_Hero_crusk_HD.glb"))
    print(f"[crab] crusk: {raw} -> {tris} tris, {len(arm.data.bones)} bones, {kb} KB")
    if a.preview: preview(arm, hero, a.out, a.preview)

def preview(arm, hero, tex_dir, out):
    os.makedirs(out, exist_ok=True)
    m = bpy.data.materials.new("T"); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = 0.6
    ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = bpy.data.images.load(os.path.join(tex_dir, "SB_Hero_crusk_HD_albedo.png")); nt.links.new(ta.outputs[0], b.inputs["Base Color"])
    tg = nt.nodes.new("ShaderNodeTexImage"); tg.image = bpy.data.images.load(os.path.join(tex_dir, "SB_Hero_crusk_HD_glow.png")); nt.links.new(tg.outputs[0], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 1.5
    hero.data.materials.clear(); hero.data.materials.append(m)
    sc = bpy.context.scene; sc.render.engine = "CYCLES"
    w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.13, 0.11, 0.17, 1)
    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(48), 0, math.radians(-35))); bpy.context.object.data.energy = 3.2
    bpy.ops.object.light_add(type="SUN", rotation=(math.radians(70), 0, math.radians(160))); bpy.context.object.data.energy = 1.4
    bpy.ops.object.camera_add(); cam = bpy.context.object; sc.camera = cam; cam.data.lens = 50
    def shoot(yaw, path, res=(700, 1000), samples=20):
        sc.cycles.samples = samples; sc.render.resolution_x, sc.render.resolution_y = res
        r = 5.0; a_ = math.radians(yaw); cam.location = (math.sin(a_) * r, -math.cos(a_) * r, 1.2)
        d = Vector((0, -0.25, 0.72)) - cam.location; cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = path; bpy.ops.render.render(write_still=True)
    for tag, yaw in (("front", 0), ("three", 35), ("back", 180)): shoot(yaw, os.path.join(out, f"crusk_{tag}.png"))
    acts = key_anims(arm)
    k = 0
    for act, n in zip(acts, (12, 12, 12)):
        arm.animation_data.action = act; fr = act.frame_range
        for i in range(n):
            sc.frame_set(int(fr[0] + (fr[1] - fr[0]) * i / max(1, n - 1)))
            shoot(40, os.path.join(out, f"crusk_anim_{k:03d}.png"), res=(360, 480), samples=8); k += 1

if __name__ == "__main__":
    main()
