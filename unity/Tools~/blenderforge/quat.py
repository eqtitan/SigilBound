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
        "brown": ("#9a6440", "#5e3a22", "#c88a60"), "deep": ("#6a4028", "#3a2014", "#9a6440"),
        "pale": ("#f3d6c1", "#c9a08a", "#fff0e4"), "ash": ("#b9b4c4", "#7a7488", "#dcd8e6"), "blue": ("#8fb3d9", "#56769a", "#c0d8f0")}

def shade(h, k):
    h = h.lstrip("#"); c = [int(h[i:i + 2], 16) for i in (0, 2, 4)]
    return "#" + "".join(f"{max(0, min(255, int(v * k))):02x}" for v in c)

# Heroes (Core/Looks.cs colours). hide: colour roles whose faces are removed; kit: identity pieces
# (see kit()); cape: cape colour (default: cloth). Step 13: the starters; step 14: Ulfgar, Morwenna,
# Oriel, Vaelith (the owner's team in the build 27 screenshots).
HEROES = {
    "ysolde": dict(base="hooded", skin="fair", hair="#c8281e", armor="#5a1e1e", accent="#ff7a3a", cloth="#3e1414", glow="#ffb347",
                   hide=["hood"], kit=["long_hair", "circlet", "whip"], rim="#ff7a3a"),
    "gorrum": dict(base="monarch", skin="deep", hair="#1a1410", armor="#3a2a22", accent="#ff5a1a", cloth="#2a1e18", glow="#ff7a2a",
                   hide=["hair", "crown"], kit=["horns", "hammer", "runes"], rim="#ff7a3a", height=1.1),
    "hask":   dict(base="monarch", skin="tan", hair="#4a4036", armor="#5c656e", accent="#d8e8c8", cloth="#2f6b3a", glow=None,
                   hide=["crown", "hair"], kit=["helm", "shield"], rim="#3fb57a"),
    "corvan": dict(base="monarch", skin="brown", hair="#d0e6f0", armor="#20507a", accent="#7fd4ff", cloth="#173a5a", glow="#7fd4ff",
                   hide=["crown"], kit=["staff"], rim="#3a8ee8", height=0.97),
    "ulfgar": dict(base="monarch", skin="tan", hair="#c8ccd0", armor="#8a9aac", accent="#bfe8ff", cloth="#3a4a62", glow=None, cape="#2a3a52",
                   hide=["crown", "hair"], kit=["long_hair", "beard", "helm", "shield", "cape"], rim="#3fb57a", height=1.08),
    "morwenna": dict(base="hooded", skin="ash", hair="#120a1a", armor="#1a0f24", accent="#a050ff", cloth="#140b1e", glow="#d090ff",
                   hide=["hood"], kit=["long_hair", "crown", "orb", "cape", "robe"], rim="#a050ff", height=1.06),
    "oriel":  dict(base="adventurer", skin="deep", hair="#f0e6c8", armor="#e08a2c", accent="#ffe27a", cloth="#b5521f", glow="#ffe27a",
                   hide=[], kit=["halo", "staff", "robe"], rim="#ffe27a", height=1.04),
    "vaelith": dict(base="adventurer", skin="fair", hair="#ff5a2a", armor="#3a1010", accent="#ffb020", cloth="#24100a", glow="#ffd060", cape="#5a1410",
                   hide=["hair"], kit=["flame_hair", "crown", "sword", "cape"], rim="#ff7a3a", height=1.06),
    # Step 15.
    "aurelia": dict(base="monarch", skin="fair", hair="#e8b84a", armor="#d9b24a", accent="#f5f0e0", cloth="#2a3f8f", glow="#fff2b0", cape="#2a3f8f",
                   hide=["crown", "hair"], kit=["long_hair", "helm", "shield", "cape"], rim="#ffe27a", height=1.03),
    "velka":  dict(base="hooded", skin="blue", hair="#e8f4ff", armor="#3a6a8a", accent="#bfefff", cloth="#2c5470", glow="#bfefff",
                   hide=["hood"], kit=["long_hair", "circlet", "staff", "robe"], rim="#3a8ee8", height=1.03),
    "thrain": dict(base="monarch", skin="tan", hair="#a8321a", armor="#5a3a2a", accent="#ffa040", cloth="#7a5034", glow=None, cape="#5a2a1a",
                   hide=["crown"], kit=["beard", "hammer", "cape"], rim="#ff7a3a", height=0.88),
    "kaito":  dict(base="adventurer", skin="fair", hair="#1a1a1a", armor="#2f5a4a", accent="#7affc0", cloth="#1e3a30", glow="#a8ffdf",
                   hide=[], kit=["spiky_hair", "bandana", "katana"], rim="#3fb57a", height=1.02),
    # Step 16.
    "lumen":  dict(base="adventurer", skin="fair", hair="#f2d27a", armor="#c8d0dc", accent="#ffcc33", cloth="#4f5a6c", glow="#fff2b0", cape="#4f5a6c",
                   hide=[], kit=["sword", "cape"], rim="#ffe27a", height=1.06),
    "nyx":    dict(base="hooded", skin="pale", hair="#1e1a2a", armor="#26203a", accent="#b48cff", cloth="#1a1628", glow="#c9a3ff",
                   hide=[], kit=["mask", "daggers"], rim="#a050ff", height=0.97),
    "tamsin": dict(base="hooded", skin="tan", hair="#8a6a2a", armor="#5f7f3a", accent="#e8d07a", cloth="#4a6a2a", glow=None,
                   hide=["hood"], kit=["braid", "bow", "quiver"], rim="#3fb57a", height=0.98),
    "sable":  dict(base="hooded", skin="pale", hair="#3a2050", armor="#232a48", accent="#c8d8ff", cloth="#1c2240", glow="#c8d8ff", cape="#161a30",
                   hide=["hood"], kit=["veil", "mirror", "cape", "robe"], rim="#a050ff", height=1.0),
    # Step 17.
    "brannoc": dict(base="adventurer", skin="tan", hair="#5a3a22", armor="#4f7a34", accent="#d8b04a", cloth="#7a5232", glow=None,
                   hide=[], kit=["bandana", "spear"], rim="#3fb57a", height=1.0),
    "mira":   dict(base="hooded", skin="fair", hair="#2f8f8f", armor="#3f7f9a", accent="#e8e0c8", cloth="#2a5a70", glow=None,
                   hide=["hood"], kit=["ponytail", "shield"], rim="#3a8ee8", height=0.95),
    "pell":   dict(base="adventurer", skin="pale", hair="#c8642a", armor="#8a4a2a", accent="#ffb347", cloth="#6a3420", glow="#ffb347",
                   hide=[], kit=["candle", "robe"], rim="#ff7a3a", height=0.9),
    "odo":    dict(base="monarch", skin="brown", hair="#1e1610", armor="#4e3626", accent="#9ab84a", cloth="#6b7a3a", glow=None,
                   hide=["crown", "hair"], kit=["beard", "club"], rim="#3fb57a", height=1.03),
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
        "Boot": F.painted("P_Boot", "#2a1f1a", shade="#140e0a", light="#4a3a2e", strokes=0.2, edge=0.3),
        "Cape": F.painted("P_Cape", h.get("cape") or h["cloth"], shade=shade(h.get("cape") or h["cloth"], 0.45), light=shade(h.get("cape") or h["cloth"], 1.6), strokes=0.28, edge=0.15),
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
    "adventurer": {"LightGreen": "armor", "Green": "cloth", "Brown": "pants", "Brown2": "pants", "Black": "boot", "Grey": "boot", "Hair": "hair",
                   "Eyebrows": "hair", "Gold": "accent", "Skin": "skin", "Eye": "eye"},
    "monarch": {"Metal": "armor", "Metal_Dark": "trim", "Blue": "cloth", "DarkBrown": "pants", "Beige": "accent", "Gold": "crown",
                "Hair_White": "hair", "Skin": "skin", "Eye": "eye"},
}
HEAD_MESH = {"hooded": "Medieval_Head", "monarch": "King_Head", "adventurer": "Adventurer_Head"}
ROLE_PAL = {"armor": "Armor", "cloth": "Cloth", "pants": "Leather", "accent": "Accent", "trim": "Accent", "blade": "Steel",
            "wood": "Wood", "skin": "Skin", "hair": "Hair", "eye": "Eye", "hood": "Cloth", "crown": "Gold", "boot": "Boot"}

def import_base(h):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # Keep the file's own rest transforms (no bind-pose guessing), so the exported skeleton matches
    # the base node-for-node and the base clips play on it unchanged.
    bpy.ops.import_scene.gltf(filepath=os.path.join(MODELS, BASE_FILES[h["base"]]), guess_original_bind_pose=False)
    arm = next(o for o in bpy.context.scene.objects if o.type == "ARMATURE")
    for o in list(bpy.context.scene.objects):
        if o.type == "MESH" and (o.name.startswith(("Sword", "Icosphere", "Axe", "Shield", "Staff", "Bow", "Crown", "Backpack")) or len(o.data.vertices) < 60):
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

def head_frame(head_mesh):
    """H(x, y, z): a point in the head mesh's own box, -1..1 per axis (x right, y BACK, z up; the
    face looks toward -y), measured after hidden faces are removed. Head kit is placed with this:
    a fixed head size put the helm and circlet inside the (large, chibi) Quaternius heads."""
    ws = [head_mesh.matrix_world @ v.co for v in head_mesh.data.vertices]
    lo = Vector([min(w[i] for w in ws) for i in range(3)]); hi = Vector([max(w[i] for w in ws) for i in range(3)])
    c, half = (lo + hi) / 2, (hi - lo) / 2
    return (lambda x, y, z: c + Vector((x * half.x, y * half.y, z * half.z))), half

def dome(name, loc, size, cut=0.0):
    """The upper part of an ellipsoid (a helm cap): vertices below loc.z + cut*size.z are removed."""
    import bmesh
    o = F.ellipsoid(name, loc, size, seg=20, rings=12)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < cut * size[2]], context="VERTS")
    bm.to_mesh(o.data); bm.free()
    return o

def kit(h, arm, P, head_mesh):
    out = []
    head = bone_world(arm, "Head")
    H, half = head_frame(head_mesh)
    hh = 0.12  # a small unit for props on the head (horns)
    hand_r = bone_world(arm, "Wrist.R"); hand_l = bone_world(arm, "Wrist.L")
    for k in h["kit"]:
        if k == "long_hair":
            # A fall of tapered strands from the crown down the back.
            for i in range(9):
                t = -1 + i / 4.0
                pts = [H(t * 0.8, 0.35, 0.8), H(t * 1.0, 1.15, 0.0), H(t * 0.9, 1.25, -1.6)]
                out.append(rigid(F.strand(f"Hair{i}", pts, half.x * 0.28, half.x * 0.08), arm, "Head", P["Hair"]))
        elif k == "flame_hair":
            # Hair as rising flames (Vaelith): strands swept up and back from the crown.
            for i in range(7):
                t = -1 + i / 3.0
                pts = [H(t * 0.7, 0.1, 0.6), H(t * 0.9, 0.5, 1.4), H(t * 0.5, 1.0, 2.1 - abs(t) * 0.5)]
                out.append(rigid(F.strand(f"Flame{i}", pts, half.x * 0.32, half.x * 0.03), arm, "Head", P.get("Glow", P["Hair"]) if i % 2 else P["Hair"]))
        elif k == "beard":
            for i, t in enumerate((-0.45, 0.0, 0.45)):
                pts = [H(t, -0.85, -0.55), H(t * 0.8, -1.05, -1.2), H(t * 0.4, -1.0, -1.9 + abs(t))]
                out.append(rigid(F.strand(f"Beard{i}", pts, half.x * 0.3, half.x * 0.06), arm, "Head", P["Hair"]))
        elif k in ("circlet", "crown"):
            ring = H(0, 0, 0.55)
            out.append(rigid(F.torus("Circlet", ring, half.x * 1.06, half.x * 0.08, sy=half.y / half.x * 0.85), arm, "Head", P["Gold"]))
            gem = F.ellipsoid("Gem", H(0, -0.92, 0.6), (half.x * 0.14, half.x * 0.08, half.x * 0.14))
            out.append(rigid(gem, arm, "Head", P.get("Glow", P["Accent"])))
            if k == "crown":
                for i in range(7):
                    a = math.radians(-90 + (i - 3) * 30)
                    base_p = ring + Vector((math.cos(a) * half.x * 1.06, math.sin(a) * half.y * 0.9, 0))
                    out.append(rigid(F.strand(f"Spike{i}", [base_p, base_p + Vector((0, 0, half.z * (0.55 if i % 2 else 0.35)))], half.x * 0.12, half.x * 0.01), arm, "Head", P["Gold"]))
        elif k == "spiky_hair":
            # A crest of short spikes swept back over the base hair (Kaito).
            for i in range(7):
                t = -1 + i / 3.0
                b0 = H(t * 0.6, 0.0 + abs(t) * 0.2, 0.75)
                out.append(rigid(F.strand(f"Spike{i}", [b0, b0 + Vector((t * half.x * 0.3, half.y * 0.7, half.z * 0.55))], half.x * 0.22, half.x * 0.02), arm, "Head", P["Hair"]))
        elif k == "bandana":
            # A headband round the brow with two tails at the back.
            out.append(rigid(F.torus("Bandana", H(0, 0.0, 0.3), half.x * 1.08, half.x * 0.1, sy=half.y / half.x * 1.0), arm, "Head", P["Accent"]))
            for i, sx in enumerate((-1, 1)):
                pts = [H(sx * 0.15, 1.0, 0.3), H(sx * 0.45, 1.5, 0.0), H(sx * 0.6, 1.8, -0.5)]
                out.append(rigid(F.strand(f"BandanaTail{i}", pts, half.x * 0.1, half.x * 0.05), arm, "Head", P["Accent"]))
        elif k == "mask":
            # A cloth mask over the mouth and nose (Nyx); the glowing eyes stay visible.
            out.append(rigid(dome("Mask", H(0, -0.6, -0.55), (half.x * 1.02, half.y * 0.5, half.z * 0.33), cut=-1.0), arm, "Head", P["Cloth"]))
        elif k == "braid":
            # A thick braid down the back: a chain of beads, tied with an accent band.
            for i in range(7):
                out.append(rigid(F.ellipsoid(f"Braid{i}", H(0, 1.05 + i * 0.05, 0.1 - i * 0.32), (half.x * 0.22, half.x * 0.2, half.z * 0.2), seg=10, rings=6), arm, "Head", P["Hair"]))
            out.append(rigid(F.torus("BraidTie", H(0, 1.4, -2.1), half.x * 0.18, half.x * 0.05), arm, "Head", P["Accent"]))
        elif k == "veil":
            # A long veil from a band on the brow, falling down the back (Sable).
            out.append(rigid(F.torus("VeilBand", H(0, 0.05, 0.5), half.x * 1.06, half.x * 0.06, sy=half.y / half.x * 0.9), arm, "Head", P["Accent"]))
            for i in range(9):
                t = -1 + i / 4.0
                pts = [H(t * 0.95, 0.2, 0.85), H(t * 1.2, 1.2, 0.2), H(t * 1.35, 1.4, -2.2)]
                out.append(rigid(F.strand(f"Veil{i}", pts, half.x * 0.22, half.x * 0.16), arm, "Head", P["Cape"]))
        elif k == "ponytail":
            # A high ponytail: a tie at the back of the crown and a tapering tail swinging down.
            out.append(rigid(F.torus("PonyTie", H(0, 0.95, 0.55), half.x * 0.16, half.x * 0.06, rot=(math.radians(70), 0, 0)), arm, "Head", P["Accent"]))
            pts = [H(0, 1.0, 0.6), H(0, 1.45, 0.4), H(0.05, 1.6, -0.4), H(0.1, 1.45, -1.3)]
            out.append(rigid(F.strand("Ponytail", pts, half.x * 0.3, half.x * 0.06), arm, "Head", P["Hair"]))
        elif k == "halo":
            out.append(rigid(F.torus("Halo", H(0, 0.2, 1.45), half.x * 0.95, half.x * 0.07), arm, "Head", P.get("Glow", P["Gold"])))
        elif k == "helm":
            # A steel cap over the top of the head, down to the brow, with a nose guard.
            out.append(rigid(dome("Helm", H(0, 0.1, 0.32), (half.x * 1.12, half.y * 1.0, half.z * 0.95), cut=0.0), arm, "Head", P["Steel"]))
            out.append(rigid(F.torus("HelmRim", H(0, 0.1, 0.34), half.x * 1.12, half.x * 0.07, sy=half.y / half.x * 1.0 / 1.12), arm, "Head", P["Accent"]))
            out.append(rigid(F.box("Nasal", H(0, -1.0, 0.15), (half.x * 0.09, half.x * 0.05, half.z * 0.25)), arm, "Head", P["Steel"]))
        elif k == "horns":
            for s in (-1, 1):
                pts = [head + Vector((s * hh * 0.45, 0, hh * 0.75)), head + Vector((s * hh * 1.0, 0, hh * 1.05)), head + Vector((s * hh * 1.15, -hh * 0.1, hh * 1.6))]
                out.append(rigid(F.strand(f"Horn{s}", pts, hh * 0.16, hh * 0.02), arm, "Head", P["White"]))
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
        elif k == "orb":
            # A glowing orb held in the left hand, in a little gold cage.
            c = bp(arm, "Wrist.L", 0, 0.12, 0)
            out.append(rigid(F.ellipsoid("HandOrb", c, (0.09, 0.09, 0.09)), arm, "Wrist.L", P.get("Glow", P["Accent"])))
            out.append(rigid(F.torus("OrbCage", c, 0.1, 0.008, rot=(math.radians(90), 0, 0)), arm, "Wrist.L", P["Gold"]))
        elif k == "sword":
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("SwordGrip", [W(0, 0.08, 0.14), W(0, 0.08, -0.06)], 0.022), arm, "Wrist.R", P["Leather"]))
            out.append(rigid(F.strand("Guard", [W(-0.11, 0.08, -0.08), W(0.11, 0.08, -0.08)], 0.02, 0.02), arm, "Wrist.R", P["Gold"]))
            out.append(rigid(F.strand("Blade", [W(0, 0.08, -0.09), W(0, 0.08, -0.55), W(0, 0.08, -0.95)], 0.035, 0.004), arm, "Wrist.R", P.get("Glow", P["Steel"]) if h.get("glow") else P["Steel"]))
        elif k == "katana":
            # A long, slightly curved single-edged blade with a round guard (Kaito).
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("KatanaGrip", [W(0, 0.08, 0.2), W(0, 0.08, -0.06)], 0.02, 0.02), arm, "Wrist.R", P["Cloth"]))
            out.append(rigid(F.torus("Tsuba", W(0, 0.08, -0.08), 0.05, 0.012, rot=(0, 0, 0)), arm, "Wrist.R", P["Gold"]))
            pts = [W(0, 0.08, -0.09), W(0.02, 0.08, -0.5), W(0.07, 0.08, -0.9), W(0.13, 0.08, -1.1)]
            out.append(rigid(F.strand("KatanaBlade", pts, 0.022, 0.004), arm, "Wrist.R", P["Steel"]))
            if "Glow" in P:
                out.append(rigid(F.strand("KatanaEdge", [W(0.01, 0.08, -0.15), W(0.035, 0.08, -0.55), W(0.08, 0.08, -0.92)], 0.008, 0.002), arm, "Wrist.R", P["Glow"]))
        elif k == "daggers":
            # A curved dagger in each hand (Nyx), with a glowing edge when the hero has a glow colour.
            for side in ("R", "L"):
                W = lambda x, y, z, b="Wrist." + side: bp(arm, b, x, y, z)
                out.append(rigid(F.strand(f"DaggerGrip{side}", [W(0, 0.08, 0.1), W(0, 0.08, -0.04)], 0.018, 0.018), arm, "Wrist." + side, P["Leather"]))
                out.append(rigid(F.strand(f"DaggerGuard{side}", [W(-0.06, 0.08, -0.05), W(0.06, 0.08, -0.05)], 0.014, 0.014), arm, "Wrist." + side, P["Accent"]))
                out.append(rigid(F.strand(f"DaggerBlade{side}", [W(0, 0.08, -0.06), W(0.03, 0.08, -0.25), W(0.08, 0.08, -0.38)], 0.026, 0.003), arm, "Wrist." + side, P.get("Glow", P["Steel"])))
        elif k == "bow":
            # A longbow in the left hand, limbs curving away from the archer, with a straight string.
            W = lambda x, y, z: bp(arm, "Wrist.L", x, y, z)
            limb = [W(0.05, 0.08, 0.7), W(-0.12, 0.08, 0.35), W(-0.15, 0.08, 0.0), W(-0.12, 0.08, -0.35), W(0.05, 0.08, -0.7)]
            out.append(rigid(F.strand("Bow", limb, 0.022, 0.022), arm, "Wrist.L", P["Wood"]))
            out.append(rigid(F.strand("BowString", [W(0.05, 0.08, 0.7), W(0.05, 0.08, -0.7)], 0.004, 0.004), arm, "Wrist.L", P["White"]))
            out.append(rigid(F.strand("BowGrip", [W(-0.15, 0.08, 0.08), W(-0.15, 0.08, -0.08)], 0.03, 0.03), arm, "Wrist.L", P["Accent"]))
        elif k == "quiver":
            # A quiver slung across the back, arrows showing over the right shoulder.
            chest = bone_world(arm, "Chest")
            a, b = chest + Vector((-0.15, 0.24, -0.25)), chest + Vector((0.15, 0.24, 0.25))
            out.append(rigid(F.strand("Quiver", [a, b], 0.065, 0.065), arm, "Chest", P["Leather"]))
            for i in range(3):
                tip = b + Vector((0.04 * (i - 1) + 0.06, 0.01 * i, 0.16))
                out.append(rigid(F.strand(f"Arrow{i}", [b, tip], 0.006, 0.006), arm, "Chest", P["Wood"]))
                out.append(rigid(F.ellipsoid(f"Fletch{i}", tip, (0.02, 0.008, 0.035), seg=8, rings=4), arm, "Chest", P["White"]))
        elif k == "mirror":
            # A round hand mirror in the left hand (Sable): a silver face in a gold rim.
            c = bp(arm, "Wrist.L", 0, 0.17, 0)
            out.append(rigid(F.strand("MirrorHandle", [bp(arm, "Wrist.L", 0, 0.04, 0), bp(arm, "Wrist.L", 0, 0.1, 0)], 0.016, 0.016), arm, "Wrist.L", P["Gold"]))
            out.append(rigid(F.ellipsoid("MirrorFace", c, (0.1, 0.1, 0.018)), arm, "Wrist.L", P.get("Glow", P["Steel"])))
            out.append(rigid(F.torus("MirrorRim", c, 0.1, 0.014), arm, "Wrist.L", P["Gold"]))
        elif k == "spear":
            # A long spear through the right fist, leaf blade up, with a cloth tassel under the head.
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("SpearShaft", [W(0, 0.08, 0.9), W(0, 0.08, -1.15)], 0.022, 0.022), arm, "Wrist.R", P["Wood"]))
            out.append(rigid(F.strand("SpearHead", [W(0, 0.08, -1.12), W(0, 0.08, -1.25), W(0, 0.08, -1.42)], 0.045, 0.004), arm, "Wrist.R", P["Steel"]))
            out.append(rigid(F.ellipsoid("Tassel", W(0.02, 0.08, -1.08), (0.035, 0.035, 0.06), seg=8, rings=4), arm, "Wrist.R", P["Accent"]))
        elif k == "club":
            # A knobbly wooden club (Odo): a thick tapering log with a few studs.
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("Club", [W(0, 0.08, 0.15), W(0, 0.08, -0.35), W(0, 0.08, -0.75)], 0.035, 0.1), arm, "Wrist.R", P["Wood"]))
            for i, (dx, dz) in enumerate(((0.09, -0.55), (-0.09, -0.65), (0.0, -0.78), (0.07, -0.72))):
                out.append(rigid(F.ellipsoid(f"Stud{i}", W(dx, 0.08 + (0.05 if i % 2 else -0.05), dz), (0.025, 0.025, 0.025), seg=8, rings=4), arm, "Wrist.R", P["Steel"]))
        elif k == "candle":
            # A big candle in a brass holder in the left hand, its flame glowing (Pell).
            W = lambda x, y, z: bp(arm, "Wrist.L", x, y, z)
            out.append(rigid(F.torus("CandleDish", W(0, 0.1, 0.0), 0.06, 0.015, rot=(math.radians(90), 0, 0)), arm, "Wrist.L", P["Gold"]))
            out.append(rigid(F.strand("Candle", [W(0, 0.1, 0.0), W(0, 0.1, -0.22)], 0.035, 0.032), arm, "Wrist.L", P["White"]))
            out.append(rigid(F.ellipsoid("Flame", W(0, 0.1, -0.28), (0.03, 0.03, 0.06), seg=10, rings=6), arm, "Wrist.L", P.get("Glow", P["Accent"])))
        elif k == "cape":
            # A cape hanging from the shoulders down the back, flaring toward the hem (rigid on the chest).
            chest = bone_world(arm, "Chest"); top = chest.z + 0.2; back = chest.y + 0.2
            verts, faces, cols, rows = [], [], 7, 6
            for j in range(rows):
                t = j / (rows - 1); z = top - t * (top - 0.42); w = 0.22 + 0.16 * t
                for i in range(cols):
                    u = -1 + 2 * i / (cols - 1)
                    verts.append((chest.x + u * w, back + 0.05 * t + 0.04 * (1 - u * u), z))
            for j in range(rows - 1):
                for i in range(cols - 1):
                    a = j * cols + i; faces.append((a, a + 1, a + 1 + cols, a + cols))
            cape = F.mesh_obj("Cape", verts, faces)
            sol = cape.modifiers.new("Solid", "SOLIDIFY"); sol.thickness = 0.02; F.apply_mods(cape)
            out.append(rigid(cape, arm, "Chest", P["Cape"]))
        elif k == "robe":
            # A flared skirt from the waist (rigid on the hips; the legs stay inside it at rest).
            hips = bone_world(arm, "Hips")
            prof = [(0.32, 0.42), (0.25, 0.7), (0.165, hips.z + 0.06)]
            robe = F.lathe("Robe", prof, seg=20, sy=0.85, cx=hips.x, cy=hips.y - 0.02, cap_top=False, cap_bottom=False)
            sol = robe.modifiers.new("Solid", "SOLIDIFY"); sol.thickness = 0.015; F.apply_mods(robe)
            out.append(rigid(robe, arm, "Hips", P["Cloth"]))
            out.append(rigid(F.torus("Sash", Vector((hips.x, hips.y - 0.02, hips.z + 0.05)), 0.165, 0.018, sy=0.85), arm, "Hips", P["Accent"]))
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
    head_mesh = next(o for o in meshes if o.name.split(".")[0] == HEAD_MESH[h["base"]])
    pieces = kit(h, arm, P, head_mesh)
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
