# Story-scenery MOCK-UPS (docs/sigilbound/art/STORY_SCENES.md): battle-camera renders of each
# chapter's lair (stage 6) and two full six-stop route strips, built from the prop kit plus the
# landmark set (make_landmarks.py). Nothing here is used by the game.
#   python story_review.py OUT_DIR [shot-name ...]
import math, os, sys, copy
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import review as R, make_props as mp, make_landmarks as L, paint as P

T = R.THEMES
def theme(name, **kw):
    t = dict(kw); t.setdefault("props", ["rock0", "rock1", "tuft0"]); return t

EXTRA = {
    "green marsh": theme("", top="#1a2e2a", bot="#7fa088", grass="#3a5630", grass2="#4f6236", worn="#6e5a40", hill="#34502e", rock="#58605a", bark="#433a30",
                         leaves=["#4a5a30"], tuft="#7a8a44", water="#22402f", cloth="#5a4a30", sil="#223c36", mote="#d8ff7a", style="hills",
                         props=["reeds0", "reeds1", "oak1", "rock2", "bush0", "tuft1"]),
    "blossom forest": theme("", top="#2a2438", bot="#e0b0d0", grass="#557636", grass2="#748646", worn="#977660", hill="#4a6a34", rock="#8a8088", bark="#4a3036",
                            leaves=["#e89ac0", "#f0b0cc", "#d880aa"], tuft="#6a8a3c", cloth="#5a3a7a", sil="#463858", mote="#ffc0dc", style="forest",
                            props=["oak0", "oak2", "bush1", "rock0", "tuft2"]),
    "moonlit shore": theme("", top="#101a2e", bot="#5a7ab0", grass="#a39c8c", grass2="#958e80", worn="#77705f", hill="#8a8474", rock="#5c6272", bark="#8a8478",
                           leaves=["#3a5a4a"], tuft="#6a7a5a", cloth="#c8d0e0", sil="#1a2642", mote="#cfe2ff", style="peaks",
                           props=["rock3", "rock0", "stone0", "tuft0", "pine0"]),
    "amethyst badlands": theme("", top="#0e0a16", bot="#3a2a5a", grass="#5e4e6c", grass2="#6f5c80", worn="#9584a6", hill="#54466a", rock="#5e4c70", bark="#4a3c4e",
                               strata=["#56466c", "#6f5c86", "#463a5a", "#88729e"], tuft="#6a5a7a", cloth="#3a2a5a", sil="#221a36", mote="#c890ff", style="mesas",
                               props=["mesa0", "crystal0", "rock1", "crystal1", "tuft1"]),
    "burning ruins": theme("", top="#1a0a0a", bot="#ff6a2a", grass="#665834", grass2="#7a6238", worn="#8a7a6a", hill="#5a4a30", rock="#7a6e64", bark="#4a3a2e",
                           leaves=["#5a5a2a", "#7a5a26"], tuft="#7a6a3a", cloth="#6a1a1a", sil="#381410", mote="#ff8a3a", style="ruins", flag=True,
                           props=["column1", "wall0", "column2", "dead0", "rock0", "brazier"]),
    "saltreach harbor": theme("", top="#1a2436", bot="#c89060", grass="#8a8070", grass2="#9a907c", worn="#6e6658", hill="#7a7262", rock="#5a5c62", bark="#5a4632",
                              tuft="#7a7a5a", water="#163a4a", cloth="#c8b890", sil="#1e2c3a", mote="#e0f0ff", style="sea",
                              props=["post0", "crate", "lantern", "post1", "rock0"]),
    "black fens": theme("", top="#080e0a", bot="#2a3a28", grass="#1e2a20", grass2="#26322a", worn="#3a3a30", hill="#1a2620", rock="#3a4038", bark="#2a2620",
                        tuft="#3a4a30", water="#0a1410", cloth="#2a3020", sil="#0e1812", mote="#b0ff6a", style="forest",
                        props=["dead0", "dead1", "reeds0", "mushrooms0", "reeds1"]),
    "wraithmoor barrows": theme("", top="#1a1c26", bot="#8a8e9a", grass="#56584a", grass2="#626452", worn="#7a7868", hill="#4a4c40", rock="#6a6c70", bark="#3a3632",
                                tuft="#6a6a52", cloth="#3a3a4a", sil="#202230", mote="#d8e0f0", style="hills",
                                props=["stone0", "stone1", "dead1", "mound", "rock1", "tuft0"]),
    "ember groves": theme("", top="#140806", bot="#c04a1a", grass="#3a3230", grass2="#4a3c34", worn="#5a4a40", hill="#34302e", rock="#4a4440", bark="#2a2220",
                          leaves=["#ff7a2a", "#ffaa3a", "#e04a1a"], tuft="#5a4a3a", cloth="#6a1a10", sil="#2a0e0a", mote="#ff9a3a", style="forest",
                          props=["oak0", "oak1", "bush0", "rock2"]),
}
ALL = dict(T); ALL.update(EXTRA)
for k in ("storm coast", "titan caldera"):  # the review's boss extras (debris) stay off in story shots
    ALL[k] = dict(ALL[k]); ALL[k]["debris"] = False

EXTRA["glass desert"] = theme("", top="#2a3a4e", bot="#d8c8a8", grass="#b8aa8a", grass2="#c8bc9c", worn="#9a8e76", hill="#a89a7c", rock="#8a8478", bark="#5a4e44",
                              tuft="#9a9070", cloth="#6a8aa0", sil="#3a4458", mote="#c8f4ff", style="dunes", props=["dead0", "crystal0", "crystal1", "rock1", "tuft2"])
EXTRA["stormcrown crag"] = theme("", top="#0c1424", bot="#6a7a92", grass="#5a6260", grass2="#6a706c", worn="#7a7a74", hill="#4a5052", rock="#5a5e66", bark="#3a3430",
                                 leaves=["#2e4a3a", "#3a5240"], tuft="#6a7062", cloth="#4a5a7a", sil="#141c2c", mote="#e8f0ff", style="peaks", storm=True,
                                 props=["pine0", "pine1", "stone0", "rock3", "tuft0"])
ALL.update({k: v for k, v in EXTRA.items() if k in ("glass desert", "stormcrown crag")})
CHAPTER_THEME = ["dusk forest", "teal shore", "red badlands", "green marsh", "gold ruins", "glass desert", "stormcrown crag", "amethyst badlands",
                 "glow marsh", "burning ruins", "saltreach harbor", "black fens", "wraithmoor barrows", "ember groves", "storm coast"]
GLOW = {"fire": "#ffa040", "flame": "#ff7a2a", "shadow": "#b070ff", "tide": "#4ab8ff", "light": "#ffe07a", "gale": "#7affb0",
        "hollow": "#c060ff", "glass": "#a8f0ff", "rose": "#ffb860"}
MOOD = {"Day": (1.0, 0.0), "Late": (0.95, 0.1), "Golden": (0.9, 0.2), "Dusk": (0.85, 0.35), "Night": (0.65, 0.5)}


def _calls(src, fn):
    """Yields the argument text of each fn(...) call, with balanced parentheses."""
    i = 0
    while True:
        i = src.find(fn + "(", i)
        if i < 0: return
        j = i + len(fn) + 1; depth = 1
        while depth:
            depth += {"(": 1, ")": -1}.get(src[j], 0); j += 1
        yield src[i + len(fn) + 1:j - 1]; i = j


def _split(args):
    out, depth, cur = [], 0, ""
    for ch in args:
        if ch == "," and depth == 0: out.append(cur.strip()); cur = ""; continue
        depth += {"(": 1, ")": -1}.get(ch, 0); cur += ch
    if cur.strip(): out.append(cur.strip())
    return out


def load_sets():
    """Core/StoryScenes.cs -> [chapter][stage] = (name, mood, [piece dicts]): the mock renders the game's own data."""
    src = open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Sigilbound", "Core", "StoryScenes.cs")).read()
    src = src[src.index("// ACT1-BEGIN"):src.index("// ACT2-END")]
    stops = []
    for args in _calls(src, "S"):
        a = _split(args)
        name, mood = a[0].strip('"'), a[1].split(".")[1]
        pieces = []
        for pa in _calls(args, "P"):
            v = _split(pa); d = dict(mesh=v[0].strip('"'), x=0, z=0, yaw=0, scale=1, r=2, glow=None, y=0, fx="None", fxY=0, far=False)
            pos = ["mesh", "x", "z", "yaw", "scale", "r", "glow"]
            for n, tok in enumerate(v[1:], 1):
                if ":" in tok: key, val = [t.strip() for t in tok.split(":", 1)]
                else: key, val = pos[n], tok
                val = val.rstrip("f")
                if key == "glow": d[key] = None if val == "null" else val.strip('"')
                elif key == "fx": d[key] = val.split(".")[1]
                elif key == "far": d[key] = val == "true"
                else: d[key] = float(val)
            pieces.append(d)
        stops.append((name, mood, pieces))
    return [stops[c * 6:(c + 1) * 6] for c in range(15)]


def place(chapter, stage, sets, meshes, kit, name):
    import bpy
    sname, mood, pieces = sets[chapter][stage]
    t = copy.deepcopy(ALL[CHAPTER_THEME[chapter]])
    light, dusk = MOOD[mood]
    if dusk:
        t["bot"] = "#%02x%02x%02x" % tuple(int(min(1, c) * 255) for c in P.lerp(P.hexc(t["bot"]), P.hexc(t["sil"]) * 1.6, dusk))
        t["top"] = "#%02x%02x%02x" % tuple(int(c * 255) for c in P.lerp(P.hexc(t["top"]), P.hexc(t["top"]) * 0.55, 0.7 if mood == "Night" else dusk))
    t["boss"] = stage == 5; t["debris"] = False
    t["keepout"] = [(p["x"], p["z"], p["r"] + 0.6) for p in pieces]; t["scatter"] = 34
    R.setup(name, t, kit, lineup=False)
    ramp = bpy.data.materials["kit"]
    far_mat = R.lit_mat("far", color=P.lerp(P.hexc(t["sil"]), P.hexc(t["bot"]), 0.25) * 0.8)
    for p in pieces:
        v, s = (meshes if p["mesh"] in meshes else kit)[p["mesh"]]
        g = p["glow"] or ("fire" if p["mesh"] in ("brazier", "lantern", "torch_post") else None)
        gm = R.lit_mat("g_" + p["mesh"], color=P.hexc(GLOW[g]) * 0.4, emit=P.hexc(GLOW[g])) if g else bpy.data.materials["glow"]
        if g: gm.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 0.9
        ob = R.kit_object(p["mesh"], v, s, far_mat if p["far"] else ramp, gm)
        ob.location = (p["x"], p["z"], p["y"]); ob.rotation_euler = (0, 0, math.radians(p["yaw"])); ob.scale = (p["scale"],) * 3
        if p["mesh"] == "banner_pole":
            v2, s2 = kit["banner_cloth"]; c = R.kit_object("cloth", v2, s2, ramp, gm); c.parent = ob; c.location = (0, 0, 2.85)
    bpy.data.objects["sun"].data.energy *= light
    return sname


if __name__ == "__main__":
    # python story_review.py OUT [c:s ...]   (1-based chapter:stage; default: every lair plus the shot-tour stops)
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    picks = [tuple(int(x) - 1 for x in a.split(":")) for a in sys.argv[2:]] or [(c, 5) for c in range(15)] + [(1, 0)]
    _, kit, _ = mp.export(os.path.join(out, "_kit.bytes")); os.remove(os.path.join(out, "_kit.bytes"))
    sets = load_sets()
    need = sorted({p["mesh"] for c, s in picks for p in sets[c][s][2] if p["mesh"] not in kit})
    meshes = L.build_all(need)
    for c, st in picks:
        name = f"c{c + 1:02d}_s{st + 1}"
        sname = place(c, st, sets, meshes, kit, name)
        R.render(os.path.join(out, name + ".png"))
        print("rendered", name, sname, flush=True)
