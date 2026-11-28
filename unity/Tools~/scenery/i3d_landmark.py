# Image-to-3D landmarks (docs/sigilbound/art/CAMPAIGN_SCENES_PLAN.md): turns a generated GLB (Gemini concept ->
# fal.ai TRELLIS) into a game landmark that is painted like the rest of the scenery.
#   python i3d_landmark.py NAME SOURCE.glb   (Blender's bpy; settings per landmark in LANDMARKS below)
#   python i3d_landmark.py --all              (every landmark whose source is in Tools~/models/landmarks/)
# Steps: join the parts, turn and scale to the landmark's size (pivot at the footprint centre, base on the ground),
# decimate to the triangle budget, then split the TRELLIS texture into a gradient map: per texel a material row
# (stone, wood, thatch ... clustered from its colour) and a shade (its light and dark inside that material). The
# game paints the texture at arena build time from the chapter's ramp (Client/SceneryKit, SceneryPaint), so the
# landmark wears the same palette, banding and moss as the procedural props, by day and by night.
# Output: Resources/Scenery/Landmarks/NAME.bytes (SBL1, Client/SceneryKit.ParseLandmark), which replaces the
# procedural mesh of the same name. Then run mesh_bounds.py.
import bpy, bmesh, math, os, struct, sys
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
UNITY = os.path.normpath(os.path.join(HERE, "..", ".."))
SRC = os.path.join(UNITY, "Tools~", "models", "landmarks")
OUT = os.path.join(UNITY, "Assets", "Resources", "Scenery", "Landmarks")

# Material rows (make_props.py).
BARK, LEAF_A, LEAF_B, LEAF_C, ROCK, MOSS, WOOD, STONE, CLOTH, METAL, CAP, STEM, REED, EARTH, TRIM, COAL = range(16)

# name: height (m, before the piece's scale), yaw (deg, turns the model's front to -Z = toward the camera), sink (m below
# the ground), triangle budget, texture size, row overrides by cluster colour class.
LANDMARKS = {
    "drowned_mill": dict(height=4.6, yaw=-90, sink=0.35, tris=3600, tex=256),   # axle stub to +x (the wheel socket, SceneMeshes.Sockets)
    "cottage": dict(height=3.4, yaw=0, sink=0.05, tris=2400, tex=256),
    "wagon": dict(height=2.6, yaw=0, sink=0.05, tris=2400, tex=256),
    "stilt_hut": dict(height=4.2, yaw=0, sink=0.1, tris=2400, tex=256),
    "bellspire": dict(height=11.9, yaw=0, sink=0.2, tris=3200, tex=256),
    "stormcrown": dict(height=7.8, yaw=0, sink=0.1, tris=3600, tex=256),
    "lighthouse": dict(height=9.7, yaw=0, sink=0.5, tris=3600, tex=256),
    "sea_throne": dict(height=11.2, yaw=0, sink=0.1, tris=3600, tex=256),
    "sunken_cathedral": dict(height=6.9, yaw=0, sink=0.6, tris=3800, tex=256, glow=True, glow_v=0.5, glow_s=0.3),
    "kings_barrow": dict(height=4.2, yaw=0, sink=0.15, tris=3600, tex=256, glow=True, glow_v=0.6, glow_s=0.35),
    "tollhut": dict(height=2.9, yaw=0, sink=0.05, tris=2000, tex=256),
    "warehouse": dict(height=3.6, yaw=0, sink=0.05, tris=2000, tex=256),
    "chapel": dict(height=3.0, yaw=0, sink=0.05, tris=2000, tex=256),
    "shipwreck": dict(height=3.4, yaw=0, sink=0.4, tris=2400, tex=256),
    "gatehouse": dict(height=3.5, yaw=0, sink=0.1, tris=2000, tex=256),
    "cairn": dict(height=3.3, yaw=0, sink=0.05, tris=1600, tex=256),
}


def classify(rgb):
    """A material row from a cluster's mean colour (sRGB 0..1)."""
    r, g, b = rgb; v = max(rgb); mn = min(rgb); s = 0 if v == 0 else (v - mn) / v
    if v == mn: h = 0
    elif v == r: h = ((g - b) / (v - mn)) % 6
    elif v == g: h = (b - r) / (v - mn) + 2
    else: h = (r - g) / (v - mn) + 4
    h /= 6
    if v < 0.16: return COAL                      # window holes, deep gaps
    if s < 0.25: return EARTH if v < 0.3 else STONE   # TRELLIS textures come out desaturated  # slate roofs; stone and whitewash
    if 0.17 < h < 0.45: return MOSS
    if h < 0.04 or h > 0.93: return CLOTH
    if 0.5 < h < 0.8: return METAL if v < 0.5 else STONE
    if 0.07 <= h <= 0.17 and v > 0.55: return REED  # straw thatch, sacks
    return BARK if v < 0.32 else WOOD             # dark beams, planks


def kmeans(x, k, iters=12, seed=3):
    rng = np.random.default_rng(seed)
    c = x[rng.choice(len(x), k, replace=False)]
    for _ in range(iters):
        d = ((x[:, None, :] - c[None]) ** 2).sum(-1); lab = d.argmin(1)
        for j in range(k):
            if (lab == j).any(): c[j] = x[lab == j].mean(0)
    return c, lab


def build(name, src):
    cfg = LANDMARKS[name]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    obs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in obs: o.select_set(True)
    bpy.context.view_layer.objects.active = obs[0]
    if len(obs) > 1: bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    # image from the material's base colour
    img = None
    for m in ob.data.materials:
        if m and m.use_nodes:
            for n in m.node_tree.nodes:
                if n.type == "TEX_IMAGE" and n.image is not None: img = n.image; break
        if img: break
    assert img is not None, "no base colour texture"
    W, H = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)[..., :3]
    # size and pivot: Blender is Z-up; the base sits at z = -sink, the footprint centre at the origin
    me = ob.data
    co = np.array([v.co[:] for v in me.vertices])
    lo, hi = co.min(0), co.max(0)
    k = cfg["height"] / (hi[2] - lo[2])
    off = np.array([-(lo[0] + hi[0]) / 2, -(lo[1] + hi[1]) / 2, -lo[2]])
    for v in me.vertices:
        p = (np.array(v.co[:]) + off) * k
        a = math.radians(cfg["yaw"]); x, y = p[0] * math.cos(a) - p[1] * math.sin(a), p[0] * math.sin(a) + p[1] * math.cos(a)
        v.co = (x, y, p[2] - cfg["sink"])
    # decimate to the budget
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.mesh.quads_convert_to_tris(); bpy.ops.object.mode_set(mode="OBJECT")
    tris0 = len(me.polygons)
    if tris0 > cfg["tris"]:
        mod = ob.modifiers.new("dec", "DECIMATE"); mod.ratio = cfg["tris"] / tris0
        bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.mesh.quads_convert_to_tris(); bpy.ops.object.mode_set(mode="OBJECT")
    me = ob.data
    me.calc_loop_triangles()
    uvl = me.uv_layers.active.data
    # one vertex per (vertex, uv) pair
    verts, index, tris = [], {}, []
    for lt in me.loop_triangles:
        tri = []
        for li in lt.loops:
            vi = me.loops[li].vertex_index; uv = tuple(round(c, 5) for c in uvl[li].uv)
            key = (vi, uv)
            if key not in index:
                index[key] = len(verts)
                n = me.loops[li].normal if hasattr(me.loops[li], "normal") else me.vertices[vi].normal
                verts.append((me.vertices[vi].co[:], tuple(n), uv))
            tri.append(index[key])
        tris.append(tri)
    assert len(verts) < 65536
    # gradient map: k-means the texels used by the mesh into material clusters
    T = cfg["tex"]
    small = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)[..., :3]
    ys = (np.arange(T) + 0.5) / T * H; xs = (np.arange(T) + 0.5) / T * W
    small = small[ys.astype(int)][:, xs.astype(int)]               # nearest downsample, row 0 = bottom (Blender)
    srgb = np.where(small <= 0.0031308, small * 12.92, 1.055 * np.power(np.clip(small, 0, 1), 1 / 2.4) - 0.055)  # linear -> sRGB
    flat = srgb.reshape(-1, 3)
    lum = flat @ np.array([0.299, 0.587, 0.114])
    used = lum > 0.02                                               # unused atlas space is black
    cent, lab = kmeans(flat[used], 7)
    rows = np.array([classify(c) for c in cent])
    lab_all = ((flat[:, None, :] - cent[None]) ** 2).sum(-1).argmin(1)
    row_map = rows[lab_all]
    if cfg.get("glow"):
        # glowing parts (a rose window, a Sigil shard): bright, saturated texels become row 16 (GLOW): the game paints
        # them in the stop's glow colour and lights them (emission mask), so the boss-stage pulse still has its glow
        mx, mn = flat.max(1), flat.min(1); sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1e-6), 0)
        g = (mx > cfg.get("glow_v", 0.7)) & (sat > cfg.get("glow_s", 0.4))
        row_map = np.where(g, 16, row_map)
        print(name, "glow texels:", int(g.sum()))
    shade = np.zeros(len(flat))
    for j in range(len(cent)):
        m = lab_all == j
        if m.any():
            Lm = np.median(lum[m & used]) if (m & used).any() else lum[m].mean()
            shade[m] = 0.55 + (lum[m] - Lm) * 2.4
    shade = np.clip(shade, 0.04, 1.0)
    print(name, "clusters:", [(tuple(np.round(c, 2)), int(r)) for c, r in zip(cent, rows)])
    # write SBL1
    pos = np.array([(p[0], p[2], p[1]) for p, _, _ in verts])         # Blender (x, y, z) -> Unity (x, z, y)
    nrm = np.array([(n[0], n[2], n[1]) for _, n, _ in verts])
    uvs = np.array([uv for _, _, uv in verts])
    plo, psz = pos.min(0), np.maximum(pos.max(0) - pos.min(0), 1e-4)
    q = np.round((pos - plo) / psz * 65535 - 32768).astype("<i2")
    nq = np.clip(np.round(nrm / np.maximum(np.linalg.norm(nrm, axis=1, keepdims=True), 1e-6) * 127), -127, 127).astype("i1")
    uq = np.clip(np.round(np.mod(uvs, 1.0000001) * 65535), 0, 65535).astype("<u2")
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".bytes")
    with open(path, "wb") as f:
        f.write(b"SBL1"); f.write(bytes([len(name)])); f.write(name.encode())
        f.write(struct.pack("<ii", len(verts), len(tris) * 3))
        f.write(struct.pack("<3f", *plo)); f.write(struct.pack("<3f", *psz))
        for i in range(len(verts)):
            f.write(q[i].tobytes()); f.write(nq[i].tobytes()); f.write(uq[i].tobytes())
        for a, b, c in tris: f.write(struct.pack("<3H", a, c, b))    # handedness flips with the axis swap
        f.write(struct.pack("<hh", T, T))
        f.write(np.round(shade * 255).astype(np.uint8).tobytes())
        f.write(row_map.astype(np.uint8).tobytes())
    print(f"{name}: {tris0} -> {len(tris)} tris, {len(verts)} verts, tex {T}, {os.path.getsize(path) // 1024} KB -> {path}")
    return path


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    if args and args[0] == "--all":
        for n in LANDMARKS:
            p = os.path.join(SRC, n + ".glb")
            if os.path.exists(p): build(n, p)
    else:
        build(args[0], args[1])
