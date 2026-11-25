"""Retarget the CC0 Quaternius Universal Animation Library (Rigify DEF-* rig) onto the 65-joint
Universal Base Characters skeleton (phase F/G). World-space rotation deltas per bone, frame by frame:
    R_target(f) = R_source(f) @ R_source_rest^-1 @ R_target_rest
The hips also carry the source's translation, scaled by leg length. Output: one GLB holding the UBC
skeleton (no mesh) with every clip, for the game and for Blender previews.

    bpyenv/bin/python ual_retarget.py -- UAL.gltf UBC_BODY.gltf OUT.glb [clip ...]
"""
import bpy, sys, math
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, TGT, OUT, ONLY = argv[0], argv[1], argv[2], set(argv[3:])

MAP = {"DEF-hips": "pelvis", "DEF-spine.001": "spine_01", "DEF-spine.002": "spine_02", "DEF-spine.003": "spine_03",
       "DEF-neck": "neck_01", "DEF-head": "Head"}
for s, t in (("L", "l"), ("R", "r")):
    MAP.update({f"DEF-shoulder.{s}": f"clavicle_{t}", f"DEF-upper_arm.{s}": f"upperarm_{t}", f"DEF-forearm.{s}": f"lowerarm_{t}",
                f"DEF-hand.{s}": f"hand_{t}", f"DEF-thigh.{s}": f"thigh_{t}", f"DEF-shin.{s}": f"calf_{t}",
                f"DEF-foot.{s}": f"foot_{t}", f"DEF-toe.{s}": f"ball_{t}"})
    for f in ("index", "middle", "ring", "pinky"):
        for j in ("01", "02", "03"): MAP[f"DEF-f_{f}.{j}.{s}"] = f"{f}_{j}_{t}"
    for j, k in (("01", "01"), ("02", "02"), ("03", "03")): MAP[f"DEF-thumb.{j}.{s}"] = f"thumb_{k}_{t}"

bpy.ops.wm.read_factory_settings(use_empty=True)
def load(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path, guess_original_bind_pose=False)
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == "ARMATURE")
    for o in new:
        if o.type == "MESH": bpy.data.objects.remove(o, do_unlink=True)
    return arm
src = load(SRC); tgt = load(TGT)
src_actions = [a for a in bpy.data.actions if not ONLY or a.name.split("|")[-1] in ONLY]
for a in list(bpy.data.actions):
    if a not in src_actions: pass
names = {a.name: a for a in src_actions}
present = {s: t for s, t in MAP.items() if s in src.pose.bones and t in tgt.pose.bones}
print(f"[ual] mapped {len(present)} bones; {len(src_actions)} clips")

def world_rest(arm, b):
    return (arm.matrix_world @ arm.data.bones[b].matrix_local).to_quaternion()
offset = {t: world_rest(src, s).inverted() @ world_rest(tgt, t) for s, t in present.items()}
# Hips height ratio for root motion / bobbing
sh = (src.matrix_world @ src.data.bones["DEF-hips"].head_local).z; th = (tgt.matrix_world @ tgt.data.bones["pelvis"].head_local).z
ratio = th / max(1e-4, sh)
order = [b.name for b in tgt.data.bones]  # parents before children
inv = {t: s for s, t in present.items()}
tgt.animation_data_create(); src.animation_data_create()
hips_rest_src = src.matrix_world @ src.data.bones["DEF-hips"].head_local
hips_rest_tgt = tgt.matrix_world @ tgt.data.bones["pelvis"].head_local
for act in src_actions:
    clip = act.name.split("|")[-1]
    src.animation_data.action = act
    f0, f1 = (int(act.frame_range[0]), int(act.frame_range[1]))
    new = bpy.data.actions.new(clip); tgt.animation_data.action = new
    for pb in tgt.pose.bones: pb.rotation_mode = "QUATERNION"
    for f in range(f0, f1 + 1):
        bpy.context.scene.frame_set(f)
        for t in order:
            if t not in inv: continue
            s = inv[t]
            spb = src.pose.bones[s]; tpb = tgt.pose.bones[t]
            src_world = (src.matrix_world @ spb.matrix).to_quaternion()
            want = src_world @ offset[t]
            # convert the wanted world rotation into the bone's pose-space matrix, keeping its current head
            cur = tgt.matrix_world @ tpb.matrix
            m = want.to_matrix().to_4x4(); m.translation = cur.translation
            if t == "pelvis":
                d = (src.matrix_world @ spb.matrix).translation - hips_rest_src
                m.translation = hips_rest_tgt + d * ratio
            tpb.matrix = tgt.matrix_world.inverted() @ m
            bpy.context.view_layer.update()
            tpb.keyframe_insert("rotation_quaternion", frame=f, group=t)
            if t == "pelvis": tpb.keyframe_insert("location", frame=f, group=t)
    tr = new.slots[0] if hasattr(new, "slots") and new.slots else None
    print(f"[ual] {clip}: frames {f0}-{f1}")
    tgt.animation_data.action = None
    for pb in tgt.pose.bones: pb.matrix_basis = Matrix()
# Export the target skeleton with all new actions (as NLA strips so each becomes a glTF animation).
for o in list(bpy.data.objects):
    if o is not tgt: bpy.data.objects.remove(o, do_unlink=True)
for a in src_actions: bpy.data.actions.remove(a)
tr = tgt.animation_data.nla_tracks
for a in list(bpy.data.actions):
    t = tr.new(); t.name = a.name; st = t.strips.new(a.name, int(a.frame_range[0]), a); t.mute = True
bpy.context.view_layer.objects.active = tgt; tgt.select_set(True)
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True, export_skins=True, export_animations=True,
                          export_animation_mode="NLA_TRACKS", export_apply=False, export_yup=True)
print("[ual] wrote", OUT)
