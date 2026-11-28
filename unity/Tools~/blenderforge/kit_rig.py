"""Creature rig extensions on the UBC humanoid skeleton (owner direction 2026-11-27: the six Grok heroes).

A design's "rig" block asks for any of:
    "neck":  0.08          # metres: a longer neck (a neck_02 bone; the head moves forward and up)
    "jaw":   true          # a jaw bone under the head (the dragon jaw is skinned to it; the game opens it)
    "tail":  6             # a tail chain tail_01..tail_N from the pelvis (the game sways it on springs)
    "legs":  "digitigrade" # the legs re-posed to a digitigrade stance, made the new rest pose
    "wings": true          # wing_l / wing_r bones on the upper back (the game flutters them)

The UBC clips key translation, rotation and scale on every bone, absolutely. A bone whose rest pose
changes here (the head, the legs) would be dragged back by every clip. The game's RigExtras component
compares each bone's rest with the shared UBC rest and re-applies the difference after each frame;
bones that no clip names (tail, jaw, wings, neck_02) are driven procedurally.

Call extend(c, arm) right after ubc_parts.build(pose=False), before any kit piece is fitted.
"""
import bpy, math
from mathutils import Vector, Matrix


def _meshes():
    return [o for o in bpy.context.scene.objects if o.type == "MESH"]


def _edit(arm):
    for x in bpy.context.selected_objects: x.select_set(False)
    arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    return arm.data.edit_bones


def _object_mode():
    if bpy.context.object and bpy.context.object.mode != "OBJECT": bpy.ops.object.mode_set(mode="OBJECT")


def _shift_weighted(bones_w, d):
    """Move every body vertex by d times its weight in the given bones ({bone: factor})."""
    for o in _meshes():
        idx = {vg.index: bones_w[vg.name] for vg in o.vertex_groups if vg.name in bones_w}
        if not idx: continue
        mwi = o.matrix_world.inverted().to_3x3()
        for v in o.data.vertices:
            w = sum(g.weight * idx[g.group] for g in v.groups if g.group in idx)
            if w > 1e-4: v.co += (mwi @ d) * min(1.0, w)


def neck(arm, L):
    """A longer neck: neck_02 between neck_01 and Head; the head (and everything on it) moves forward and
    up by L, the neck skin stretching with it."""
    eb = _edit(arm)
    n1, hd = eb["neck_01"], eb["Head"]
    d = Vector((0, -L * 0.35, L))
    n2 = eb.new("neck_02"); n2.head = n1.tail.copy(); n2.tail = hd.head + d; n2.parent = n1; n2.use_connect = False
    kids = [b for b in eb if b.parent and (b.parent.name == "Head" or b.name == "Head")]
    for b in [hd] + [b for b in eb if _descends(b, "Head")]:
        b.head += d; b.tail += d
    hd.parent = n2
    _object_mode()
    _shift_weighted({"Head": 1.0, "neck_01": 0.45}, d)
    for o in _meshes():   # the stretched neck skin follows the new bone
        if "neck_01" in o.vertex_groups and "neck_02" not in o.vertex_groups:
            g1 = o.vertex_groups["neck_01"]; g2 = o.vertex_groups.new(name="neck_02")
            for v in o.data.vertices:
                for g in v.groups:
                    if g.group == g1.index and g.weight > 0.01: g2.add([v.index], g.weight * 0.5, "REPLACE"); g1.add([v.index], g.weight * 0.5, "REPLACE")


def _descends(b, name):
    p = b.parent
    while p is not None:
        if p.name == name: return True
        p = p.parent
    return False


def jaw(arm, hb):
    """A jaw bone hinged under the ears, pointing forward to the chin."""
    eb = _edit(arm)
    hd = eb["Head"]
    j = eb.new("jaw"); j.parent = hd
    j.head = Vector((hb["cx"], hb["cy"] + 0.01, hb["eye"] - 0.035)); j.tail = Vector((hb["cx"], hb["front"] - 0.06, hb["eye"] - 0.06))
    _object_mode()


def tail(arm, n, length=0.95):
    """A tail chain from the base of the spine, curving down and back to the ground: tail_01..tail_N."""
    eb = _edit(arm)
    pv = eb["pelvis"]
    root = Vector((pv.head.x, pv.head.y + 0.1, pv.head.z - 0.02))
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append(root + Vector((0.03 * math.sin(t * 2.0), 0.6 * length * t, -0.68 * length * t ** 1.3 + 0.05 * t)))
    prev = pv
    for i in range(n):
        b = eb.new(f"tail_{i + 1:02d}"); b.head = pts[i]; b.tail = pts[i + 1]; b.parent = prev; b.use_connect = i > 0
        prev = b
    _object_mode()
    return pts


def tail_weights(pts, n):
    """Weights for a mesh along the tail chain: each vertex follows its nearest segment, blended."""
    def w(p):
        best = []
        for i in range(n):
            a, b = pts[i], pts[i + 1]; ab = b - a
            t = max(0.0, min(1.0, (p - a).dot(ab) / max(1e-9, ab.length_squared)))
            best.append(((a + ab * t - p).length, i, t))
        best.sort(); d0, i0, t0 = best[0]
        out = {f"tail_{i0 + 1:02d}": 1.0}
        if t0 > 0.6 and i0 + 1 < n: out = {f"tail_{i0 + 1:02d}": 1 - (t0 - 0.6), f"tail_{i0 + 2:02d}": t0 - 0.6}
        if i0 == 0 and t0 < 0.3: out = {"pelvis": 0.3 - t0, "tail_01": 0.7 + t0}
        s = sum(out.values()); return {k: v / s for k, v in out.items()}
    return w


def wings(arm):
    """wing_l / wing_r on the upper back (children of spine_03), where the wing kit roots its wings."""
    eb = _edit(arm)
    s3 = eb["spine_03"]
    for sx, side in ((1, "l"), (-1, "r")):
        b = eb.new("wing_" + side); b.parent = s3
        b.head = Vector((s3.head.x + 0.07 * sx, s3.head.y + 0.13, s3.head.z + 0.07)); b.tail = b.head + Vector((0.25 * sx, 0.08, 0.15))
    _object_mode()


def digitigrade(arm):
    """Re-pose the legs to a digitigrade stance (knee forward, long raised metatarsal, toes on the ground)
    and make that the rest pose: the body meshes are baked to it and the armature's rest re-applied."""
    for x in bpy.context.selected_objects: x.select_set(False)
    arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE")
    dirs = {"thigh": Vector((0, -0.38, -1)), "calf": Vector((0, 0.62, -1)), "foot": Vector((0, -0.22, -1)), "ball": Vector((0, -1, -0.02))}
    for side in ("l", "r"):
        for k in ("thigh", "calf", "foot", "ball"):
            pb = arm.pose.bones[f"{k}_{side}"]
            bpy.context.view_layer.update()
            M = arm.matrix_world @ pb.matrix
            cur = (arm.matrix_world @ pb.tail - arm.matrix_world @ pb.head).normalized()
            q = cur.rotation_difference(dirs[k].normalized())
            head = M.translation
            Mn = Matrix.Translation(head) @ q.to_matrix().to_4x4() @ Matrix.Translation(-head) @ M
            pb.matrix = arm.matrix_world.inverted() @ Mn
    bpy.context.view_layer.update()
    # the toes back onto the ground: lower the foot and everything below it (the metatarsal lengthens)
    lows = [min((arm.matrix_world @ arm.pose.bones[f"ball_leaf_{s}"].tail).z, (arm.matrix_world @ arm.pose.bones[f"ball_{s}"].head).z) for s in ("l", "r")]
    drop = max(0.0, min(lows) - 0.012)
    bpy.ops.object.mode_set(mode="OBJECT")
    # bake the posed legs into the meshes, then make the pose the rest pose
    for o in _meshes():
        md = next((m for m in o.modifiers if m.type == "ARMATURE"), None)
        if md is None: continue
        for x in bpy.context.selected_objects: x.select_set(False)
        o.select_set(True); bpy.context.view_layer.objects.active = o
        name = md.name
        bpy.ops.object.modifier_copy(modifier=name)
        bpy.ops.object.modifier_apply(modifier=name)
    for x in bpy.context.selected_objects: x.select_set(False)
    arm.select_set(True); bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="POSE"); bpy.ops.pose.armature_apply(selected=False); bpy.ops.object.mode_set(mode="OBJECT")
    if drop > 0.002:
        eb = _edit(arm)
        for side in ("l", "r"):
            for k in ("foot", "ball", "ball_leaf"):
                b = eb[f"{k}_{side}"]
                if k != "foot": b.head.z -= drop
                b.tail.z -= drop
        _object_mode()
        _shift_weighted({f"{k}_{s}": 1.0 for k in ("ball", "ball_leaf") for s in ("l", "r")} | {f"foot_{s}": 0.7 for s in ("l", "r")}, Vector((0, 0, -drop)))


def extend(c, arm, head_box=None):
    """Apply the design's rig block. Returns {"tail_pts": [...], "tail_n": n} for the kit to skin to."""
    r = c.get("rig") or {}
    info = {}
    if r.get("legs") == "digitigrade": digitigrade(arm)
    if r.get("neck"): neck(arm, float(r["neck"]))
    if r.get("jaw") and head_box is not None: jaw(arm, head_box(arm))
    if r.get("tail"):
        n = int(r["tail"]); info["tail_pts"] = tail(arm, n, float(r.get("tail_len", 0.95))); info["tail_n"] = n
    if r.get("wings"): wings(arm)
    bpy.context.view_layer.update()
    return info
