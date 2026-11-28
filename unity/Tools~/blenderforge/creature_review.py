"""Review renders of a built creature (.blend from creature.py): turnaround at rest and clip frames.
    bpyenv/bin/python creature_review.py -- BLEND OUT_DIR [still:clip:frame ...] [turn] [battle:OTHER_HD_GLB] [anim:clip]"""
import bpy, sys, os, math
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]; BLEND, OUT, JOBS = a[0], a[1], a[2:]
bpy.ops.wm.open_mainfile(filepath=BLEND); os.makedirs(OUT, exist_ok=True)
sc = bpy.context.scene; arm = next(o for o in sc.objects if o.type == "ARMATURE" and o.data.bones.get("spider_body"))
hero = next(o for o in sc.objects if o.type == "MESH" and o.parent == arm)
sc.render.engine = "CYCLES"; sc.cycles.samples = 16; sc.cycles.use_denoising = True; sc.view_settings.view_transform = "Standard"
sc.render.film_transparent = False; sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGB"
w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True; bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.82, 0.82, 0.84, 1); bg.inputs[1].default_value = 0.8
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(48), 0, math.radians(-30))); bpy.context.object.data.energy = 3.0
bpy.ops.object.light_add(type="SUN", rotation=(math.radians(70), 0, math.radians(160))); bpy.context.object.data.energy = 1.2
gm = bpy.data.materials.new("G"); gm.use_nodes = True; gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.75, 0.75, 0.77, 1)
bpy.ops.mesh.primitive_plane_add(size=30); bpy.context.object.data.materials.append(gm)
tracks = {t.name: t for t in arm.animation_data.nla_tracks} if arm.animation_data else {}
def use(clip, frame):
    for t in tracks.values(): t.mute = True
    arm.animation_data.action = tracks[clip].strips[0].action; sc.frame_set(frame)
def cam(loc, target, lens=50, ortho=None):
    bpy.ops.object.camera_add(location=loc); c = bpy.context.object; c.data.lens = lens
    if ortho: c.data.type = "ORTHO"; c.data.ortho_scale = ortho
    c.rotation_euler = (Vector(target) - Vector(loc)).to_track_quat("-Z", "Y").to_euler(); sc.camera = c; return c
def shot(path, rx=700, ry=800):
    sc.render.resolution_x, sc.render.resolution_y = rx, ry; sc.render.filepath = path; bpy.ops.render.render(write_still=True)
for job in JOBS:
    if job == "turn":
        use("Idle_Loop", 1)
        for tag, loc in (("front", (0, -6, 1.0)), ("three", (2.9, -3.6, 1.45)), ("side", (6, 0.3, 1.0)), ("back", (0, 6.6, 1.0))):
            cam(loc, (0, 0.3, 0.85), ortho=2.4 if tag != "three" else None, lens=40); shot(os.path.join(OUT, f"turn_{tag}.png"))
    elif job.startswith("still:"):
        _, clip, f = job.split(":"); use(clip, int(f)); cam((3.8, -4.6, 1.5), (0, 0.2, 0.9), lens=38)
        shot(os.path.join(OUT, f"still_{clip}_{f}.png"), 640, 480)
    elif job.startswith("anim:"):
        clip = job.split(":")[1]; use(clip, 1); cam((3.8, -4.6, 1.5), (0, 0.2, 0.9), lens=38)
        st = tracks[clip].strips[0]; n = int(st.action.frame_range[1])
        sc.frame_start, sc.frame_end = 1, n; sc.cycles.samples = 8
        sc.render.resolution_x, sc.render.resolution_y = 560, 420; sc.render.filepath = os.path.join(OUT, f"anim_{clip}", "f_")
        bpy.ops.render.render(animation=True)
    elif job.startswith("battle:"):
        # Battle-camera mock: Vessa at the game's 1.3 scale beside an HD hero at its 1.156 scale, lineup spacing.
        other = job.split(":", 1)[1]; before = set(bpy.data.objects); bpy.ops.import_scene.gltf(filepath=other)
        new = [o for o in bpy.data.objects if o not in before]; oarm = next(o for o in new if o.type == "ARMATURE")
        oarm.scale = (1.156,) * 3; oarm.location = (-2.4, 0.4, 0); oarm.rotation_euler = (0, 0, math.radians(15))
        for o in new:
            if o.type == "MESH":
                tex = os.path.join(os.path.dirname(other), os.path.basename(other).replace(".glb", "_albedo.jpg"))
                if os.path.exists(tex):
                    m = bpy.data.materials.new("O"); m.use_nodes = True; t = m.node_tree.nodes.new("ShaderNodeTexImage"); t.image = bpy.data.images.load(tex)
                    m.node_tree.links.new(t.outputs[0], m.node_tree.nodes["Principled BSDF"].inputs["Base Color"]); o.data.materials.clear(); o.data.materials.append(m)
        arm.scale = (1.3,) * 3; arm.location = (0.4, 0, 0); arm.rotation_euler = (0, 0, math.radians(-10))
        use("Idle_Loop", 20); bg.inputs[0].default_value = (0.35, 0.36, 0.42, 1)
        gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.28, 0.3, 0.24, 1)
        cam((5.2, -7.4, 4.6), (-0.8, 0.2, 1.0), lens=42); shot(os.path.join(OUT, "battle_mock.png"), 960, 600)
print("[review] done", flush=True)
