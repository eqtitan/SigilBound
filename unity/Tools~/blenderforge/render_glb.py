import bpy, sys, os
sys.path.insert(0, sys.argv[-3]); import forge as F
glb, out = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=glb)
for o in [o for o in bpy.context.scene.objects if o.type == "MESH" and not o.name.startswith("SB_Hero")]: bpy.data.objects.remove(o, do_unlink=True)
arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
F.stage_and_render(1.0, out + "_viewer.png", "#ff7a3a", "close", res=(480, 720), samples=24)
F.stage_and_render(1.0, out + "_battle.png", "#ff7a3a", "battle", res=(390, 844), samples=16)
