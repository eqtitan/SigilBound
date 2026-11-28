"""Sigilbound 15-second teaser: the Blender part (owner-approved storyboard, 2026-11-26).

Renders frames 0..311 (13 s at 24 fps) of the game's own high-detail models in the Titan arena; assemble.py adds
the 2 s title card, fades, music and sound, and encodes the MP4.

    bpyenv/bin/python trailer.py -- MODELS_DIR OUT_DIR [--res 1600x900] [--samples 8] [--step 1] [--frames A-B]

MODELS_DIR is unity/Tools~/models (heroes_hd/ and ubc_anims.glb). --step 4 renders every 4th frame for a
quick framing pass. Shots (frames):
    0-47    rune circle glide, the sigil ring lights up
    48-119  hero cuts with element rim light: Morwenna, Ulfgar, Oriel, Azura, Obrecht (hearth golem)
    120-191 Titan reveal: worm's-eye tilt from its feet to the burning visor, roar
    192-263 Faultline Leap slow-motion rise, slam and shockwave; a flash of Heartfire Torrent fire over the team
    264-311 the team fights back: Ulfgar's whirlwind, Morwenna's channelled blast at the Titan
"""
import bpy, sys, os, math, json
from mathutils import Vector, Quaternion

argv = sys.argv[sys.argv.index("--") + 1:]
MODELS, OUT = argv[0], argv[1]
opt = {argv[i][2:]: argv[i + 1] for i in range(2, len(argv) - 1, 2) if argv[i].startswith("--")}
RX, RY = map(int, opt.get("res", "1600x900").split("x")); SAMPLES = int(opt.get("samples", "8")); STEP = int(opt.get("step", "1"))
F0, F1 = map(int, opt.get("frames", "0-311").split("-"))
HD = os.path.join(MODELS, "heroes_hd"); ANIMS = os.path.join(MODELS, "ubc_anims.glb")
os.makedirs(OUT, exist_ok=True)
FPS = 24

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene; sc.render.fps = FPS
bpy.ops.import_scene.gltf(filepath=ANIMS)
ACTS = {x.name.split("|")[-1]: x for x in bpy.data.actions}
for o in list(sc.objects): bpy.data.objects.remove(o, do_unlink=True)
body = json.load(open(os.path.join(HD, "body.json")))

# ---------- actors ----------
def tex(hid, k):
    for ext in (".jpg", ".png"):
        p = os.path.join(HD, f"SB_Hero_{hid}_HD_{k}{ext}")
        if os.path.exists(p): return bpy.data.images.load(p)
def hero_mat(hid):
    m = bpy.data.materials.new("T" + hid); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = 0.72
    t = nt.nodes.new("ShaderNodeTexImage"); t.image = tex(hid, "albedo"); nt.links.new(t.outputs[0], b.inputs["Base Color"])
    n = tex(hid, "normal")
    if n:
        n.colorspace_settings.name = "Non-Color"; tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = n
        nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tn.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
    g = tex(hid, "glow")
    if g: tg = nt.nodes.new("ShaderNodeTexImage"); tg.image = g; nt.links.new(tg.outputs[0], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 3.0
    return m
def clip_range(name): r = ACTS[name].frame_range; return r[0], r[1]
class Actor:
    def __init__(self, hid, x, y, face, head_h, bulk=1.0, idle="Idle_Loop"):
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=os.path.join(HD, f"SB_Hero_{hid}_HD.glb"))
        new = [o for o in bpy.data.objects if o not in before]
        self.arm = next(o for o in new if o.type == "ARMATURE"); self.mesh = next(o for o in new if o.type == "MESH" and o.name.startswith("SB_Hero"))
        for o in new:
            if o.type == "MESH" and not o.name.startswith("SB_Hero"): bpy.data.objects.remove(o, do_unlink=True)
        k = head_h / (self.arm.matrix_world @ self.arm.data.bones["Head"].head_local).z
        self.arm.scale = (k * bulk, k * bulk, k); self.arm.rotation_mode = "XYZ"
        self.home = Vector((x, y, 0)); self.arm.location = self.home
        self.face(face)
        self.mesh.data.materials.clear(); self.mesh.data.materials.append(hero_mat(hid))
        self.hid, self.idle, self.head_h = hid, idle, head_h
    def face(self, at):
        self.arm.rotation_euler = (self.arm.rotation_euler.x, 0, math.pi - math.atan2(at[0] - self.arm.location.x, at[1] - self.arm.location.y))
    def sample(self, clip, norm):
        """The pose of `clip` at normalized time `norm` (glTF action slots only evaluate on a freshly bound object)."""
        act = ACTS[clip].copy(); ad = self.arm.animation_data_create(); ad.action = act
        if hasattr(ad, "action_slot") and hasattr(act, "slots") and len(act.slots): ad.action_slot = act.slots[0]
        f0, f1 = clip_range(clip); sc.frame_set(int(round(f0 + (f1 - f0) * (norm % 1.0 if clip.endswith("_Loop") else max(0, min(1, norm))))))
        pose = {b.name: (b.location.copy(), b.rotation_quaternion.copy(), b.scale.copy()) for b in self.arm.pose.bones}
        ad.action = None; bpy.data.actions.remove(act)
        return pose
    def apply(self, pose):
        for b in self.arm.pose.bones:
            l, r, s = pose[b.name]; b.location, b.rotation_quaternion, b.scale = l, r, s

half = min(math.radians(70), (2.2 / 4.075) * 3 / 2)
CENTRE = Vector((0, 0.6, 0))
IDLES = {}
idles_txt = os.path.join(os.path.dirname(os.path.abspath(__file__)), "idles.txt")
if os.path.exists(idles_txt):
    for line in open(idles_txt):
        p = line.split()
        if len(p) == 2: IDLES[p[0]] = p[1]
TEAM_IDS = ["ulfgar", "morwenna", "azura", "oriel"]
TEAM = {}
for i, hid in enumerate(TEAM_IDS):
    t = -half + i * (2 * half / 3); x, y = math.sin(t) * 4.2, 0.6 - math.cos(t) * 3.95
    TEAM[hid] = Actor(hid, x, y, CENTRE, 1.85 * body.get(hid, [1, 1])[0], idle=IDLES.get(hid, "Idle_Loop"))
# Obrecht, the hearth golem (the non-human Flame hero), stands just off the near arc for his close-up
OBRECHT = Actor("obrecht", -5.6, -0.4, CENTRE, 1.85 * body.get("obrecht", [1, 1])[0], idle=IDLES.get("obrecht", "Idle_Loop"))
TH = 6.24  # the Titan's real head height in battle (4.8 m model x the 1.3 view scale)
HOME = Vector((0, 0.6 + 3.95 + 1.2, 0))
TITAN = Actor("titan", HOME.x, HOME.y, (0, -3), TH, 1.18)
TEAMC = Vector((0, 0.6 - 3.95 * 0.72, 0))
ACTORS = list(TEAM.values()) + [OBRECHT, TITAN]

# ---------- arena ----------
def emis(name, col, s):
    m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (0, 0, 0, 1); b.inputs["Emission Color"].default_value = col; b.inputs["Emission Strength"].default_value = s; return m, b
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 20, 0)); gm = bpy.data.materials.new("G"); gm.use_nodes = True
gb = gm.node_tree.nodes["Principled BSDF"]; gb.inputs["Base Color"].default_value = (0.06, 0.02, 0.016, 1); gb.inputs["Roughness"].default_value = 0.9
bpy.context.object.data.materials.append(gm)
RING_M, RING_B = emis("Ring", (1, 0.62, 0.18, 1), 0.0)
bpy.ops.mesh.primitive_torus_add(major_radius=4.15, minor_radius=0.035, location=(0, 0.6, 0.02)); bpy.context.object.scale = (1, 0.95, 0.2); bpy.context.object.data.materials.append(RING_M)
bpy.ops.mesh.primitive_torus_add(major_radius=3.6, minor_radius=0.02, location=(0, 0.6, 0.02)); bpy.context.object.scale = (1, 0.95, 0.2); bpy.context.object.data.materials.append(RING_M)
# sigil spokes: 12 glowing dashes on the ring
for i in range(12):
    a = i / 12 * math.tau
    bpy.ops.mesh.primitive_cube_add(size=1, location=(math.sin(a) * 3.88, 0.6 + math.cos(a) * 3.69, 0.02)); o = bpy.context.object
    o.scale = (0.05, 0.42, 0.01); o.rotation_euler = (0, 0, -a); o.data.materials.append(RING_M)
w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True; bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.42, 0.06, 0.025, 1); bg.inputs[1].default_value = 0.85
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(62), 0, math.radians(200))); SUN = bpy.context.object; SUN.data.energy = 2.4
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(55), 0, math.radians(20))); bpy.context.object.data.energy = 1.2
bpy.ops.object.light_add(type="POINT", location=(0, 0, 3)); FLASH = bpy.context.object; FLASH.data.energy = 0; FLASH.data.color = (1, 0.55, 0.2); FLASH.data.shadow_soft_size = 2
bpy.ops.object.light_add(type="POINT", location=(0, 0, 2)); RIM = bpy.context.object; RIM.data.energy = 0; RIM.data.shadow_soft_size = 0.5
sc.render.engine = "CYCLES"; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
sc.render.resolution_x = RX; sc.render.resolution_y = RY; sc.render.film_transparent = False
sc.view_settings.view_transform = "AgX" if "AgX" in [v.identifier for v in type(sc.view_settings).bl_rna.properties["view_transform"].enum_items] else "Filmic"
sc.view_settings.look = "None"
bpy.ops.object.camera_add(); CAM = bpy.context.object; sc.camera = CAM; CAM.data.sensor_fit = "VERTICAL"; CAM.data.sensor_height = 24
def cam(eye, look, vfov, roll=0.0):
    CAM.location = Vector(eye); d = (Vector(look) - Vector(eye)).normalized()
    q = d.to_track_quat("-Z", "Y") @ Quaternion((0, 0, 1), math.radians(roll)); CAM.rotation_euler = q.to_euler()
    CAM.data.lens = 12 / math.tan(math.radians(vfov) / 2)
def lerp(a, b, t): return a + (b - a) * t
def ease(t): t = max(0.0, min(1.0, t)); return t * t * (3 - 2 * t)
def V(*a): return Vector(a)
ELEM = {"morwenna": (0.62, 0.3, 1.0), "ulfgar": (0.3, 0.95, 0.55), "oriel": (1.0, 0.85, 0.35), "azura": (0.3, 0.6, 1.0), "obrecht": (1.0, 0.45, 0.12)}
# ---------- effects ----------
def ring_fx(col, s=5.0):
    m, _ = emis("FxR", col, s)
    bpy.ops.mesh.primitive_torus_add(major_radius=1, minor_radius=0.07, location=(0, 0, 0.05)); o = bpy.context.object; o.data.materials.append(m); o.scale = (0.001,) * 3; return o
def disc_fx(col, s=6.0):
    m, _ = emis("FxD", col, s)
    bpy.ops.mesh.primitive_circle_add(vertices=32, radius=1, fill_type="NGON", location=(0, 0, 0.04)); o = bpy.context.object; o.data.materials.append(m); o.scale = (0.001,) * 3; return o
def fire_mat(name, col, s):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial"); mix = nt.nodes.new("ShaderNodeMixShader"); tr = nt.nodes.new("ShaderNodeBsdfTransparent"); em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = col; em.inputs["Strength"].default_value = s
    noise = nt.nodes.new("ShaderNodeTexNoise"); noise.inputs["Scale"].default_value = 3.5; noise.inputs["Detail"].default_value = 6
    mp = nt.nodes.new("ShaderNodeMapping"); tc = nt.nodes.new("ShaderNodeTexCoord"); nt.links.new(tc.outputs["Object"], mp.inputs[0]); nt.links.new(mp.outputs[0], noise.inputs["Vector"])
    ramp = nt.nodes.new("ShaderNodeValToRGB"); ramp.color_ramp.elements[0].position = 0.42; ramp.color_ramp.elements[1].position = 0.72
    nt.links.new(noise.outputs["Fac"], ramp.inputs[0]); nt.links.new(ramp.outputs[0], mix.inputs[0])
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2]); nt.links.new(mix.outputs[0], out.inputs[0])
    return m, mp
def cone_fx(mat, r):
    bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=r, radius2=0.25, depth=1); o = bpy.context.object; o.data.materials.append(mat); bpy.ops.object.shade_smooth(); o.scale = (0.001,) * 3; return o
def aim_cone(o, frm, to, radius):
    d = to - frm; o.location = frm + d * 0.5; o.rotation_mode = "QUATERNION"; o.rotation_quaternion = d.normalized().to_track_quat("-Z", "Y"); o.scale = (radius, radius, d.length)
def hide(*objs):
    for o in objs: o.scale = (0.001,) * 3
SHOCK = [ring_fx((1, 0.62, 0.2, 1), 6) for _ in range(3)]; CRACK = disc_fx((1, 0.34, 0.05, 1), 7)
SHEATH_M, SHEATH_MP = fire_mat("Sheath", (1.0, 0.35, 0.05, 1), 10); CORE_M, CORE_MP = fire_mat("Core", (1.0, 0.85, 0.4, 1), 18)
SHEATH = cone_fx(SHEATH_M, 1.6); CORE = cone_fx(CORE_M, 0.8)
POOLS = [disc_fx((1.0, 0.32, 0.03, 1), 8) for _ in range(5)]
SWIRL_M, _ = emis("Swirl", (0.35, 1.0, 0.6, 1), 9)
bpy.ops.mesh.primitive_torus_add(major_radius=1.0, minor_radius=0.05); SWIRL = bpy.context.object; SWIRL.data.materials.append(SWIRL_M); hide(SWIRL)
bpy.ops.mesh.primitive_torus_add(major_radius=1.0, minor_radius=0.04); SWIRL2 = bpy.context.object; SWIRL2.data.materials.append(SWIRL_M); hide(SWIRL2)
BEAM_M, BEAM_MP = fire_mat("Beam", (0.65, 0.3, 1.0, 1), 16); BEAM = cone_fx(BEAM_M, 0.5)
ORB_M, ORB_B = emis("Orb", (0.65, 0.3, 1.0, 1), 0)
bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, radius=0.35); ORB = bpy.context.object; ORB.data.materials.append(ORB_M); hide(ORB)
# signature motes for the hero cuts: small glowing spheres drifting up around the hero
MOTE_M, MOTE_B = emis("Mote", (1, 1, 1, 1), 3); MOTES = []
for i in range(16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=8, ring_count=4, radius=0.016); o = bpy.context.object; o.data.materials.append(MOTE_M); hide(o); MOTES.append(o)
# ---------- shots ----------
WORM = V(0, 0.6 - 3.95 * math.cos(-half + 2 * half / 3) - 5.0, 0.9)
LEAPCAM = V(-2.4, -7.0, 0.9)   # behind and left of the team, low: the arc and the landing both read
def titan_pose(clip, norm): return (TITAN, clip, norm)
def frame(f):
    t = f / FPS; plan = {}   # actor -> (clip, norm)
    for a in ACTORS: plan[a] = (a.idle, (t * 0.9 + hash(a.hid) % 7 / 7.0))
    for a in ACTORS: a.arm.location = a.home.copy()
    TITAN.face((0, -3))
    hide(*SHOCK, CRACK, SHEATH, CORE, *POOLS, SWIRL, SWIRL2, BEAM, ORB, *MOTES)
    FLASH.data.energy = 0; RIM.data.energy = 0; RING_B.inputs["Emission Strength"].default_value = 3.0
    if f < 48:      # 1. rune circle glide; the sigil ring lights up
        k = f / 47.0
        RING_B.inputs["Emission Strength"].default_value = lerp(0.0, 6.0, ease((k - 0.15) / 0.7))
        ang = lerp(-0.5, 0.35, k); r = lerp(3.2, 6.5, ease(k)); hgt = lerp(11.0, 6.5, ease(k))
        cam(V(math.sin(ang) * r, 0.6 - math.cos(ang) * r, hgt), V(0, 0.6 + lerp(0, 2.5, ease(k)), lerp(0, 1.5, ease(k))), lerp(52, 46, k), lerp(-8, 0, k))
        FLASH.location = V(0, 0.6, 0.6); FLASH.data.energy = 500 * ease((k - 0.3) / 0.7); FLASH.data.color = (1, 0.65, 0.25)
    elif f < 120:   # 2. hero cuts with element rim light
        order = ["morwenna", "ulfgar", "oriel", "azura", "obrecht"]
        i = min(4, (f - 48) // 14 + (1 if f >= 118 else 0)); i = min(4, (f - 48) * 5 // 72); u = ((f - 48) - i * 72 / 5) / (72 / 5)
        a = OBRECHT if order[i] == "obrecht" else TEAM[order[i]]
        fwd = (CENTRE - a.home); fwd.z = 0; fwd.normalize(); side = V(fwd.y, -fwd.x, 0)
        hh = a.head_h * (1.32 if order[i] == "obrecht" else 1.0)
        sgn = {"morwenna": 1, "ulfgar": 1, "oriel": 1, "azura": -1, "obrecht": 1}[order[i]]
        eye = a.home + fwd * lerp(2.6, 2.2, u) + side * 0.7 * sgn + V(0, 0, hh * 0.78)
        FLASH.location = eye + V(0, 0, 0.8) + side * 0.6 * sgn; FLASH.data.color = (1, 0.88, 0.78); FLASH.data.energy = 220
        cam(eye, a.home + V(0, 0, hh * 0.72), 34, -3 if i % 2 else 3)
        col = ELEM[order[i]]; RIM.data.color = col; RIM.location = a.home - fwd * 1.1 + V(0, 0, hh * 0.95); RIM.data.energy = 900
        MOTE_B.inputs["Emission Color"].default_value = col + (1,)
        for j, o in enumerate(MOTES):
            ph = (j / len(MOTES) + u * 0.6) % 1.0; ang = j * 2.4 + u * 2
            o.location = a.home + V(math.cos(ang) * 0.75, math.sin(ang) * 0.75, 0.2 + ph * hh * 1.1); s = 0.6 + (1 - ph) * 0.8; o.scale = (s, s, s)
        plan[a] = (a.idle, u * 0.5)
    elif f < 192:   # 3. Titan reveal: worm's-eye tilt from its feet to the burning visor; it roars
        k = (f - 120) / 71.0
        look_z = lerp(0.4, TH * 0.98, ease(k / 0.75))
        shake = V(math.sin(f * 2.3), 0, math.cos(f * 2.9)) * (0.07 if k > 0.7 else 0.01)
        eye = HOME + V(1.4, -lerp(5.4, 6.3, ease(k)), lerp(0.35, 0.65, k))
        cam(eye + shake, V(0, HOME.y, look_z), lerp(56, 62, ease(k)), lerp(4, 0, k))
        plan[TITAN] = ("Interact", max(0, (k - 0.55) / 0.45)) if k > 0.55 else ("Idle_Loop", k)
        FLASH.location = HOME + V(0, -1.2, TH * 0.95); FLASH.data.energy = 2500 * max(0, (k - 0.6) / 0.4)
    elif f < 264:   # 4. Faultline Leap: slow-motion rise, slam and shockwave; a flash of Heartfire Torrent
        land = TEAMC + V(0, 1.5, 0)
        if f < 228:
            k = (f - 192) / 36.0; kk = 0.5 * (1 - (1 - k) ** 2)
            p = HOME.lerp(land, kk) + V(0, 0, 4 * 8 * kk * (1 - kk)); TITAN.arm.location = p
            plan[TITAN] = ("Jump_Start", min(1, k * 1.4))
            cam(LEAPCAM, p + V(0, 0, TH * 0.5), lerp(52, 64, ease(k)))
        elif f < 240:
            k = (f - 228) / 12.0; kk = 0.5 + 0.5 * k * k
            p = HOME.lerp(land, kk) + V(0, 0, 4 * 8 * kk * (1 - kk)); TITAN.arm.location = p
            plan[TITAN] = ("Jump_Loop", k)
            cam(LEAPCAM, p.lerp(land, ease(k)) + V(0, 0, TH * 0.5), lerp(64, 60, ease(k)))
        elif f < 250:   # slam
            u = (f - 240) / 10.0; TITAN.arm.location = land; plan[TITAN] = ("Jump_Land", min(1, u * 1.5))
            for i2, r in enumerate(SHOCK):
                v = max(0, u * 10 / 24 - i2 * 0.12) / 0.5; r.location = land; r.scale = ((0.4 + v * 8) if v > 0 else 0.001,) * 2 + (0.2,)
            CRACK.location = land + V(0, 0, 0.03); CRACK.scale = (min(1, u * 3) * 4,) * 2 + (1,)
            FLASH.location = land + V(0, 0, 2.5); FLASH.data.energy = 9000 * max(0, 1 - u * 1.2)
            cam(LEAPCAM + V(0, 0, 0.12 * math.sin(u * 30)), land + V(0, 0, TH * 0.55), 60, 6 * math.sin(u * 14))
        else:           # Heartfire Torrent flash: over its shoulder, the fire sweeps the team
            u = (f - 250) / 14.0; TITAN.arm.location = HOME
            back = (HOME - TEAMC); back.z = 0; back.normalize(); right = V(-back.y, back.x, 0)
            plan[TITAN] = ("Ult_Channel", 0.55 + u * 0.3)
            cam(HOME + right * 2.8 + back * 5.2 + V(0, 0, TH * 1.15), TEAMC + V(0, 0, 0.8), 54)
            mouth = HOME - back * 0.8 + V(0, 0, TH * 1.0)
            aim = V(lerp(-3.2, 3.2, u), TEAMC.y, 0.5)
            fl = 1 + 0.08 * math.sin(f * 2.1)
            aim_cone(SHEATH, mouth, aim, 1.7 * fl); aim_cone(CORE, mouth, aim + V(0, 0, 0.2), 0.8 * fl)
            SHEATH_MP.inputs["Location"].default_value = (0, 0, -f * 0.25); CORE_MP.inputs["Location"].default_value = (0, 0, -f * 0.3)
            for j, pl in enumerate(POOLS):
                px = lerp(-3.2, 3.2, j / 4)
                if px <= aim.x: r = 1.1; pl.location = V(px, TEAMC.y + math.cos(j * 2.1) * 0.5, 0.035); pl.scale = (r, r * 0.8, 1)
            FLASH.location = aim + V(0, 0, 1.2); FLASH.data.energy = 3500 + 1200 * math.sin(f * 1.7)
    else:           # 5. the team fights back
        TITAN.arm.location = HOME
        if f < 288:     # Ulfgar's whirlwind, the Titan looming behind
            u = (f - 264) / 24.0; a = TEAM["ulfgar"]
            fwd = (HOME - a.home); fwd.z = 0; fwd.normalize(); side = V(fwd.y, -fwd.x, 0)
            if side.dot(a.home - TEAMC) < 0: side = -side
            a.face(HOME); plan[a] = ("Ult_Whirlwind", u * 0.95)
            cam(a.home - fwd * 2.4 + side * 2.6 + V(0, 0, 1.1), a.home + fwd * 2.5 + V(0, 0, 2.4), 54, -4)
            for o, rr, hz in ((SWIRL, 1.2, 0.9), (SWIRL2, 1.6, 0.5)):
                o.location = a.home + V(0, 0, hz); o.rotation_euler = (0, 0, u * 20); s = rr * ease(u / 0.3); o.scale = (s, s, 1)
            RIM.data.color = ELEM["ulfgar"]; RIM.location = a.home + V(0, 0, 1.5); RIM.data.energy = 600 * ease(u / 0.3)
        else:           # Morwenna's channelled blast straight at the Titan
            u = (f - 288) / 24.0; a = TEAM["morwenna"]
            fwd = (HOME - a.home); fwd.z = 0; fwd.normalize(); side = V(fwd.y, -fwd.x, 0)
            if side.dot(a.home - TEAMC) < 0: side = -side
            a.face(HOME); plan[a] = ("Ult_Channel", u * 0.9)
            cam(a.home - fwd * 1.7 + side * 0.7 + V(0, 0, 1.75), HOME + V(0, 0, TH * 0.72), 52, 3)
            hand = a.home + fwd * 0.6 + V(0, 0, 1.5)
            if u < 0.5:
                ORB.location = hand; s = 0.4 + u * 2.4; ORB.scale = (s, s, s); ORB_B.inputs["Emission Strength"].default_value = 12
            else:
                v = (u - 0.5) / 0.5; hit = HOME + V(0, -0.6, TH * 0.75)
                aim_cone(BEAM, hand, hand.lerp(hit, min(1, v * 2.5)), 0.6); BEAM_MP.inputs["Location"].default_value = (0, 0, -f * 0.4)
                FLASH.location = hit; FLASH.data.color = (0.65, 0.35, 1.0); FLASH.data.energy = 6000 * max(0, (v - 0.3))
                plan[TITAN] = ("Hit_Chest", max(0, (v - 0.35) / 0.65))
            RIM.data.color = ELEM["morwenna"]; RIM.location = hand + V(0, 0, 0.3); RIM.data.energy = 900
    # pose everyone for this frame, then render
    poses = {a: a.sample(*plan[a]) for a in ACTORS}
    for a, p in poses.items(): a.apply(p)
    bpy.context.view_layer.update()
    sc.render.filepath = os.path.join(OUT, f"{f:03d}.png"); bpy.ops.render.render(write_still=True)

for f in range(F0, F1 + 1, STEP):
    frame(f); print(f"[trailer] {f}", flush=True)
print("[trailer] done", flush=True)
