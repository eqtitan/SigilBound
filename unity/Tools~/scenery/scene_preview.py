# PREVIEWS of the generated campaign scenes (Core/SceneGen.cs; docs/sigilbound/art/CAMPAIGN_SCENES_PLAN.md). Not used
# by the game. Renders each stage's generated layout (dumped by `dotnet run --project unity/Tests~ -- scenes OUT.json`)
# from the game's own camera parameters with the shipped meshes (Resources/Scenery/*.bytes, Landmarks/*.bytes), the
# biome ramp and painted ground (paint.py mirrors SceneryPaint), the stage's light and fog, grey-blue hero stand-ins on
# the battle slots, and draws a top-down plan with every camera's ground footprint and the hero lane.
#   python scene_preview.py LAYOUT.json OUT_DIR CHAPTER [CHAPTER ...]   (Blender's bpy; Cycles on the CPU)
import bpy, math, os, struct, sys, glob, json
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import paint as P
import review as R
import story_review as SR

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "Resources", "Scenery"))
K = 0.55
W_L, H_L, W_P, H_P = 640, 360, 250, 540
SAMPLES = int(os.environ.get("SAMPLES", "12"))


# ---------------- meshes
def read_sbp(path):
    b = open(path, "rb").read(); o = 4
    count, = struct.unpack_from("<i", b, o); o += 4
    out = {}
    for _ in range(count):
        ln = b[o]; o += 1; name = b[o:o + ln].decode(); o += ln
        vc, = struct.unpack_from("<i", b, o); o += 4; subs = b[o]; o += 1
        ic = struct.unpack_from("<%di" % subs, b, o); o += 4 * subs
        lo = np.array(struct.unpack_from("<3f", b, o)); sz = np.array(struct.unpack_from("<3f", b, o + 12)); o += 24
        raw = np.frombuffer(b, dtype=np.uint8, count=vc * 11, offset=o).reshape(vc, 11); o += vc * 11
        pos = lo + (raw[:, :6].copy().view("<i2").astype(np.float64) + 32768) / 65535.0 * sz
        nrm = raw[:, 6:9].copy().view("i1").astype(np.float64)
        uv = np.stack([raw[:, 9] / 255.0, (raw[:, 10] + 0.5) / 16.0], 1)
        sl = []
        for n in ic:
            sl.append(np.frombuffer(b, dtype="<u2", count=n, offset=o).astype(np.int64)); o += 2 * n
        out[name] = dict(pos=pos, nrm=nrm, uv=uv, subs=sl, tex=None)
    return out


def read_sbl(path):
    b = open(path, "rb").read(); o = 4
    ln = b[o]; o += 1; name = b[o:o + ln].decode(); o += ln
    vc, ic = struct.unpack_from("<ii", b, o); o += 8
    lo = np.array(struct.unpack_from("<3f", b, o)); sz = np.array(struct.unpack_from("<3f", b, o + 12)); o += 24
    raw = np.frombuffer(b, dtype=np.uint8, count=vc * 13, offset=o).reshape(vc, 13); o += vc * 13
    pos = lo + (raw[:, :6].copy().view("<i2").astype(np.float64) + 32768) / 65535.0 * sz
    nrm = raw[:, 6:9].copy().view("i1").astype(np.float64)
    uv = raw[:, 9:13].copy().view("<u2").astype(np.float64) / 65535.0
    idx = np.frombuffer(b, dtype="<u2", count=ic, offset=o).astype(np.int64); o += 2 * ic
    tw, th = struct.unpack_from("<hh", b, o); o += 4
    shade = np.frombuffer(b, dtype=np.uint8, count=tw * th, offset=o).reshape(th, tw) / 255.0; o += tw * th
    row = np.frombuffer(b, dtype=np.uint8, count=tw * th, offset=o).reshape(th, tw)
    return name, dict(pos=pos, nrm=nrm, uv=uv, subs=[idx], tex=(shade, row))


MESHES = {}
def load_meshes():
    for f in ("props.bytes", "story_act1.bytes", "story_act2.bytes"):
        MESHES.update(read_sbp(os.path.join(RES, f)))
    for f in glob.glob(os.path.join(RES, "Landmarks", "*.bytes")):
        n, m = read_sbl(f); MESHES[n] = m


# ---------------- materials
FOGR = [26, 64, (0.5, 0.5, 0.5)]
def fogged(nt, shader_out):
    out = nt.nodes["Material Output"]
    cam = nt.nodes.new("ShaderNodeCameraData"); mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = FOGR[0]; mr.inputs["From Max"].default_value = FOGR[1]
    nt.links.new(cam.outputs["View Distance"], mr.inputs["Value"])
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (*FOGR[2], 1)
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(mr.outputs[0], mix.inputs[0]); nt.links.new(shader_out, mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs[0])


def tex_mat(name, img, rough=0.9, emit=None, emit_k=0.0, closest=False):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = rough
    b.inputs["Specular IOR Level"].default_value = 0.15
    if img is not None:
        t = nt.nodes.new("ShaderNodeTexImage"); t.image = img; t.extension = "EXTEND"; t.interpolation = "Closest" if closest else "Linear"
        nt.links.new(t.outputs[0], b.inputs["Base Color"])
    if emit is not None:
        b.inputs["Emission Color"].default_value = (*emit, 1); b.inputs["Emission Strength"].default_value = emit_k
    fogged(nt, b.outputs[0])
    return m


def col_mat(name, c, emit=None, k=0.0):
    m = tex_mat(name, None, emit=emit, emit_k=k)
    m.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (*c, 1)
    return m


def mesh_object(name, m, mats, loc, scale, yaw_deg, uv_override=None):
    pos = m["pos"]; verts = np.stack([pos[:, 0], pos[:, 2], pos[:, 1]], 1)
    tris = []; mi = []
    for si, s in enumerate(m["subs"]):
        t = s.reshape(-1, 3)[:, [0, 2, 1]]; tris.append(t); mi.append(np.full(len(t), min(si, len(mats) - 1)))
    tris = np.concatenate(tris); mi = np.concatenate(mi)
    me = bpy.data.meshes.new(name)
    me.vertices.add(len(verts)); me.vertices.foreach_set("co", verts.astype(np.float32).ravel())
    me.loops.add(len(tris) * 3); me.loops.foreach_set("vertex_index", tris.astype(np.int32).ravel())
    me.polygons.add(len(tris)); me.polygons.foreach_set("loop_start", np.arange(0, len(tris) * 3, 3, dtype=np.int32)); me.polygons.foreach_set("loop_total", np.full(len(tris), 3, dtype=np.int32))
    me.polygons.foreach_set("material_index", mi.astype(np.int32))
    uv = uv_override if uv_override is not None else m["uv"]
    ul = me.uv_layers.new(name="UV"); ul.data.foreach_set("uv", uv[tris.ravel()].astype(np.float32).ravel())
    me.update(calc_edges=True)
    me.polygons.foreach_set("use_smooth", np.ones(len(tris), dtype=bool))
    for mt in mats: me.materials.append(mt)
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    ob.location = loc; ob.scale = (scale,) * 3; ob.rotation_euler = (0, 0, -math.radians(yaw_deg))
    return ob


# ---------------- a stage
def theme_of(biome):
    t = dict(SR.ALL[biome]) if biome in SR.ALL else dict(R.THEMES[biome])
    return t


def landmark_image(name, tex, rows_k, sil, sun, glow=None):
    shade, row = tex
    rmp = P.ramp(rows_k, sil, sun)
    xi = np.clip((shade * 63).round().astype(int), 0, 63)
    img = rmp[np.minimum(row, 15) * 4 + 1, xi]
    g = P.hexc(glow or "#ffd890")
    img = np.where((row >= 16)[..., None], g[None, None] * (0.6 + 0.4 * shade[..., None]) * 1.4, img)   # glowing texels
    return R.to_image(name, np.clip(img, 0, 1))


def build(st, aspect_key):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"; sc.cycles.samples = SAMPLES; sc.cycles.device = "CPU"; sc.cycles.use_denoising = False
    sc.cycles.max_bounces = 3; sc.cycles.diffuse_bounces = 2; sc.cycles.glossy_bounces = 1; sc.cycles.transparent_max_bounces = 6
    sc.view_settings.view_transform = "Standard"
    t = theme_of(st["biome"]); li = st["light"]
    world, lane_k = li["world"], li["lane"]
    top, bot = P.hexc(t["top"]), P.hexc(t["bot"])
    sil = P.hexc(t["sil"])
    # the mood: the horizon drifts toward the shadow colour, the sky darkens (Client/Arena.Build)
    bot = P.lerp(bot, sil * 1.6, li["dusk"]); top = P.lerp(top, top * 0.55, 0.7 if li["mood"] == "Night" else li["dusk"])
    sun = P.lerp(bot, np.ones(3), 0.45)
    FOGR[0], FOGR[1], FOGR[2] = li["fogNear"], li["fogFar"], tuple(bot)
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    amb = P.lerp(bot, np.ones(3), 0.35) * 0.55
    w.node_tree.nodes["Background"].inputs[0].default_value = (*amb, 1); w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    # sun (Unity Euler(pitch, yaw, 0) -> travel direction)
    p, y = math.radians(li["pitch"]), math.radians(li["yaw"])
    d = (math.sin(y) * math.cos(p), math.cos(y) * math.cos(p), -math.sin(p))
    sl = bpy.data.lights.new("sun", "SUN"); sl.energy = 3.0 * li["sun"]; sl.angle = 0.15
    sl.color = tuple(P.lerp(np.ones(3), P.lerp(bot, np.ones(3), 0.4), 0.35)) if li["mood"] != "Night" else (0.75, 0.82, 1.0)
    so = bpy.data.objects.new("sun", sl); sc.collection.objects.link(so)
    from mathutils import Vector
    so.rotation_euler = Vector(d).to_track_quat('-Z', 'Y').to_euler()
    # ramps: near (world-tinted) and far (pushed to the silhouette colour, as Scenery.Build)
    rows = [c * world for c in R.rows_for(t)]
    near_img = R.to_image("ramp", P.ramp(rows, sil, sun)); near = tex_mat("near", near_img)
    far_rows = [np.clip(P.lerp(c, sil, 0.55) * 0.7, 0, 1) for c in rows]
    far_img = R.to_image("framp", P.ramp(far_rows, sil, P.lerp(sil, sun, 0.3))); farm = tex_mat("far", far_img)
    mote = P.hexc(t["mote"])
    glow = col_mat("glow", mote * 0.4, emit=mote, k=0.8); fire = col_mat("fire", P.hexc("#3a1a08"), emit=P.hexc("#ffb050"), k=3.0)
    # ground: the generated heights on a grid, the painted ground with the road and the mood
    water = st["water"] if st["water"] > 0 else None
    seed = st["chapter"] * 7 + st["stage"]
    lift = 1.0
    spots = None
    if t.get("leaves"): spots = (*(P.hexc(t["leaves"][-1]) * 0.46), 0.35)
    gtex = P.ground(P.hexc(t["grass"]) * 0.46, P.hexc(t["grass2"]) * 0.46, P.hexc(t["worn"]) * 0.46 * 0.9, P.hexc(t["rock"]) * 0.5,
                    seed=seed % 97, flag=t.get("flag", False), wet=P.hexc(t["worn"]) * 0.25, water_z=water,
                    horizon=P.lerp(P.hexc(t["hill"]) * 0.46, bot * 0.6, 0.5), path=st["path"], world=world, lane=lane_k, lift=lift, spots=spots)
    gimg = R.to_image("ground", gtex[::-1, ::-1])
    n = 46; hs = np.array(st["heights"]).reshape(n, n)
    xs = -45 + np.arange(n) * 2.0
    X, Z = np.meshgrid(xs, xs)
    verts = np.stack([X.ravel(), Z.ravel(), hs.ravel()], 1)
    faces = []
    for iz in range(n - 1):
        for ix in range(n - 1):
            a = iz * n + ix; faces.append((a, a + 1, a + n + 1, a + n))
    me = bpy.data.meshes.new("ground"); me.from_pydata(verts.tolist(), [], faces)
    ul = me.uv_layers.new(name="UV")
    for poly in me.polygons:
        for li_ in poly.loop_indices:
            v = verts[me.loops[li_].vertex_index]; ul.data[li_].uv = ((v[0] + 45) / 90, (v[1] + 45) / 90)
    me.materials.append(tex_mat("groundm", gimg)); me.polygons.foreach_set("use_smooth", np.ones(len(faces), dtype=bool))
    g = bpy.data.objects.new("ground", me); sc.collection.objects.link(g)
    if water is not None:
        bpy.ops.mesh.primitive_plane_add(size=1); wp = bpy.context.object
        wp.scale = (90, 16, 1); wp.location = (0, water + 8, 0.06)
        wcol = P.hexc(t["water"])
        wimg = R.to_image("water", P.water(wcol * 0.75 * world, P.lerp(wcol * 1.25, P.hexc(t["worn"]) * 0.6, 0.25) * world, P.hexc("#e8f0f0") * 0.75 * world, bot, seed)[::-1, ::-1])
        wp.data.materials.append(tex_mat("waterm", wimg, rough=0.6))
    # sky and ridges (straight cards, as SceneryPaint.Backdrop)
    skyimg = R.to_image("sky", P.sky(top, bot, sun, seed, cloudiness=t.get("clouds", 0.5), storm=t.get("storm", False)))
    R.backdrop("Sky", 50, 44, -32, -2, 50, R.emission_mat("skym", skyimg))
    for layer, (hw, zz, bz, hh) in enumerate(((42, 38, -28, 15), (36, 33, -26, 10), (30, 28, -24, 7))):
        rimg = R.to_image(f"ridge{layer}", P.ridge(layer, t["style"], sil, bot, sun, seed))
        R.backdrop(f"SkyRidge{layer}", hw, zz, bz, -1.5, hh, R.emission_mat(f"rm{layer}", rimg, alpha=True))
    # the ring: a worn decal is in the ground; the two rune lines
    for k, wd, a in ((1.0, 0.06, 0.55), (0.82, 0.03, 0.3)):
        bpy.ops.mesh.primitive_circle_add(vertices=96, radius=1, fill_type="NOTHING", location=(0, 0.6, 0.03))
        c = bpy.context.object; c.scale = (4.2 * k, 3.95 * k, 1)
        bpy.ops.object.convert(target="CURVE"); c.data.bevel_depth = wd / 2
        c.data.materials.append(col_mat("rune", (0.6, 0.45, 0.2), emit=(1.0, 0.81, 0.42), k=a * 2))
    # placements
    lmk = {}
    for i, it in enumerate(st["items"]):
        m = MESHES.get(it["mesh"])
        if m is None: continue
        if m["tex"] is not None:
            key = (it["mesh"], it["far"])
            if key not in lmk:
                img = landmark_image("lm_" + it["mesh"], m["tex"], far_rows if it["far"] else rows, sil, sun, SR.GLOW.get(it["glow"]))
                lmk[key] = tex_mat("lmm_" + it["mesh"], img)
            mats = [lmk[key]]
        else:
            g2 = fire if it["glow"] == "fire" or it["mesh"] in ("brazier", "lantern", "torch_post") else glow
            if it["glow"] in SR.GLOW: g2 = col_mat("g_" + it["glow"], P.hexc(SR.GLOW[it["glow"]]) * 0.4, emit=P.hexc(SR.GLOW[it["glow"]]), k=2.0)
            mats = [farm if it["far"] or it["layer"] == "Skyline" else near, g2]
        mesh_object(f"p{i}", m, mats, (it["x"], it["z"], it["y"]), it["s"], it["yaw"])
        if it["mesh"] == "banner_pole" and "banner_cloth" in MESHES:
            c = mesh_object(f"c{i}", MESHES["banner_cloth"], [near, glow], (it["x"], it["z"], it["y"] + 2.85 * it["s"]), it["s"], it["yaw"])
    # hero stand-ins on the battle slots: 4 allies near, 3 enemies far, element-tinted mid-tones
    cols = [(0.62, 0.3, 0.22), (0.3, 0.5, 0.3), (0.28, 0.4, 0.62), (0.66, 0.56, 0.3)]
    def slot(ally, i, nn):
        step = 2.2 / ((4.2 + 3.95) / 2); half = 0 if nn <= 1 else min(math.radians(70), step * (nn - 1) / 2)
        tt = 0 if nn <= 1 else -half + i * (2 * half / (nn - 1)); a = tt if ally else math.pi - tt
        return (math.sin(a) * 4.2, 0.6 - math.cos(a) * 3.95)
    for i in range(4):
        x, z = slot(True, i, 4); stand_in(f"a{i}", x, z, cols[i])
    for i in range(3):
        x, z = slot(False, i, 3); stand_in(f"e{i}", x, z, (0.35, 0.25, 0.42))
    return t


def stand_in(name, x, z, col):
    bpy.ops.mesh.primitive_cylinder_add(radius=0.3, depth=1.35, location=(x, z, 0.68)); b = bpy.context.object
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.24, location=(x, z, 1.62)); h = bpy.context.object
    m = col_mat(name, col)
    b.data.materials.append(m); h.data.materials.append(m)


def camera(cam, w, h):
    sc = bpy.context.scene
    c = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(c); sc.camera = c
    c.data.sensor_fit = "VERTICAL"; c.data.angle = math.radians(cam["fov"]); c.data.clip_end = 160
    e, tg = cam["eye"], cam["target"]
    from mathutils import Vector
    c.location = (e[0], e[2], e[1])
    d = Vector((tg[0], tg[2], tg[1])) - c.location
    c.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    sc.render.resolution_x, sc.render.resolution_y = w, h


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def plan(st, path, size=360):
    """Top-down: the field, the hero lane, the road, every placement by layer, the default and reveal cameras' footprints."""
    from PIL import Image, ImageDraw
    im = Image.new("RGB", (size, size), (30, 32, 38)); dr = ImageDraw.Draw(im)
    S = size / 64.0
    def px(x, z): return (size / 2 + x * S, size * 0.78 - z * S)
    dr.rectangle([px(-30, 28), px(30, -16)], outline=(70, 70, 80))
    if st["water"] > 0: dr.rectangle([px(-30, 26), px(30, st["water"])], fill=(30, 50, 70))
    for k in st["cams"]:
        col = (255, 210, 90) if k["name"].startswith("angle") else (120, 200, 255) if k["name"].startswith("reveal") else (150, 150, 150)
        if not (k["name"].startswith("angle") or k["name"].startswith("reveal0@1.78")): continue
        f = [px(q[0], q[1]) for q in k["foot"]]; dr.polygon(f, outline=col)
        dr.ellipse([px(k["eye"][0] - 0.4, k["eye"][2] + 0.4), px(k["eye"][0] + 0.4, k["eye"][2] - 0.4)], fill=col)
    dr.line([px(x, z) for x, z in st["path"]], fill=(150, 120, 80), width=3)
    lane = [px(5.5 * math.cos(a), 0.6 + 5.25 * math.sin(a)) for a in np.linspace(0, 2 * math.pi, 48)]
    dr.polygon(lane, outline=(230, 80, 80))
    lc = {"Scatter": (90, 140, 80), "Tuft": (70, 110, 60), "Mid": (60, 170, 110), "Frame": (200, 120, 220), "Skyline": (110, 110, 170),
          "Approach": (170, 140, 90), "SetPiece": (240, 200, 120), "Landmark": (255, 120, 60), "LandmarkPart": (255, 160, 100), "Boss": (230, 60, 60), "Lantern": (255, 220, 120)}
    for it in st["items"]:
        r = max(1.5, it["r"] * S * (0.5 if it["layer"] in ("Scatter", "Tuft") else 0.8)); x, y = px(it["x"], it["z"])
        dr.ellipse([x - r, y - r, x + r, y + r], outline=lc.get(it["layer"], (200, 200, 200)))
    return im


def chapter_sheet(layout, out, ch):
    from PIL import Image, ImageDraw, ImageFont
    stages = [s for s in layout if s["chapter"] == ch and not s["hard"]]
    tiles = []
    for st in stages:
        tag = f"ch{ch + 1:02d}_s{st['stage'] + 1}"
        build(st, "l")
        cams = {k["name"]: k for k in st["cams"]}
        land = next(k for n, k in cams.items() if n.startswith("angle@1.78"))
        portrait = next(k for n, k in cams.items() if n.startswith("angle@0.46"))
        camera(land, W_L, H_L); render(os.path.join(out, tag + "_default16x9.png"))
        bpy.data.objects.remove(bpy.context.scene.camera)
        camera(portrait, W_P, H_P); render(os.path.join(out, tag + "_portrait.png"))
        extra = None
        rv = [k for n, k in cams.items() if n.startswith("reveal0@1.78")]
        if rv:
            bpy.data.objects.remove(bpy.context.scene.camera)
            camera(rv[0], W_L, H_L); render(os.path.join(out, tag + "_reveal.png")); extra = tag + "_reveal.png"
        plan(st, None).save(os.path.join(out, tag + "_plan.png"))
        tiles.append((st, tag, extra))
        print("rendered", tag, flush=True)
    # contact sheet: one row per stage: default 16:9 | portrait | plan (| reveal for the lair)
    rowh = H_L; W = W_L + W_P * rowh // H_P + 360 + W_L
    sheet = Image.new("RGB", (W, 40 + rowh * len(tiles)), (16, 16, 20)); d = ImageDraw.Draw(sheet)
    try: font = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", 18); small = ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", 13)
    except Exception: font = small = ImageFont.load_default()
    d.text((10, 10), f"PREVIEW (Blender render of the generated layout; not a game screenshot) - Chapter {ch + 1}: {stages[0]['chapterName']} ({stages[0]['biome']})", fill=(255, 220, 140), font=font)
    for i, (st, tag, extra) in enumerate(tiles):
        y = 40 + i * rowh; x = 0
        a = Image.open(os.path.join(out, tag + "_default16x9.png")).convert("RGB"); sheet.paste(a, (x, y)); x += W_L
        b = Image.open(os.path.join(out, tag + "_portrait.png")).convert("RGB"); b = b.resize((W_P * rowh // H_P, rowh)); sheet.paste(b, (x, y)); x += b.width
        c = Image.open(os.path.join(out, tag + "_plan.png")).convert("RGB"); sheet.paste(c, (x, y)); x += 360
        if extra: sheet.paste(Image.open(os.path.join(out, extra)).convert("RGB"), (x, y))
        d.rectangle([4, y + 4, 330, y + 44], fill=(0, 0, 0))
        d.text((10, y + 6), f"Stage {ch + 1}-{st['stage'] + 1}: {st['name']}", fill=(255, 255, 255), font=font)
        d.text((10, y + 27), f"{st['light']['mood']} · {st['tris']} tris (High) · default view 16:9", fill=(200, 200, 200), font=small)
        d.text((W_L + 6, y + 6), "portrait", fill=(220, 220, 220), font=small)
        d.text((W_L + b.width + 6, y + 6), "plan: lane (red), default-camera footprints (gold), reveal (blue)", fill=(220, 220, 220), font=small)
        if extra: d.text((W_L + b.width + 366, y + 6), "lair reveal, first key", fill=(220, 220, 220), font=small)
    p = os.path.join(out, f"chapter{ch + 1:02d}_preview.jpg")
    sheet.save(p, quality=86); print("sheet", p)


if __name__ == "__main__":
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:]
    layout = json.load(open(args[0])); out = args[1]; os.makedirs(out, exist_ok=True)
    load_meshes()
    for ch in args[2:]: chapter_sheet(layout, out, int(ch) - 1)
