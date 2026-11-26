"""Hand-authored clips for the high-detail rig, added to ubc_anims.glb next to the retargeted UAL set
(owner requests, 2026-11-25). Poses are built in WORLD space (front = -Y, up = +Z, the hero's right
side = -X) by aiming bones along directions and rotating about bone heads, so they don't depend on
each bone's local axes. 30 fps.

- Defeat_Slump: knees buckle into a kneel, head bowed; then the body tips forward as dead weight,
  PIVOTING ON THE PLANTED KNEES (the torso folds first, the hips follow, accelerating), the chest
  lands, the hips settle, legs straight behind, head turned to one side; holds.
- Spear_Thrust: guard, draw back with a twist, a lunge that drives the spear straight out (the fist
  turned so the shaft points forward), recover to guard. Impact at about 0.45.
- Hit_Kneel: a heavy hit staggers the hero back, drops them to one knee (hand braced on the other
  knee), a held beat, then they rise.

    bpyenv/bin/python make_clips.py -- IN_ANIMS.glb OUT_ANIMS.glb
"""
import bpy, sys, math
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT = argv[0], argv[1]
NEW = {"Defeat_Slump", "Spear_Thrust", "Hit_Kneel"}
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.scene.render.fps = 30
bpy.ops.import_scene.gltf(filepath=SRC)
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
for o in [o for o in bpy.data.objects if o.type == "MESH"]: bpy.data.objects.remove(o, do_unlink=True)
for a in [a for a in bpy.data.actions if a.name.split("|")[-1].split(".")[0] in NEW]: bpy.data.actions.remove(a)
existing = list(bpy.data.actions)
# Re-imported clips come back as "Name.001" (Blender renames on import); the game finds clips by name,
# so restore the plain names, and drop the T-pose reference clip (a default first clip held heroes in T-pose).
import re as _re
for a in list(existing):
    a.name = _re.sub(r"\.\d{3}$", "", a.name.split("|")[-1])
    if a.name == "A_TPose": existing.remove(a); bpy.data.actions.remove(a)
arm.animation_data_create(); arm.animation_data.action = None
mw = arm.matrix_world
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
V = lambda x, y, z: Vector((x, y, z)).normalized()
upd = lambda: bpy.context.view_layer.update()

def reset():
    for pb in arm.pose.bones: pb.rotation_mode = "QUATERNION"; pb.matrix_basis = Matrix()
    upd()

def world(bone): return mw @ arm.pose.bones[bone].matrix
def head(bone): return mw @ arm.pose.bones[bone].head
def tail(bone): return mw @ arm.pose.bones[bone].tail

def turn(bone, q):
    """Rotate `bone` (and its children) by quaternion q (world) about the bone's head."""
    pb = arm.pose.bones.get(bone)
    if pb is None: return
    w = mw @ pb.matrix; h = w.translation.copy()
    pb.matrix = mw.inverted() @ (Matrix.Translation(h) @ q.to_matrix().to_4x4() @ Matrix.Translation(-h) @ w); upd()

def rot(bone, axis, deg):
    from mathutils import Quaternion
    turn(bone, Quaternion(axis, math.radians(deg)))

def aim(bone, d):
    """Point the bone (head -> tail) along world direction d, by the smallest rotation."""
    cur = (tail(bone) - head(bone)).normalized()
    turn(bone, cur.rotation_difference(d.normalized()))

def blade(side):  # the weapon's direction in the fist (as blenderforge/ubc.py grip_frame)
    return (head("index_01_" + side) - head("pinky_01_" + side)).normalized()

def point_weapon(side, d):
    turn("hand_" + side, blade(side).rotation_difference(d.normalized()))

def move(bone, delta):
    pb = arm.pose.bones[bone]; w = mw @ pb.matrix; w.translation += delta
    pb.matrix = mw.inverted() @ w; upd()

def key(frame):
    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame, group=pb.name)
        if pb.name == "pelvis": pb.keyframe_insert("location", frame=frame, group=pb.name)

def new_action(name):
    act = bpy.data.actions.new(name); arm.animation_data.action = act; return act

def finish():
    arm.animation_data.action = None; reset()

reset()
REST_KNEE_Y = head("calf_l").y
GROUND_KNEE = 0.09

def arms_hang(spread=0.15, fwd=0.0):
    aim("upperarm_l", V(spread, -fwd, -1)); aim("upperarm_r", V(-spread, -fwd, -1))
    aim("lowerarm_l", V(spread * 0.6, -0.15 - fwd, -1)); aim("lowerarm_r", V(-spread * 0.6, -0.15 - fwd, -1))

def stand():
    reset(); arms_hang()

# ---------------------------------------------------------------- Defeat_Slump
def sag():
    reset(); arms_hang(0.12)
    rot("spine_02", X, 8); rot("neck_01", X, 14); rot("Head", X, 10)
    aim("thigh_l", V(0, -0.3, -1)); aim("thigh_r", V(0, -0.3, -1))
    aim("calf_l", V(0, 0.45, -1)); aim("calf_r", V(0, 0.45, -1))
    move("pelvis", Vector((0, 0.03, -0.1)))

def fall(u):
    """u = 0 kneeling, upright torso slumped; u = 1 prone. Pivots on the planted knees."""
    e = u * u                                   # gravity: slow start, fast landing
    theta = math.radians(88 * e)                # thigh angle from vertical (hips go forward over the knees)
    torso = 20 + 72 * min(1.0, (u * 1.25) ** 2)  # the torso folds ahead of the hips
    reset()
    rot("pelvis", X, torso)                     # whole body tilts forward (front = -Y)
    hip_to_knee = V(0, math.sin(theta), -math.cos(theta))
    aim("thigh_l", hip_to_knee); aim("thigh_r", hip_to_knee)
    aim("calf_l", V(0, 1, -0.03)); aim("calf_r", V(0, 1, -0.03))   # shins flat behind
    aim("foot_l", V(0, 0.9, -0.35)); aim("foot_r", V(0, 0.9, -0.35))
    # arms: dangling while kneeling, swept back along the sides when down
    s = min(1.0, u * 1.1); s = s * s * (3 - 2 * s)
    for side, sx in (("l", 1), ("r", -1)):
        d = V(sx * 0.15, 0, -1).lerp(V(sx * 0.14, 1, -0.12), s)
        aim("upperarm_" + side, d); aim("lowerarm_" + side, d.lerp(V(sx * 0.1, 1, -0.05), s * 0.6))
    rot("neck_01", X, 18 * (1 - u)); rot("Head", X, 12 * (1 - u))
    if u > 0.6: rot("Head", Z, 70 * ((u - 0.6) / 0.4) ** 2)   # face turns to one side as it lands
    kn = head("calf_l")                          # keep the knees planted where the kneel put them
    move("pelvis", Vector((0, REST_KNEE_Y - kn.y, GROUND_KNEE - kn.z)))

new_action("Defeat_Slump")
stand(); key(0)
sag(); key(9)
fall(0.0); key(18)          # down on the knees
fall(0.0); rot("spine_02", X, 4); key(30)   # a beat: head hangs
for k in range(0, 9):       # the tip-over: 0.53 s, accelerating
    fall(k / 8); key(32 + k * 2)
fall(1.0); rot("spine_02", X, -3); key(51)  # settle (small rebound)
fall(1.0); key(56); key(66)
finish()

# ---------------------------------------------------------------- Spear_Thrust
def stance(fwd_l=0.25, back_r=0.2, drop=0.04, ahead=0.0, twist=0.0):
    aim("thigh_l", V(0.05, -fwd_l, -1)); aim("calf_l", V(0, 0.02, -1))
    aim("thigh_r", V(-0.05, back_r, -1)); aim("calf_r", V(0, back_r * 0.7, -1))
    if twist: rot("spine_01", Z, twist)
    move("pelvis", Vector((0, -ahead, -drop)))

def guard():
    reset(); stance()
    aim("upperarm_r", V(-0.25, 0.1, -1)); aim("lowerarm_r", V(-0.05, -1, 0.25)); point_weapon("r", V(0, -1, 0.5))
    aim("upperarm_l", V(0.3, -0.35, -1)); aim("lowerarm_l", V(0, -1, 0.3))

def draw():
    reset(); stance(twist=-16)
    aim("upperarm_r", V(-0.3, 0.6, -0.75)); aim("lowerarm_r", V(-0.05, -1, 0.15)); point_weapon("r", V(0, -1, 0.15))
    aim("upperarm_l", V(0.35, -0.6, -0.7)); aim("lowerarm_l", V(0, -1, 0.35))

def thrust(reach=1.0):
    reset(); stance(fwd_l=0.6, back_r=0.5, drop=0.1, ahead=0.25 * reach, twist=14)
    aim("upperarm_r", V(-0.12, -1, -0.05)); aim("lowerarm_r", V(0, -1, 0)); point_weapon("r", V(0, -1, -0.02))
    aim("upperarm_l", V(0.4, 0.5, -0.8)); aim("lowerarm_l", V(0.1, 0.2, -1))

new_action("Spear_Thrust")
guard(); key(0); draw(); key(8); thrust(); key(12); thrust(); key(17); guard(); key(27)
finish()

# ---------------------------------------------------------------- Hit_Kneel
def stagger():
    reset(); arms_hang(0.3, -0.2)
    rot("spine_02", X, -12); rot("neck_01", X, -10); rot("Head", X, -8)
    move("pelvis", Vector((0, 0.09, -0.02)))

def one_knee(breath=0.0):
    reset()
    aim("thigh_r", V(0, 0.12, -1)); aim("calf_r", V(0, 1, -0.04)); aim("foot_r", V(0, 0.9, -0.3))   # right knee down
    aim("thigh_l", V(0.04, -1, -0.3)); aim("calf_l", V(0, 0.05, -1))                                # left foot planted
    kn = head("calf_r"); move("pelvis", Vector((0, REST_KNEE_Y + 0.05 - kn.y, GROUND_KNEE - kn.z)))
    rot("spine_01", X, 14 + breath); rot("spine_03", X, 6); rot("neck_01", X, 16); rot("Head", X, 8)
    aim("upperarm_l", V(0.2, -0.55, -0.85)); aim("lowerarm_l", V(0, -0.5, -0.85))   # hand on the left knee
    aim("upperarm_r", V(-0.18, -0.05, -1)); aim("lowerarm_r", V(-0.1, -0.3, -1))

new_action("Hit_Kneel")
stand(); key(0); stagger(); key(5); one_knee(); key(13); one_knee(3); key(20); one_knee(); key(27)
sag(); key(38); stand(); key(48)
finish()

# ---------------------------------------------------------------- export everything
tr = arm.animation_data.nla_tracks
for t in list(tr): tr.remove(t)
for a in existing + [a for a in bpy.data.actions if a.name in NEW]:
    t = tr.new(); t.name = a.name; t.strips.new(a.name, int(a.frame_range[0]), a); t.mute = True
for o in bpy.context.selected_objects: o.select_set(False)
arm.select_set(True); bpy.context.view_layer.objects.active = arm
bpy.ops.export_scene.gltf(filepath=OUT, export_format="GLB", use_selection=True, export_skins=True, export_animations=True,
                          export_animation_mode="NLA_TRACKS", export_apply=False, export_yup=True)
print("[clips] wrote", OUT, len(existing) + len(NEW), "clips")
