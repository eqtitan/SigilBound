# Review renders for the arena scenery (Cycles, CPU): a prop lineup and a battle-camera diorama per
# theme, using the baked kit (make_props.py) and the painted textures (paint.py, the C# mirror).
#   python review.py OUT_DIR [theme ...]
import bpy, bmesh, math, os, sys, random
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_props as mp
import paint as P

K = 0.55  # albedo scale for the ramp's mid tone (SceneryPaint.Albedo)
# name: (sky top, horizon, theme fields..., ridge style, extras)
THEMES = {
    "dusk forest": dict(top="#1f2b46", bot="#c9805a", grass="#4f6a2a", grass2="#6f7432", worn="#8a6844", hill="#41582a", rock="#7d746a", bark="#4e3726",
                        leaves=["#3c6a26", "#557a2a", "#a8702a", "#b8522a"], tuft="#5f7a2c", cloth="#7a2a22", sil="#2e2c46", mote="#e08a3a", style="forest",
                        props=["oak0", "pine0", "oak1", "rock0", "bush0", "rock1", "oak2", "pine1", "tuft0"]),
    "teal shore": dict(top="#16283a", bot="#4f7c8a", grass="#c2a87e", grass2="#b39b72", worn="#8c7a5e", hill="#a8956a", rock="#6a7078", bark="#9a8a76",
                       leaves=["#4a6a3a", "#5a7a40"], tuft="#8f9a52", water="#1f5f6c", cloth="#d8d0b8", sil="#24404e", mote="#e8f6ff", style="sea",
                       props=["rock1", "reeds0", "rock3", "post0", "crate", "bush1", "rock2", "tuft1"]),
    "red badlands": dict(top="#2a1a1e", bot="#a8483a", grass="#94503a", grass2="#ad6844", worn="#c09070", hill="#8a4632", rock="#a0553a", bark="#5e4636",
                         strata=["#8a4430", "#b06a46", "#6e3426", "#c4885a"], tuft="#857040", cloth="#d0a040", sil="#44201f", mote="#ffc890", style="mesas",
                         props=["mesa0", "rock0", "dead0", "mesa1", "rock3", "tuft2"]),
    "gold ruins": dict(top="#28203a", bot="#c8a870", grass="#58652f", grass2="#76763c", worn="#8e8474", hill="#4c5a2c", rock="#a49a88", bark="#5a4636",
                       leaves=["#4c682c", "#667a32"], tuft="#6a7a34", cloth="#7a2a4a", sil="#36304c", mote="#ffd27a", style="ruins", flag=True,
                       props=["column0", "wall0", "arch", "column1", "banner_pole", "brazier", "crystal0", "oak0", "wall1", "column2"]),
    "glow marsh": dict(top="#0e1a2a", bot="#2a5a7a", grass="#2c4846", grass2="#385850", worn="#5e586c", hill="#284242", rock="#4a5460", bark="#38384a",
                       leaves=["#2a4a4a"], tuft="#4a7a6a", water="#123040", cloth="#2a3a5a", sil="#122838", mote="#80e8ff", style="hills",
                       props=["mushrooms0", "reeds1", "oak1", "mushrooms1", "rock2", "dead1", "reeds0"]),
    "storm coast": dict(top="#0a1018", bot="#3a4a5e", grass="#4a5052", grass2="#565c5c", worn="#6a6a66", hill="#3e4446", rock="#3a3e46", bark="#3a3432",
                        tuft="#5a6456", water="#0e2234", cloth="#8a9aa8", sil="#0e1622", mote="#b8d0ff", style="peaks", storm=True, boss=True, debris=True,
                        props=["stone0", "rock3", "stone1", "banner_pole", "rock0", "tuft2", "mound"]),
    "titan caldera": dict(top="#1a0a0a", bot="#c0401a", grass="#3a3436", grass2="#463e3e", worn="#6a625e", hill="#332e30", rock="#2e2a2e", bark="#2a2222",
                          tuft="#3a3030", cloth="#5a1a10", sil="#1e0c0a", mote="#ff7a2a", style="volcano", lava=True, boss=True, debris=True,
                          props=["rock3", "brazier", "column1", "rock0", "dead0", "crystal1", "banner_pole", "rock2"]),
}


def rows_for(t):
    h = P.hexc
    leaves = t.get("leaves") or [t["tuft"]]
    st = t.get("strata")
    r = [None] * 16
    r[mp.BARK] = h(t["bark"]); r[mp.ROCK] = h(st[0] if st else t["rock"]); r[mp.MOSS] = P.lerp(h(t["tuft"]), h(st[0] if st else t["rock"]), 0.35)
    for i, k in enumerate(mp.LEAVES): r[k] = h(leaves[i % len(leaves)])
    r[mp.WOOD] = P.lerp(h(t["bark"]) * 1.35, h(t["worn"]), 0.3)
    r[mp.STONE] = h(st[2]) if st else P.lerp(h(t["rock"]), h(t["worn"]), 0.35) * 1.08
    r[mp.CLOTH] = h(t["cloth"]); r[mp.METAL] = h("#3c3a40"); r[mp.CAP] = h(t["mote"]) * 0.5; r[mp.STEM] = h("#d8cdb0")
    r[mp.REED] = P.lerp(h(t["tuft"]), h(t["worn"]), 0.3); r[mp.EARTH] = h(st[1]) if st else h(t["hill"]); r[mp.TRIM] = h("#c9a24a"); r[mp.COAL] = h("#2a1a14")
    return [np.clip(c * K, 0, 1) for c in r]


def sun_of(t): return P.lerp(P.hexc(t["bot"]), np.ones(3), 0.45)


def to_image(name, arr):
    """arr: (h, w, 3|4), row 0 = bottom."""
    hgt, w = arr.shape[:2]
    img = bpy.data.images.new(name, w, hgt, alpha=True)
    img.colorspace_settings.name = "sRGB"   # before the pixels: changing it resets a generated buffer
    a = np.ones((hgt, w, 4)); a[..., :arr.shape[2]] = arr
    img.pixels.foreach_set(a.astype(np.float32).ravel())
    img.update(); img.pack()
    return img


def emission_mat(name, img, alpha=False):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img; tex.extension = "EXTEND"
    em = nt.nodes.new("ShaderNodeEmission"); out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(tex.outputs[0], em.inputs[0])
    if alpha:
        tr = nt.nodes.new("ShaderNodeBsdfTransparent"); mix = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(tex.outputs[1], mix.inputs[0]); nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs[0])
    else:
        nt.links.new(em.outputs[0], out.inputs[0])
    return m


FOG = [None]


def add_fog(nt, shader_out, out_in):
    if FOG[0] is None: nt.links.new(shader_out, out_in); return
    cam = nt.nodes.new("ShaderNodeCameraData"); mr = nt.nodes.new("ShaderNodeMapRange")
    mr.inputs["From Min"].default_value = 26; mr.inputs["From Max"].default_value = 64
    nt.links.new(cam.outputs["View Distance"], mr.inputs["Value"])
    em = nt.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (*FOG[0], 1)
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(mr.outputs[0], mix.inputs[0]); nt.links.new(shader_out, mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out_in)


def lit_mat(name, img=None, color=None, emit=None, rough=0.85):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = rough
    add_fog(nt, b.outputs[0], nt.nodes["Material Output"].inputs[0])
    if img is not None:
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img; tex.interpolation = "Linear"; tex.extension = "EXTEND"
        nt.links.new(tex.outputs[0], b.inputs["Base Color"])
    if color is not None: b.inputs["Base Color"].default_value = (*color, 1)
    if emit is not None:
        b.inputs["Emission Color"].default_value = (*emit, 1); b.inputs["Emission Strength"].default_value = 2.5
    return m


def kit_object(name, verts, subs, mat, glow_mat):
    me = bpy.data.meshes.new(name)
    pos = [tuple((p[0].x, p[0].z, p[0].y)) for p in verts]   # kit is Y-up; Blender is Z-up
    tris = []
    mats = []
    for si, s in enumerate(subs):
        for i in range(0, len(s), 3):
            tris.append((s[i], s[i + 2], s[i + 1])); mats.append(si)
    me.from_pydata(pos, [], tris)
    uv = me.uv_layers.new(name="UV")
    for poly in me.polygons:
        poly.material_index = mats[poly.index]
        for li in poly.loop_indices:
            vi = me.loops[li].vertex_index
            _, n, sh, row = verts[vi]
            uv.data[li].uv = (sh, (row + 0.5) / 16)
    me.normals_split_custom_set_from_vertices([(p[1].x, p[1].z, p[1].y) for p in verts])
    me.materials.append(mat); me.materials.append(glow_mat)
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    return ob


def arc(name, radius, y0, height, span, centre, mat, segs=48):
    bm = bmesh.new(); uv = bm.loops.layers.uv.new()
    vs = []
    for i in range(segs + 1):
        a = -span / 2 + span * i / segs
        x, z = math.sin(a) * radius + centre[0], math.cos(a) * radius + centre[1]
        vs.append((bm.verts.new((x, z, y0)), bm.verts.new((x, z, y0 + height)), i / segs))
    for i in range(segs):
        (a0, a1, u0), (b0, b1, u1) = vs[i], vs[i + 1]
        f = bm.faces.new((a0, b0, b1, a1))
        for l, (uu, vv) in zip(f.loops, ((u0, 0), (u1, 0), (u1, 1), (u0, 1))): l[uv].uv = (uu, vv)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    return ob


def backdrop(name, half_w, z, back_z, y0, height, mat):
    """Mirror of SceneryPaint.Backdrop: a straight front card with chamfered corners and side cards."""
    c = 8.0
    path = [(-half_w, back_z), (-half_w, z - c), (-half_w + c, z), (half_w - c, z), (half_w, z - c), (half_w, back_z)]
    L = [0.0]
    for i in range(1, len(path)): L.append(L[-1] + math.dist(path[i - 1], path[i]))
    bm = bmesh.new(); uv = bm.loops.layers.uv.new()
    vs = [(bm.verts.new((x, zz, y0)), bm.verts.new((x, zz, y0 + height)), L[i] / L[-1]) for i, (x, zz) in enumerate(path)]
    for i in range(len(vs) - 1):
        (a0, a1, u0), (b0, b1, u1) = vs[i], vs[i + 1]
        f = bm.faces.new((a0, b0, b1, a1))
        for l, (uu, vv) in zip(f.loops, ((u0, 0), (u1, 0), (u1, 1), (u0, 1))): l[uv].uv = (uu, vv)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(ob)
    return ob


def setup(theme, t, meshes, lineup):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"; sc.cycles.samples = 24; sc.cycles.device = "CPU"
    sc.render.resolution_x, sc.render.resolution_y = (1280, 720)
    sc.view_settings.view_transform = "Standard"
    top, bot, sun = P.hexc(t["top"]), P.hexc(t["bot"]), sun_of(t)
    FOG[0] = None if lineup else tuple(bot)
    w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (*P.lerp(bot, np.ones(3), 0.35) * 0.55, 1)
    w.node_tree.nodes["Background"].inputs[1].default_value = 0.9
    ramp_img = to_image("ramp", P.ramp(rows_for(t), P.hexc(t["sil"]), sun))
    ramp_img.pixels.update()
    mat = lit_mat("kit", ramp_img)
    mat.node_tree.nodes["Image Texture"].interpolation = "Linear"
    mote = P.hexc(t["mote"])
    glow = lit_mat("glow", color=mote * 0.4, emit=mote)
    sunl = bpy.data.lights.new("sun", "SUN"); sunl.energy = 3.2; sunl.color = tuple(P.lerp(sun, np.ones(3), 0.5)); sunl.angle = 0.2
    so = bpy.data.objects.new("sun", sunl); sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(42), 0, math.radians(-150))
    seed = sum(map(ord, theme))
    if lineup:
        bpy.ops.mesh.primitive_plane_add(size=60); g = bpy.context.object
        g.data.materials.append(lit_mat("g", color=P.hexc(t["worn"]) * K))
        x = -len(t["props"]) * 1.1
        for name in t["props"]:
            v, s = meshes[name]
            ob = kit_object(name, v, s, mat, glow); ob.location = (x, 0, 0)
            if name == "banner_pole":
                v2, s2 = meshes["banner_cloth"]; c = kit_object("cloth", v2, s2, mat, glow); c.location = (x, 0, 2.85)
            x += 2.2
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
        cam.location = (0, -15, 4.2); cam.rotation_euler = (math.radians(80), 0, 0); cam.data.lens = 26
        w.node_tree.nodes["Background"].inputs[0].default_value = (*(bot * 0.7), 1)
        return
    # diorama: painted ground, sky arc, three ridge arcs, props scattered off the ring, as the game does
    water_z = 10.5 if ("water" in t or t.get("lava")) else None
    gtex = P.ground(P.hexc(t["grass"]) * 0.46, P.hexc(t["grass2"]) * 0.46, P.hexc(t["worn"]) * 0.46 * 0.9, P.hexc(t["rock"]) * 0.5,
                    seed=seed % 7, flag=t.get("flag", False), wet=P.hexc(t["worn"]) * 0.25, water_z=water_z,
                    horizon=P.lerp(P.hexc(t["hill"]) * 0.46, P.hexc(t["bot"]) * 0.6, 0.5))
    gimg = to_image("ground", gtex[::-1, ::-1])
    bpy.ops.mesh.primitive_plane_add(size=90); g = bpy.context.object
    g.data.materials.append(lit_mat("ground", gimg))
    if water_z is not None:
        bpy.ops.mesh.primitive_plane_add(size=1); wp = bpy.context.object
        wp.scale = (90, 16, 1); wp.location = (0, water_z + 8, 0.06)
        if t.get("lava"):
            alb, glo = P.lava(seed)
            m = lit_mat("lava", to_image("crust", alb[::-1, ::-1]))
            nt = m.node_tree; et = nt.nodes.new("ShaderNodeTexImage"); et.image = to_image("veins", glo[::-1, ::-1])
            b = nt.nodes["Principled BSDF"]; nt.links.new(et.outputs[0], b.inputs["Emission Color"]); b.inputs["Emission Strength"].default_value = 1.1
        else:
            wcol = P.hexc(t["water"])
            m = lit_mat("water", to_image("water", P.water(wcol * 0.75, P.lerp(wcol * 1.25, P.hexc(t["worn"]) * 0.6, 0.25), P.hexc("#e8f0f0") * 0.75, P.hexc(t["bot"]), seed)[::-1, ::-1]), rough=0.75)
        wp.data.materials.append(m)
    fog = bot
    skyimg = to_image("sky", P.sky(top, bot, sun, seed, cloudiness=0.65 if t.get("storm") else 0.5, storm=t.get("storm", False)))
    backdrop("Sky", 50, 44, -32, -2, 50, emission_mat("skym", skyimg))
    for layer, (hw, zz, bz, hh) in enumerate(((42, 38, -28, 15), (36, 33, -26, 10), (30, 28, -24, 7))):
        rimg = to_image(f"ridge{layer}", P.ridge(layer, t["style"], P.hexc(t["sil"]), fog, sun, seed))
        backdrop(f"SkyRidge{layer}", hw, zz, bz, -1.5, hh, emission_mat(f"rm{layer}", rimg, alpha=True))
    rng = random.Random(seed)
    names = t["props"]
    placed = 0
    while placed < t.get("scatter", 46):
        a, r = rng.uniform(0, math.tau), rng.uniform(6.8, 20.5)
        x, z = math.cos(a) * r, math.sin(a) * r * 0.9 + 4
        if (x / 6.4) ** 2 + ((z - 0.6) / 5.8) ** 2 < 1 or z < -7: continue
        if any((x - kx) ** 2 + (z - kz) ** 2 < kr * kr for kx, kz, kr in t.get("keepout", ())): continue  # a story landmark stands here
        if water_z is not None and z > water_z - 0.5: continue
        name = names[rng.randrange(len(names))]
        v, s = meshes[name]
        ob = kit_object(name, v, s, mat, glow); ob.location = (x, z, 0); ob.rotation_euler = (0, 0, rng.uniform(0, math.tau))
        k = rng.uniform(0.8, 1.35); ob.scale = (k, k, k)
        if name == "banner_pole":
            v2, s2 = meshes["banner_cloth"]; c = kit_object("cloth", v2, s2, mat, glow); c.parent = ob; c.location = (0, 0, 2.85)
        placed += 1
    for k in range(30):  # tufts at the ring edge
        a = rng.uniform(0, math.tau); kk = rng.uniform(1.05, 1.5)
        x, z = math.sin(a) * 6.2 * kk, 0.6 + math.cos(a) * 5.6 * kk
        if z < -6.5: continue
        v, s = meshes["tuft%d" % (k % 3)]; ob = kit_object("t", v, s, mat, glow); ob.location = (x, z, 0); ob.scale = (0.7,) * 3
    if t.get("boss"):  # boss-stage dressing (Scenery.Build): braziers, banners, and a frozen moment of the reactor
        titan = t.get("lava", False)
        for sx in (-1, 1):
            v, s_ = meshes["brazier"]; ob = kit_object("brazier", v, s_, mat, lit_mat("fire", color=P.hexc("#3a1a08"), emit=P.hexc("#ffb050"))); ob.location = (sx * 6.7, 4.4, 0); ob.scale = (1.1,) * 3
            v, s_ = meshes["banner_pole"]; ob = kit_object("pole", v, s_, mat, glow); ob.location = (sx * (4.8 if titan else 4.0), 9 if titan else 7.8, 0); ob.scale = (1.15,) * 3
            v2, s2 = meshes["banner_cloth"]; c = kit_object("cloth", v2, s2, mat, glow); c.parent = ob; c.location = (0, 0, 2.85); c.rotation_euler = (math.radians(-28 * sx), 0, 0)
        for k in range(3 if t.get("debris") else 0):  # debris mid-fall
            v, s_ = meshes["debris%d" % (k % 2)]; ob = kit_object("debris", v, s_, mat, glow)
            ob.location = (rng.uniform(-7, 7), rng.uniform(8, 11), rng.uniform(2.5, 6)); ob.rotation_euler = (rng.uniform(0, 6), rng.uniform(0, 6), 0); ob.scale = (1.5,) * 3
    # stand-ins for the heroes on the rim
    for i, (x, z) in enumerate(((-2.4, -2.6), (0, -3.3), (2.4, -2.6), (-2.4, 3.8), (0, 4.5), (2.4, 3.8))):
        bpy.ops.mesh.primitive_cylinder_add(radius=0.28, depth=1.7, location=(x, z, 0.85))
        bpy.context.object.data.materials.append(lit_mat(f"h{i}", color=(0.55, 0.5, 0.45)))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.sensor_fit = "VERTICAL"; cam.data.angle = math.radians(42); cam.data.clip_end = 150
    cam.location, look = ((0, -9.6, 7.2), (0, 1.1, 0.9)) if os.environ.get("CAM") == "menu" else ((0, -13.5, 7.4), (0, 4.0, 1.2))
    if os.environ.get("CAM") == "low": cam.location, look = (2.5, -6.0, 1.5), (0, 6.0, 4.5)
    if os.environ.get("CAM") == "fight":  # the game's default battle camera (BattleLayout "angle", 16:9): x 5.83, y 6.47, z -6.96
        cam.location, look = (5.83, -6.96, 6.47), (0.0, 0.5, 1.0); cam.data.angle = math.radians(50)
    from mathutils import Vector as _V
    d = _V(look) - _V(cam.location)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    # fog: a volume would be slow; tint distant things through the mist pass instead
    sc.world.mist_settings.start = 26; sc.world.mist_settings.depth = 38
    bpy.context.view_layer.use_pass_mist = True
    sc.use_nodes = True


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


if __name__ == "__main__":
    out = sys.argv[1]
    os.makedirs(out, exist_ok=True)
    picks = sys.argv[2:] or list(THEMES)
    _, meshes, _ = mp.export(os.path.join(out, "props.bytes"))
    for name in picks:
        t = THEMES[name]
        setup(name, t, meshes, lineup=True); render(os.path.join(out, f"props_{name.replace(' ', '_')}.png"))
        setup(name, t, meshes, lineup=False); render(os.path.join(out, f"arena_{name.replace(' ', '_')}.png"))
        print("rendered", name, flush=True)
