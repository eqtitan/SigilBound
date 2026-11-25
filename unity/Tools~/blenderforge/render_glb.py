# Preview an exported free-path hero the way the game shows it: the hero GLB posed with the BASE
# model's own Idle clip (the game plays the base clips on the identical skeleton), rendered with
# Cycles (EEVEE can't load textures headless). Uses the baked albedo/glow PNGs next to the GLB.
#   python render_glb.py -- <hero.glb> <base.glb> <out-prefix>
import bpy, sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import forge as F
glb, base, out = sys.argv[-3], sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=base)
idle = next(a for a in bpy.data.actions if a.name.endswith("|Idle"))
for o in list(bpy.context.scene.objects): bpy.data.objects.remove(o, do_unlink=True)
bpy.ops.import_scene.gltf(filepath=glb)
for o in [o for o in bpy.context.scene.objects if o.type == "MESH" and not o.name.startswith("SB_Hero")]: bpy.data.objects.remove(o, do_unlink=True)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
ad = arm.animation_data or arm.animation_data_create(); ad.action = idle
if hasattr(ad, "action_slot") and idle.slots: ad.action_slot = idle.slots[0]
bpy.context.scene.frame_set(12)
hero = next(o for o in bpy.context.scene.objects if o.name.startswith("SB_Hero"))
stem = glb[:-len("_LOD0.glb")]
m = bpy.data.materials.new("Baked"); m.use_nodes = True; nt = m.node_tree; b = nt.nodes["Principled BSDF"]
ta = nt.nodes.new("ShaderNodeTexImage"); ta.image = bpy.data.images.load(stem + "_albedo.png"); nt.links.new(ta.outputs["Color"], b.inputs["Base Color"])
tg = nt.nodes.new("ShaderNodeTexImage"); tg.image = bpy.data.images.load(stem + "_glow.png"); nt.links.new(tg.outputs["Color"], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 1.5
b.inputs["Roughness"].default_value = 0.6
hero.data.materials.clear(); hero.data.materials.append(m)
F.stage_and_render(1.0, out + "_viewer.png", "#ff7a3a", "close", res=(480, 720), samples=24)
F.stage_and_render(1.0, out + "_battle.png", "#ff7a3a", "battle", res=(390, 844), samples=16)
