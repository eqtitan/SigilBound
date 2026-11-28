"""Attach separately generated accessories (skeleton library 7.3, owner decision 2026-11-28) to a rigged HD hero.

    bpyenv/bin/python accessory_attach.py -- HERO_HD.glb OUT_DIR HERO_ID ITEM.glb:SOCKET[:chain] [ITEM.glb:SOCKET[:chain]]
        [--size ITEM=METRES] [--grip ITEM=FRACTION] [--chains K] [--bones J]

HERO_HD.glb is i3d_rig's output (UBC skeleton in T-pose, placeholder material, SB_Hero_<id>_HD_albedo.jpg [+ _glow.png]
beside it). Each ITEM.glb is a raw TRELLIS model of one accessory (artgen/prompts/accessory.txt). Writes
OUT_DIR/SB_Hero_<HERO_ID>_HD.glb + _albedo.jpg [+ _glow.png]: one mesh, one atlas (the body's albedo on the left two
thirds, up to two accessory textures stacked in the right third), so the game loads it like any other HD hero.

Sockets are RigPhysics.StandardSockets (Core/Rig/RigPhysics.cs; offsets there are glTFast model space, here converted
to Blender's: x_b = -x, y_b = -z, z_b = y):
  hand_r / hand_l   rigid on the hand. The item's long axis (as generated: upright, business end up) points forward
                    out of the fist; the grip point is --grip (fraction of the length from the bottom, default 0.2)
                    and sits in the palm. --size is the item's length (default 0.8 m).
  shield_arm        rigid on lowerarm_l, face outward (+X), centred on the forearm. --size = diameter (default 0.6 m).
  head_top          rigid on Head, resting on the top of the head mesh. --size = width (default 0.45 m).
  back / hip_l / hip_r  rigid on spine_03 / pelvis, touching the body surface there. --size = length (default 0.7 m).
  back:chain        cloth (capes): generated seen from behind, laid flat. Scaled from the shoulders to the knees and
                    the shoulder width, hung just off the back (pushed out of the body where they meet), and weighted
                    to K spring chains of J bones (nodes sb_<k>_<j>, the last node the tip, parented to spine_03),
                    the top band blended into spine_03. Client/SpringBones.cs finds and simulates them; the per-hero
                    numbers are Core/Kit/SpringRigs.cs.
Every rigid item is bound 100% to its bone, so it can never follow the legs (the fused-shell failure this replaces).
"""
import bpy, bmesh, sys, os, math, argparse
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

SOCKETS = {  # name: (bone, glTFast offset)
    "hand_r": ("hand_r", (0, 0, 0)), "hand_l": ("hand_l", (0, 0, 0)), "back": ("spine_03", (0, 0.05, -0.16)),
    "hip_l": ("pelvis", (-0.17, 0, 0)), "hip_r": ("pelvis", (0.17, 0, 0)), "shield_arm": ("lowerarm_l", (0, 0.12, 0)),
    "head_top": ("Head", (0, 0.2, 0)),
}
DEFAULT_SIZE = {"hand_r": 0.8, "hand_l": 0.8, "shield_arm": 0.6, "head_top": 0.45, "back": 0.7, "hip_l": 0.7, "hip_r": 0.7}
MAX_TRIS = {"chain": 5000, "rigid": 3500}


def b_off(o): return Vector((-o[0], -o[2], o[1]))


def args():
    argv = sys.argv[sys.argv.index("--") + 1:]
    ap = argparse.ArgumentParser()
    ap.add_argument("hero_glb"); ap.add_argument("out"); ap.add_argument("hero_id"); ap.add_argument("items", nargs="+")
    ap.add_argument("--size", action="append", default=[]); ap.add_argument("--grip", action="append", default=[])
    ap.add_argument("--chains", type=int, default=5); ap.add_argument("--bones", type=int, default=4)
    a = ap.parse_args(argv)
    kv = lambda lst: {k: float(v) for k, v in (x.split("=") for x in lst)}
    a.size, a.grip = kv(a.size), kv(a.grip)
    return a


def import_glb(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


def item_mesh(path, max_tris):
    new = import_glb(path)
    ms = [o for o in new if o.type == "MESH"]
    for o in bpy.context.selected_objects: o.select_set(False)
    for o in ms: o.select_set(True)
    bpy.context.view_layer.objects.active = ms[0]
    if len(ms) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active
    o.parent = None
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    for e in new:
        if e != o and e.name in bpy.data.objects: bpy.data.objects.remove(e, do_unlink=True)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-4)
    # drop small floating islands (TRELLIS crumbs)
    bm.verts.ensure_lookup_table(); seen = set(); isl = []
    for v in bm.verts:
        if v.index in seen: continue
        st = [v]; seen.add(v.index); cur = []
        while st:
            u = st.pop(); cur.append(u)
            for e in u.link_edges:
                w = e.other_vert(u)
                if w.index not in seen: seen.add(w.index); st.append(w)
        isl.append(cur)
    isl.sort(key=len, reverse=True)
    kill = [v for c in isl[1:] if len(c) < 0.02 * len(isl[0]) for v in c]
    bmesh.ops.delete(bm, geom=kill, context="VERTS")
    bm.to_mesh(o.data); bm.free()
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    if tris > max_tris:
        m = o.modifiers.new("dec", "DECIMATE"); m.ratio = max_tris / tris
        bpy.ops.object.modifier_apply(modifier=m.name)
    img = None
    for mat in o.data.materials:
        if mat and mat.use_nodes:
            for n in mat.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image and any(l.to_socket.name == "Base Color" for l in n.outputs[0].links):
                    img = n.image
    return o, img


def cloth_shell(o, thick=0.02):
    """Image-to-3D models a cape as worn by an invented mannequin (pilot 2026-11-28: a whole torso and legs inside).
    The cape's outer side is the one that looks like a smooth sheet from outside (lowest spread of first-hit depth);
    keep only faces that are exposed on that side or lie within `thick` of it (the cloth's own inner surface), and turn
    the model so the outer side faces +Y (behind the hero). Returns the side it found (+1 / -1 as generated)."""
    bm = bmesh.new(); bm.from_mesh(o.data); bm.faces.ensure_lookup_table()
    bvh = BVHTree.FromBMesh(bm)
    P = [v.co for v in bm.verts]; lo = Vector([min(p[i] for p in P) for i in range(3)]); hi = Vector([max(p[i] for p in P) for i in range(3)])
    def spread(sign):
        ys = []
        for i in range(24):
            for j in range(24):
                x = lo.x + (hi.x - lo.x) * (0.2 + 0.6 * i / 23); z = lo.z + (hi.z - lo.z) * (0.2 + 0.6 * j / 23)
                h = bvh.ray_cast(Vector((x, (hi.y + 1) if sign > 0 else (lo.y - 1), z)), Vector((0, -sign, 0)))
                if h[0] is not None: ys.append(h[0].y)
        if len(ys) < 10: return 9.0
        m = sum(ys) / len(ys); return (sum((y - m) ** 2 for y in ys) / len(ys)) ** 0.5
    side = 1 if spread(1) <= spread(-1) else -1
    d = Vector((0, side, 0)); kill = []
    for f in bm.faces:
        c = f.calc_center_median(); h = bvh.ray_cast(c + d * 1e-4, d)
        if h[0] is not None and h[3] > thick: kill.append(f)
    bmesh.ops.delete(bm, geom=kill, context="FACES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    bm.to_mesh(o.data); bm.free()
    if side < 0:
        o.data.transform(Matrix.Rotation(math.pi, 4, "Z"))
    print(f"[acc] cloth: outer side {'+Y' if side > 0 else '-Y'} as generated, dropped {len(kill)} hidden faces (mannequin/inside)")
    return side


def verts_w(o): return [o.matrix_world @ v.co for v in o.data.vertices]


def bbox(pts):
    return Vector([min(p[i] for p in pts) for i in range(3)]), Vector([max(p[i] for p in pts) for i in range(3)])


def main():
    a = args()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    new = import_glb(a.hero_glb)
    arm = next(o for o in new if o.type == "ARMATURE")
    body = next(o for o in new if o.type == "MESH" and any(m.type == "ARMATURE" for m in o.modifiers))
    stem = a.hero_glb[:-len(".glb")]
    from PIL import Image
    alb = Image.open(stem + "_albedo.jpg").convert("RGB"); W, H = alb.size
    glow = Image.open(stem + "_glow.png").convert("RGB") if os.path.exists(stem + "_glow.png") else None
    AW = W + W // 2
    atlas = Image.new("RGB", (AW, H), (128, 128, 128)); atlas.paste(alb, (0, 0))
    gatlas = None
    if glow:
        gatlas = Image.new("RGB", (AW // 2, H // 2)); gatlas.paste(glow, (0, 0))
    uv_name = body.data.uv_layers.active.name
    for l in body.data.uv_layers.active.data: l.uv = (l.uv[0] * W / AW, l.uv[1])
    if len(a.items) > 2: sys.exit("at most two accessories per atlas")

    mw = arm.matrix_world
    def bone(n): return arm.data.bones[n]
    def head(n): return mw @ bone(n).head_local
    def tail(n): return mw @ bone(n).tail_local
    BP = verts_w(body)

    for slot, spec in enumerate(a.items):
        parts = spec.split(":"); path, sock = parts[0], parts[1]; chain = len(parts) > 2 and parts[2] == "chain"
        key = os.path.splitext(os.path.basename(path))[0]
        o, img = item_mesh(path, MAX_TRIS["chain" if chain else "rigid"])
        # texture into the slot (right third, slot 0 top, slot 1 bottom), UVs remapped
        sx0, sy0, sw = W, slot * (H // 2), W // 2
        if img is not None:
            tmp = os.path.join(a.out, f"_tex_{slot}.png"); os.makedirs(a.out, exist_ok=True)
            img.filepath_raw = tmp; img.file_format = "PNG"; img.save()
            atlas.paste(Image.open(tmp).convert("RGB").resize((sw, sw), Image.LANCZOS), (sx0, sy0)); os.remove(tmp)
        uvl = o.data.uv_layers.active
        for l in uvl.data:
            u, v = l.uv
            u = min(max(u, 0.002), 0.998); v = min(max(v, 0.002), 0.998)
            l.uv = ((sx0 + u * sw) / AW, 1 - (sy0 + (1 - v) * sw) / H)
        uvl.name = uv_name
        bname, off = SOCKETS[sock]
        P = verts_w(o); lo, hi = bbox(P)
        size = a.size.get(key, DEFAULT_SIZE[sock])
        groups = {}
        if chain:
            # cape: strip the invented wearer, outer side to +Y (behind the hero)
            cloth_shell(o)
            P = verts_w(o); lo, hi = bbox(P)
            top_z = max(head("upperarm_l").z, head("upperarm_r").z) + 0.05
            bot_z = (head("calf_l").z + head("calf_r").z) / 2
            span = (head("upperarm_l") - head("upperarm_r")).length * 1.25
            topband = [p for p in P if p.z > hi.z - 0.12 * (hi.z - lo.z)]
            tw = max(p.x for p in topband) - min(p.x for p in topband)
            kz = (top_z - bot_z) / (hi.z - lo.z); kx = span / tw; ky = min(kx, kz) * 0.6   # flatten the generated drape: it hangs from the back
            cx = (lo.x + hi.x) / 2
            for v in o.data.vertices:
                v.co = Vector(((v.co.x - cx) * kx, (v.co.y - lo.y) * ky, bot_z + (v.co.z - lo.z) * kz))
            P = [v.co.copy() for v in o.data.vertices]
            # the body's back surface: max y per (x, z) cell, ignoring anything far behind the torso (a tail)
            torso = [p for p in BP if head("spine_02").z < p.z < top_z and abs(p.x) < span / 2]
            back_y = max(p.y for p in torso)
            cell = 0.04; grid = {}
            for p in BP:
                if p.y > back_y + 0.06 or p.y < -0.05: continue
                k = (int(math.floor(p.x / cell)), int(math.floor(p.z / cell)))
                grid[k] = max(grid.get(k, -9), p.y)
            ymin_top = min(p.y for p in P if p.z > top_z - 0.15)
            dy = back_y + 0.02 - ymin_top
            pushed = 0
            for v in o.data.vertices:
                v.co.y += dy
                k0 = (int(math.floor(v.co.x / cell)), int(math.floor(v.co.z / cell)))
                near = [grid[(k0[0] + i, k0[1] + j)] for i in (-1, 0, 1) for j in (-1, 0, 1) if (k0[0] + i, k0[1] + j) in grid]
                if near and v.co.y < max(near) + 0.02:
                    v.co.y = max(near) + 0.02; pushed += 1
            # spring chains: K columns across the width, J bones from the top band to the hem
            K, J = a.chains, a.bones
            P = [v.co.copy() for v in o.data.vertices]; lo, hi = bbox(P)
            xs = [lo.x + (hi.x - lo.x) * (0.1 + 0.8 * k / (K - 1)) for k in range(K)]
            z0, z1 = top_z - 0.06, lo.z
            base = 1 + max([int(b.name.split("_")[1]) for b in arm.data.bones if b.name.startswith("sb_") and b.name.count("_") == 2] or [-1])
            bpy.context.view_layer.objects.active = arm
            bpy.ops.object.select_all(action="DESELECT"); arm.select_set(True)
            bpy.ops.object.mode_set(mode="EDIT")
            eb = arm.data.edit_bones; inv = mw.inverted()
            chains = []
            for k, x in enumerate(xs):
                pts = []
                for j in range(J + 1):
                    z = z0 + (z1 - z0) * j / J
                    near = [p.y for p in P if abs(p.x - x) < 0.06 and abs(p.z - z) < 0.06]
                    y = (sorted(near)[len(near) // 2] if near else (pts[-1].y if pts else back_y + 0.03))
                    pts.append(Vector((x, y, z)))
                names = []
                for j in range(J + 1):
                    nm = f"sb_{base + k}_{j}"; b = eb.new(nm)
                    h = pts[j]; t = pts[j + 1] if j < J else pts[j] + (pts[j] - pts[j - 1]) * 0.3
                    b.head, b.tail = inv @ h, inv @ t
                    b.parent = eb["spine_03"] if j == 0 else eb[names[-1]]
                    b.use_connect = j > 0; b.use_deform = j < J
                    names.append(nm)
                chains.append(names)
            bpy.ops.object.mode_set(mode="OBJECT")
            for v, p in zip(o.data.vertices, P):
                t = (z0 - p.z) / (z0 - z1) * J
                # across: the two neighbouring chains
                f = (p.x - xs[0]) / (xs[-1] - xs[0]) * (K - 1); f = min(max(f, 0), K - 1)
                k0 = min(int(f), K - 2); wk = [(k0, 1 - (f - k0)), (k0 + 1, f - k0)]
                # along: tent around the bone midpoints
                g = min(max(t - 0.5, 0), J - 1); j0 = min(int(g), J - 2) if J > 1 else 0
                wj = [(j0, 1 - (g - j0)), (j0 + 1, g - j0)] if J > 1 else [(0, 1.0)]
                spine = min(max(1 - t / 0.5, 0), 1)
                w = {"spine_03": spine} if spine > 0 else {}
                for kk, a_ in wk:
                    for jj, b_ in wj:
                        if a_ * b_ > 0: w[chains[kk][jj]] = w.get(chains[kk][jj], 0) + a_ * b_ * (1 - spine)
                top = sorted(w.items(), key=lambda kv: -kv[1])[:4]; s = sum(x for _, x in top)
                for n, x in top: groups.setdefault(n, []).append((v.index, x / s))
            print(f"[acc] {key}: cape on {K}x{J} chains sb_{base}..{base + K - 1}, scale {kx:.2f}/{kz:.2f}, pushed {pushed} verts off the body")
        else:
            if sock in ("hand_r", "hand_l"):
                L = hi.z - lo.z; k = size / L
                grip_f = a.grip.get(key, 0.2)
                gc = [p for p in P if abs((p.z - lo.z) / L - grip_f) < 0.03]
                gx = sum(p.x for p in gc) / len(gc); gy = sum(p.y for p in gc) / len(gc); gz = lo.z + grip_f * L
                d = (head(bname) - head("lowerarm" + bname[4:])).normalized()
                palm = head(bname) + d * 0.06 + Vector((0, 0, -0.015))
                R = Matrix.Rotation(math.radians(90), 4, "X")   # up (+Z) -> forward (-Y)
                M = Matrix.Translation(palm) @ R @ Matrix.Scale(k, 4) @ Matrix.Translation(Vector((-gx, -gy, -gz)))
            elif sock == "shield_arm":
                L = max(hi.x - lo.x, hi.z - lo.z); k = size / L; c = (lo + hi) / 2
                fa = (head("lowerarm_l") + head("hand_l")) / 2
                R = Matrix.Rotation(math.radians(90), 4, "Z")   # face (-Y) -> outward (+X)
                M = Matrix.Translation(fa + Vector((0.06 + (hi.y - lo.y) * k / 2, 0, 0))) @ R @ Matrix.Scale(k, 4) @ Matrix.Translation(-c)
            elif sock == "head_top":
                k = size / (hi.x - lo.x); c = Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))
                hp = head(bname) + b_off(off) * 0.0
                top = max(p.z for p in BP if (Vector((p.x, p.y)) - Vector((hp.x, hp.y))).length < 0.12)
                M = Matrix.Translation(Vector((hp.x, hp.y, top - 0.03))) @ Matrix.Scale(k, 4) @ Matrix.Translation(-c)
            else:
                L = max(hi - lo); k = size / L; c = (lo + hi) / 2
                M = Matrix.Translation(head(bname) + b_off(off)) @ Matrix.Scale(k, 4) @ Matrix.Translation(-c)
            o.matrix_world = M @ o.matrix_world
            bpy.ops.object.select_all(action="DESELECT"); o.select_set(True); bpy.context.view_layer.objects.active = o
            bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
            groups[bname] = [(v.index, 1.0) for v in o.data.vertices]
            print(f"[acc] {key}: rigid on {bname} ({sock}), {len(o.data.vertices)} verts, scale {k:.3f}")
        o.vertex_groups.clear()
        for n, lst in groups.items():
            vg = o.vertex_groups.new(name=n)
            for i, w in lst: vg.add([i], w, "REPLACE")
        o.data.materials.clear()
        for m in list(o.modifiers): o.modifiers.remove(m)
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True); body.select_set(True); bpy.context.view_layer.objects.active = body
        bpy.ops.object.join()

    os.makedirs(a.out, exist_ok=True)
    out = os.path.join(a.out, f"SB_Hero_{a.hero_id}_HD")
    atlas.save(out + "_albedo.jpg", quality=88, optimize=True)
    if gatlas: gatlas.save(out + "_glow.png")
    mat = bpy.data.materials.new("M_Hero_HD"); body.data.materials.clear(); body.data.materials.append(mat)
    for o in bpy.context.scene.objects: o.select_set(False)
    arm.select_set(True); body.select_set(True)
    bpy.ops.export_scene.gltf(filepath=out + ".glb", export_format="GLB", use_selection=True, export_skins=True, export_animations=False,
                              export_apply=False, export_yup=True, export_materials="PLACEHOLDER")
    tris = sum(len(p.vertices) - 2 for p in body.data.polygons)
    print(f"[acc] {a.hero_id}: {tris} tris, {len(arm.data.bones)} bones, atlas {AW}x{H}, {os.path.getsize(out + '.glb') // 1024} KB")


if __name__ == "__main__":
    main()
