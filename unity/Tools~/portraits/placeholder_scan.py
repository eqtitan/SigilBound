"""Find untextured primitive-looking parts in the shipped HD heroes.
For every loose mesh island: world bbox, vertex count, UV bbox in the atlas. A part that is >3 cm across but
whose UVs all fall inside a tiny atlas patch is shaded by one flat colour (no painted pass); flag it, and
note when it is box-like (<= 24 verts) or a low-poly cylinder.
Usage: python placeholder_scan.py -- MODELS_DIR OUT_TSV [id ...]"""
import bpy, bmesh, sys, os, glob
import numpy as np
a = sys.argv[sys.argv.index("--") + 1:]; MD, OUT, IDS = a[0], a[1], a[2:]
if not IDS: IDS = sorted(os.path.basename(p)[8:-7] for p in glob.glob(os.path.join(MD, "SB_Hero_*_HD.glb")))
rows = []
for hid in IDS:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=os.path.join(MD, f"SB_Hero_{hid}_HD.glb"))
    for o in bpy.context.scene.objects:
        if o.type != "MESH": continue
        bm = bmesh.new(); bm.from_mesh(o.data)
        bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.0005)   # weld UV-seam splits: islands = real parts
        bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
        uvl = bm.loops.layers.uv.active
        ap = next((os.path.join(MD, f"SB_Hero_{hid}_HD_albedo{e}") for e in (".jpg", ".png") if os.path.exists(os.path.join(MD, f"SB_Hero_{hid}_HD_albedo{e}"))), None)
        if ap is None: bm.free(); continue
        img = bpy.data.images.load(ap)
        W_, H_ = img.size; px = np.array(img.pixels[:], dtype=np.float32).reshape(H_, W_, img.channels)[:, :, :3]
        seen = set()
        for f0 in bm.faces:
            if f0.index in seen: continue
            stack = [f0]; isl = []
            seen.add(f0.index)
            while stack:
                f = stack.pop(); isl.append(f)
                for e in f.edges:
                    for g in e.link_faces:
                        if g.index not in seen: seen.add(g.index); stack.append(g)
            vs = {v for f in isl for v in f.verts}
            ps = [o.matrix_world @ v.co for v in vs]
            size = max(max(p[i] for p in ps) - min(p[i] for p in ps) for i in range(3))
            if size < 0.05 or uvl is None: continue
            us = [l[uvl].uv for f in isl for l in f.loops]
            xs = np.clip((np.array([u.x for u in us]) % 1.0) * W_, 0, W_ - 1).astype(int)
            ys = np.clip((np.array([u.y for u in us]) % 1.0) * H_, 0, H_ - 1).astype(int)
            # sample the face centres too, so a big face with a painted interior is not judged by its corners only
            cs = [sum((l[uvl].uv for l in f.loops), type(us[0])((0, 0))) / len(f.loops) for f in isl]
            xs = np.concatenate([xs, np.clip((np.array([u.x for u in cs]) % 1.0) * W_, 0, W_ - 1).astype(int)])
            ys = np.concatenate([ys, np.clip((np.array([u.y for u in cs]) % 1.0) * H_, 0, H_ - 1).astype(int)])
            col = px[ys, xs]; spread = float(np.sqrt(col.var(axis=0).sum()))
            mean = col.mean(axis=0); bright = float(mean.max())
            flat = spread < 0.02 and bright > 0.12          # a lit, flat-colour part (dark trims are naturally flat)
            boxy = len(vs) <= 16 and size >= 0.06           # a box, quad slab or 6-sided cylinder
            if flat or boxy:
                c = sum(ps, ps[0] * 0) / len(ps)
                rows.append((hid, len(vs), len(isl), round(size, 3), round(c.x, 2), round(c.y, 2), round(c.z, 2), round(spread, 4), [round(float(v), 2) for v in mean], ("flat " if flat else "") + ("boxy" if boxy else "")))
        bm.free()
    print("[scan]", hid, flush=True)
with open(OUT, "w") as f:
    f.write("hero\tverts\tfaces\tsize_m\tx\ty\tz\tspread\tmean_rgb\twhy\n")
    for r in rows: f.write("\t".join(map(str, r)) + "\n")
print("flagged", len(rows))
