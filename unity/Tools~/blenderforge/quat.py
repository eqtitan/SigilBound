# Free-path heroes (docs/sigilbound/PLAN.md steps 13-18): start from the CC0 Quaternius base
# body the game already uses (godot/models: decoded copies), repaint every part with the
# hand-painted material from forge.py in the hero's palette (Core/Looks.cs), add the hero's
# identity kit rigidly on the right bones, bake one painted atlas, and export a GLB that keeps
# the Quaternius skeleton and its 24 animations, so every existing clip still plays.
#   python quat.py -- --hero ysolde --out <dir> [--bake 1024] [--render]
import argparse, math, os, sys
import bpy
from mathutils import Vector, Matrix

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import forge as F

HERE = os.path.dirname(os.path.abspath(__file__))
MODELS = os.path.normpath(os.path.join(HERE, "..", "..", "..", "godot", "models"))
BASE_FILES = {"hooded": "hooded_adventurer.glb", "adventurer": "adventurer.glb", "monarch": "monarch.glb"}
SKIN = {"fair": ("#f0c4a4", "#c98a70", "#ffe2cc"), "tan": ("#d9a074", "#a06a48", "#f4c8a0"),
        "brown": ("#9a6440", "#5e3a22", "#c88a60"), "deep": ("#6a4028", "#3a2014", "#9a6440")}

def shade(h, k):
    h = h.lstrip("#"); c = [int(h[i:i + 2], 16) for i in (0, 2, 4)]
    return "#" + "".join(f"{max(0, min(255, int(v * k))):02x}" for v in c)

# The four starters (Core/Looks.cs colours). parts: base material name -> palette entry.
HEROES = {
    "ysolde": dict(base="hooded", skin="fair", hair="#c8281e", armor="#5a1e1e", accent="#ff7a3a", cloth="#3e1414", glow="#ffb347",
                   hide=["hood"], kit=["long_hair", "circlet", "whip"], rim="#ff7a3a"),
    "gorrum": dict(base="monarch", skin="deep", hair="#1a1410", armor="#3a2a22", accent="#ff5a1a", cloth="#2a1e18", glow="#ff7a2a",
                   hide=["hair", "crown"], kit=["horns", "hammer", "runes"], rim="#ff7a3a", height=1.1),
    "hask":   dict(base="monarch", skin="tan", hair="#4a4036", armor="#5c656e", accent="#d8e8c8", cloth="#2f6b3a", glow=None,
                   hide=["crown"], kit=["helm", "shield"], rim="#3fb57a"),
    "corvan": dict(base="monarch", skin="brown", hair="#d0e6f0", armor="#20507a", accent="#7fd4ff", cloth="#173a5a", glow="#7fd4ff",
                   hide=["crown"], kit=["staff"], rim="#3a8ee8", height=0.97),
}

def palette(h):
    sk = SKIN[h["skin"]]
    m = {
        "Skin": F.painted("P_Skin", sk[0], shade=sk[1], light=sk[2], strokes=0.06, edge=0.08, ao=0.4),
        "Armor": F.painted("P_Armor", h["armor"], shade=shade(h["armor"], 0.5), light=shade(h["armor"], 1.6), strokes=0.2, edge=0.35, rough=0.5),
        "Cloth": F.painted("P_Cloth", h["cloth"], shade=shade(h["cloth"], 0.5), light=shade(h["cloth"], 1.5), strokes=0.24, edge=0.15),
        "Leather": F.painted("P_Leather", shade(h["armor"], 0.75), shade=shade(h["armor"], 0.4), light=shade(h["armor"], 1.3), strokes=0.22, edge=0.3),
        "Accent": F.painted("P_Accent", h["accent"], shade=shade(h["accent"], 0.6), light=shade(h["accent"], 1.3), metal=0.4, rough=0.35, edge=0.45, strokes=0.08),
        "Gold": F.painted("P_Gold", "#e0a640", shade="#8a5a18", light="#fff0a0", metal=0.9, rough=0.3, edge=0.5, strokes=0.08),
        "Hair": F.painted("P_Hair", h["hair"], shade=shade(h["hair"], 0.45), light=shade(h["hair"], 1.5), strokes=0.35, edge=0.3, rough=0.45),
        "White": F.painted("P_White", "#f4efe6", strokes=0.02, edge=0.0, ao=0.2),
        "Eye": (F.painted("P_Eye", h["glow"], glow=h["glow"], glow_strength=4, strokes=0.0, edge=0) if h.get("glow") else F.painted("P_Eye", "#1a1410", strokes=0.0, edge=0.0)),
        "Wood": F.painted("P_Wood", "#7a5232", shade="#3e2614", light="#b07a4a", strokes=0.4, edge=0.2),
        "Steel": F.painted("P_Steel", "#9aa4ae", shade="#4a525a", light="#e8f0f8", metal=0.85, rough=0.3, edge=0.55, strokes=0.06),
    }
    if h.get("glow"):
        m["Glow"] = F.painted("P_Glow", h["glow"], glow=h["glow"], glow_strength=6, strokes=0.0, edge=0)
    return m

# Colour roles per base model, as Core/Looks.cs Roles ("head:X" = only on the head mesh), and
# role -> palette entry. A role of None removes those faces (hood, crown, a bald hero's hair).
ROLES = {
    "hooded": {"DarkBrown": "armor", "Black": "cloth", "Brown": "cloth", "LightBrown": "accent", "Gold": "accent", "Metal": "trim",
               "Metal_Dark": "blade", "Brown2": "wood", "Skin": "skin", "head:Black": "cloth", "head:Brown": "eye", "head:DarkBrown": "hood", "White": "hair"},
    "monarch": {"Metal": "armor", "Metal_Dark": "trim", "Blue": "cloth", "DarkBrown": "pants", "Beige": "accent", "Gold": "crown",
                "Hair_White": "hair", "Skin": "skin", "Eye": "eye"},
}
HEAD_MESH = {"hooded": "Medieval_Head", "monarch": "King_Head", "adventurer": "Adventurer_Head"}
ROLE_PAL = {"armor": "Armor", "cloth": "Cloth", "pants": "Leather", "accent": "Accent", "trim": "Accent", "blade": "Steel",
            "wood": "Wood", "skin": "Skin", "hair": "Hair", "eye": "Eye", "hood": "Cloth", "crown": "Gold"}

def import_base(h):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Keep the file's own rest transforms (no bind-pose guessing), so the exported skeleton matches
    # the base node-for-node and the base clips play on it unchanged.
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, BASE_FILES[h["base"]]), guess_original_bind_pose=False)
    arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    for o in list(bpy.context.scene.objects):
        if o.type == "MESH" and (o.name.startswith(("Sword", "Icosphere", "Axe", "Shield", "Staff", "Bow", "Crown")) or len(o.data.vertices) < 60):
            bpy.data.objects.remove(o, do_unlink=True)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    return arm, meshes

def bp(arm, bone, x=0.0, y=0.0, z=0.0):
    """A point in a bone's own rest frame, in metres: y runs along the bone (toward the child,
    e.g. the fingers for a wrist), x and z across it. Kit built this way follows the bone in any
    pose; the Quaternius rest pose is a T-pose, so world offsets at rest would swing with the arm."""
    b = arm.data.bones[bone]
    k = 1.0 / arm.matrix_world.to_scale().x  # the base armature is scaled x100
    return arm.matrix_world @ (b.matrix_local @ Vector((x * k, y * k, z * k)))

def bone_world(arm, name, tail=False):
    b = arm.data.bones[name]
    return arm.matrix_world @ (b.tail_local if tail else b.head_local)

def rigid(obj, arm, bone, mat):
    """Bind a kit piece fully to one bone (weight 1) so it moves with the animations."""
    F.assign(obj, mat)
    vg = obj.vertex_groups.new(name=bone); vg.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    mod = obj.modifiers.new("Armature", "ARMATURE"); mod.object = arm
    obj.parent = arm
    obj.matrix_parent_inverse = arm.matrix_world.inverted()  # keep its world placement (the base armature is scaled x100)
    return obj

def kit(h, arm, P):
    out = []
    head = bone_world(arm, "Head"); head_top = bone_world(arm, "Head", tail=True)
    hh = 0.12  # half a head, in metres (the Quaternius Head bone itself is nearly zero length)
    hand_r = bone_world(arm, "Wrist.R"); hand_l = bone_world(arm, "Wrist.L")
    for k in h["kit"]:
        if k == "long_hair":
            # A fall of tapered strands from the crown down the back.
            for i in range(9):
                a = math.radians(-70 + i * 17.5)
                top = head + Vector((math.sin(a) * hh * 0.55, math.cos(a) * hh * 0.35 + hh * 0.25, hh * 0.95))
                pts = [top, top + Vector((math.sin(a) * hh * 0.25, hh * 0.45, -hh * 0.9)), top + Vector((math.sin(a) * hh * 0.3, hh * 0.55, -hh * 2.0))]
                out.append(rigid(F.strand(f"Hair{i}", pts, hh * 0.16, hh * 0.04), arm, "Head", P["Hair"]))
        elif k == "circlet":
            c = F.torus("Circlet", head + Vector((0, 0, hh * 0.72)), hh * 0.58, hh * 0.05, rot=(math.radians(8), 0, 0))
            out.append(rigid(c, arm, "Head", P["Gold"]))
            gem = F.ellipsoid("Gem", head + Vector((0, -hh * 0.6, hh * 0.78)), (hh * 0.08, hh * 0.05, hh * 0.08))
            out.append(rigid(gem, arm, "Head", P.get("Glow", P["Accent"])))
        elif k == "horns":
            for s in (-1, 1):
                pts = [head + Vector((s * hh * 0.45, 0, hh * 0.75)), head + Vector((s * hh * 1.0, 0, hh * 1.05)), head + Vector((s * hh * 1.15, -hh * 0.1, hh * 1.6))]
                out.append(rigid(F.strand(f"Horn{s}", pts, hh * 0.16, hh * 0.02), arm, "Head", P["White"]))
        elif k == "helm":
            dome = F.ellipsoid("Helm", head + Vector((0, 0, hh * 0.7)), (hh * 0.68, hh * 0.68, hh * 0.55))
            out.append(rigid(dome, arm, "Head", P["Steel"]))
            out.append(rigid(F.box("Nasal", head + Vector((0, -hh * 0.66, hh * 0.45)), (hh * 0.06, hh * 0.04, hh * 0.32)), arm, "Head", P["Steel"]))
        elif k == "whip":
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("Grip", [W(0, 0.02, 0), W(0, 0.16, 0)], 0.022), arm, "Wrist.R", P["Accent"]))
            pts = [W(0, 0.15, 0), W(0.04, 0.4, 0.05), W(0.12, 0.7, 0.02), W(0.2, 0.95, -0.08), W(0.18, 1.15, -0.2)]
            out.append(rigid(F.strand("Whip", pts, 0.018, 0.004), arm, "Wrist.R", P["Leather"]))
        elif k == "hammer":
            # Haft through the fist (across the bone, along z), head at the far end.
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("Haft", [W(0, 0.08, 0.3), W(0, 0.08, -0.8)], 0.028), arm, "Wrist.R", P["Wood"]))
            out.append(rigid(F.ellipsoid("HammerHead", W(0, 0.08, -0.86), (0.18, 0.12, 0.12), seg=8, rings=4), arm, "Wrist.R", P["Steel"]))
            if "Glow" in P: out.append(rigid(F.ellipsoid("HammerRune", W(0.0, 0.08, -0.86), (0.19, 0.06, 0.05), seg=8, rings=4), arm, "Wrist.R", P["Glow"]))
        elif k == "runes" and "Glow" in P:
            chest = bone_world(arm, "Chest")
            for i, dx in enumerate((-0.06, 0.0, 0.06)):
                out.append(rigid(F.box(f"Rune{i}", chest + Vector((dx, -0.15, 0.02 * i)), (0.012, 0.01, 0.08)), arm, "Chest", P["Glow"]))
        elif k == "shield":
            c = bp(arm, "Wrist.L", -0.08, 0.04, 0)
            disc = F.torus("ShieldRim", c, 0.26, 0.03, rot=(math.radians(90), 0, 0))
            out.append(rigid(disc, arm, "Wrist.L", P["Steel"]))
            out.append(rigid(F.ellipsoid("ShieldFace", c, (0.25, 0.05, 0.25)), arm, "Wrist.L", P["Cloth"]))
            out.append(rigid(F.ellipsoid("ShieldBoss", c + Vector((0, -0.05, 0)), (0.06, 0.04, 0.06)), arm, "Wrist.L", P["Accent"]))
        elif k == "staff":
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("Staff", [W(0, 0.08, 0.95), W(0, 0.08, -0.9)], 0.024), arm, "Wrist.R", P["Wood"]))
            out.append(rigid(F.ellipsoid("Orb", W(0, 0.08, -0.98), (0.08, 0.08, 0.08)), arm, "Wrist.R", P.get("Glow", P["Accent"])))
    return out

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--hero", required=True); ap.add_argument("--out", required=True)
    ap.add_argument("--bake", type=int, default=0); ap.add_argument("--render", action="store_true")
    a = ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else sys.argv[1:])
    os.makedirs(a.out, exist_ok=True)
    h = HEROES[a.hero]
    arm, meshes = import_base(h)
    if a.render:  # 'before': the base body with flat palette colours, like today's look minus the kit
        F.eevee_render(1.0, os.path.join(a.out, f"{a.hero}_before.png"), "viewer", h["rim"], height=1.9 * h.get("height", 1))
    P = palette(h)
    import bmesh
    roles = ROLES[h["base"]]; hide = set(h.get("hide", []))
    for o in meshes:
        is_head = o.name.split(".")[0] == HEAD_MESH[h["base"]]
        names = [m.name.split(".")[0] if m else "" for m in o.data.materials]
        role = [roles.get("head:" + n) if is_head and ("head:" + n) in roles else roles.get(n, "cloth") for n in names]
        drop = [i for i, r in enumerate(role) if r in hide]
        if drop:
            bm = bmesh.new(); bm.from_mesh(o.data)
            bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.material_index in drop], context="FACES")
            bm.to_mesh(o.data); bm.free()
        for i, r in enumerate(role):
            o.data.materials[i] = P[ROLE_PAL.get(r, "Cloth")]
    pieces = kit(h, arm, P)
    # One skinned mesh: join the body parts and the kit (kit pieces carry rigid weights).
    F.select_only(meshes + pieces); bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    hero = bpy.context.view_layer.objects.active; hero.name = f"SB_Hero_{a.hero}"
    for o in [o for o in bpy.context.scene.objects if o.type == "MESH" and o != hero]:
        bpy.data.objects.remove(o, do_unlink=True)  # leftover helper meshes (e.g. an unused Icosphere)
    tris = sum(len(p.vertices) - 2 for p in hero.data.polygons)
    print(f"[quat] {a.hero}: {tris} tris, {len(hero.data.materials)} materials before bake, {len(arm.data.bones)} bones, {len(bpy.data.actions)} clips")
    if a.bake:
        print("[quat] baked", *F.bake_atlas(hero, a.bake, a.out))
    # No clips (the game plays the base model's own clips on this identical skeleton; re-exported
    # clips came out offset) and no material (the game applies the baked albedo + glow PNGs).
    if a.bake:
        os.replace(os.path.join(a.out, "albedo.png"), os.path.join(a.out, f"SB_Hero_{a.hero}_albedo.png"))
        os.replace(os.path.join(a.out, "glow.png"), os.path.join(a.out, f"SB_Hero_{a.hero}_glow.png"))
    if arm.animation_data: arm.animation_data.action = None   # export the rest pose, not an imported clip's pose
    for pb in arm.pose.bones:
        pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
    p = os.path.join(a.out, f"SB_Hero_{a.hero}_LOD0.glb")
    bpy.ops.export_scene.gltf(filepath=p, export_format="GLB", use_selection=False, export_skins=True, export_animations=False,
                              export_apply=False, export_yup=True, export_materials="PLACEHOLDER")
    print("[quat] exported", p, os.path.getsize(p) // 1024, "KB")
    if a.render:
        pass  # baked material as-is (forge.game_look rendered blank on this rig)
        F.eevee_render(1.0, os.path.join(a.out, f"{a.hero}_viewer.png"), "viewer", h["rim"], height=1.9 * h.get("height", 1))

if __name__ == "__main__":
    main()
