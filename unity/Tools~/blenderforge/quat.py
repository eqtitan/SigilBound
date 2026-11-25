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
    # Step 18.
    "wren":   dict(base="hooded", skin="fair", hair="#e8e2d0", armor="#7a5a3e", accent="#ffc84a", cloth="#5c4330", glow="#ffc84a",
                   hide=[], kit=["lantern", "robe"], rim="#ffe27a", height=0.94),
    "kettle": dict(base="adventurer", skin="ash", hair="#2a2a30", armor="#5a5560", accent="#9a5ae8", cloth="#2a2a30", glow=None,
                   hide=[], kit=["long_hair", "hook"], rim="#a050ff", height=1.02),
    "maelis": dict(base="monarch", skin="brown", hair="#163a5a", armor="#1a4a6a", accent="#4ad0ff", cloth="#123048", glow="#4ad0ff", cape="#123048",
                   hide=[], kit=["long_hair", "trident", "cape"], rim="#3a8ee8", height=1.04),
    "seraphine": dict(base="hooded", skin="pale", hair="#fff4d8", armor="#f0e8d8", accent="#ffd870", cloth="#f4eee0", glow="#fff0a0",
                   hide=["hood"], kit=["long_hair", "halo", "bell", "robe"], rim="#ffe27a", height=1.02),
    # Roster phase 1, Roadwardens (docs/sigilbound/ROSTER.md). Step 21:
    "bram":   dict(base="monarch", skin="tan", hair="#6a5a4a", armor="#2f5a6e", accent="#c8b070", cloth="#3a4a52", glow=None,
                   hide=["crown"], kit=["beard", "anchor"], rim="#3a8ee8", height=1.05),
    "nettle": dict(base="adventurer", skin="fair", hair="#8a5a2a", armor="#6a8a3a", accent="#e8c84a", cloth="#4a5a2a", glow=None,
                   hide=[], kit=["bandana", "daggers"], rim="#3fb57a", height=0.92),
    "hobb":   dict(base="monarch", skin="brown", hair="#3a2a1a", armor="#8a8070", accent="#ffd870", cloth="#5a4a3a", glow="#ffd870",
                   hide=["crown", "hair"], kit=["helm", "shield", "lantern"], rim="#ffe27a", height=1.0),
    "greta":  dict(base="hooded", skin="fair", hair="#c8b8a0", armor="#4a7a8a", accent="#e8d8b0", cloth="#2a4a5a", glow=None,
                   hide=["hood"], kit=["braid", "net"], rim="#3a8ee8", height=0.96),
    # Step 22:
    "cole":   dict(base="adventurer", skin="fair", hair="#2a2420", armor="#5a4a3a", accent="#ff8a3a", cloth="#3a2a22", glow=None,
                   hide=[], kit=["spiky_hair", "axe"], rim="#ff7a3a", height=1.0),
    "tilly":  dict(base="hooded", skin="pale", hair="#5a4a6a", armor="#3a3048", accent="#c8a8ff", cloth="#2a2238", glow="#e0c8ff",
                   hide=["hood"], kit=["veil", "candle"], rim="#a050ff", height=0.92),
    "ronan":  dict(base="monarch", skin="tan", hair="#8a5a2a", armor="#6a5a3a", accent="#c8d870", cloth="#4a6a3a", glow=None,
                   hide=["crown"], kit=["beard", "hammer"], rim="#3fb57a", height=1.04),
    "isla":   dict(base="hooded", skin="fair", hair="#e8d890", armor="#e8e0c8", accent="#ffd870", cloth="#c8c0a8", glow="#fff0a0",
                   hide=["hood"], kit=["long_hair", "bell"], rim="#ffe27a", height=0.97),
    # Step 23:
    "dunstan": dict(base="monarch", skin="tan", hair="#8a8a8a", armor="#6a7a5a", accent="#d8c890", cloth="#3a5a3a", glow=None, cape="#2a4a2a",
                   hide=["crown", "hair"], kit=["beard", "helm", "shield", "cape"], rim="#3fb57a", height=1.04),
    "marisol": dict(base="adventurer", skin="brown", hair="#1a1a2a", armor="#2a6a8a", accent="#7ae0ff", cloth="#1a4a5a", glow="#7ae0ff",
                   hide=[], kit=["ponytail", "trident"], rim="#3a8ee8", height=0.98),
    "vesper": dict(base="hooded", skin="pale", hair="#2a2030", armor="#2a2238", accent="#7ad07a", cloth="#1a1622", glow="#9aff9a",
                   hide=[], kit=["mask", "daggers"], rim="#a050ff", height=0.96),
    "brega":  dict(base="monarch", skin="deep", hair="#3a5a5a", armor="#4a6a6a", accent="#c8e8d0", cloth="#2a4a4a", glow=None,
                   hide=["crown", "hair"], kit=["helm", "shield"], rim="#3a8ee8", height=1.0),
    # Step 24:
    "fennick": dict(base="adventurer", skin="pale", hair="#d86a2a", armor="#7a3a2a", accent="#ffd040", cloth="#5a2a1a", glow="#ffb040",
                   hide=[], kit=["bandana", "candle"], rim="#ff7a3a", height=0.92),
    "orla":   dict(base="hooded", skin="tan", hair="#5a7a5a", armor="#3a6a6a", accent="#a8e0c8", cloth="#2a5050", glow="#a8e0c8",
                   hide=["hood"], kit=["long_hair", "staff", "robe"], rim="#3a8ee8", height=1.0),
    "silas":  dict(base="hooded", skin="deep", hair="#1a1a1a", armor="#2a2a3a", accent="#8a7aff", cloth="#1a1a28", glow="#a898ff", cape="#1a1a28",
                   hide=["hood"], kit=["veil", "shield"], rim="#a050ff", height=1.02),
    "wynn":   dict(base="adventurer", skin="fair", hair="#e8e0c0", armor="#c8a860", accent="#fff0a0", cloth="#8a6a3a", glow="#fff2b0",
                   hide=[], kit=["spiky_hair", "bow", "quiver"], rim="#ffe27a", height=1.0),
    # Roster phase 2, Glass and Storm. Step 26:
    "kestrel": dict(base="adventurer", skin="tan", hair="#3a2a1a", armor="#5a7a9a", accent="#e0f0ff", cloth="#3a4a6a", glow="#c0e8ff",
                   hide=[], kit=["bandana", "spear"], rim="#3fb57a", height=1.02),
    "sorrel": dict(base="monarch", skin="brown", hair="#2a2a2a", armor="#4a3a4a", accent="#9aff6a", cloth="#3a2a3a", glow=None, cape="#2a1a2a",
                   hide=["crown"], kit=["beard", "club", "cape"], rim="#a050ff", height=1.06),
    "amber":  dict(base="hooded", skin="fair", hair="#e8a040", armor="#c87a30", accent="#ffe0a0", cloth="#7a4a20", glow="#ffb060",
                   hide=["hood"], kit=["braid", "helm", "shield"], rim="#ff7a3a", height=0.98),
    "tobias": dict(base="adventurer", skin="fair", hair="#a07040", armor="#6a8a5a", accent="#ffd080", cloth="#4a6a3a", glow=None,
                   hide=[], kit=["long_hair", "lute"], rim="#3fb57a", height=1.0),
    # Step 27:
    "nerys":  dict(base="hooded", skin="pale", hair="#9ac8e8", armor="#4a7aa0", accent="#e0f4ff", cloth="#2a4a6a", glow="#bfefff",
                   hide=["hood"], kit=["long_hair", "circlet", "mirror", "robe"], rim="#3a8ee8", height=0.98),
    "hale":   dict(base="monarch", skin="tan", hair="#c8a060", armor="#a89070", accent="#ffe08a", cloth="#6a5030", glow="#ffe08a", cape="#8a3a1a",
                   hide=["crown"], kit=["beard", "hammer", "cape", "runes"], rim="#ffe27a", height=1.06),
    "juno":   dict(base="hooded", skin="tan", hair="#e8501a", armor="#8a2a1a", accent="#ffb030", cloth="#5a1a10", glow="#ffb030", cape="#3a0e08",
                   hide=["hood"], kit=["veil", "whip", "robe"], rim="#ff7a3a", height=0.97),
    "pike":   dict(base="adventurer", skin="tan", hair="#4a3a2a", armor="#4a5a6a", accent="#c8d8e0", cloth="#2a3a4a", glow=None,
                   hide=[], kit=["bandana", "hook"], rim="#3a8ee8", height=1.0),
    # Step 28:
    "rhea":   dict(base="monarch", skin="fair", hair="#f2d27a", armor="#d8dce4", accent="#ffd860", cloth="#5a6a8a", glow="#fff2b0", cape="#5a6a8a",
                   hide=["crown", "hair"], kit=["long_hair", "helm", "shield", "cape"], rim="#ffe27a", height=1.0),
    "corwin": dict(base="adventurer", skin="pale", hair="#1a1a24", armor="#2a2238", accent="#c070ff", cloth="#1a1428", glow="#d090ff",
                   hide=[], kit=["spiky_hair", "katana"], rim="#a050ff", height=1.03),
    "astrid": dict(base="adventurer", skin="fair", hair="#e8e8f0", armor="#6a90b0", accent="#c8f0ff", cloth="#3a5a7a", glow="#c8f0ff", cape="#2a4a6a",
                   hide=[], kit=["ponytail", "spear", "cape"], rim="#3fb57a", height=1.04),
    "caspian": dict(base="monarch", skin="brown", hair="#1a3a4a", armor="#1a5a6a", accent="#5ae0e0", cloth="#0e3a48", glow="#5ae0e0", cape="#0e3a48",
                   hide=[], kit=["long_hair", "beard", "trident", "cape"], rim="#3a8ee8", height=1.08),
    # Step 29:
    "ignis":  dict(base="adventurer", skin="deep", hair="#ff6a1a", armor="#6a1a10", accent="#ffc040", cloth="#3a0e08", glow="#ffb030",
                   hide=["hair"], kit=["flame_hair", "staff", "robe"], rim="#ff7a3a", height=1.03),
    "lyra":   dict(base="hooded", skin="pale", hair="#e8f0ff", armor="#c8d8f0", accent="#e0f0ff", cloth="#8aa0c8", glow="#e8f4ff",
                   hide=["hood"], kit=["long_hair", "halo", "mirror", "robe"], rim="#ffe27a", height=1.0),
    "malachar": dict(base="monarch", skin="ash", hair="#2a2030", armor="#2a2434", accent="#a070ff", cloth="#1a1424", glow="#b080ff", cape="#140e1a",
                   hide=["crown", "hair"], kit=["helm", "horns", "shield", "cape"], rim="#a050ff", height=1.1),
    "rowan":  dict(base="monarch", skin="tan", hair="#5a4a2a", armor="#3a5a2a", accent="#c8e080", cloth="#2a4020", glow=None, cape="#1e3418",
                   hide=["crown"], kit=["long_hair", "beard", "shield", "cape"], rim="#3fb57a", height=1.05),
    # Step 30:
    "selene": dict(base="hooded", skin="pale", hair="#c8d0ff", armor="#2a3a6a", accent="#a8c8ff", cloth="#1a2448", glow="#c8d8ff",
                   hide=["hood"], kit=["long_hair", "circlet", "orb", "robe"], rim="#3a8ee8", height=1.02),
    "brann":  dict(base="monarch", skin="tan", hair="#8a2a1a", armor="#5a2a1a", accent="#ff8a30", cloth="#3a1a10", glow="#ff8a30",
                   hide=["crown", "hair"], kit=["beard", "gauntlets", "runes"], rim="#ff7a3a", height=1.08),
    "ysra":   dict(base="hooded", skin="ash", hair="#3a1a3a", armor="#2a1428", accent="#ff6ac8", cloth="#1a0a1a", glow="#ff8ad8", cape="#2a0e24",
                   hide=["hood"], kit=["veil", "daggers"], rim="#a050ff", height=0.98),
    "oswin":  dict(base="adventurer", skin="brown", hair="#f0e0a0", armor="#e8d8a8", accent="#ffd860", cloth="#8a6a2a", glow="#fff2b0",
                   hide=["hair"], kit=["helm", "spear"], rim="#ffe27a", height=1.05),
    # Roster phase 3, The Hollow Unbound. Step 32:
    "grimsby": dict(base="adventurer", skin="ash", hair="#d8d0c0", armor="#4a4450", accent="#e8dcc0", cloth="#2a2630", glow="#b8a8ff",
                   hide=[], kit=["mask", "staff"], rim="#a050ff", height=0.96),
    "marrow": dict(base="monarch", skin="ash", hair="#b8b4ac", armor="#6a6660", accent="#ff6a2a", cloth="#3a3632", glow="#ff7a2a", cape="#2a2622",
                   hide=["crown"], kit=["long_hair", "club", "runes", "cape"], rim="#ff7a3a", height=1.15),
    "ember":  dict(base="hooded", skin="fair", hair="#f0e0c0", armor="#e8d8c0", accent="#ffb050", cloth="#c8b090", glow="#ffb050", cape="#a89070",
                   hide=["hood"], kit=["veil", "candle", "robe"], rim="#ff7a3a", height=0.97),
    "quill":  dict(base="adventurer", skin="fair", hair="#5a3a2a", armor="#3a4a6a", accent="#e8d8a0", cloth="#2a3450", glow=None,
                   hide=[], kit=["circlet", "book"], rim="#ffe27a", height=0.98),
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
# Clean-shaven heroes (Shave = true in Core/Looks.cs): their beard is removed (adventurer heads only).
SHAVE = {"pell", "kettle", "lumen", "kaito", "oriel", "nettle", "cole", "marisol", "fennick", "wynn", "kestrel", "tobias", "astrid", "ignis", "oswin", "grimsby", "quill"}
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

def skin_robe(obj, arm, top_z, hem_z):
    """Let a robe follow the legs (PLAN step 20): each vertex blends from the hips at the waist
    toward the thigh on its side at the hem, so the skirt swings with each stride instead of the
    legs poking through a rigid cone. Left is +x on the Quaternius rigs."""
    xl, xr = bone_world(arm, "UpperLeg.L").x, bone_world(arm, "UpperLeg.R").x
    for n in ("Hips", "UpperLeg.L", "UpperLeg.R"):
        if n not in obj.vertex_groups: obj.vertex_groups.new(name=n)
    g = {n: obj.vertex_groups[n] for n in ("Hips", "UpperLeg.L", "UpperLeg.R")}
    for v in obj.data.vertices:
        w = obj.matrix_world @ v.co
        t = max(0.0, min(1.0, 1.8 * (top_z - w.z) / max(1e-6, top_z - hem_z))) * 0.95  # how much the legs lead (most of it by mid-skirt)
        sl = max(0.0, min(1.0, (w.x - xr) / max(1e-6, xl - xr)))                 # 1 = left leg's side
        sl = sl * sl * (3 - 2 * sl)                                              # smooth across the middle
        g["Hips"].add([v.index], 1.0 - t, "REPLACE")
        g["UpperLeg.L"].add([v.index], t * sl, "REPLACE")
        g["UpperLeg.R"].add([v.index], t * (1 - sl), "REPLACE")
    return obj

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
                # From the sides of the crown, out and up (measured head, so they clear a helm).
                pts = [H(s * 0.8, 0.05, 0.55), H(s * 1.35, 0.0, 0.95), H(s * 1.5, -0.15, 1.6)]
                out.append(rigid(F.strand(f"Horn{s}", pts, half.x * 0.22, half.x * 0.03), arm, "Head", P["White"]))
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
        elif k == "lantern":
            # A lantern hanging from the left hand: a brass cage round a glowing core (Wren).
            W = lambda x, y, z: bp(arm, "Wrist.L", x, y, z)
            out.append(rigid(F.strand("LanternHandle", [W(0, 0.08, 0.0), W(0, 0.08, 0.12)], 0.008, 0.008), arm, "Wrist.L", P["Gold"]))
            c = W(0, 0.08, 0.24)
            out.append(rigid(F.ellipsoid("LanternLight", c, (0.07, 0.07, 0.09), seg=10, rings=6), arm, "Wrist.L", P.get("Glow", P["Accent"])))
            for i, rz in enumerate((0, 90)):
                out.append(rigid(F.torus(f"LanternCage{i}", c, 0.085, 0.009, rot=(math.radians(90), 0, math.radians(rz))), arm, "Wrist.L", P["Gold"]))
            out.append(rigid(F.ellipsoid("LanternCap", W(0, 0.08, 0.14), (0.06, 0.06, 0.025), seg=10, rings=4), arm, "Wrist.L", P["Gold"]))
        elif k == "hook":
            # A big iron hook on a short haft in the right hand (Kettle).
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("HookHaft", [W(0, 0.08, 0.15), W(0, 0.08, -0.35)], 0.024, 0.024), arm, "Wrist.R", P["Wood"]))
            pts = [W(0, 0.08, -0.33), W(0, 0.08, -0.6), W(0.12, 0.08, -0.75), W(0.24, 0.08, -0.62), W(0.22, 0.08, -0.5)]
            out.append(rigid(F.strand("Hook", pts, 0.03, 0.006), arm, "Wrist.R", P["Steel"]))
            out.append(rigid(F.torus("HookBand", W(0, 0.08, -0.34), 0.03, 0.01), arm, "Wrist.R", P["Accent"]))
        elif k == "trident":
            # A trident through the right fist, three glowing prongs up (Maelis).
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("TridentShaft", [W(0, 0.08, 0.8), W(0, 0.08, -1.0)], 0.022, 0.022), arm, "Wrist.R", P["Gold"]))
            out.append(rigid(F.strand("TridentBar", [W(-0.12, 0.08, -1.0), W(0.12, 0.08, -1.0)], 0.02, 0.02), arm, "Wrist.R", P["Gold"]))
            for i, dx in enumerate((-0.12, 0.0, 0.12)):
                tip = -1.32 if dx == 0 else -1.22
                out.append(rigid(F.strand(f"Prong{i}", [W(dx, 0.08, -1.0), W(dx, 0.08, tip)], 0.018, 0.003), arm, "Wrist.R", P.get("Glow", P["Steel"])))
        elif k == "bell":
            # A golden hand bell in the left hand (Seraphine).
            W = lambda x, y, z: bp(arm, "Wrist.L", x, y, z)
            out.append(rigid(F.strand("BellHandle", [W(0, 0.08, 0.05), W(0, 0.08, -0.12)], 0.016, 0.016), arm, "Wrist.L", P["Wood"]))
            c = W(0, 0.08, -0.12)
            bell = F.lathe("Bell", [(0.09, -0.12), (0.075, -0.08), (0.055, -0.03), (0.03, 0.0)], seg=16, cap_bottom=False)
            # lathe builds around the world Z axis at the origin: move it to the hand, mouth down.
            bell.location = c
            out.append(rigid(bell, arm, "Wrist.L", P["Gold"]))
            out.append(rigid(F.ellipsoid("Clapper", c + Vector((0, 0, -0.11)), (0.022, 0.022, 0.022), seg=8, rings=4), arm, "Wrist.L", P.get("Glow", P["Accent"])))
        elif k == "anchor":
            # A ship's anchor carried as a weapon (Bram): iron shank through the fist, ring at the
            # bottom, stock bar, and two curved flukes at the top end.
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("AnchorShank", [W(0, 0.08, 0.3), W(0, 0.08, -0.72)], 0.03, 0.03), arm, "Wrist.R", P["Steel"]))
            out.append(rigid(F.torus("AnchorRing", W(0, 0.08, 0.36), 0.06, 0.014, rot=(0, math.radians(90), 0)), arm, "Wrist.R", P["Steel"]))
            out.append(rigid(F.strand("AnchorStock", [W(-0.16, 0.08, 0.16), W(0.16, 0.08, 0.16)], 0.022, 0.022), arm, "Wrist.R", P["Wood"]))
            flukes = [W(-0.26, 0.08, -0.5), W(-0.22, 0.08, -0.7), W(0, 0.08, -0.78), W(0.22, 0.08, -0.7), W(0.26, 0.08, -0.5)]
            out.append(rigid(F.strand("AnchorArms", flukes, 0.03, 0.03), arm, "Wrist.R", P["Steel"]))
            for i, sx in enumerate((-1, 1)):
                out.append(rigid(F.ellipsoid(f"Fluke{i}", W(sx * 0.27, 0.08, -0.47), (0.05, 0.02, 0.07), seg=8, rings=4), arm, "Wrist.R", P["Steel"]))
        elif k == "net":
            # A fishing net hanging from the left hand (Greta): a fan of cords with cross cords and
            # cork floats along the bottom edge.
            W = lambda x, y, z: bp(arm, "Wrist.L", x, y, z)
            for i in range(5):
                u = -1 + i / 2.0
                out.append(rigid(F.strand(f"NetCord{i}", [W(u * 0.04, 0.1, 0), W(u * 0.14, 0.35, 0.02), W(u * 0.24, 0.62, 0.04)], 0.006, 0.006), arm, "Wrist.L", P["Accent"]))
            for j, y in enumerate((0.25, 0.42, 0.6)):
                w = 0.04 + (y - 0.1) * 0.38
                out.append(rigid(F.strand(f"NetCross{j}", [W(-w, y, 0.02), W(0, y + 0.02, 0.03), W(w, y, 0.02)], 0.005, 0.005), arm, "Wrist.L", P["Accent"]))
            for i in range(4):
                out.append(rigid(F.ellipsoid(f"Float{i}", W(-0.18 + i * 0.12, 0.63, 0.04), (0.025, 0.02, 0.025), seg=8, rings=4), arm, "Wrist.L", P["Wood"]))
        elif k == "axe":
            # A woodsman's axe (Cole): a long haft through the fist and a flared bit on one side.
            W = lambda x, y, z: bp(arm, "Wrist.R", x, y, z)
            out.append(rigid(F.strand("AxeHaft", [W(0, 0.08, 0.25), W(0, 0.08, -0.8)], 0.024, 0.02), arm, "Wrist.R", P["Wood"]))
            head = F.lathe("AxeBit", [(0.02, -0.07), (0.11, -0.05), (0.14, 0.0), (0.11, 0.05), (0.02, 0.07)], seg=3, sx=1.0, sy=0.18, cap_top=False, cap_bottom=False)
            head.location = W(0.08, 0.08, -0.72)
            out.append(rigid(head, arm, "Wrist.R", P["Steel"]))
            out.append(rigid(F.box("AxePoll", W(-0.03, 0.08, -0.72), (0.04, 0.03, 0.06)), arm, "Wrist.R", P["Steel"]))
        elif k == "lute":
            # A lute held in the left hand (Tobias): a pear-shaped body, a long neck and a bent-back pegbox.
            W = lambda x, y, z: bp(arm, "Wrist.L", x, y, z)
            out.append(rigid(F.ellipsoid("LuteBody", W(0, 0.12, 0.05), (0.13, 0.17, 0.05), seg=16, rings=10), arm, "Wrist.L", P["Wood"]))
            out.append(rigid(F.ellipsoid("LuteRose", W(0, 0.12, -0.002), (0.035, 0.035, 0.006), seg=12, rings=4), arm, "Wrist.L", P["Leather"]))
            out.append(rigid(F.strand("LuteNeck", [W(0, 0.26, 0.04), W(0, 0.52, 0.04)], 0.018, 0.016), arm, "Wrist.L", P["Leather"]))
            out.append(rigid(F.box("LutePegbox", W(0, 0.57, 0.07), (0.022, 0.05, 0.012), rot=(math.radians(35), 0, 0)), arm, "Wrist.L", P["Wood"]))
            for i, dx in enumerate((-0.012, 0.0, 0.012)):
                out.append(rigid(F.strand(f"LuteString{i}", [W(dx, 0.05, 0.0), W(dx, 0.53, 0.025)], 0.002, 0.002), arm, "Wrist.L", P["White"]))
        elif k == "gauntlets":
            # Oversized armoured fists for a pit fighter (Brann): a fist block and a bracer on each hand.
            for side in ("R", "L"):
                W = lambda x, y, z, b="Wrist." + side: bp(arm, b, x, y, z)
                out.append(rigid(F.ellipsoid(f"Fist{side}", W(0, 0.09, 0), (0.085, 0.09, 0.075), seg=12, rings=8), arm, "Wrist." + side, P["Steel"]))
                out.append(rigid(F.torus(f"Bracer{side}", W(0, -0.02, 0), 0.07, 0.022, rot=(math.radians(90), 0, 0)), arm, "Wrist." + side, P["Accent"]))
                for i, dx in enumerate((-0.04, 0.0, 0.04)):
                    out.append(rigid(F.ellipsoid(f"Knuckle{side}{i}", W(dx, 0.16, -0.05), (0.018, 0.018, 0.018), seg=8, rings=4), arm, "Wrist." + side, P.get("Glow", P["Accent"])))
        elif k == "book":
            # A thick tome in the left hand (Quill): a cloth cover with the pages showing.
            W = lambda x, y, z: bp(arm, "Wrist.L", x, y, z)
            # F.box takes full sizes: a 22 x 30 cm tome, 5 cm thick, pages showing on the open edge.
            out.append(rigid(F.box("BookCover", W(0, 0.15, 0.03), (0.22, 0.3, 0.05)), arm, "Wrist.L", P["Cloth"]))
            out.append(rigid(F.box("BookPages", W(0.012, 0.15, 0.03), (0.2, 0.28, 0.056)), arm, "Wrist.L", P["White"]))
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
            out.append(skin_robe(rigid(robe, arm, "Hips", P["Cloth"]), arm, hips.z + 0.06, 0.42))
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
        if is_head and a.hero in SHAVE:
            # Clean-shaven (Core/Looks.cs Shave): the adventurer head's beard shares the "hair"
            # material, so remove hair faces on the lower front of the head (the face looks to -y).
            hair = {i for i, r in enumerate(role) if r == "hair"}
            mw = o.matrix_world; ws = [mw @ v.co for v in o.data.vertices]
            cz = (min(w.z for w in ws) + max(w.z for w in ws)) / 2; cy = (min(w.y for w in ws) + max(w.y for w in ws)) / 2
            bm = bmesh.new(); bm.from_mesh(o.data)
            beard = [f for f in bm.faces if f.material_index in hair and (mw @ f.calc_center_median()).z < cz and (mw @ f.calc_center_median()).y < cy]
            bmesh.ops.delete(bm, geom=beard, context="FACES")
            bm.to_mesh(o.data); bm.free()
            print(f"[quat] {a.hero}: shaved ({len(beard)} beard faces)")
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
