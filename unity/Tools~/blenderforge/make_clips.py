"""Hand-authored clips for the high-detail rig, added to ubc_anims.glb next to the retargeted UAL set.

Defeat_Slump (owner request, 2026-11-25): the hero's arms drop and head bows, the knees buckle and the
hips sink into a kneel, then the body tips forward from the knees and lands face-down on the stomach,
head turned to one side, and holds. Poses are built in WORLD space (front = -Y, up = +Z) by rotating
bones about their heads, so they don't depend on each bone's local axes.

    bpyenv/bin/python make_clips.py -- IN_ANIMS.glb OUT_ANIMS.glb
"""
import bpy, sys, math
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = argv[0], argv[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
for o in [o for o in bpy.data.objects if o.type == "MESH"]: bpy.data.objects.remove(o, do_unlink=True)
existing = [a for a in bpy.data.actions]
arm.animation_data_create(); arm.animation_data.action = None
order = [b.name for b in arm.data.bones]
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
mw = arm.matrix_world

def reset():
    for pb in arm.pose.bones: pb.matrix_basis = Matrix(); pb.rotation_mode = "QUATERNION"
    bpy.context.view_layer.update()

def rot(bone, axis, deg):
    """Rotate `bone` (and so its children) by `deg` about a WORLD axis through the bone's head."""
    pb = arm.pose.bones.get(bone)
    if pb is None: return
    w = mw @ pb.matrix
    head = w.translation.copy()
    R = Matrix.Translation(head) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-head)
    pb.matrix = mw.inverted() @ (R @ w)
    bpy.context.view_layer.update()

def move(bone, delta):
    pb = arm.pose.bones[bone]; w = mw @ pb.matrix; w.translation += delta
    pb.matrix = mw.inverted() @ w; bpy.context.view_layer.update()

def bone_len(name):
    b = arm.data.bones[name]; return ((mw @ b.tail_local) - (mw @ b.head_local)).length

thigh, calf = bone_len("thigh_l"), bone_len("calf_l")

def arms_down(deg=72, forward=0):
    rot("upperarm_l", Y, deg); rot("upperarm_r", Y, -deg)
    if forward: rot("upperarm_l", X, -forward); rot("upperarm_r", X, -forward)
    rot("lowerarm_l", X, -12); rot("lowerarm_r", X, -12)

def pose_stand():
    reset(); arms_down()

def pose_sag():          # weight drops, head bows, knees start to give
    reset(); arms_down(76)
    rot("spine_02", X, 8); rot("neck_01", X, 14); rot("Head", X, 10)
    rot("thigh_l", X, -18); rot("thigh_r", X, -18); rot("calf_l", X, 36); rot("calf_r", X, 36)
    move("pelvis", Vector((0, 0.04, -0.09)))

def pose_kneel():        # on the knees: shins flat behind, thighs upright, slumped torso
    reset(); arms_down(80, forward=10)
    rot("calf_l", X, 90); rot("calf_r", X, 90)
    rot("foot_l", X, 30); rot("foot_r", X, 30)
    rot("spine_01", X, 10); rot("spine_02", X, 10); rot("spine_03", X, 6); rot("neck_01", X, 18); rot("Head", X, 12)
    move("pelvis", Vector((0, 0, -calf + 0.02)))

def pose_tip():          # tipping forward from the knees, hands reaching down
    reset()
    rot("pelvis", X, 48)                                  # the whole body leans forward about the hips...
    rot("thigh_l", X, -48 + 30); rot("thigh_r", X, -48 + 30)  # ...the thighs follow only partly (pivot near the knees)
    rot("calf_l", X, 90 - 30); rot("calf_r", X, 90 - 30)
    rot("upperarm_l", Y, 40); rot("upperarm_r", Y, -40); rot("upperarm_l", X, -60); rot("upperarm_r", X, -60)
    rot("neck_01", X, 10)
    move("pelvis", Vector((0, -thigh * 0.45, -calf - thigh * 0.25)))

def pose_prone():        # face-down on the stomach, legs straight behind, arms by the sides, head to one side
    reset()
    rot("pelvis", X, 90)
    # arms lie back along the sides (T-pose arms point sideways: swing them backward, flat on the ground)
    rot("upperarm_l", Z, 80); rot("upperarm_r", Z, -80)
    rot("upperarm_l", Y, 12); rot("upperarm_r", Y, -12)
    rot("lowerarm_l", Z, 10); rot("lowerarm_r", Z, -10)
    rot("neck_01", X, -12); rot("Head", Z, 70); rot("Head", X, -8)
    rot("foot_l", X, 55); rot("foot_r", X, 55)
    ph = (mw @ arm.pose.bones["pelvis"].matrix).translation
    move("pelvis", Vector((0, -thigh * 0.95, 0.13 - ph.z)))

def key(frame):
    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
        if pb.name == "pelvis": pb.keyframe_insert("location", frame=frame, group=pb.name)

act = bpy.data.actions.new("Defeat_Slump"); arm.animation_data.action = act
for f, pose in ((0, pose_stand), (10, pose_sag), (22, pose_kneel), (32, pose_kneel), (42, pose_tip), (52, pose_prone), (60, pose_prone)):
    pose(); key(f)
# Ease the fall: the last drop lands with a little settle.
for fc in (act.fcurves if hasattr(act, "fcurves") else []):
    for kp in fc.keyframe_points: kp.interpolation = "BEZIER"
arm.animation_data.action = None; reset()
tr = arm.animation_data.nla_tracks
for t in list(tr): tr.remove(t)
for a in existing + [act]:
    t = tr.new(); t.name = a.name; t.strips.new(a.name, int(a.frame_range[0]), a); t.mute = True
for o in bpy.context.selected_objects: o.select_set(False)
arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True, export_skins=True, export_animations=True,
                          export_animation_mode="NLA_TRACKS", export_apply=False, export_yup=True)
print("[clips] wrote", OUT, len(existing) + 1, "clips")
