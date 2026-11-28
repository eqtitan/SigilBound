"""Labelled preview renders of the Titan's moves (titan_moves.py) for owner review: the shipped GLB and its own clips
in a stand-in arena seen from the battle camera, with the body effects the game adds (seam and core flare in the
wind-up, the chest-core breath, ridge glow, crystals breaking off at the health stages, shock rings).

  bpyenv/bin/python titan_preview.py -- HD_DIR OUT_DIR MODE [ELEMENT]
    HD_DIR   holds SB_Hero_titan_HD.glb, _albedo.jpg, _glow.png, titan.json and titan_clips.json
    MODE     poses (a sheet of key frames per clip, side view) | fists | leap | breath | sweep | collapse | ridges
Frames are written as OUT_DIR/<mode>/f####.png; titan_gif.py labels them and builds the GIFs and the contact sheet.
Positions follow the game (Unity metres mapped to Blender: x, z, y): the Titan stands at z 5.75 (BattleLayout
TitanSlot), the team on the near rim around z -3.3, the worm's-eye battle camera behind them.
"""
import bpy, sys, os, json, math
from mathutils import Vector, Euler, Matrix

argv = sys.argv[sys.argv.index("--") + 1:]
HD, OUT, MODE = argv[0], argv[1], argv[2]
EL = argv[3] if len(argv) > 3 else "Flame"
spec = json.load(open(os.path.join(HD, "titan.json"))); clips = json.load(open(os.path.join(HD, "titan_clips.json")))
SEAM = spec["seams"][EL]
FPS = 24

def lin(h, a=1.0):
    h = h.lstrip("#"); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [a]

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene; sc.render.fps = FPS
bpy.ops.import_scene.gltf(filepath=os.path.join(HD, "SB_Hero_titan_HD.glb"))
arm = next(o for o in sc.objects if o.type == "ARMATURE"); body = next(o for o in sc.objects if o.type == "MESH")
K = spec["head"] / clips["headBoneY"]                       # model metres -> battle metres
HOLDER = bpy.data.objects.new("TitanHolder", None); sc.collection.objects.link(HOLDER)
arm.parent = HOLDER; arm.scale = (K, K, K); arm.location = (0, 0, -clips["ground"] * K)
TITAN_HOME = Vector((0, 5.75, 0))
HOLDER.location = TITAN_HOME
ALLIES = [Vector((x, -3.35 + 0.18 * abs(x), 0)) for x in (-3.3, -1.1, 1.1, 3.3)]
TEAM = Vector((0, -2.24, 0))

# ---- the Titan's material: albedo, seams (glow RGB) x element x gain, ridges (255 - glow alpha) x element x ridge gain
m = bpy.data.materials.new("Titan"); m.use_nodes = True; nt = m.node_tree; N = nt.nodes; Lk = nt.links.new
bsdf = N["Principled BSDF"]; bsdf.inputs["Roughness"].default_value = 0.75
ta = N.new("ShaderNodeTexImage"); ta.image = bpy.data.images.load(os.path.join(HD, "SB_Hero_titan_HD_albedo.jpg")); Lk(ta.outputs["Color"], bsdf.inputs["Base Color"])
tg = N.new("ShaderNodeTexImage"); tg.image = bpy.data.images.load(os.path.join(HD, "SB_Hero_titan_HD_glow.png")); tg.image.colorspace_settings.name = "Non-Color"
tg.image.alpha_mode = "CHANNEL_PACKED"
bw = N.new("ShaderNodeRGBToBW"); Lk(tg.outputs["Color"], bw.inputs[0])
inv = N.new("ShaderNodeMath"); inv.operation = "SUBTRACT"; inv.inputs[0].default_value = 1.0; Lk(tg.outputs["Alpha"], inv.inputs[1])
seamc = N.new("ShaderNodeRGB"); seamc.outputs[0].default_value = lin(SEAM)
seamg = N.new("ShaderNodeValue"); seamg.name = "SeamGain"; seamg.outputs[0].default_value = spec["glowGain"]
ridgeg = N.new("ShaderNodeValue"); ridgeg.name = "RidgeGain"; ridgeg.outputs[0].default_value = 0.0
def mulf(a, b):
    n = N.new("ShaderNodeMath"); n.operation = "MULTIPLY"; Lk(a, n.inputs[0]); Lk(b, n.inputs[1]); return n.outputs[0]
two = N.new("ShaderNodeMath"); two.operation = "MULTIPLY"; Lk(bw.outputs[0], two.inputs[0]); two.inputs[1].default_value = 2.0  # MaskFromGlow: lum x 2
s1 = mulf(two.outputs[0], seamg.outputs[0])
r1 = mulf(inv.outputs[0], ridgeg.outputs[0])
tot = N.new("ShaderNodeMath"); tot.operation = "ADD"; Lk(s1, tot.inputs[0]); Lk(r1, tot.inputs[1])
Lk(seamc.outputs[0], bsdf.inputs["Emission Color"]); Lk(tot.outputs[0], bsdf.inputs["Emission Strength"])
body.data.materials.clear(); body.data.materials.append(m)

def key_value(node, frame, v):
    node.outputs[0].default_value = v; node.outputs[0].keyframe_insert("default_value", frame=frame)

# ---- arena
def emit_mat(name, hexc, strength, alpha=1.0):
    mm = bpy.data.materials.new(name); mm.use_nodes = True; n = mm.node_tree.nodes; n.remove(n["Principled BSDF"])
    e = n.new("ShaderNodeEmission"); e.inputs[0].default_value = lin(hexc); e.inputs[1].default_value = strength
    tr = n.new("ShaderNodeBsdfTransparent"); mx = n.new("ShaderNodeMixShader"); mx.inputs[0].default_value = alpha
    mm.node_tree.links.new(tr.outputs[0], mx.inputs[1]); mm.node_tree.links.new(e.outputs[0], mx.inputs[2])
    mm.node_tree.links.new(mx.outputs[0], n["Material Output"].inputs[0]); mm.blend_method = "BLEND" if hasattr(mm, "blend_method") else None
    return mm, e, mx
def solid_mat(name, hexc, rough=0.9):
    mm = bpy.data.materials.new(name); mm.use_nodes = True; b = mm.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = lin(hexc); b.inputs["Roughness"].default_value = rough; return mm
bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 2, 0)); g = bpy.context.object; g.data.materials.append(solid_mat("Ground", "#3a302a"))
bpy.ops.mesh.primitive_circle_add(vertices=96, radius=4.1, fill_type="NGON", location=(0, 0.6, 0.01)); rim = bpy.context.object
rim.scale = (1, 3.95 / 4.1, 1); rim.data.materials.append(solid_mat("Floor", "#55483d"))
hero_mat = solid_mat("Heroes", "#2c3440", 0.6)
for a in ALLIES:
    bpy.ops.mesh.primitive_cylinder_add(radius=0.28, depth=1.2, location=a + Vector((0, 0, 0.6))); bpy.context.object.data.materials.append(hero_mat)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.24, location=a + Vector((0, 0, 1.45))); bpy.context.object.data.materials.append(hero_mat)
w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.16, 0.12, 0.13, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
sun = bpy.data.lights.new("Key", "SUN"); sun.energy = 2.6; so = bpy.data.objects.new("Key", sun); sc.collection.objects.link(so)
so.rotation_euler = Euler((math.radians(50), 0, math.radians(200)))
fill = bpy.data.lights.new("Fill", "SUN"); fill.energy = 0.8; fo = bpy.data.objects.new("Fill", fill); sc.collection.objects.link(fo)
fo.rotation_euler = Euler((math.radians(60), 0, math.radians(20)))

sc.render.engine = "CYCLES"; sc.cycles.samples = int(os.environ.get("SB_SAMPLES", "6")); sc.render.resolution_percentage = int(os.environ.get("SB_PCT", "80")); sc.cycles.device = "CPU"; sc.cycles.use_denoising = True
sc.cycles.max_bounces = 3; sc.view_settings.view_transform = "AgX" if MODE != "poses" else "Standard"
# bloom (the game renders bloom on its seams): compositor glare
sc.use_nodes = True if hasattr(sc, "use_nodes") else None
try:
    ct = sc.node_tree; rl = ct.nodes.get("Render Layers") or ct.nodes.new("CompositorNodeRLayers"); co = ct.nodes.get("Composite") or ct.nodes.new("CompositorNodeComposite")
    gl = ct.nodes.new("CompositorNodeGlare"); gl.glare_type = "FOG_GLOW"; gl.threshold = 1.0; gl.size = 7
    ct.links.new(rl.outputs[0], gl.inputs[0]); ct.links.new(gl.outputs[0], co.inputs[0])
except Exception as e:
    print("no compositor glare:", e)

cam = bpy.data.cameras.new("Cam"); co_ = bpy.data.objects.new("Cam", cam); sc.collection.objects.link(co_); sc.camera = co_
def look(eye, target, fov_v, res):
    sc.render.resolution_x, sc.render.resolution_y = res
    co_.location = Vector(eye); cam.sensor_fit = "VERTICAL"; cam.angle_y = math.radians(fov_v)
    co_.rotation_euler = (Vector(target) - Vector(eye)).to_track_quat("-Z", "Y").to_euler()

def bone_world(name):
    pb = arm.pose.bones.get(name)
    return arm.matrix_world @ pb.head if pb else None

# ---- NLA sequencing: play clips back to back on the armature (speeds as the game plays them)
ad = arm.animation_data or arm.animation_data_create()
for t in list(ad.nla_tracks): ad.nla_tracks.remove(t)
ad.action = None
TRACKS = [ad.nla_tracks.new(), ad.nla_tracks.new()]
def seq(clip, start, speed=1.0, hold_until=None, track=0, blend_in=3):
    act = bpy.data.actions[clip]; L = clips["clips"][clip]["len"]
    st = TRACKS[track].strips.new(clip + str(start), int(start), act)
    st.action_frame_start = 0; st.action_frame_end = round(L * 30)  # the baked keys are 30 fps
    st.scale = FPS / 30.0 / speed
    st.blend_in = blend_in if start > 1 else 0
    st.extrapolation = "HOLD_FORWARD" if hold_until else "NOTHING"
    st.use_auto_blend = False
    return start + L * FPS / speed

def ev(clip, name): return clips["clips"][clip]["events"][name] * clips["clips"][clip]["len"]
def frames_for(clip, speed=1.0): return clips["clips"][clip]["len"] * FPS / speed

FR = {"cur": 1}
def render_range(f0, f1, step=1):
    d = os.path.join(OUT, MODE); os.makedirs(d, exist_ok=True)
    for f in range(int(f0), int(f1) + 1, step):
        sc.frame_set(f); sc.render.filepath = os.path.join(d, f"f{f:04d}.png"); bpy.ops.render.render(write_still=True)

# ---- effects (all keyed on frames)
def key_holder(frame, loc):
    HOLDER.location = Vector(loc); HOLDER.keyframe_insert("location", frame=frame)
def flare(f_start, f_peak, f_end, peak=3.2, base=None):
    base = spec["glowGain"] if base is None else base
    key_value(seamg, f_start, base); key_value(seamg, f_peak, peak); key_value(seamg, f_end, base)
def ridge_glow(f_start, f_peak, f_end, peak=2.6):
    key_value(ridgeg, f_start, 0.0); key_value(ridgeg, f_peak, peak); key_value(ridgeg, f_end, 0.0)
core_mat, core_e, core_mx = emit_mat("Core", SEAM, 0.0, 0.0)
bpy.ops.mesh.primitive_uv_sphere_add(radius=0.11, segments=24, ring_count=12); core = bpy.context.object; core.data.materials.append(core_mat)
cc = core.constraints.new("CHILD_OF"); cc.target = arm; cc.subtarget = "chest_core"
core.matrix_parent_inverse = Matrix(); cc.inverse_matrix = Matrix()
def core_glow(frame, strength, alpha):
    core_e.inputs[1].default_value = strength; core_e.inputs[1].keyframe_insert("default_value", frame=frame)
    core_mx.inputs[0].default_value = alpha; core_mx.inputs[0].keyframe_insert("default_value", frame=frame)
core_glow(1, 0, 0)

def shock(frame, pos, radius, color=SEAM, dur=12):
    mm, e, mx = emit_mat("Shock", color, 6.0, 0.9)
    bpy.ops.mesh.primitive_torus_add(major_radius=1.0, minor_radius=0.06, location=(pos[0], pos[1], 0.15)); o = bpy.context.object; o.data.materials.append(mm)
    o.scale = (0.01, 0.01, 1); o.keyframe_insert("scale", frame=frame - 1)
    o.scale = (0.2, 0.2, 1); o.keyframe_insert("scale", frame=frame)
    o.scale = (radius, radius, 3); o.keyframe_insert("scale", frame=frame + dur)
    mx.inputs[0].default_value = 0; mx.inputs[0].keyframe_insert("default_value", frame=frame - 1)
    mx.inputs[0].default_value = 0.95; mx.inputs[0].keyframe_insert("default_value", frame=frame)
    mx.inputs[0].default_value = 0; mx.inputs[0].keyframe_insert("default_value", frame=frame + dur)
    # a molten crack decal: a flat emissive star under the impact
    mm2, e2, mx2 = emit_mat("Crack", color, 4.0, 0.0)
    bpy.ops.mesh.primitive_circle_add(vertices=10, radius=1.0, fill_type="TRIFAN", location=(pos[0], pos[1], 0.03)); c = bpy.context.object
    c.data.materials.append(mm2)
    for i, v in enumerate(c.data.vertices):
        if i < 10: v.co *= (1.0 if i % 2 == 0 else 0.35)
    c.scale = (radius * 0.45,) * 3
    for fr, a in ((frame - 1, 0), (frame, 0.9), (frame + dur * 3, 0.5), (frame + dur * 6, 0)):
        mx2.inputs[0].default_value = a; mx2.inputs[0].keyframe_insert("default_value", frame=fr)

def dust(frame, pos, n=14, radius=2.5):
    mm = solid_mat("Dust", "#8a7a66");
    for i in range(n):
        a = i / n * math.tau; d = Vector((math.cos(a), math.sin(a), 0))
        bpy.ops.mesh.primitive_ico_sphere_add(radius=0.2, subdivisions=2, location=(pos[0], pos[1], 0.2)); o = bpy.context.object; o.data.materials.append(mm)
        o.scale = (0.01,) * 3; o.keyframe_insert("scale", frame=frame - 1); o.keyframe_insert("location", frame=frame - 1)
        o.scale = (0.6,) * 3; o.keyframe_insert("scale", frame=frame)
        o.location = Vector((pos[0], pos[1], 0.25)) + d * radius; o.scale = (1.0,) * 3; o.keyframe_insert("location", frame=frame + 14); o.keyframe_insert("scale", frame=frame + 14)
        o.scale = (0.01,) * 3; o.keyframe_insert("scale", frame=frame + 22)

def ridge_break(frame, stage):
    """Crystals of `stage` shrink to stubs and their shards fly off (the game: TitanBody.SetHealth)."""
    shard_m, se, _ = emit_mat("Shard", SEAM, 2.0, 1.0)
    rock = solid_mat("Crystal", "#8d8f94", 0.4)
    for b in arm.pose.bones:
        if not (b.name.startswith("ridge_") and b.name.endswith(f"_s{stage}")): continue
        b.scale = (1, 1, 1); b.keyframe_insert("scale", frame=frame - 1)
        b.scale = (0.3, 0.3, 0.3); b.keyframe_insert("scale", frame=frame + 2)
        sc.frame_set(frame); p = arm.matrix_world @ b.head
        out = (p - (HOLDER.location + Vector((0, 0, p.z)))).normalized() + Vector((0, 0, 0.8))
        for k in range(3):
            bpy.ops.mesh.primitive_ico_sphere_add(radius=0.16, subdivisions=1, location=p); o = bpy.context.object
            o.data.materials.append(rock if k else shard_m); o.scale = (0.01,) * 3; o.keyframe_insert("scale", frame=frame - 1)
            o.scale = (1, 1.6, 1); o.keyframe_insert("scale", frame=frame)
            o.keyframe_insert("location", frame=frame)
            v = out * (3.0 + k) + Vector(((k - 1) * 1.2, (k - 1) * 0.5, 0))
            for s in range(1, 13):
                t = s / FPS; o.location = p + v * t + Vector((0, 0, -4.9 * t * t)); o.keyframe_insert("location", frame=frame + s)
            o.rotation_euler = (3 * k, 2, 1); o.keyframe_insert("rotation_euler", frame=frame + 12)
            o.scale = (0.01,) * 3; o.keyframe_insert("scale", frame=frame + 13)

def beam(f0, f1, a, b, f2=None, c=None):
    """The breath: a cone of fire from the chest socket, its end sweeping a -> b (and back to c)."""
    tgt = bpy.data.objects.new("BeamTarget", None); sc.collection.objects.link(tgt)
    tgt.location = a; tgt.keyframe_insert("location", frame=f0); tgt.location = b; tgt.keyframe_insert("location", frame=f1)
    if f2: tgt.location = c; tgt.keyframe_insert("location", frame=f2)
    mm, e, mx = emit_mat("Beam", SEAM, 3.0, 0.0)
    bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=0.35, radius2=1.1, depth=1.0); cone = bpy.context.object; cone.data.materials.append(mm)
    for v in cone.data.vertices: v.co.z += 0.5      # base at the origin, opening along +Z
    cone.rotation_mode = "QUATERNION"
    end = f2 or f1
    for f in range(int(f0) - 1, int(end) + 2):
        sc.frame_set(f); p = bone_world("chest_core"); q = tgt.matrix_world.translation
        d = q - p; cone.location = p; cone.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d)
        cone.scale = (1, 1, d.length); cone.keyframe_insert("location", frame=f); cone.keyframe_insert("rotation_quaternion", frame=f); cone.keyframe_insert("scale", frame=f)
    for fr, al in ((f0 - 1, 0), (f0 + 2, 0.6), (end - 3, 0.6), (end + 1, 0)):
        mx.inputs[0].default_value = al; mx.inputs[0].keyframe_insert("default_value", frame=fr)
    mm2, e2, mx2 = emit_mat("Splash", SEAM, 3.0, 0.0)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.9, segments=16, ring_count=8); sp = bpy.context.object; sp.data.materials.append(mm2)
    sp.scale = (1, 1, 0.35); cpy = sp.constraints.new("COPY_LOCATION"); cpy.target = tgt
    for fr, al in ((f0 - 1, 0), (f0 + 3, 0.7), (end - 3, 0.7), (end + 1, 0)):
        mx2.inputs[0].default_value = al; mx2.inputs[0].keyframe_insert("default_value", frame=fr)

# battle camera (worm's-eye, the default Titan view): low behind the team, looking up at the Titan
WORM = dict(eye=(0, -8.2, 0.9), target=(0, 5.75, 3.4), fov=58, res=(360, 640))
SIDE = dict(eye=(13.5, 0.6, 1.8), target=(0, 1.2, 2.6), fov=40, res=(640, 360))

def bake_all():
    """Holder location keys are linear between keys; strips are baked into the frame range by Cycles directly."""
    for fc in (HOLDER.animation_data.action.fcurves if HOLDER.animation_data and HOLDER.animation_data.action and hasattr(HOLDER.animation_data.action, "fcurves") else []):
        for k in fc.keyframe_points: k.interpolation = "LINEAR"

if MODE == "poses":
    # a sheet: every clip at 6 moments, side view, Titan at the origin
    HOLDER.location = Vector((0, 0, 0)); sc.cycles.samples = int(os.environ.get("SB_SAMPLES", "6"))
    side = os.environ.get("SB_VIEW", "34")   # "side" = profile from its right, else a three-quarter front view
    look((-16, -1.0, 3.4) if side == "side" else (11.5, -12.0, 3.6), (0, 0.6, 3.0), 44, (260, 260))
    d = os.path.join(OUT, "poses"); os.makedirs(d, exist_ok=True)
    for name, c in clips["clips"].items():
        ad.action = bpy.data.actions[name]
        if hasattr(ad, "action_slot") and ad.action.slots: ad.action_slot = ad.action.slots[0]
        n = 6
        for i in range(n):
            t = c["len"] * i / (n - 1) * 0.999; sc.frame_set(int(round(t * 30)) + 0)
            sc.render.filepath = os.path.join(d, f"{name}_{i}.png"); bpy.ops.render.render(write_still=True)
    sys.exit(0)

# ---------------------------------------------------------------- the moves, as the battle stages them
log = {"mode": MODE, "fps": FPS, "phases": []}
def phase(f, label): log["phases"].append([int(f), label])

if MODE == "fists":   # Bedrock Hammer: knuckle-walk in, wind-up flare, two-fist slam on the target, recovery
    target = ALLIES[1]; d_ = (TITAN_HOME - target); d_.z = 0; d_.normalize()
    dest = target + d_ * spec["reach"]
    f = 1; phase(f, "approach (knuckle-walk)")
    walk_frames = int(1.5 * FPS)
    for i in range(0, walk_frames + 1, 2):
        u = i / walk_frames; u = u * u * (3 - 2 * u); key_holder(f + i, TITAN_HOME.lerp(dest, u))
    s = f
    while s < f + walk_frames: s = seq("Titan_Walk", s, 0.8)
    f2 = f + walk_frames
    sl = clips["clips"]["Titan_Slam"]; L = sl["len"]
    imp = f2 + ev("Titan_Slam", "impact") * FPS
    seq("Titan_Slam", f2, 1.0, hold_until=True, track=1, blend_in=4)
    flare(f2, f2 + ev("Titan_Slam", "top") * FPS, imp + 10); ridge_glow(f2 + 4, imp - 2, imp + 18)
    core_glow(f2, 0, 0); core_glow(imp - 4, 5, 0.55); core_glow(imp + 10, 0, 0)
    phase(f2, "wind-up: seams and core flare"); phase(imp, "IMPACT (fists connect)"); phase(imp + 6, "recovery")
    sc.frame_set(int(imp)); fl = (bone_world("fist_l") + bone_world("fist_r")) / 2
    shock(int(imp), fl, 4.0); dust(int(imp), fl)
    log["contact_vs_target_m"] = round(((fl - target) * Vector((1, 1, 0))).length, 2)
    look((6.5, -11.5, 3.0), (0.4, 0.6, 3.4), 60, WORM["res"])   # the battle view pulled back: the whole Titan and its target
    end = f2 + L * FPS + 4
elif MODE == "leap":   # Faultline Leap: crouch on the knuckles, spring, fly, land on the team with a quake
    f = 1; seq("Titan_Leap", f, 1.0, hold_until=True)
    to = f + ev("Titan_Leap", "takeoff") * FPS
    flare(f, to, to + 30, 2.8); ridge_glow(f + 4, to, to + 30)
    key_holder(f, TITAN_HOME); key_holder(int(to), TITAN_HOME)
    rise, fall = int(0.6 / 0.4 * FPS * 0.55), int(0.5 * FPS)        # the rise plays in slow motion in the game
    land_at = int(to) + rise + fall; apex = 12.0
    for i in range(0, rise + fall + 1):
        k = 0.5 * (1 - (1 - i / rise) ** 2) if i <= rise else 0.5 + 0.5 * ((i - rise) / fall) ** 2
        p = TITAN_HOME.lerp(TEAM, k) + Vector((0, 0, 4 * apex * k * (1 - k))); key_holder(int(to) + i, p)
    lstart = land_at - ev("Titan_Land", "impact") * FPS
    seq("Titan_Land", lstart, 1.0, hold_until=True, track=1, blend_in=2)
    phase(f, "wind-up: crouch on the knuckles, seams flare"); phase(to, "takeoff"); phase(land_at, "IMPACT (lands on the team)"); phase(land_at + 6, "recovery (aftershocks)")
    shock(land_at, TEAM, 6.0); dust(land_at, TEAM, 18, 3.5)
    for k in range(3): shock(land_at + 6 * (k + 1), TEAM, 5 + 2 * k, dur=10)
    look((4.5, -17.5, 2.2), (0, 1.0, 6.5), 66, WORM["res"])   # pulled back to keep the whole arc and the landing in view
    end = lstart + frames_for("Titan_Land") + 2
elif MODE == "breath":   # Heartfire Torrent: gather (core flares), chest thrust, the torrent sweeps the team and back
    f = 1; seq("Titan_Breath", f, 1.0, hold_until=True)
    rel = f + ev("Titan_Breath", "release") * FPS; sw2 = f + ev("Titan_Breath", "sweep2") * FPS; endb = f + ev("Titan_Breath", "end") * FPS
    flare(f, rel, endb + 8, 3.4); ridge_glow(f + 10, rel, endb + 8, 2.2)
    core_glow(f, 0, 0); core_glow(rel - 2, 6, 0.75); core_glow(endb, 5, 0.6); core_glow(endb + 8, 0, 0)
    a = ALLIES[-1] + Vector((0.6, 0, 0.6)); b = ALLIES[0] + Vector((-0.6, 0, 0.6))
    beam(int(rel), int(sw2), a, b, int(endb), a)
    phase(f, "wind-up: chest core swells"); phase(rel, "RELEASE from the chest core"); phase(sw2, "sweep back"); phase(endb, "recovery")
    look((7.5, -9.0, 2.4), (0, 1.5, 3.4), 52, (640, 360))
    end = f + frames_for("Titan_Breath") + 2
elif MODE == "sweep":   # Earthsplitter (the fight-ending blow): long-arm sweep raking the ground
    f = 1; seq("Titan_Sweep", f, 1.0, hold_until=True)
    imp = f + ev("Titan_Sweep", "impact") * FPS
    flare(f, imp - 6, imp + 12, 3.2); ridge_glow(f + 4, imp, imp + 18)
    sc.frame_set(int(imp)); fr = bone_world("fist_r")
    shock(int(imp), fr, 5.0); dust(int(imp), fr, 16, 3)
    phase(f, "wind-up: arm cocked back, seams flare"); phase(imp, "IMPACT (fist rakes the ground)"); phase(imp + 8, "recovery")
    look(WORM["eye"], (0, 3.0, 2.6), 62, WORM["res"])
    end = f + frames_for("Titan_Sweep") + 2
elif MODE == "collapse":
    f = 1; seq("Titan_Collapse", f, 1.0, hold_until=True)
    g_ = f + ev("Titan_Collapse", "ground") * FPS
    key_value(seamg, f, spec["glowGain"]); key_value(seamg, int(g_) + 30, 0.25)
    sc.frame_set(int(g_)); dust(int(g_), HOLDER.location + Vector((0, -2.0, 0)), 14, 3)
    phase(f, "stagger"); phase(g_, "knees and fists hit the ground"); phase(g_ + 12, "glow dies")
    look((7.5, -7.0, 1.6), (0, 4.5, 3.0), 52, (640, 360))
    end = f + frames_for("Titan_Collapse") + 12
elif MODE == "ridges":   # the health stages: crystals break off at 66% and 33%
    ad.action = bpy.data.actions["Titan_Idle"]
    if hasattr(ad, "action_slot") and ad.action.slots: ad.action_slot = ad.action.slots[0]
    for t in TRACKS: ad.nla_tracks.remove(t)
    ridge_glow(1, 6, 14, 2.4); ridge_break(20, 1); ridge_glow(16, 20, 30, 3.0)
    ridge_glow(40, 44, 56, 3.0); ridge_break(44, 2)
    phase(1, "full health: ridges intact"); phase(20, "66%: first crystals shatter"); phase(44, "33%: more break off"); phase(60, "stubs remain")
    look((-6.5, 10.5, 7.5), (0, 5.75, 4.4), 46, (480, 480))
    end = 70
bake_all()
sc.frame_start, sc.frame_end = 1, int(end)
os.makedirs(OUT, exist_ok=True); json.dump(log, open(os.path.join(OUT, f"{MODE}.json"), "w"))
step = int(os.environ.get("SB_STEP", "1"))
render_range(1, int(end), step)
