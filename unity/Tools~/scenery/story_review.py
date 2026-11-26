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

GLOW = {"fire": "#ffa040", "flame": "#ff7a2a", "shadow": "#b070ff", "tide": "#4ab8ff", "light": "#ffe07a", "gale": "#7affb0",
        "hollow": "#c060ff", "glass": "#a8f0ff", "rose": "#ffb860"}

# A placement: (mesh, x, z, height, yaw, scale, glow key or None)
def lair(theme_name, items, keep, light=1.0, dusk=0.0):
    return dict(theme=theme_name, items=items, keep=keep, boss=True, light=light, dusk=dusk)

def stop(theme_name, items, keep, light=1.0, dusk=0.0, boss=False):
    return dict(theme=theme_name, items=items, keep=keep, boss=boss, light=light, dusk=dusk)

MILL_FAR = [("drowned_mill", 3.0, 24.0, 0, -12, 0.95, None), ("drowned_mill_wheel", 0.7, 24.0, 0.9, 78, 0.95, None)]
THRONE_FAR = [("sea_throne", -2.0, 25.0, 0, 8, 1.1, "gale")]

SHOTS = {
    # ---------------- lairs (stage 6 of every chapter)
    "c01_thornwick_lair": lair("dusk forest", [("thornwick", 0.5, 10.8, 0, 6, 1.6, None)], [(0.5, 11, 7)], dusk=0.25),
    "c02_drowned_mill_lair": lair("teal shore", [("drowned_mill", 1.0, 12.8, 0, -12, 1.45, None), ("drowned_mill_wheel", -2.2, 12.2, 1.2, 78, 1.45, None),
                                                 ("rowboat", 5.5, 10.6, 0.05, 30, 1.0, None)], [(1, 13, 6)], light=0.85, dusk=0.35),
    "c03_ashgrave_lair": lair("red badlands", [("ashgrave", 0, 9.8, 0, 0, 1.55, None)], [(0, 12, 7.5)], dusk=0.2),
    "c04_saltmarsh_lair": lair("green marsh", [("saltmarsh", 0.5, 12.0, 0, 0, 1.6, None)], [(0.5, 12, 7.5)]),
    "c05_bellspire_lair": lair("gold ruins", [("bellspire", 1.5, 12.5, 0, 15, 1.2, None), ("bellspire_bell", 0.2, 12.5, 9.3, 15, 1.2, None)], [(1.5, 13, 4.5)], dusk=0.2),
    "c06_glass_wastes_lair": lair("blossom forest", [("glass_wastes", 0, 12.0, 0, 0, 1.15, "glass")], [(0, 13, 6.5)]),
    "c07_stormcrown_lair": lair("moonlit shore", [("stormcrown", 0, 14.0, 0, 0, 1.45, None), ("bolt", 3.0, 14.8, 0, 0, 1.0, "gale")], [(0, 14, 7)]),
    "c08_hollow_deep_lair": lair("amethyst badlands", [("hollow_deep", 0, 13.0, 0, 0, 1.15, "hollow")], [(0, 13, 6.5)]),
    "c09_sunken_cathedral_lair": lair("glow marsh", [("sunken_cathedral", 0, 8.6, 0, 0, 1.3, "rose")], [(0, 13, 8)]),
    "c10_last_pyre_lair": lair("burning ruins", [("last_pyre", 0, 13.0, 0, 0, 1.15, "fire")], [(0, 13, 6)]),
    "c11_saltreach_lair": lair("saltreach harbor", [("lighthouse", 5.5, 21.0, 0, 0, 1.1, None), ("lighthouse_lamp", 5.5, 21.0, 8.4, 25, 1.1, "flame")], [(5.5, 21, 6)], dusk=0.35),
    "c12_black_fens_lair": lair("black fens", [("drowned_shrine", 0, 11.0, 0, 0, 1.65, "shadow")], [(0, 11, 6.5)]),
    "c13_wraithmoor_lair": lair("wraithmoor barrows", [("kings_barrow", 0, 14.0, 0, 0, 1.35, "tide")], [(0, 13, 9)]),
    "c14_ember_groves_lair": lair("ember groves", [("great_tree", 0, 14.0, 0, 0, 1.15, "light")], [(0, 14, 6)]),
    "c15_storm_coast_lair": lair("storm coast", [("sea_throne", 0, 12.5, 0, 0, 0.75, "gale"), ("bolt", 4.5, 14.0, 0, 0, 0.8, "gale")], [(0, 14, 7)], light=0.95),

    # ---------------- route strip: The Drowned Mill (stages 1-5; stage 6 is the lair above)
    "c02_s1_flooded_road": stop("teal shore", MILL_FAR + [("post0", -5.5, 6.0, 0, 0, 1.0, None), ("post1", -4.6, 8.5, 0, 0, 1.0, None), ("post0", 5.4, 7.2, 0, 0, 1.0, None),
                                                          ("rowboat", -7.5, 9.6, 0.05, 70, 1.0, None)], [(3, 24, 4)], dusk=0.0),
    "c02_s2_millpond_reeds": stop("teal shore", MILL_FAR[:0] + [("drowned_mill", 6.0, 21.0, 0, -20, 1.0, None), ("drowned_mill_wheel", 3.8, 21.0, 0.9, 70, 1.0, None),
                                                               ("reeds0", -6.0, 7.5, 0, 0, 1.3, None), ("reeds1", 6.5, 6.5, 0, 0, 1.3, None), ("reeds0", -3.0, 9.6, 0, 40, 1.2, None),
                                                               ("rowboat", -2.0, 11.2, 0.05, 15, 1.0, None)], [(6, 21, 4)], dusk=0.05),
    "c02_s3_sluice_gate": stop("teal shore", [("sluice_gate", -1.0, 11.0, 0, 0, 1.2, None), ("drowned_mill", 8.5, 20.0, 0, -25, 0.95, None),
                                              ("drowned_mill_wheel", 6.3, 20.0, 0.9, 65, 0.95, None), ("reeds1", 4.5, 9.0, 0, 0, 1.2, None)], [(-1, 11, 4)], dusk=0.1),
    "c02_s4_millers_cottage": stop("teal shore", [("cottage", -5.0, 9.5, 0, 20, 1.15, None), ("drowned_mill", 7.0, 17.5, 0, -20, 1.0, None),
                                                  ("drowned_mill_wheel", 4.7, 17.5, 0.9, 70, 1.0, None), ("crate", 3.5, 7.0, 0, 30, 1.0, None)], [(-5, 9.5, 4.5)], dusk=0.2),
    "c02_s5_mill_yard": stop("teal shore", [("mill_yard", -1.5, 9.0, 0, 10, 1.15, None), ("drowned_mill", 6.5, 15.0, 0, -15, 1.05, None),
                                            ("drowned_mill_wheel", 4.1, 15.0, 0.9, 75, 1.05, None), ("cottage", -9.5, 13.5, 0, 40, 0.9, None)], [(-1.5, 9, 4.5)], light=0.9, dusk=0.3),

    # ---------------- route strip: The Storm Coast
    "c15_s1_cliff_path": stop("storm coast", THRONE_FAR + [("stone0", -5.5, 6.5, 0, 0, 1.1, None), ("stone1", 6.0, 8.0, 0, 0, 1.0, None),
                                                           ("rock3", -3.5, 10.0, 0, 0, 1.4, None)], [(-2, 25, 5)], light=1.05),
    "c15_s2_the_wreck": stop("storm coast", [("sea_throne", -6.0, 25.0, 0, 15, 1.1, "gale"), ("shipwreck", 1.5, 11.5, 0, 70, 1.15, None),
                                             ("crate", -4.5, 8.0, 0, 20, 1.0, None), ("crate", -5.2, 8.6, 0.6, 50, 0.8, None)], [(1.5, 11.5, 5)], light=1.0),
    "c15_s3_dead_beacon": stop("storm coast", [("beacon", -4.0, 11.0, 0, 0, 1.15, None), ("sea_throne", 5.0, 24.0, 0, -10, 1.1, "gale"),
                                               ("rock0", 4.0, 8.0, 0, 0, 1.3, None)], [(-4, 11, 4)], light=0.95),
    "c15_s4_sea_stacks": stop("storm coast", [("sea_stacks", 2.0, 14.0, 0, 0, 1.1, None), ("sea_throne", -5.0, 22.5, 0, 10, 1.15, "gale"),
                                              ("bolt", -9.0, 23.5, 0, 0, 1.0, "gale")], [(2, 14, 5.5)], light=0.9),
    "c15_s5_hollow_camp": stop("storm coast", [("sea_throne", 1.0, 20.0, 0, 0, 1.2, "gale"), ("banner_pole", -4.0, 9.0, 0, 0, 1.15, None), ("banner_pole", 4.5, 9.5, 0, 0, 1.15, None),
                                               ("brazier", -6.5, 6.0, 0, 0, 1.1, "fire"), ("brazier", 6.6, 6.5, 0, 0, 1.1, "fire"), ("crate", -2.5, 10.5, 0, 10, 1.0, None),
                                               ("bolt", 8.0, 21.5, 0, 0, 1.0, "gale")], [(1, 20, 5)], light=0.85),
}


def place(shot, meshes, kit):
    t = copy.deepcopy(ALL[shot["theme"]])
    if shot["dusk"]:  # later stops of a route drift toward dusk inside the same palette
        t["bot"] = "#%02x%02x%02x" % tuple(int(c * 255) for c in P.lerp(P.hexc(t["bot"]), P.hexc(t["sil"]) * 1.6, shot["dusk"]))
    t["boss"] = shot["boss"]; t["keepout"] = shot["keep"]; t["scatter"] = 34
    R.setup(shot["name"], t, kit, lineup=False)
    import bpy
    ramp = bpy.data.materials["kit"]
    for (name, x, z, y, yaw, sc, glow) in shot["items"]:
        v, s = (meshes if name in meshes else kit)[name]
        gm = R.lit_mat("g_" + name, color=P.hexc(GLOW[glow]) * 0.4, emit=P.hexc(GLOW[glow])) if glow else bpy.data.materials["glow"]
        if glow: gm.node_tree.nodes["Principled BSDF"].inputs["Emission Strength"].default_value = 0.9  # nearer the game's glow than the review's 2.5
        ob = R.kit_object(name, v, s, ramp, gm)
        ob.location = (x, z, y); ob.rotation_euler = (0, 0, math.radians(yaw)); ob.scale = (sc, sc, sc)
        if name == "banner_pole":
            v2, s2 = kit["banner_cloth"]; c = R.kit_object("cloth", v2, s2, ramp, gm); c.parent = ob; c.location = (0, 0, 2.85)
    sun = bpy.data.objects["sun"]; sun.data.energy *= shot["light"]


if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    picks = sys.argv[2:] or list(SHOTS)
    _, kit, _ = mp.export(os.path.join(out, "_kit.bytes")); os.remove(os.path.join(out, "_kit.bytes"))
    meshes = L.build_all()
    for name in picks:
        shot = dict(SHOTS[name]); shot["name"] = name
        place(shot, meshes, kit)
        R.render(os.path.join(out, name + ".png"))
        print("rendered", name, flush=True)
