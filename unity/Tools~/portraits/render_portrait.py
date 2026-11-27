"""Bust portraits of the shipped HD heroes, posed in their in-game idle, on a transparent film.
Usage: python render_portrait.py -- MODELS_DIR ANIMS_GLB IDLES_TXT OUT_DIR id [id ...]"""
import bpy, sys, os, math
from mathutils import Vector
a = sys.argv[sys.argv.index("--") + 1:]; MD, ANIMS, IDLES, OUT, IDS = a[0], a[1], a[2], a[3], a[4:]
IDLE = {}
if os.path.exists(IDLES):
    for line in open(IDLES):
        p = line.split()
        if len(p) == 2: IDLE[p[0]] = p[1]
os.makedirs(OUT, exist_ok=True)
for hid in IDS:
    try:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.gltf(filepath=ANIMS)
        acts = {x.name.split("|")[-1]: x for x in bpy.data.actions}
        for o in list(bpy.context.scene.objects): bpy.data.objects.remove(o, do_unlink=True)
        bpy.ops.import_scene.gltf(filepath=os.path.join(MD, f"SB_Hero_{hid}_HD.glb"))
        arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
        meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
        clip = next((acts[k] for k in (IDLE.get(hid), "Idle_Loop") if k and k in acts), None)
        if clip: arm.animation_data_create(); arm.animation_data.action = clip; bpy.context.scene.frame_set(12)
        def tex(k):
            for ext in (".jpg", ".png"):
                p = os.path.join(MD, f"SB_Hero_{hid}_HD_{k}{ext}")
                if os.path.exists(p): return bpy.data.images.load(p)
        m = bpy.data.materials.new("T"); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = 0.72
        ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = tex("albedo"); nt.links.new(ta.outputs[0], b.inputs["Base Color"])
        n = tex("normal")
        if n:
            tn = nt.nodes.new("ShaderNodeTexImage"); tn.image = n; n.colorspace_settings.name = "Non-Color"
            nm = nt.nodes.new("ShaderNodeNormalMap"); nt.links.new(tn.outputs[0], nm.inputs[1]); nt.links.new(nm.outputs[0], b.inputs["Normal"])
        g = tex("glow")
        if g: tg = nt.nodes.new("ShaderNodeTexImage"); tg.image = g; nt.links.new(tg.outputs[0], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 1.5
        for me in meshes: me.data.materials.clear(); me.data.materials.append(m)
        sc = bpy.context.scene; sc.render.engine = "CYCLES"; sc.cycles.samples = 24
        sc.render.resolution_x = 384; sc.render.resolution_y = 512; sc.render.film_transparent = True
        sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGBA"
        sc.view_settings.view_transform = "Standard"
        w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True; bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.35, 0.32, 0.42, 1); bg.inputs[1].default_value = 0.6
        # key from front-left above, warm; rim from behind-right, cool
        bpy.ops.object.light_add(type="SUN", rotation=(math.radians(50), 0, math.radians(-38))); k = bpy.context.object.data; k.energy = 3.4; k.color = (1, 0.95, 0.86)
        bpy.ops.object.light_add(type="SUN", rotation=(math.radians(60), 0, math.radians(150))); r = bpy.context.object.data; r.energy = 2.6; r.color = (0.75, 0.85, 1)
        # frame on the head bone: head centre a little above the middle, shoulders in the bottom third
        bpy.context.view_layer.update()
        hb = next((pb for pb in arm.pose.bones if pb.name.lower() in ("head", "head.x", "def-head")), None)
        if hb is None: hb = next(pb for pb in arm.pose.bones if "head" in pb.name.lower())
        head = arm.matrix_world @ hb.head; tip = arm.matrix_world @ hb.tail
        hl = max(0.12, (tip - head).length)
        centre = head + Vector((0, 0, hl * 0.75))
        dist = hl * 15.5
        ang = math.radians(18)
        bpy.ops.object.camera_add(location=(centre.x + math.sin(ang) * dist, centre.y - math.cos(ang) * dist, centre.z - hl * 2.2))
        cam = bpy.context.object; sc.camera = cam; cam.data.lens = 85
        look = centre - Vector((0, 0, hl * 0.9))
        cam.rotation_euler = (look - cam.location).to_track_quat("-Z", "Y").to_euler()
        sc.render.filepath = os.path.join(OUT, f"{hid}.png"); bpy.ops.render.render(write_still=True)
        print("[portrait] ok", hid, flush=True)
    except Exception as e:
        print("[portrait] FAIL", hid, e, flush=True)
