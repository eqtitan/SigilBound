"""Skeleton library reference rigs (docs/sigilbound/art/SKELETON_LIBRARY.md) for authoring clips in Blender.

  bpyenv/bin/python skeleton_library.py -- REF.glb OUT_DIR [template ...]
    REF.glb   the reference biped with its clips (unity/Tools~/models/ubc_anims.glb)
    OUT_DIR   gets ref_<template>.glb (armature only, natural joint limits as Blender Limit Rotation constraints,
              ready to pose and keyframe) and skeleton_library.json (bones, parents, human bone names, limits)
    templates biped, biped_digitigrade, brute, biped_addons (tail, wings, ears, hair, cloth chains); default all

Every template keeps the UBC joint names, so clips authored on any of them bake to Humanoid clips the same way
(Editor/HumanoidClipBaker.cs: add the file to Core/Rig/RigProfiles.ClipSources). Extra bones are never human
bones: the metatarsal folds after the clip (HumanoidRig hock), add-on chains swing (SpringBones) or flutter.
Limits are in degrees about the bone's local X (bend), Y (twist), Z (side); they guide the animator and the
character TD, the game's own limits are Unity's muscle ranges plus RigProfiles' muscle clamps.
"""
import bpy, sys, os, json, math
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
ref, out = argv[0], argv[1]
want = argv[2:] or ["biped", "biped_digitigrade", "brute", "biped_addons"]
os.makedirs(out, exist_ok=True)

HUMAN = {  # UBC joint -> Unity human bone
    "pelvis": "Hips", "spine_01": "Spine", "spine_02": "Chest", "spine_03": "UpperChest", "neck_01": "Neck", "Head": "Head",
    **{f"{p}_{s}": f"{'Left' if s == 'l' else 'Right'}{h}" for s in "lr" for p, h in
       [("clavicle", "Shoulder"), ("upperarm", "UpperArm"), ("lowerarm", "LowerArm"), ("hand", "Hand"),
        ("thigh", "UpperLeg"), ("calf", "LowerLeg"), ("foot", "Foot"), ("ball", "Toes")]},
    **{f"{f}_0{i}_{s}": f"{'Left' if s == 'l' else 'Right'}{F}{['Proximal', 'Intermediate', 'Distal'][i - 1]}" for s in "lr" for i in (1, 2, 3)
       for f, F in [("thumb", "Thumb"), ("index", "Index"), ("middle", "Middle"), ("ring", "Ring"), ("pinky", "Little")]},
}
# natural limits (degrees): (min x, max x, min y, max y, min z, max z)
LIMITS = {
    "spine_01": (-30, 40, -25, 25, -25, 25), "spine_02": (-20, 30, -20, 20, -20, 20), "spine_03": (-15, 25, -20, 20, -15, 15),
    "neck_01": (-35, 40, -45, 45, -30, 30), "Head": (-30, 35, -40, 40, -25, 25),
    "clavicle": (-15, 30, -10, 10, -15, 15), "upperarm": (-90, 160, -90, 90, -100, 60), "lowerarm": (0, 150, -90, 90, 0, 0),
    "hand": (-70, 70, -15, 15, -30, 30), "thigh": (-120, 40, -45, 45, -45, 30), "calf": (0, 150, -10, 10, 0, 0),
    "foot": (-45, 30, -10, 10, -20, 20), "ball": (-40, 40, 0, 0, 0, 0), "meta": (-60, 30, 0, 0, -5, 5),
}
BRUTE = {"arm": 1.6, "leg": 0.58, "spine": 1.1, "neck": 0.7}   # the Titan: arms 0.87 m, legs 0.5 m on a 1.85 m frame

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=ref)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    for o in list(bpy.data.objects):
        if o != arm: bpy.data.objects.remove(o, do_unlink=True)
    if arm.animation_data: arm.animation_data_clear()
    for a in list(bpy.data.actions): bpy.data.actions.remove(a)
    return arm

def edit(arm):
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    return arm.data.edit_bones

def scale_chain(eb, names, k):
    """Lengthen each bone by k, moving its whole subtree with its tail."""
    for n in names:
        if n not in eb: continue
        b = eb[n]; d = (b.tail - b.head) * (k - 1)
        sub = [c for c in eb if c != b and b in c.parent_recursive]
        b.tail += d
        for c in sub: c.head += d; c.tail += d

def add_chain(eb, parent, prefix, start, step, n):
    p = eb[parent]; h = Vector(start); out = []
    for i in range(n):
        b = eb.new(f"{prefix}_{i}"); b.head = h; b.tail = h + Vector(step); b.parent = p; b.use_connect = i > 0
        out.append(b.name); p = b; h = b.tail.copy()
    return out

def build(template):
    arm = reset(); eb = edit(arm); extra = {}
    if template == "brute":
        for s in "lr":
            scale_chain(eb, [f"upperarm_{s}", f"lowerarm_{s}"], BRUTE["arm"])
            scale_chain(eb, [f"thigh_{s}", f"calf_{s}"], BRUTE["leg"])
        scale_chain(eb, ["spine_01", "spine_02", "spine_03"], BRUTE["spine"]); scale_chain(eb, ["neck_01"], BRUTE["neck"])
        # stand on the ground again: lift everything above the feet back down
        low = min(b.head.z for b in eb if b.name.startswith("ball"))
        for b in eb: b.head.z -= low; b.tail.z -= low
    if template == "biped_digitigrade":
        for s in "lr":
            calf, foot = eb[f"calf_{s}"], eb[f"foot_{s}"]
            knee, ankle = calf.head.copy(), foot.head.copy()
            hock = knee.lerp(ankle, 0.55) + Vector((0, 0.10, 0))   # the hock sits back (glTF -Z = Blender +Y back)
            calf.tail = hock
            m = eb.new(f"meta_{s}"); m.head = hock; m.tail = ankle + Vector((0, -0.04, 0.10)); m.parent = calf
            foot.head = m.tail; foot.parent = m
            extra[f"meta_{s}"] = "metatarsal: folds against the knee after the clip (RigProfile.HockFold)"
    if template == "biped_addons":
        pel, ch, hd = eb["pelvis"].head, eb["spine_03"].tail, eb["Head"].head
        for n in add_chain(eb, "pelvis", "tail", pel + Vector((0, 0.08, 0)), (0, 0.13, -0.03), 5): extra[n] = "tail (spring)"
        for s, x in (("l", 1), ("r", -1)):
            for n in add_chain(eb, "spine_03", f"wing_{s}", ch + Vector((0.08 * x, 0.10, -0.05)), (0.16 * x, 0.06, 0.10), 3): extra[n] = "wing (flutter)"
            for n in add_chain(eb, "Head", f"ear_{s}", hd + Vector((0.07 * x, 0.0, 0.14)), (0.03 * x, 0.0, 0.06), 2): extra[n] = "ear (flutter)"
        for n in add_chain(eb, "Head", "hair", hd + Vector((0, 0.08, 0.08)), (0, 0.04, -0.09), 4): extra[n] = "hair (spring)"
        for c in range(6):
            a = math.pi * 2 * c / 6
            for n in add_chain(eb, "pelvis", f"sb_{c}", pel + Vector((0.14 * math.sin(a), 0.12 * math.cos(a), -0.04)), (0.03 * math.sin(a), 0.03 * math.cos(a), -0.16), 3): extra[n] = "cloth ring (spring, sb_<chain>_<bone>)"
    bones = [(b.name, b.parent.name if b.parent else None) for b in eb]
    bpy.ops.object.mode_set(mode="POSE")
    limits = {}
    for pb in arm.pose.bones:
        key = pb.name.rsplit("_", 1)[0] if pb.name.endswith(("_l", "_r")) else pb.name
        lim = LIMITS.get(key)
        if not lim: continue
        c = pb.constraints.new("LIMIT_ROTATION"); c.owner_space = "LOCAL"
        c.use_limit_x = c.use_limit_y = c.use_limit_z = True
        c.min_x, c.max_x, c.min_y, c.max_y, c.min_z, c.max_z = [math.radians(v) for v in lim]
        limits[pb.name] = lim
    bpy.ops.object.mode_set(mode="OBJECT")
    path = os.path.join(out, f"ref_{template}.glb")
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", export_animations=False, export_skins=False, use_selection=False)
    return {"file": os.path.basename(path), "bones": [{"name": n, "parent": p, "human": HUMAN.get(n), "extra": extra.get(n), "limits": limits.get(n)} for n, p in bones]}

lib = {t: build(t) for t in want}
json.dump(lib, open(os.path.join(out, "skeleton_library.json"), "w"), indent=1)
for t, v in lib.items():
    print(f"{t}: {len(v['bones'])} bones, {sum(1 for b in v['bones'] if b['human'])} human, {sum(1 for b in v['bones'] if b['extra'])} extra -> {v['file']}")
