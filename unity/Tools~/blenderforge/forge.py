# blenderforge: hero models made by script with free tools only (Blender as a Python module, CC0
# or self-made inputs). A hero is a recipe of parts: a skin-modifier body, a sculpted-by-numbers
# head with stylized face, curve-strand hair, lathe-turned armour and props. Materials are
# procedural "painted" shaders (local colour, strokes, baked AO, top light, edge highlights) that
# are baked into one hand-painted-style albedo atlas plus a glow mask, then the hero is rigged with
# a Mixamo-style humanoid skeleton and exported as SB_Hero_<id>_LOD0.glb for the Unity import tool.
#
#   python forge.py --hero ysolde --out <dir> [--render] [--bake 1024|2048] [--no-export]
# (python = an interpreter with the `bpy` module, e.g. pip install bpy==5.2.4 on Python 3.13)
import argparse, math, os, sys
import bpy, bmesh
from mathutils import Vector, Matrix, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

# ---------------------------------------------------------------- scene helpers
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.unit_settings.scale_length = 1.0
    return sc

def link(obj):
    bpy.context.scene.collection.objects.link(obj)
    return obj

def mesh_obj(name, verts, faces, edges=()):
    me = bpy.data.meshes.new(name)
    me.from_pydata([Vector(v) for v in verts], list(edges), [tuple(f) for f in faces])
    me.update()
    return link(bpy.data.objects.new(name, me))

def select_only(objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]

def apply_mods(obj):
    select_only([obj])
    for m in list(obj.modifiers):
        try: bpy.ops.object.modifier_apply(modifier=m.name)
        except RuntimeError: obj.modifiers.remove(m)

def smooth(obj, angle=60):
    for p in obj.data.polygons: p.use_smooth = True
    try:
        select_only([obj]); bpy.ops.object.shade_auto_smooth(angle=math.radians(angle))
    except Exception:
        pass

def subsurf(obj, levels=2):
    m = obj.modifiers.new("Subsurf", "SUBSURF"); m.levels = levels; m.render_levels = levels
    return m

def bevel(obj, width=0.006, segments=2):
    m = obj.modifiers.new("Bevel", "BEVEL"); m.width = width; m.segments = segments; m.limit_method = "ANGLE"
    return m

def curve_to_mesh(obj):
    select_only([obj]); bpy.ops.object.convert(target="MESH")
    return bpy.context.view_layer.objects.active

# ---------------------------------------------------------------- shape primitives
def lathe(name, profile, seg=24, sx=1.0, sy=1.0, cx=0.0, cy=0.0, cap_top=True, cap_bottom=True, phase=0.0):
    """Surface of revolution around Z. profile: [(radius, z), ...] bottom to top; sx/sy squash."""
    verts, faces = [], []
    n = len(profile)
    for (r, z) in profile:
        for i in range(seg):
            a = 2 * math.pi * i / seg + phase
            verts.append((cx + math.cos(a) * r * sx, cy + math.sin(a) * r * sy, z))
    for j in range(n - 1):
        for i in range(seg):
            a, b = j * seg + i, j * seg + (i + 1) % seg
            faces.append((a, b, b + seg, a + seg))
    if cap_bottom: faces.append(tuple(reversed(range(seg))))
    if cap_top: faces.append(tuple(range((n - 1) * seg, n * seg)))
    return mesh_obj(name, verts, faces)

def ellipsoid(name, loc, size, seg=24, rings=16):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me)); o.location = loc
    return o

def box(name, loc, size, rot=(0, 0, 0)):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    o = link(bpy.data.objects.new(name, me)); o.location = loc; o.rotation_euler = Euler(rot)
    return o

def torus(name, loc, major, minor, rot=(0, 0, 0), seg=32, sseg=10, sx=1, sy=1):
    verts, faces = [], []
    for i in range(seg):
        a = 2 * math.pi * i / seg
        for j in range(sseg):
            b = 2 * math.pi * j / sseg
            r = major + math.cos(b) * minor
            verts.append((math.cos(a) * r * sx, math.sin(a) * r * sy, math.sin(b) * minor))
    for i in range(seg):
        for j in range(sseg):
            a = i * sseg + j; b = ((i + 1) % seg) * sseg + j
            c = ((i + 1) % seg) * sseg + (j + 1) % sseg; d = i * sseg + (j + 1) % sseg
            faces.append((a, b, c, d))
    o = mesh_obj(name, verts, faces); o.location = loc; o.rotation_euler = Euler(rot)
    return o

def strand(name, pts, r0, r1=None, bevel_res=3, closed=False):
    """A tapered tube along points (hair clumps, whips, tails). Radii taper r0 -> r1."""
    r1 = r0 * 0.15 if r1 is None else r1
    cu = bpy.data.curves.new(name, "CURVE"); cu.dimensions = "3D"
    cu.bevel_depth = 1.0; cu.bevel_resolution = bevel_res; cu.use_fill_caps = True; cu.resolution_u = 4
    sp = cu.splines.new("BEZIER"); sp.bezier_points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        bp = sp.bezier_points[i]; bp.co = Vector(p); bp.handle_left_type = bp.handle_right_type = "AUTO"
        t = i / max(1, len(pts) - 1); bp.radius = r0 + (r1 - r0) * t
    o = link(bpy.data.objects.new(name, cu))
    return curve_to_mesh(o)

def flat_strand(name, pts, w0, w1, thick=0.012):
    """A ribbon clump (hair locks, cloth tails): wide and flat rather than round."""
    o = strand(name, pts, 1.0, 1.0)
    # squash the tube in its local "thickness" direction by scaling around the curve: approximate
    # with a per-vertex flatten toward the spline using nearest spline point.
    me = o.data
    import mathutils
    for v in me.vertices:
        # find nearest segment point
        best, bt = None, 0
        for i in range(len(pts) - 1):
            a, b = Vector(pts[i]), Vector(pts[i + 1])
            ab = b - a; t = max(0, min(1, (v.co - a).dot(ab) / max(ab.length_squared, 1e-9)))
            q = a + ab * t; d = (v.co - q).length
            if best is None or d < best[0]: best = (d, q, ab.normalized(), (i + t) / (len(pts) - 1))
        d, q, tan, tt = best
        off = v.co - q
        w = (w0 + (w1 - w0) * tt)
        # thickness axis: perpendicular to tangent and roughly "outward" (radial from body axis)
        out = Vector((q.x, q.y, 0)); out = out.normalized() if out.length > 1e-6 else Vector((0, 1, 0))
        out = (out - tan * out.dot(tan)).normalized()
        side = tan.cross(out).normalized()
        v.co = q + side * off.dot(side) * w + out * off.dot(out) * thick
    return o

# ---------------------------------------------------------------- painted materials
def painted(name, base, shade=None, light=None, metal=0.0, rough=0.65, glow=None, glow_strength=3.0, strokes=0.18, edge=0.25, ao=0.55):
    """A procedural 'hand-painted' material: local colour + soft brush-stroke variation, ambient
    occlusion and a top light painted in, warm edge highlights on convex edges. Baked to the albedo."""
    def rgb(h):
        h = h.lstrip("#"); return tuple(int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
    def srgb_to_lin(c): return tuple((x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (1.0,)
    b = rgb(base)
    sh = rgb(shade) if shade else tuple(x * 0.55 for x in b)
    li = rgb(light) if light else tuple(min(1, x * 1.25 + 0.04) for x in b)
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; L = nt.links
    for n in list(N): N.remove(n)
    out = N.new("ShaderNodeOutputMaterial"); out.location = (1200, 0)
    bsdf = N.new("ShaderNodeBsdfPrincipled"); bsdf.location = (900, 0)
    bsdf.inputs["Metallic"].default_value = metal; bsdf.inputs["Roughness"].default_value = rough
    L.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    tc = N.new("ShaderNodeTexCoord")
    # top light: world normal Z -> shade..light
    geo = N.new("ShaderNodeNewGeometry")
    sep = N.new("ShaderNodeSeparateXYZ"); L.new(geo.outputs["Normal"], sep.inputs[0])
    mr = N.new("ShaderNodeMapRange"); mr.inputs["From Min"].default_value = -1; mr.inputs["From Max"].default_value = 1
    L.new(sep.outputs["Z"], mr.inputs["Value"])
    ramp = N.new("ShaderNodeValToRGB"); L.new(mr.outputs["Result"], ramp.inputs["Fac"])
    ramp.color_ramp.elements[0].color = srgb_to_lin(sh); ramp.color_ramp.elements[0].position = 0.15
    ramp.color_ramp.elements[1].color = srgb_to_lin(li); ramp.color_ramp.elements[1].position = 0.95
    mid = ramp.color_ramp.elements.new(0.55); mid.color = srgb_to_lin(b)
    col = ramp.outputs["Color"]
    # brush strokes: stretched noise, multiplies the colour a little
    mp = N.new("ShaderNodeMapping"); mp.inputs["Scale"].default_value = (14, 14, 46)
    L.new(tc.outputs["Object"], mp.inputs["Vector"])
    nz = N.new("ShaderNodeTexNoise"); nz.inputs["Scale"].default_value = 1.0; nz.inputs["Detail"].default_value = 3
    L.new(mp.outputs["Vector"], nz.inputs["Vector"])
    st = N.new("ShaderNodeMapRange"); st.inputs["To Min"].default_value = 1 - strokes; st.inputs["To Max"].default_value = 1 + strokes * 0.6
    st.inputs["From Min"].default_value = 0.3; st.inputs["From Max"].default_value = 0.7
    L.new(nz.outputs["Fac"], st.inputs["Value"])
    mul = N.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"; mul.inputs["Factor"].default_value = 1
    L.new(col, mul.inputs["A"]); L.new(st.outputs["Result"], mul.inputs["B"]); col = mul.outputs["Result"]
    # ambient occlusion painted in
    aon = N.new("ShaderNodeAmbientOcclusion"); aon.inputs["Distance"].default_value = 0.06; aon.samples = 16
    aor = N.new("ShaderNodeMapRange"); aor.inputs["To Min"].default_value = 1 - ao
    L.new(aon.outputs["AO"], aor.inputs["Value"])
    m2 = N.new("ShaderNodeMix"); m2.data_type = "RGBA"; m2.blend_type = "MULTIPLY"; m2.inputs["Factor"].default_value = 1
    L.new(col, m2.inputs["A"]); L.new(aor.outputs["Result"], m2.inputs["B"]); col = m2.outputs["Result"]
    # edge highlights on convex edges (pointiness)
    pr = N.new("ShaderNodeMapRange"); pr.inputs["From Min"].default_value = 0.52; pr.inputs["From Max"].default_value = 0.62
    pr.inputs["To Max"].default_value = edge
    L.new(geo.outputs["Pointiness"], pr.inputs["Value"])
    m3 = N.new("ShaderNodeMix"); m3.data_type = "RGBA"; m3.blend_type = "ADD"
    L.new(pr.outputs["Result"], m3.inputs["Factor"]); L.new(col, m3.inputs["A"]); m3.inputs["B"].default_value = srgb_to_lin(li)
    col = m3.outputs["Result"]
    L.new(col, bsdf.inputs["Base Color"])
    if glow:
        g = srgb_to_lin(rgb(glow))
        bsdf.inputs["Emission Color"].default_value = g; bsdf.inputs["Emission Strength"].default_value = glow_strength
        m["sb_glow"] = 1.0
    m["sb_metal"] = metal; m["sb_rough"] = rough
    return m

def assign(obj, mat):
    obj.data.materials.clear(); obj.data.materials.append(mat)
    return obj

# ---------------------------------------------------------------- body
# Joint layout for a 1.85 m stylized hero (Z up, facing -Y). Scaled per build.
def skeleton(h=1.0, sh=1.0, hip=1.0, arm_down=48):
    s = h
    a = math.radians(arm_down)
    J = {
        "Hips": (0, 0, 0.98 * s), "Spine": (0, 0.0, 1.10 * s), "Spine1": (0, 0.0, 1.24 * s), "Spine2": (0, 0.0, 1.36 * s),
        "Neck": (0, 0.0, 1.50 * s), "Head": (0, -0.01, 1.58 * s), "HeadTop": (0, -0.01, 1.88 * s),
    }
    for side, x in (("Left", 1), ("Right", -1)):
        sx = x * 0.075 * sh
        J[side + "Shoulder"] = (sx, 0.0, 1.43 * s)
        ux = x * 0.18 * sh
        J[side + "Arm"] = (ux, 0.01, 1.42 * s)
        ul = 0.27 * s; ll = 0.25 * s; hl = 0.17 * s
        e = (ux + x * math.sin(a) * ul, 0.01, 1.42 * s - math.cos(a) * ul)
        J[side + "ForeArm"] = e
        w = (e[0] + x * math.sin(a - 0.12) * ll, -0.03, e[2] - math.cos(a - 0.12) * ll)
        J[side + "Hand"] = w
        J[side + "HandEnd"] = (w[0] + x * math.sin(a - 0.2) * hl * 0.7, -0.04, w[2] - math.cos(a - 0.2) * hl * 0.7)
        lx = x * 0.095 * hip
        J[side + "UpLeg"] = (lx, 0.0, 0.93 * s)
        J[side + "Leg"] = (lx * 1.05, -0.01, 0.50 * s)
        J[side + "Foot"] = (lx * 1.1, 0.02, 0.09 * s)
        J[side + "ToeBase"] = (lx * 1.1, -0.11 * s, 0.03 * s)
        J[side + "ToeEnd"] = (lx * 1.1, -0.17 * s, 0.03 * s)
    return {k: Vector(v) for k, v in J.items()}

BONES = [  # (bone, head joint, tail joint, parent)
    ("Hips", "Hips", "Spine", None), ("Spine", "Spine", "Spine1", "Hips"), ("Spine1", "Spine1", "Spine2", "Spine"),
    ("Spine2", "Spine2", "Neck", "Spine1"), ("Neck", "Neck", "Head", "Spine2"), ("Head", "Head", "HeadTop", "Neck"),
]
for _s in ("Left", "Right"):
    BONES += [(_s + "Shoulder", "Spine2", _s + "Arm", "Spine2"), (_s + "Arm", _s + "Arm", _s + "ForeArm", _s + "Shoulder"),
              (_s + "ForeArm", _s + "ForeArm", _s + "Hand", _s + "Arm"), (_s + "Hand", _s + "Hand", _s + "HandEnd", _s + "ForeArm"),
              (_s + "UpLeg", _s + "UpLeg", _s + "Leg", "Hips"), (_s + "Leg", _s + "Leg", _s + "Foot", _s + "UpLeg"),
              (_s + "Foot", _s + "Foot", _s + "ToeBase", _s + "Leg"), (_s + "ToeBase", _s + "ToeBase", _s + "ToeEnd", _s + "Foot")]

def body(J, r, name="Body"):
    """Skin-modifier body: radii r[joint] = (x, y). Returns a smooth organic mesh."""
    order = ["Hips", "Spine", "Spine1", "Spine2", "Neck", "Head"]
    keys = list(order)
    edges = [(0, 1), (1, 2), (2, 3), (3, 4), (4, 5)]
    def add(k, parent):
        keys.append(k); edges.append((keys.index(parent), len(keys) - 1))
    for s in ("Left", "Right"):
        add(s + "Shoulder", "Spine2"); add(s + "Arm", s + "Shoulder"); add(s + "ForeArm", s + "Arm"); add(s + "Hand", s + "ForeArm"); add(s + "HandEnd", s + "Hand")
        add(s + "UpLeg", "Hips"); add(s + "Leg", s + "UpLeg"); add(s + "Foot", s + "Leg"); add(s + "ToeBase", s + "Foot")
    o = mesh_obj(name, [J[k] for k in keys], [], edges)
    sk = o.modifiers.new("Skin", "SKIN"); sk.use_smooth_shade = True; sk.branch_smoothing = 0.6
    for i, k in enumerate(keys):
        sv = o.data.skin_vertices[0].data[i]
        base = k.replace("Left", "").replace("Right", "")
        sv.radius = r.get(base, (0.04, 0.04))
        sv.use_root = (k == "Hips")
    subsurf(o, 1)
    apply_mods(o)
    smooth(o, 180)
    return o

def hand(J, side, mat, size=1.0):
    """Stylized hand: palm and finger block (a mitten with a separate thumb), aligned to the forearm."""
    w, e = J[side + "Hand"], J[side + "HandEnd"]
    d = (e - w).normalized(); x = 1 if side == "Left" else -1
    q = Vector((0, 0, -1)).rotation_difference(d)
    palm = ellipsoid("Palm", (0, 0, 0), (0.034 * size, 0.017 * size, 0.045 * size), 14, 10)
    fing = ellipsoid("Fingers", (0, -0.002, -0.06 * size), (0.032 * size, 0.015 * size, 0.035 * size), 14, 10)
    thumb = ellipsoid("Thumb", (x * -0.0 + 0.0, -0.022 * size, -0.018 * size), (0.011 * size, 0.011 * size, 0.03 * size), 10, 8)
    thumb.rotation_euler = Euler((math.radians(-35), 0, 0)); thumb.location = Vector((0, -0.022 * size, -0.02 * size))
    parts = [palm, fing, thumb]
    for p in parts:
        p.rotation_mode = "QUATERNION"; p.rotation_quaternion = q @ p.rotation_euler.to_quaternion()
        p.location = w + q @ Vector(p.location) + d * 0.012
        assign(p, mat)
    return parts

# ---------------------------------------------------------------- head and face
def head(J, scale=1.0, skin=None, eye_glow=None, iris="#ffa030", brow_angle=12, brow_col="#5a1a10", jaw=1.0):
    hz = J["Head"].z
    c = Vector((0, -0.012, hz + 0.135 * scale))
    o = ellipsoid("Head", c, (0.105 * scale, 0.118 * scale, 0.135 * scale), 32, 24)
    # shape: narrower chin, fuller cranium, slight face plane
    for v in o.data.vertices:
        z = v.co.z / (0.135 * scale)
        if z < 0:
            k = 1 - 0.32 * (z * z) * jaw
            v.co.x *= k; v.co.y = v.co.y * (1 - 0.15 * z * z) - 0.012 * scale * (-z)
        if v.co.y < 0 and abs(z) < 0.55: v.co.y *= 0.93  # flatter face
    subsurf(o, 1); apply_mods(o); smooth(o, 180)
    parts = [o]
    if skin: assign(o, skin)
    # nose and ears
    n = ellipsoid("Nose", c + Vector((0, -0.112 * scale, -0.022 * scale)), (0.011 * scale, 0.013 * scale, 0.02 * scale), 12, 8)
    parts.append(n)
    for x in (1, -1):
        e = ellipsoid("Ear", c + Vector((x * 0.1 * scale, 0.0, -0.005 * scale)), (0.012 * scale, 0.026 * scale, 0.04 * scale), 12, 8)
        parts.append(e)
    if skin:
        for p in parts[1:]: assign(p, skin)
    # eyes: big stylized almonds with a bright iris and a highlight
    white = painted("EyeWhite", "#f4efe8", ao=0.2, strokes=0.02, edge=0)
    irism = painted("Iris_" + iris, iris, glow=eye_glow, glow_strength=0.8, strokes=0.0, edge=0, ao=0.1)
    pupil = painted("Pupil", "#1a0c08", strokes=0, edge=0, ao=0); lash = painted("Lash", "#2a1210", strokes=0, edge=0, ao=0)
    browm = painted("Brow", brow_col, strokes=0.05, edge=0.0)
    face = []
    for x in (1, -1):
        ex = x * 0.043 * scale
        ec = c + Vector((ex, -0.098 * scale, 0.006 * scale))
        w = ellipsoid("EyeWhite", ec, (0.027 * scale, 0.012 * scale, 0.017 * scale), 16, 10); w.rotation_euler = Euler((0, x * math.radians(8), 0)); assign(w, white)
        i_ = ellipsoid("Iris", ec + Vector((0, -0.007 * scale, 0)), (0.0135 * scale, 0.008 * scale, 0.0155 * scale), 14, 10); assign(i_, irism)
        pu = ellipsoid("Pupil", ec + Vector((0, -0.0135 * scale, 0)), (0.0065 * scale, 0.003 * scale, 0.008 * scale), 10, 8); assign(pu, pupil)
        la = box("Lash", ec + Vector((x * 0.002 * scale, -0.006 * scale, 0.0155 * scale)), (0.031 * scale, 0.006 * scale, 0.004 * scale), (0, x * math.radians(-6), 0)); assign(la, lash)
        face += [pu, la]
        b = box("Brow", c + Vector((ex, -0.106 * scale, 0.04 * scale)), (0.03 * scale, 0.008 * scale, 0.006 * scale), (0, x * math.radians(-brow_angle), 0)); assign(b, browm)
        face += [w, i_, b]
    mouth = box("Mouth", c + Vector((0, -0.112 * scale, -0.06 * scale)), (0.022 * scale, 0.004 * scale, 0.0035 * scale)); assign(mouth, painted("Mouth", "#7a3030", edge=0))
    face.append(mouth)
    return parts + face, c

# ---------------------------------------------------------------- recipes
def build(hero):
    mod = __import__("recipes", fromlist=[hero])
    return getattr(mod, hero)()

# ---------------------------------------------------------------- rig
def rig(J, meshes, rigid):
    """Mixamo-style humanoid armature; deforming meshes get automatic weights, rigid parts are
    bound 100% to one bone (rigid = {object: bone})."""
    arm = bpy.data.armatures.new("Armature"); ao = link(bpy.data.objects.new("Armature", arm))
    select_only([ao]); bpy.ops.object.mode_set(mode="EDIT")
    eb = {}
    for name, h, t, parent in BONES:
        b = arm.edit_bones.new(name); b.head = J[h]; b.tail = J[t]
        if (b.tail - b.head).length < 1e-4: b.tail = b.head + Vector((0, 0, 0.05))
        if parent: b.parent = eb[parent]; b.use_connect = False
        eb[name] = b
    bpy.ops.object.mode_set(mode="OBJECT")
    deform = [m for m in meshes if m not in rigid]
    for o, bone in rigid.items():
        vg = o.vertex_groups.new(name=bone); vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    joined_rigid = list(rigid.keys())
    # join deforming meshes, auto-weight, then join rigid parts in
    select_only(deform); bpy.context.view_layer.objects.active = deform[0]
    if len(deform) > 1: bpy.ops.object.join()
    skin = bpy.context.view_layer.objects.active; skin.name = "Hero"
    select_only([skin]); ao.select_set(True); bpy.context.view_layer.objects.active = ao
    bpy.ops.object.parent_set(type="ARMATURE_AUTO")
    if joined_rigid:
        select_only([skin] + joined_rigid); bpy.context.view_layer.objects.active = skin
        bpy.ops.object.join()
    skin = bpy.context.view_layer.objects.active
    if not any(m.type == "ARMATURE" for m in skin.modifiers):
        mm = skin.modifiers.new("Armature", "ARMATURE"); mm.object = ao
    skin.parent = ao
    return ao, skin

def pose(ao, rot):
    """rot: {bone: (axis 'X'|'Y'|'Z', degrees)} or a list of them; rotations about WORLD axes
    through the bone's head, applied parent-first (for preview renders and idle poses)."""
    from mathutils import Quaternion
    select_only([ao]); bpy.ops.object.mode_set(mode="POSE")
    order = [b[0] for b in BONES]
    for name in order:
        if name not in rot: continue
        pb = ao.pose.bones[name]
        specs = rot[name] if isinstance(rot[name], list) else [rot[name]]
        bpy.context.view_layer.update()
        cur = (ao.matrix_world @ pb.matrix).to_quaternion()
        qw = Quaternion()
        for axis, deg in specs:
            qw = Quaternion({"X": (1, 0, 0), "Y": (0, 1, 0), "Z": (0, 0, 1)}[axis], math.radians(deg)) @ qw
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = pb.rotation_quaternion @ (cur.inverted() @ qw @ cur)
    bpy.context.view_layer.update()
    bpy.ops.object.mode_set(mode="OBJECT")

# ---------------------------------------------------------------- bake to one painted atlas
def bake_atlas(obj, size=1024, out_dir="."):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"; sc.cycles.samples = 16; sc.cycles.device = "CPU"
    select_only([obj])
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(60), island_margin=0.004)
    bpy.ops.object.mode_set(mode="OBJECT")
    albedo = bpy.data.images.new(f"T_Albedo", size, size); glow = bpy.data.images.new("T_Glow", size, size)
    def add_target(img):
        for m in obj.data.materials:
            nt = m.node_tree
            for n in [n for n in nt.nodes if n.name == "BakeTarget"]: nt.nodes.remove(n)
            t = nt.nodes.new("ShaderNodeTexImage"); t.name = "BakeTarget"; t.image = img
            nt.nodes.active = t
    add_target(albedo)
    sc.render.bake.use_pass_direct = False; sc.render.bake.use_pass_indirect = False; sc.render.bake.use_pass_color = True
    sc.render.bake.margin = 4
    bpy.ops.object.bake(type="DIFFUSE")
    add_target(glow)
    bpy.ops.object.bake(type="EMIT")
    pa = os.path.join(out_dir, "albedo.png"); pg = os.path.join(out_dir, "glow.png")
    albedo.filepath_raw = pa; albedo.file_format = "PNG"; albedo.save()
    glow.filepath_raw = pg; glow.file_format = "PNG"; glow.save()
    # one material using the baked atlas (+ the glow mask as emission for previews)
    m = bpy.data.materials.new("M_Hero_Body"); m.use_nodes = True
    nt = m.node_tree; bsdf = nt.nodes["Principled BSDF"]
    ti = nt.nodes.new("ShaderNodeTexImage"); ti.image = albedo
    nt.links.new(ti.outputs["Color"], bsdf.inputs["Base Color"])
    tg = nt.nodes.new("ShaderNodeTexImage"); tg.image = glow
    nt.links.new(tg.outputs["Color"], bsdf.inputs["Emission Color"]); bsdf.inputs["Emission Strength"].default_value = 1.5
    bsdf.inputs["Roughness"].default_value = 0.6
    obj.data.materials.clear(); obj.data.materials.append(m)
    return pa, pg

# ---------------------------------------------------------------- preview renders
def stage_and_render(objs_center_z, out_png, rim="#ff7a3a", mode="close", res=(720, 1080), samples=48, ground="#5d6b3a"):
    sc = bpy.context.scene
    for o in [o for o in bpy.data.objects if o.name.split(".")[0] in ("Ground", "Key", "Rim", "Cam")]: bpy.data.objects.remove(o, do_unlink=True)
    sc.render.engine = "CYCLES"; sc.cycles.samples = samples; sc.cycles.device = "CPU"; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = res; sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
    w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes["Background"]; bg.inputs[0].default_value = (0.42, 0.45, 0.5, 1); bg.inputs[1].default_value = 0.8
    def rgbl(h):
        h = h.lstrip("#"); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c]
    gp = bpy.data.meshes.new("Ground"); bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=12); bm.to_mesh(gp); bm.free()
    g = link(bpy.data.objects.new("Ground", gp)); gm = painted("Ground", ground, strokes=0.25, edge=0, ao=0.3); assign(g, gm)
    sun = bpy.data.lights.new("Key", "SUN"); sun.energy = 3.6; sun.angle = math.radians(8)
    so = link(bpy.data.objects.new("Key", sun)); so.rotation_euler = Euler((math.radians(42), 0, math.radians(-30)))
    # element rim: an area light behind and above the hero (the game's shader adds this as a fresnel rim)
    rl = bpy.data.lights.new("Rim", "AREA"); rl.energy = 260.0; rl.size = 1.2; rl.color = rgbl(rim)
    ro = link(bpy.data.objects.new("Rim", rl)); ro.location = Vector((0.4, 1.6, objs_center_z + 1.4))
    ro.rotation_euler = (Vector((0, 0, objs_center_z)) - ro.location).to_track_quat("-Z", "Y").to_euler()
    cam = bpy.data.cameras.new("Cam"); co = link(bpy.data.objects.new("Cam", cam)); sc.camera = co
    pitch = math.radians(34)
    if mode == "close":
        cam.lens = 50; d = 4.6; target = Vector((0, 0, objs_center_z))
        co.location = target + Vector((math.sin(math.radians(-22)) * d * math.cos(pitch), -math.cos(math.radians(-22)) * d * math.cos(pitch), d * math.sin(pitch) * 0.55))
    else:  # battle framing: 34 deg pitch, portrait 62 deg lens-equivalent, hero ~10% of the frame height
        cam.sensor_fit = "VERTICAL"; cam.angle_y = math.radians(62); d = 13.0; target = Vector((0, 0, objs_center_z))
        co.location = target + Vector((math.sin(math.radians(-18)) * d * math.cos(pitch), -math.cos(math.radians(-18)) * d * math.cos(pitch), d * math.sin(pitch)))
    direction = target - co.location
    co.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = out_png
    bpy.ops.render.render(write_still=True)

def game_look(obj, rim="#ff7a3a", ramp=0.35, wrap=0.3, rim_strength=0.7):
    """Preview material matching the game's Sigilbound/CharacterPainted shader: wrap diffuse blended
    with a three-band painterly ramp, ambient, an element-coloured fresnel rim and the glow mask.
    Needs the baked atlas (bake_atlas). Used for EEVEE preview renders only."""
    def rgbl(h):
        h = h.lstrip("#"); c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
        return [x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c] + [1.0]
    src = obj.data.materials[0]
    imgs = [n.image for n in src.node_tree.nodes if n.type == "TEX_IMAGE"]
    alb = next(i for i in imgs if "Albedo" in i.name); glw = next((i for i in imgs if "Glow" in i.name), None)
    m = bpy.data.materials.new("GameLook"); m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; Ln = nt.links
    for n in list(N): N.remove(n)
    out = N.new("ShaderNodeOutputMaterial")
    ta = N.new("ShaderNodeTexImage"); ta.image = alb
    dif = N.new("ShaderNodeBsdfDiffuse"); dif.inputs["Color"].default_value = (1, 1, 1, 1)
    s2r = N.new("ShaderNodeShaderToRGB"); Ln.new(dif.outputs["BSDF"], s2r.inputs["Shader"])
    bw = N.new("ShaderNodeRGBToBW"); Ln.new(s2r.outputs["Color"], bw.inputs["Color"])
    rp = N.new("ShaderNodeValToRGB"); Ln.new(bw.outputs["Val"], rp.inputs["Fac"])
    e = rp.color_ramp.elements; e[0].position = 0.0; e[0].color = (0.32, 0.3, 0.36, 1); e[1].position = 0.55; e[1].color = (1.0, 0.97, 0.92, 1)
    mid = e.new(0.12); mid.color = (0.62, 0.6, 0.64, 1); hi = e.new(0.9); hi.color = (1.12, 1.08, 1.0, 1)
    rp.color_ramp.interpolation = "EASE"
    mul = N.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"; mul.inputs["Factor"].default_value = 1
    Ln.new(ta.outputs["Color"], mul.inputs["A"]); Ln.new(rp.outputs["Color"], mul.inputs["B"])
    lw = N.new("ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.35
    rmp = N.new("ShaderNodeMapRange"); rmp.inputs["From Min"].default_value = 0.45; rmp.inputs["From Max"].default_value = 0.95; rmp.inputs["To Max"].default_value = rim_strength
    Ln.new(lw.outputs["Facing"], rmp.inputs["Value"])
    rc = N.new("ShaderNodeMix"); rc.data_type = "RGBA"; rc.blend_type = "ADD"
    Ln.new(rmp.outputs["Result"], rc.inputs["Factor"]); Ln.new(mul.outputs["Result"], rc.inputs["A"]); rc.inputs["B"].default_value = rgbl(rim)
    col = rc.outputs["Result"]
    if glw:
        tg = N.new("ShaderNodeTexImage"); tg.image = glw
        ad = N.new("ShaderNodeMix"); ad.data_type = "RGBA"; ad.blend_type = "ADD"; ad.inputs["Factor"].default_value = 1.6
        Ln.new(col, ad.inputs["A"]); Ln.new(tg.outputs["Color"], ad.inputs["B"]); col = ad.outputs["Result"]
    em = N.new("ShaderNodeEmission"); Ln.new(col, em.inputs["Color"]); em.inputs["Strength"].default_value = 1.0
    Ln.new(em.outputs["Emission"], out.inputs["Surface"])
    obj.data.materials.clear(); obj.data.materials.append(m)

def eevee_render(center_z, out_png, mode="viewer", rim="#ff7a3a", res=(480, 720), height=1.9):
    """Game-like preview: 'viewer' = the hero screen (plain backdrop, 3/4 view); 'battle' = the battle
    camera (34 deg pitch, 62 deg vertical FOV, hero about 10% of a 390x844 frame)."""
    sc = bpy.context.scene
    for o in [o for o in bpy.data.objects if o.name.split(".")[0] in ("Ground", "Key", "Rim", "Cam", "Blob")]: bpy.data.objects.remove(o, do_unlink=True)
    sc.render.engine = "BLENDER_EEVEE"; sc.render.resolution_x, sc.render.resolution_y = res
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("W"); sc.world = w; w.use_nodes = True
    bgc = (0.068, 0.062, 0.115, 1) if mode == "viewer" else (0.5, 0.55, 0.62, 1)
    w.node_tree.nodes["Background"].inputs[0].default_value = bgc
    sun = bpy.data.lights.new("Key", "SUN"); sun.energy = 3.0
    so = link(bpy.data.objects.new("Key", sun)); so.rotation_euler = Euler((math.radians(45), 0, math.radians(-35)))
    if mode == "battle":
        gp = bpy.data.meshes.new("Ground"); bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=14); bm.to_mesh(gp); bm.free()
        g = link(bpy.data.objects.new("Ground", gp)); gm = bpy.data.materials.new("GroundM"); gm.use_nodes = True
        gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.16, 0.2, 0.07, 1); g.data.materials.append(gm)
    cam = bpy.data.cameras.new("Cam"); co = link(bpy.data.objects.new("Cam", cam)); sc.camera = co
    cam.sensor_fit = "VERTICAL"
    if mode == "viewer":
        cam.angle_y = math.radians(30); d = height * 2.25; yaw = math.radians(-35); pitch = math.radians(6)
        target = Vector((0, 0, height * 0.5))
    else:
        cam.angle_y = math.radians(62); d = 12.0; yaw = math.radians(-12); pitch = math.radians(34)
        target = Vector((0, 0, center_z))
    co.location = target + Vector((math.sin(yaw) * d * math.cos(pitch), -math.cos(yaw) * d * math.cos(pitch), d * math.sin(pitch)))
    co.rotation_euler = (target - co.location).to_track_quat("-Z", "Y").to_euler()
    sc.render.filepath = out_png
    bpy.ops.render.render(write_still=True)

def export_glb(path):
    bpy.ops.export_scene.gltf(filepath=path, export_format="GLB", use_selection=False, export_skins=True, export_animations=False,
                              export_apply=False, export_yup=True, export_materials="EXPORT", export_image_format="AUTO")

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--hero", required=True); ap.add_argument("--out", required=True)
    ap.add_argument("--render", action="store_true"); ap.add_argument("--bake", type=int, default=0)
    ap.add_argument("--no-export", action="store_true"); ap.add_argument("--samples", type=int, default=48)
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:])
    os.makedirs(a.out, exist_ok=True)
    reset()
    import recipes
    r = getattr(recipes, a.hero)()
    ao, hero = rig(r["J"], r["meshes"], r["rigid"])
    tris = sum(len(p.vertices) - 2 for p in hero.data.polygons)
    print(f"[forge] {a.hero}: {tris} tris, {len(hero.data.materials)} materials, {len(ao.data.bones)} bones")
    if a.bake:
        pa, pg = bake_atlas(hero, a.bake, a.out); print("[forge] baked", pa, pg)
    if not a.no_export:
        p = os.path.join(a.out, f"SB_Hero_{a.hero}_LOD0.glb"); export_glb(p); print("[forge] exported", p, os.path.getsize(p) // 1024, "KB")
    if a.render and a.bake:
        pose(ao, r.get("pose", {}))
        game_look(hero, r.get("rim", "#ff7a3a"))
        eevee_render(r.get("center", 1.0), os.path.join(a.out, f"{a.hero}_viewer.png"), "viewer", r.get("rim", "#ff7a3a"), height=r.get("height", 1.9))
        eevee_render(r.get("center", 1.0), os.path.join(a.out, f"{a.hero}_battle.png"), "battle", r.get("rim", "#ff7a3a"), res=(780, 1688))
    elif a.render:
        pose(ao, r.get("pose", {}))
        stage_and_render(r.get("center", 1.0), os.path.join(a.out, f"{a.hero}_close.png"), r.get("rim", "#ff7a3a"), "close", samples=a.samples)
        stage_and_render(r.get("center", 1.0), os.path.join(a.out, f"{a.hero}_battle.png"), r.get("rim", "#ff7a3a"), "battle", res=(780, 1688), samples=max(16, a.samples // 2))

if __name__ == "__main__":
    main()
