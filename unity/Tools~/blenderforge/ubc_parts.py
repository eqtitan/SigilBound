"""High-detail hero builder (docs/sigilbound/PLAN.md phase F, step 57).

Builds one hero from a design (concepts.json / new_heroes.json format) on the CC0 Quaternius Universal
Base Characters + Fantasy outfit parts in godot/models/ubc: picks the head, hair, outfit pieces, tints
them to the hero's palette, adds kit pieces bound rigidly to bones, merges everything onto ONE 65-joint
skeleton, decimates to a triangle budget and bakes one atlas (albedo, tangent normal, glow).

    bpyenv/bin/python ubc.py -- --design concepts.json --hero brannoc --out DIR [--tex 1024] [--tris 25000] [--render]

Writes SB_Hero_<id>_HD.glb (rest pose, no clips, placeholder material) + _albedo/_normal/_glow PNGs,
and SB_Hero_<id>_HD_LOD1.glb (same atlas, about a third of the triangles).
Kit pieces are still simple shapes; steps 59-63 replace them with modelled ones."""
import bpy, sys, os, json, math, re, glob
from mathutils import Vector, Euler, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
U = os.path.join(REPO, "godot", "models", "ubc")
FILES = {os.path.basename(f)[:-5]: f for f in glob.glob(os.path.join(U, "*", "*.gltf"))}
SKIN = {"fair": "#f0c8a8", "pale": "#f4dccc", "tan": "#c8915f", "brown": "#8a5a3a", "deep": "#5a3a26", "ash": "#9a9aa8", "blue": "#7aa8d0"}
ELEM = {"Flame": "#ff7a3a", "Gale": "#3fb57a", "Tide": "#3a8ee8", "Light": "#ffe27a", "Shadow": "#a050ff"}

def hexc(h, a=1.0):
    h = (h or "#888888").strip()
    if not h.startswith("#"): h = SKIN.get(h, "#888888")
    r, g, b = (int(h[i:i + 2], 16) / 255 for i in (1, 3, 5))
    lin = lambda c: c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4
    return (lin(r), lin(g), lin(b), a)

def part_file(name, sex, slot):
    if not name or str(name).startswith(("custom", "none")): return None
    n = name.strip()
    cands = [n, ("Female_" if sex == "female" else "Male_") + n]
    if sex == "female" and not n.startswith("Female_"): cands = ["Female_" + n] + cands[:1]
    for c in cands:
        if c in FILES: return c
        # Female_Ranger_Feet_Boots -> Female_Ranger_Feet; Knight_Feet -> Male_Knight_Feet_Armor
        for k in FILES:
            if k.startswith(c) or c.startswith(k + "_"):
                if sex == "female" and not k.startswith("Female_") and k.startswith("Male_"): continue
                return k
    return None

DEFAULT = {"male": {"arms": "Male_Peasant_Arms", "body": "Male_Peasant_Body", "legs": "Male_Peasant_Legs", "feet": "Male_Peasant_Feet"},
           "female": {"arms": "Female_Peasant_Arms", "body": "Female_Peasant_Body", "legs": "Female_Peasant_Legs", "feet": "Female_Peasant_Feet"}}
FEMALE_ALT = {"Knight": "Ranger", "Noble": "Ranger", "Wizard": "Peasant"}

def tint_material(mat, color, k):
    """Base colour = mix(texture, grey(texture) * colour * 1.8, k)."""
    if not mat or not mat.use_nodes: return
    nt = mat.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf is None: return
    link = next((l for l in nt.links if l.to_socket == bsdf.inputs["Base Color"]), None)
    if link is None:
        bsdf.inputs["Base Color"].default_value = color; return
    src = link.from_socket
    bw = nt.nodes.new("ShaderNodeRGBToBW"); nt.links.new(src, bw.inputs[0])
    mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"; mul.inputs[0].default_value = 1.0
    nt.links.new(bw.outputs[0], mul.inputs[6]); mul.inputs[7].default_value = tuple(min(1, c * 1.9) for c in color[:3]) + (1,)
    mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.inputs[0].default_value = k
    nt.links.new(src, mix.inputs[6]); nt.links.new(mul.outputs[2], mix.inputs[7])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])

def flat(name, color, emit=0.0, metal=0.0, rough=0.5):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = color; b.inputs["Metallic"].default_value = metal; b.inputs["Roughness"].default_value = rough
    if emit:
        b.inputs["Emission Color"].default_value = color; b.inputs["Emission Strength"].default_value = emit
    return m

def cull_covered_skin(gap=0.06):
    """Delete the skin body's faces that clothing covers (a ray from the face along its normal hits an
    outfit piece within `gap`), so the underbody costs only the visible skin: hands, neck, gaps."""
    import bmesh
    from mathutils.bvhtree import BVHTree
    skin = [o for o in bpy.context.scene.objects if o.type == "MESH" and any(m and "Regular" in m.name for m in o.data.materials) and len(o.data.polygons) > 8000]
    cloth = [o for o in bpy.context.scene.objects if o.type == "MESH" and o not in skin and not any(m and ("Regular" in m.name or "Eye" in m.name) for m in o.data.materials)]
    if not skin or not cloth: return
    verts, polys = [], []
    for o in cloth:
        mw = o.matrix_world; base = len(verts)
        verts += [mw @ v.co for v in o.data.vertices]; polys += [[base + i for i in p.vertices] for p in o.data.polygons]
    bvh = BVHTree.FromPolygons(verts, polys)
    for o in skin:
        mw = o.matrix_world; n3 = mw.to_3x3().inverted().transposed()
        bm = bmesh.new(); bm.from_mesh(o.data)
        dead = []
        for f in bm.faces:
            mat = o.data.materials[f.material_index]
            if mat is None or "Regular" not in mat.name: continue      # keep eyes, brows
            c = mw @ f.calc_center_median(); n = (n3 @ f.normal).normalized()
            hit = bvh.ray_cast(c + n * 0.002, n, gap)
            inside = bvh.ray_cast(c, -n, 0.015)            # clothing that dips just inside the skin
            if hit[0] is not None or inside[0] is not None:
                dead.append(f)
        bmesh.ops.delete(bm, geom=dead, context="FACES")
        bm.to_mesh(o.data); bm.free()
        print(f"[ubc] skin under the outfit: culled {len(dead)} covered faces, kept {len(o.data.polygons)}")

def build(c, pose=True):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sex = c.get("sex", "male"); o = c.get("outfit", {}); pal = c.get("palette", {})
    # A full skin body under the outfit (how modular kits are dressed): any gap between mixed pieces
    # (e.g. a short-sleeved top with forearm-only arms) shows skin, not a hole; covered faces are culled.
    parts = [("skin", "Regular_Female_FullBody" if sex == "female" else "Regular_Male_FullBody")]
    for slot in ("arms", "body", "legs", "feet"):
        f = part_file(o.get(slot), sex, slot)
        if f is None and sex == "female" and o.get(slot) and not str(o.get(slot)).startswith(("custom", "none")):
            for a, b in FEMALE_ALT.items():
                if a in o[slot]: f = part_file(o[slot].replace(a, b), sex, slot)
        robe_legs = slot == "legs" and str(o.get(slot, "")).startswith("none")
        # Under a robe: plain leggings (whatever shows when a knee lifts the skirt is cloth, not bare skin)
        if f is None: f = DEFAULT[sex][slot]
        if f: parts.append((slot, f))
    hp = part_file(o.get("head"), sex, "head")
    # A custom head built on a pack piece ("Knight_Head_Armet with storm wings", "Ranger_Head_Hood with thorn
    # antlers"): load the pack piece; kit_head.py adds the extras. Not when the design reshapes it away.
    hs_ = str(o.get("head", ""))
    if hp is None and hs_.startswith("custom") and "reshaped" not in hs_:
        m_ = re.search(r"((?:Female_|Male_)?(?:Knight|Ranger|Noble)_Head_[A-Za-z]+)", hs_)
        if m_: hp = part_file(m_.group(1), sex, "head")
    if hp: parts.append(("headgear", hp))
    for a in o.get("acc", []) or []:
        f = part_file(a, sex, "acc")
        if f: parts.append(("acc", f))
    hs = (c.get("hair") or {}).get("style", "")
    if sex == "female":
        hs = "Ponytail" if ("ponytail" in hs.lower() or "braid" in hs.lower() or hs == "Hair2") else ("Buzzed" if "crop" in hs.lower() else "SimpleParted")
    elif hs.startswith("custom"):
        hs = "Ponytail" if "ponytail" in hs.lower() else "SlickBack" if "swept" in hs.lower() else "Mohawk" if "spik" in hs.lower() else "SimpleParted"
    if hs in ("Balding", "Buzzed", "Dreads", "Mohawk", "Ponytail", "SimpleParted", "SlickBack") and not hp:
        parts.append(("hair", "Hair_" + hs))
    fac = c.get("facial", "none")
    if sex == "male" and fac in ("Beard", "Moustache", "MuttonChops"): parts.append(("hair", "Hair_" + fac))

    master = None
    for slot, f in parts:
        before = set(bpy.data.objects)
        bpy.ops.import_scene.gltf(filepath=FILES[f])
        new = [x for x in bpy.data.objects if x not in before]
        arm = next((x for x in new if x.type == "ARMATURE"), None)
        for m in [x for x in new if x.type == "MESH"]:
            for mat in m.data.materials:
                if mat is None: continue
                n = mat.name
                if "Eye" in n: continue
                if "Regular" in n: tint_material(mat, hexc(c.get("skin")), 0.9)
                elif "Hair" in n: tint_material(mat, hexc((c.get("hair") or {}).get("colour")), 0.95)
                else:
                    col = {"body": pal.get("main"), "arms": pal.get("main"), "legs": pal.get("secondary"), "feet": pal.get("secondary"),
                           "headgear": pal.get("metal"), "acc": pal.get("accent")}.get(slot, pal.get("main"))
                    # Under a gown the fallback top is the gown's bodice: dyed to the gown's colour.
                    gown = str(o.get("body", "")).startswith("custom") and any(k in str(o.get("body", "")).lower() for k in ("robe", "gown", "dress", "habit"))
                    tint_material(mat, hexc(col), 0.93 if gown and slot in ("body", "arms") else 0.7)
            if master is not None and arm is not None:
                for md in m.modifiers:
                    if md.type == "ARMATURE": md.object = master
                m.parent = master
        if master is None: master = arm
        elif arm is not None: bpy.data.objects.remove(arm, do_unlink=True)

    cull_covered_skin()
    if not pose:
        bpy.context.view_layer.update()
        return master
    # Body build: height/bulk via armature scale (concept only).
    bd = c.get("body", {}); master.scale = (master.scale.x * bd.get("bulk", 1), master.scale.y * bd.get("bulk", 1), master.scale.z * bd.get("height", 1))
    # A-pose: arms down from the T-pose.
    bpy.context.view_layer.objects.active = master; bpy.ops.object.mode_set(mode="POSE")
    for side, sgn in (("l", 1), ("r", -1)):
        pb = master.pose.bones.get("upperarm_" + side)
        if pb:
            pb.rotation_mode = "XYZ"
            w = master.matrix_world.to_3x3() @ pb.bone.matrix_local.to_3x3()
            axis_world = Vector((0, 1 if side == "r" else -1, 0)) * -1
            axis_local = (w.inverted() @ Vector((0, -1, 0))).normalized()
            pb.rotation_mode = "AXIS_ANGLE"; pb.rotation_axis_angle = (-math.radians(62) * sgn, *axis_local)
        lb = master.pose.bones.get("lowerarm_" + side)
        if lb:
            w = master.matrix_world.to_3x3() @ lb.bone.matrix_local.to_3x3()
            ax = (w.inverted() @ Vector((0, 0, 1))).normalized()
            lb.rotation_mode = "AXIS_ANGLE"; lb.rotation_axis_angle = (-math.radians(18) * sgn, *ax)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    return master

def bone_pos(arm, name):
    pb = arm.pose.bones[name]; return arm.matrix_world @ pb.head, arm.matrix_world @ pb.tail

def add_props(c, arm):
    pal = c.get("palette", {}); el = hexc(ELEM.get(c.get("element"), pal.get("glow")))
    metal = flat("Metal", hexc(pal.get("metal", "#aab0b8")), metal=0.9, rough=0.3)
    acc = flat("Accent", hexc(pal.get("accent")), metal=0.3, rough=0.4)
    main = flat("Main", hexc(pal.get("main")), rough=0.6)
    glow = flat("Glow", el, emit=6)
    text = " ".join([str(c.get("weapon", ""))] + [str(k) for k in c.get("kit", [])] + [str(x) for x in (c.get("outfit", {}).get("acc") or [])] + [str(c.get("outfit", {}).get("head", ""))]).lower()
    w = str(c.get("weapon", "")).lower()
    has = lambda txt, *ks: any(re.search(r"\b" + k + r"(e?s)?\b", txt) for k in ks)
    hand, tip = bone_pos(arm, "hand_r")
    hl, _ = bone_pos(arm, "hand_l")
    def obj(prim, mat, loc, scale, rot=(0, 0, 0)):
        getattr(bpy.ops.mesh, prim)(location=loc, rotation=rot); ob = bpy.context.object; ob.scale = scale; ob.data.materials.append(mat); return ob
    up = Vector((0, 0, 1))
    longw = any(k in w for k in ("spear", "lance", "glaive", "trident", "halberd", "scythe", "staff", "sceptre", "pole", "pike", "oar", "polearm"))
    blade = any(k in w for k in ("sword", "katana", "rapier", "blade", "sabre", "cutlass", "edge"))
    heavy = any(k in w for k in ("hammer", "maul", "mace", "club", "flail", "tankard", "pick", "anvil", "axe"))
    small = any(k in w for k in ("dagger", "knife", "sickle", "hook", "fan", "quill", "wand"))
    ranged = any(k in w for k in ("bow", "crossbow"))
    focus = any(k in w for k in ("orb", "lantern", "candle", "bell", "mirror", "book", "tome", "conch", "lamp", "harp", "lute"))
    if longw:
        obj("primitive_cylinder_add", main if "staff" in w else metal, hand + Vector((0, 0, 0.55)), (0.02, 0.02, 1.15))
        if "staff" in w or "sceptre" in w: obj("primitive_uv_sphere_add", glow, hand + Vector((0, 0, 1.75)), (0.09, 0.09, 0.09))
        else: obj("primitive_cone_add", metal, hand + Vector((0, 0, 1.82)), (0.06, 0.02, 0.2))
        if "trident" in w:
            for dx in (-0.08, 0.08): obj("primitive_cone_add", metal, hand + Vector((dx, 0, 1.75)), (0.03, 0.015, 0.14))
        if "scythe" in w or "glaive" in w or "halberd" in w: obj("primitive_cube_add", metal, hand + Vector((0.18, 0, 1.6)), (0.22, 0.01, 0.06))
    elif blade:
        obj("primitive_cube_add", metal, hand + Vector((0.0, -0.05, 0.5)), (0.025, 0.006, 0.45))
        obj("primitive_cube_add", acc, hand + Vector((0, -0.05, 0.06)), (0.09, 0.02, 0.015))
    elif heavy:
        obj("primitive_cylinder_add", main, hand + Vector((0, 0, 0.35)), (0.02, 0.02, 0.4))
        obj("primitive_cube_add", metal, hand + Vector((0, 0, 0.78)), (0.14, 0.09, 0.09))
    elif small:
        obj("primitive_cube_add", metal, hand + Vector((0, -0.04, 0.18)), (0.015, 0.005, 0.16))
        obj("primitive_cube_add", metal, hl + Vector((0, -0.04, 0.18)), (0.015, 0.005, 0.16))
    elif ranged:
        bpy.ops.mesh.primitive_torus_add(location=hl + Vector((0, -0.05, 0.1)), rotation=(0, math.radians(90), 0), major_radius=0.55, minor_radius=0.015)
        ob = bpy.context.object; ob.scale = (1, 0.35, 1); ob.data.materials.append(main)
    elif focus:
        obj("primitive_uv_sphere_add", glow, hand + Vector((0.05, -0.08, 0.05)), (0.08, 0.08, 0.08))
    else:
        obj("primitive_cylinder_add", main, hand + Vector((0, 0, 0.5)), (0.02, 0.02, 1.0)); obj("primitive_uv_sphere_add", glow, hand + Vector((0, 0, 1.5)), (0.07,) * 3)
    if has(text, "shield"):
        bpy.ops.mesh.primitive_cylinder_add(location=hl + Vector((0.05, -0.12, 0.05)), rotation=(math.radians(90), 0, 0)); ob = bpy.context.object
        ob.scale = (0.24, 0.32 if has(text, "tower") else 0.24, 0.03); ob.data.materials.append(metal)
    head, htip = bone_pos(arm, "Head"); top = htip + Vector((0, 0, 0.12))
    if has(text, "halo") or (c.get("rarity") == "Legendary" and not has(text, "crown", "helm", "hood")):
        bpy.ops.mesh.primitive_torus_add(location=top + Vector((0, 0, 0.12)), major_radius=0.14, minor_radius=0.012); bpy.context.object.data.materials.append(glow)
    if has(text, "crown", "circlet", "diadem"):
        obj("primitive_cylinder_add", acc, top - Vector((0, 0, 0.02)), (0.11, 0.11, 0.035))
    if has(text, "horn", "antler"):
        for sx in (-1, 1): obj("primitive_cone_add", acc, top + Vector((0.1 * sx, 0.02, 0.0)), (0.035, 0.035, 0.14), (0, math.radians(25 * sx), 0))
    neck, _ = bone_pos(arm, "neck_01")
    if has(text, "cape", "cloak", "mantle"):
        bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=0.42, radius2=0.2, depth=1.25, location=neck + Vector((0, 0.13, -0.66)), end_fill_type="NOTHING"); cp = bpy.context.object; cp.scale = (1, 0.35, 1); cp.data.materials.append(main)
        import bmesh; bm = bmesh.new(); bm.from_mesh(cp.data); bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.y < -0.05], context="VERTS"); bm.to_mesh(cp.data); bm.free()
    if has(text, "wing"):
        for sx in (-1, 1): obj("primitive_cube_add", glow if c.get("element") in ("Light", "Flame") else main, neck + Vector((0.42 * sx, 0.22, -0.15)), (0.4, 0.01, 0.22), (0, math.radians(-25 * sx), 0))
    if has(text, "robe", "skirt", "dress", "gown") or str(c.get("outfit", {}).get("legs", "")).startswith("none"):
        pel, _ = bone_pos(arm, "pelvis")
        bpy.ops.mesh.primitive_cone_add(location=pel + Vector((0, 0, -0.42)), radius1=0.36, radius2=0.16, depth=0.95); bpy.context.object.data.materials.append(main)
    # Species stand-ins (new heroes): ears, tail, shell, antlers.
    sp = str(c.get("species", "human")).lower()
    skinm = flat("SkinX", hexc(c.get("skin")), rough=0.7)
    furm = flat("Fur", hexc((c.get("hair") or {}).get("colour", pal.get("secondary"))), rough=0.8)
    if sp != "human":
        hx = htip + Vector((0, 0, -0.08))
        if any(k in sp for k in ("elf", "sylph", "sprite", "dryad", "gnome", "fae")):
            for sx in (-1, 1): obj("primitive_cone_add", skinm, hx + Vector((0.11 * sx, 0.0, -0.02)), (0.02, 0.012, 0.09), (0, math.radians(-70 * sx), 0))
        if any(k in sp for k in ("fox", "cat", "lion", "wolf", "bear", "hare", "rabbit", "raven", "owl")):
            long_ = "hare" in sp or "rabbit" in sp
            if not ("raven" in sp or "owl" in sp):
                for sx in (-1, 1): obj("primitive_cone_add", furm, hx + Vector((0.08 * sx, 0.01, 0.13 if long_ else 0.08)), (0.04, 0.02, 0.16 if long_ else 0.07), (0, math.radians(12 * sx), 0))
            else:
                obj("primitive_cone_add", acc, hx + Vector((0, -0.12, -0.02)), (0.03, 0.03, 0.06), (math.radians(-90), 0, 0))  # beak
            if not ("raven" in sp or "owl" in sp or "bear" in sp):
                pel, _ = bone_pos(arm, "pelvis")
                obj("primitive_cylinder_add", furm, pel + Vector((0, 0.22, -0.18)), (0.05, 0.05, 0.32), (math.radians(55), 0, 0))
        if "turtle" in sp:
            sp3, _ = bone_pos(arm, "spine_03"); bpy.ops.mesh.primitive_uv_sphere_add(location=sp3 + Vector((0, 0.18, -0.1))); ob = bpy.context.object; ob.scale = (0.32, 0.16, 0.4); ob.data.materials.append(furm)
        if "shark" in sp:
            sp3, _ = bone_pos(arm, "spine_03"); obj("primitive_cone_add", skinm, sp3 + Vector((0, 0.2, 0.0)), (0.02, 0.12, 0.2), (math.radians(-20), 0, 0))
        if any(k in sp for k in ("goat", "stag", "dryad")) and not has(text, "horn", "antler"):
            for sx in (-1, 1): obj("primitive_cone_add", acc, top + Vector((0.09 * sx, 0.02, -0.02)), (0.03, 0.03, 0.15), (0, math.radians(30 * sx), 0))
        if "golem" in sp:
            for dz in (0.0, 0.35):
                sp3, _ = bone_pos(arm, "spine_02"); obj("primitive_ico_sphere_add", furm, sp3 + Vector((0.2, -0.05, dz)), (0.13, 0.1, 0.1))
        if "wraith" in sp or "spirit" in sp:
            pel, _ = bone_pos(arm, "pelvis")
            bpy.ops.mesh.primitive_cone_add(location=pel + Vector((0, 0, -0.45)), radius1=0.12, radius2=0.3, depth=0.9); bpy.context.object.data.materials.append(flat("Ghost", el, emit=1.5))
    # Element aura ring on the ground
    bpy.ops.mesh.primitive_torus_add(location=(0, 0, 0.01), major_radius=0.55, minor_radius=0.02); bpy.context.object.data.materials.append(glow)

