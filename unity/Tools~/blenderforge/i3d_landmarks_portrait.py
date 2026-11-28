"""Joint landmarks for roster heroes remodelled from their portraits (owner decision 2026-11-27: the portrait is
canon) with image-to-3D meshes (TRELLIS via fal.ai, cleaned by i3d_prep.py), rigged by i3d_rig.py. Same format
as i3d_landmarks.py: meshes 1.85 m tall, feet at z 0, facing -Y, the character's right is -X; a joint is (x, z)
(y = centre of the first solid layer from the front) or (x, y, z). rigid: (bone, test) overrides; blend: (bone,
share, test) gives long robes part of their weight on a calmer bone; tear: edge-stretch factor above which faces
are dropped when the mesh leaves its own pose."""


def _near_line(a, b, r):
    """Test: within r of the segment a-b (a held spear, a sword, a log club)."""
    from mathutils import Vector
    A, B = Vector(a), Vector(b)
    def t(p):
        ab = B - A; u = max(0.0, min(1.0, (p - A).dot(ab) / ab.length_squared))
        return (p - (A + ab * u)).length < r
    return t


def _near_poly(pts, r):
    tests = [_near_line(a, b, r) for a, b in zip(pts, pts[1:])]
    return lambda p: any(t(p) for t in tests)


def _near(c, r):
    from mathutils import Vector
    C = Vector(c)
    return lambda p: (p - C).length < r


def _limb(a, b, r, skip=0.07):
    """Test: within r of the bone a-b but more than skip from the joint a (which keeps its heat blend)."""
    from mathutils import Vector
    A = Vector(a); near = _near_line(a, b, r)
    return lambda p: near(p) and (p - A).length > skip


def _legs(hip, knee_l, knee_r, ank_l, ank_r, toe_l, toe_r):
    return {"hip_l": (hip[0], hip[1]), "hip_r": (-hip[0], hip[1]), "knee_l": knee_l, "knee_r": knee_r,
            "ankle_l": ank_l, "ankle_r": ank_r, "toe_l": toe_l, "toe_r": toe_r}


def _hum(spine, sh, arm_l, arm_r, hip, knees, ankles, toes):
    """Joints from compact specs: spine = (pelvis, spine_01, spine_02, spine_03, neck, head, head_top) heights (or
    (y, z) pairs to pin y); sh = (x, z) of the left shoulder (mirrored); arm_l / arm_r = (elbow, wrist, handend);
    hip = (x, z); knees / ankles / toes = (left, right)."""
    names = ("pelvis", "spine_01", "spine_02", "spine_03", "neck", "head", "head_top")
    j = {n: ((0, v) if not isinstance(v, tuple) else (0, v[0], v[1])) for n, v in zip(names, spine)}
    j["shoulder_l"] = sh; j["shoulder_r"] = (-sh[0], sh[1])
    for s, arm in (("l", arm_l), ("r", arm_r)):
        j["elbow_" + s], j["wrist_" + s], j["handend_" + s] = arm
    j.update(_legs(hip, knees[0], knees[1], ankles[0], ankles[1], toes[0], toes[1]))
    return j


_STD = {"tear": 2.5, "fill_holes": True, "arm_reach": 0.14, "split_limbs": True}
BATCH = {}


LANDMARKS = {
    # boar pelt with the boar's head on his right shoulder, spear in his left hand (as the portrait)
    "brannoc": {
        "joints": {
            "pelvis": (0, 0.93), "spine_01": (0, 1.03), "spine_02": (0, 1.15), "spine_03": (0, 1.28), "neck": (0, 1.47),
            "head": (0, 1.53), "head_top": (0, 1.75),
            "shoulder_l": (0.22, 1.38), "elbow_l": (0.32, 0.0, 1.15), "wrist_l": (0.39, -0.13, 1.06), "handend_l": (0.42, -0.20, 1.03),
            "shoulder_r": (-0.22, 1.38), "elbow_r": (-0.36, 0.0, 1.12), "wrist_r": (-0.40, -0.05, 0.90), "handend_r": (-0.40, -0.06, 0.80),
            **_legs((0.10, 0.88), (0.17, 0.48), (-0.17, 0.48), (0.18, 0.12), (-0.18, 0.12), (0.20, -0.20, 0.02), (-0.22, -0.20, 0.02)),
        },
        "tear": 2.5, "fill_holes": True,
        "rigid": [("hand_l", lambda p: p.x > 0.295 and p.z > 0.08 and _near_line((0.31, -0.11, 0.05), (0.44, -0.27, 1.86), 0.04)(p)),
                  ("hand_l", lambda p: p.x > 0.37 and p.z > 1.42),
                  # the boar's head and the pelt over the shoulders ride the chest, not the arm
                  ("spine_03", lambda p: p.x < -0.13 and p.z > 1.42), ("spine_03", lambda p: p.x < -0.13 and p.z > 1.22 and p.y < -0.12),
                  # the pelt cloak down the back: torso, not arms or legs
                  ("spine_02", lambda p: p.y > 0.16 and 0.65 < p.z <= 1.42), ("pelvis", lambda p: p.y > 0.16 and p.z <= 0.65)],
        "glow_hue": (400, 400),
    },
    # frog-skin hood; regenerated (2026-11-28) with the log club held down at his right side and both arms clear
    # (the first mesh had it over his shoulder with the arm folded against the chest, and tore apart)
    "odo": {**_STD,
        "joints": _hum((0.75, 0.88, 1.03, 1.18, 1.45, 1.52, 1.80), (0.40, 1.30),
                       ((0.48, 0.12, 1.02), (0.53, 0.12, 0.75), (0.55, 0.10, 0.62)), ((-0.48, 0.12, 1.02), (-0.47, 0.10, 0.75), (-0.45, 0.05, 0.62)),
                       (0.15, 0.68), ((0.20, 0.35), (-0.20, 0.35)), ((0.25, 0.10), (-0.18, 0.10)), ((0.27, -0.18, 0.02), (-0.22, -0.18, 0.02))),
        "rigid": [("hand_r", _near_line((-0.50, -0.82, 0.52), (-0.45, 0.82, 0.78), 0.19)),
                  ("lowerarm_l", _limb((0.48, 0.12, 1.02), (0.53, 0.12, 0.75), 0.11, 0.06)), ("lowerarm_r", _limb((-0.48, 0.12, 1.02), (-0.47, 0.12, 0.75), 0.11, 0.06)),
                  ("upperarm_l", _limb((0.40, 0.12, 1.30), (0.48, 0.12, 1.02), 0.11, 0.10)), ("upperarm_r", _limb((-0.40, 0.12, 1.30), (-0.48, 0.12, 1.02), 0.11, 0.10)),
                  # the hood's cape down the back: torso
                  ("spine_02", lambda p: p.y > 0.25 and 0.65 < p.z < 1.35), ("pelvis", lambda p: p.y > 0.25 and p.z <= 0.65)],
        "glow_hue": (400, 400),
    },
    # sigil-shard crown, high collar, floor-length cloak; the orb and its chain in his left hand (as the portrait)
    "vex": {
        "joints": {
            "pelvis": (0, -0.10, 0.92), "spine_01": (0, -0.10, 1.02), "spine_02": (0, -0.10, 1.14), "spine_03": (0, -0.10, 1.27),
            "neck": (0, -0.12, 1.47), "head": (0, -0.10, 1.53), "head_top": (0, -0.10, 1.68),
            "shoulder_l": (0.24, 1.40), "elbow_l": (0.27, -0.12, 1.13), "wrist_l": (0.29, -0.33, 1.22), "handend_l": (0.31, -0.42, 1.25),
            "shoulder_r": (-0.24, 1.40), "elbow_r": (-0.26, 1.08), "wrist_r": (-0.22, 0.85), "handend_r": (-0.20, 0.76),
            **_legs((0.10, 0.88), (0.12, -0.06, 0.48), (-0.12, -0.06, 0.48), (0.13, -0.06, 0.09), (-0.12, -0.06, 0.09),
                    (0.15, -0.25, 0.02), (-0.14, -0.25, 0.02)),
        },
        "tear": 2.5, "fill_holes": True, "arm_reach": 0.15,
        "rigid": [("hand_l", _near((0.33, -0.42, 1.38), 0.14)), ("hand_l", lambda p: 0.22 < p.x < 0.36 and 0.86 < p.z < 1.18 and p.y < -0.18),
                  ("Head", lambda p: p.z > 1.62),
                  # the cloak's back and its flared hem hang from the shoulders and hips
                  ("spine_02", lambda p: p.y > 0.12 and 0.9 < p.z < 1.40), ("pelvis", lambda p: p.y > 0.12 and p.z <= 0.9),
                  ("pelvis", lambda p: abs(p.x) > 0.30 and p.z < 0.75)],
        # the front of the robe between and around the legs: half on the pelvis, so it sways rather than splits
        "blend": [("pelvis", 0.5, lambda p: p.z < 0.88 and abs(p.x) < 0.32)],
        "glow_hue": (280, 330),
    },
    # sunburst disc and banners on his back; both hands on the pommel of a sword held point-down in front
    "aurex": {
        "joints": {
            "pelvis": (0, 0.92), "spine_01": (0, 1.02), "spine_02": (0, 1.14), "spine_03": (0, 1.26), "neck": (0, 1.40),
            "head": (0, 1.45), "head_top": (0, 1.62),
            "shoulder_l": (0.24, 1.36), "elbow_l": (0.33, 0.0, 1.12), "wrist_l": (0.12, -0.12, 1.08), "handend_l": (0.03, -0.20, 1.07),
            "shoulder_r": (-0.24, 1.36), "elbow_r": (-0.33, 0.0, 1.12), "wrist_r": (-0.12, -0.12, 1.08), "handend_r": (-0.03, -0.20, 1.07),
            **_legs((0.10, 0.88), (0.16, 0.50), (-0.18, 0.50), (0.20, 0.10), (-0.20, 0.10), (0.27, -0.15, 0.02), (-0.24, -0.15, 0.02)),
        },
        "tear": 2.5, "arm_reach": 0.13, "split_limbs": True, "fill_holes": True, "fill_max": 1.6, "cap_sample": (0.0, -0.2, 1.25),  # the belly the crossed forearms hid is one big hole: cap it in the breastplate white
        "rigid": [("hand_r", lambda p: abs(p.x) < 0.075 and p.y < -0.14 and p.z < 1.02),
                  ("hand_r", lambda p: abs(p.x) < 0.17 and p.y < -0.12 and 0.72 < p.z < 0.84),
                  # the forearms and gauntlets crossed in front of the belly: rigid (bone heat bleeds them into the torso)
                  ("hand_r", lambda p: p.x <= 0 and _near((-0.07, -0.17, 1.08), 0.08)(p)), ("hand_l", lambda p: p.x > 0 and _near((0.07, -0.17, 1.08), 0.08)(p)),
                  ("lowerarm_r", _limb((-0.33, 0.0, 1.12), (-0.12, -0.12, 1.08), 0.08, 0.06)), ("lowerarm_l", _limb((0.33, 0.0, 1.12), (0.12, -0.12, 1.08), 0.08, 0.06)),
                  # sunburst rays, cross-bar and banners: rigid on the chest
                  ("spine_03", lambda p: p.y > 0.08 and abs(p.x) > 0.25), ("spine_03", lambda p: abs(p.x) > 0.30 and 0.45 < p.z < 1.0),
                  ("spine_03", lambda p: 1.37 < p.z < 1.47 and abs(p.x) > 0.36),
                  ("spine_03", lambda p: p.z > 1.52 and (abs(p.x) > 0.13 or p.y > 0.13))],
        # the tabard in front and the coat tails behind the legs: mostly on the pelvis (a wide stance would stretch them into sheets)
        "blend": [("pelvis", 0.75, lambda p: 0.2 < p.z < 0.88 and abs(p.x) < 0.27 and (p.y > 0.06 or p.y < -0.12))],
        "glow_hue": (400, 400),
    },
}

# ---- portrait-canon batch (27 roster heroes, 2026-11-28) ----
BATCH["fennick"] = {**_STD,
    "joints": _hum((0.62, 0.72, 0.85, 0.98, 1.22, 1.27, 1.75), (0.20, 1.08),
                   ((0.30, -0.02, 0.85), (0.32, -0.25, 0.93), (0.36, -0.37, 0.93)), ((-0.30, 0.82), (-0.42, 0.60), (-0.45, 0.48)),
                   (0.10, 0.55), ((0.14, 0.30), (-0.14, 0.30)), ((0.17, 0.08), (-0.17, 0.08)), ((0.15, -0.15, 0.02), (-0.20, -0.15, 0.02))),
    "rigid": [("hand_l", _near_line((0.37, -0.02, 0.03), (0.45, -0.62, 1.45), 0.05)), ("hand_l", _near((0.45, -0.62, 1.45), 0.15)),
              ("spine_03", lambda p: p.y > 0.14 and p.z > 0.6)],
    "glow_hue": (400, 400)}
BATCH["holt"] = {**_STD,
    "joints": _hum((0.80, 0.92, 1.05, 1.20, 1.45, 1.52, 1.80), (0.38, 1.30),
                   ((0.50, 0.0, 1.05), (0.50, -0.12, 0.92), (0.52, -0.20, 0.90)), ((-0.48, 1.0), (-0.45, 0.72), (-0.45, 0.62)),
                   (0.15, 0.75), ((0.18, 0.35), (-0.18, 0.35)), ((0.20, 0.10), (-0.20, 0.10)), ((0.22, -0.18, 0.02), (-0.22, -0.18, 0.02))),
    "rigid": [("hand_r", lambda p: p.x < -0.36 and p.z < 0.70),
              ("hand_l", _near_line((0.50, -0.07, 0.42), (0.60, -0.20, 1.30), 0.06)), ("hand_l", _near((0.66, -0.25, 1.40), 0.21))],
    "blend": [("pelvis", 0.6, lambda p: p.z < 0.75 and p.y < -0.10 and abs(p.x) < 0.33)],
    "glow_hue": (400, 400)}
BATCH["kharra"] = {**_STD,
    "joints": _hum((0.98, 1.08, 1.22, 1.36, 1.62, 1.68, 1.85), (0.33, 1.48),
                   ((0.40, 1.25), (0.47, 1.05), (0.47, 0.80)), ((-0.42, 1.20), (-0.38, 0.95), (-0.38, 0.82)),
                   (0.13, 0.92), ((0.20, 0.48), (-0.20, 0.48)), ((0.22, 0.10), (-0.22, 0.10)), ((0.30, -0.20, 0.02), (-0.30, -0.20, 0.02))),
    "rigid": [("hand_l", lambda p: p.x > 0.36 and 0.68 < p.z < 1.35)],
    "glow_hue": (8, 45)}
BATCH["marrow"] = {**_STD,   # regenerated (2026-11-28): the broken pillar hangs from his right hand
    "joints": _hum((0.95, 1.05, 1.20, 1.35, 1.60, 1.66, 1.84), (0.38, 1.48),
                   ((0.47, 1.15), (0.48, 0.90), (0.47, 0.76)), ((-0.47, 1.15), (-0.45, 0.92), (-0.43, 0.78)),
                   (0.14, 0.88), ((0.20, 0.45), (-0.20, 0.45)), ((0.22, 0.10), (-0.20, 0.10)), ((0.27, -0.20, 0.02), (-0.25, -0.20, 0.02))),
    "rigid": [("hand_r", lambda p: p.x < -0.30 and p.z < 0.72)],
    "glow_hue": (8, 45)}

BATCH["obrecht"] = {**_STD,
    "joints": _hum((0.70, 0.82, 0.98, 1.15, 1.42, 1.48, 1.68), (0.40, 1.38),
                   ((0.48, 1.0), (0.52, 0.70), (0.52, 0.55)), ((-0.48, 1.0), (-0.52, 0.70), (-0.52, 0.55)),
                   (0.17, 0.62), ((0.20, 0.35), (-0.20, 0.35)), ((0.22, 0.08), (-0.22, 0.08)), ((0.25, -0.20, 0.02), (-0.25, -0.20, 0.02))),
    "rigid": [("hand_l", _near_line((0.55, -0.85, 0.55), (0.55, 0.86, 0.55), 0.08)), ("hand_l", lambda p: p.y < -0.6 and p.x > 0.3),
              ("spine_03", lambda p: p.y > 0.25 and p.z > 0.8 and p.x < 0.3)],
    "glow_hue": (8, 45)}
BATCH["saskia"] = {**_STD,
    "joints": _hum((1.00, 1.10, 1.22, 1.36, 1.55, 1.60, 1.80), (0.14, 1.48),
                   ((0.22, 1.18), (0.26, 0.95), (0.26, 0.86)), ((-0.22, 1.18), (-0.30, -0.20, 0.95), (-0.32, -0.25, 0.88)),
                   (0.08, 0.95), ((0.12, 0.50), (-0.12, 0.50)), ((0.14, 0.10), (-0.12, 0.10)), ((0.15, -0.15, 0.02), (-0.13, -0.15, 0.02))),
    "rigid": [("hand_r", _near_poly([(-0.55, -0.90, 0.15), (-0.32, -0.25, 0.92), (-0.08, 0.85, 1.85)], 0.05)),
              ("hand_r", _near_line((-0.55, -0.95, 0.15), (-0.45, -0.55, 0.55), 0.10))],
    "blend": [("pelvis", 0.6, lambda p: 0.4 < p.z < 0.95 and abs(p.x) < 0.32)],
    "glow_hue": (8, 45)}
BATCH["barley"] = {**_STD,
    "joints": _hum((0.85, 0.97, 1.12, 1.27, 1.55, 1.62, 1.85), (0.30, 1.40),
                   ((0.42, 1.08), (0.43, 0.85), (0.43, 0.76)), ((-0.42, 1.08), (-0.43, 0.85), (-0.43, 0.76)),
                   (0.13, 0.78), ((0.18, 0.40), (-0.18, 0.40)), ((0.18, 0.10), (-0.18, 0.10)), ((0.22, -0.18, 0.02), (-0.20, -0.18, 0.02))),
    "rigid": [("hand_r", lambda p: p.y < -0.18 and 0.6 < p.z < 1.05 and -0.55 < p.x < -0.25),
              ("hand_l", lambda p: p.y < -0.18 and 0.6 < p.z < 1.05 and 0.25 < p.x < 0.55),
              # the fleece cloak hangs from the shoulders around the arms: chest and hips
              ("spine_03", lambda p: p.z > 1.25 and abs(p.x) > 0.15 and not (abs(p.x) < 0.14 and p.z > 1.5)),
              ("spine_02", lambda p: (abs(p.x) > 0.5 or p.y > 0.15) and 0.65 < p.z <= 1.25), ("pelvis", lambda p: (abs(p.x) > 0.5 or p.y > 0.15) and p.z <= 0.65)],
    "glow_hue": (400, 400)}
BATCH["corran"] = {**_STD,
    "joints": _hum((0.95, 1.05, 1.18, 1.30, 1.45, 1.55, 1.72), (0.20, 1.40),
                   ((0.30, 1.08), (0.38, 0.80), (0.40, 0.70)), ((-0.25, -0.05, 1.18), (-0.28, -0.20, 1.27), (-0.30, -0.27, 1.30)),
                   (0.10, 0.88), ((0.14, 0.48), (-0.12, 0.48)), ((0.17, 0.10), (-0.10, 0.10)), ((0.22, -0.15, 0.02), (-0.08, -0.20, 0.02))),
    "rigid": [("hand_r", _near_poly([(-0.25, -0.10, 0.0), (-0.27, -0.19, 0.6), (-0.29, -0.26, 1.0), (-0.30, -0.30, 1.35), (-0.30, -0.34, 1.85)], 0.06)),
              ("hand_r", lambda p: p.x < -0.26 and p.z > 1.45)],
    "blend": [("pelvis", 0.6, lambda p: 0.55 < p.z < 0.95 and abs(p.x) < 0.25)],
    "glow_hue": (400, 400)}
BATCH["nettle"] = {**_STD,
    "joints": _hum((0.95, 1.05, 1.17, 1.30, 1.42, 1.50, 1.75), (0.22, 1.35),
                   ((0.30, 1.05), (0.33, 0.85), (0.35, 0.76)), ((-0.30, 1.05), (-0.33, 0.85), (-0.35, 0.76)),
                   (0.10, 0.88), ((0.13, 0.48), (-0.13, 0.48)), ((0.16, 0.10), (-0.13, 0.10)), ((0.20, -0.20, 0.02), (-0.18, -0.20, 0.02))),
    "rigid": [("hand_r", lambda p: p.y < -0.15 and p.x < -0.22 and 0.55 < p.z < 0.95), ("hand_l", lambda p: p.x > 0.30 and 0.55 < p.z < 0.75),
              ("pelvis", lambda p: p.y > 0.15 and p.z < 0.95), ("pelvis", lambda p: p.x < -0.33 and p.z < 0.6),   # the tail
              ("spine_03", lambda p: p.y > 0.12 and p.z >= 0.95), ("spine_03", lambda p: p.z > 1.38 and abs(p.x) > 0.13 and p.y > 0.0)],
    "glow_hue": (400, 400)}
BATCH["tansy"] = {**_STD,
    "joints": _hum((0.98, 1.07, 1.17, 1.28, 1.40, 1.45, 1.62), (0.18, 1.28),
                   ((0.27, 1.0), (0.30, 0.80), (0.32, 0.72)), ((-0.25, 1.02), (-0.36, 0.85), (-0.38, 0.76)),
                   (0.09, 0.92), ((0.13, 0.48), (-0.13, 0.48)), ((0.15, 0.12), (-0.13, 0.12)), ((0.22, -0.15, 0.02), (-0.20, -0.15, 0.02))),
    "rigid": [("hand_r", lambda p: p.x < -0.30 and 0.55 < p.z < 0.98), ("Head", lambda p: p.z > 1.55)],
    "blend": [("pelvis", 0.5, lambda p: 0.6 < p.z < 0.98 and abs(p.x) < 0.26)],
    "glow_hue": (400, 400)}
BATCH["brega"] = {**_STD,
    "joints": _hum((0.70, 0.82, 0.98, 1.15, 1.48, 1.55, 1.82), (0.38, 1.40),
                   ((0.45, 1.10), (0.30, -0.25, 0.97), (0.22, -0.32, 0.95)), ((-0.55, 1.05), (-0.52, 0.75), (-0.52, 0.62)),
                   (0.17, 0.62), ((0.22, 0.35), (-0.22, 0.35)), ((0.22, 0.08), (-0.22, 0.08)), ((0.25, -0.20, 0.02), (-0.25, -0.20, 0.02))),
    "rigid": [("lowerarm_l", lambda p: p.y < -0.3 and p.x > -0.05 and 0.45 < p.z < 1.35),   # the ship's-wheel shield
              ("hand_r", lambda p: p.x < -0.35 and p.y < -0.15 and p.z < 0.8),              # the mace
              ("pelvis", lambda p: p.y > 0.35 and 0.7 < p.z < 0.95)],                       # the sword at his back
    "glow_hue": (400, 400)}
BATCH["lazlo"] = {**_STD,
    "joints": _hum((1.00, 1.10, 1.22, 1.35, (-0.05, 1.55), (-0.10, 1.62), (-0.12, 1.85)), (0.24, 1.48),
                   ((0.33, 1.18), (0.36, 0.92), (0.36, 0.84)), ((-0.33, 1.18), (-0.36, 0.0, 0.95), (-0.37, -0.02, 0.86)),
                   (0.11, 0.95), ((0.13, 0.50), (-0.13, 0.50)), ((0.15, 0.10), (-0.15, 0.10)), ((0.22, -0.18, 0.02), (-0.20, -0.18, 0.02))),
    "rigid": [("hand_r", _near_poly([(-0.36, 0.0, 0.92), (-0.42, -0.13, 0.60), (-0.56, -0.39, 0.30), (-0.63, -0.52, 0.18)], 0.045)), ("hand_r", lambda p: p.x < -0.42 and 0.8 < p.z < 1.0)],
    "blend": [("pelvis", 0.6, lambda p: 0.4 < p.z < 0.95 and (abs(p.x) > 0.21 or p.y > 0.12))],
    "glow_hue": (400, 400)}
BATCH["wenna"] = {**_STD,
    "joints": _hum((0.85, 0.95, 1.10, 1.25, (-0.05, 1.48), (-0.12, 1.55), (-0.15, 1.82)), (0.33, 1.38),
                   ((0.45, 1.05), (0.50, 0.72), (0.50, 0.62)), ((-0.45, 1.05), (-0.45, -0.12, 0.90), (-0.45, -0.20, 0.85)),
                   (0.15, 0.75), ((0.18, 0.40), (-0.18, 0.40)), ((0.25, 0.10), (-0.20, 0.10)), ((0.30, -0.18, 0.02), (-0.25, -0.20, 0.02))),
    "rigid": [("hand_r", lambda p: p.x < -0.25 and p.y < -0.12 and 0.75 < p.z < 1.3),   # the coral club
              ("spine_02", lambda p: p.y > 0.18 and p.z > 0.45)],                       # the shell
    "glow_hue": (400, 400)}
BATCH["neve"] = {**_STD, "arm_reach": 0.12,
    "joints": _hum((1.00, 1.10, 1.22, 1.35, 1.58, 1.63, 1.85), (0.18, 1.48),
                   ((0.22, 0.0, 1.20), (0.18, -0.20, 1.22), (0.12, -0.25, 1.25)), ((-0.26, 0.0, 1.22), (-0.05, -0.20, 1.24), (0.03, -0.25, 1.25)),
                   (0.09, 0.95), ((0.10, 0.50), (-0.10, 0.50)), ((0.10, 0.10), (-0.10, 0.10)), ((0.12, -0.15, 0.02), (-0.12, -0.15, 0.02))),
    "rigid": [("hand_r", lambda p: p.y < -0.2 and 1.18 < p.z < 1.6 and -0.15 < p.x < 0.35),   # the conch
              ("spine_03", lambda p: p.y > 0.1 and 1.0 < p.z < 1.55)],                         # the hair down her back
    "blend": [("pelvis", 0.6, lambda p: p.z < 1.02 and (abs(p.x) > 0.15 or abs(p.y) > 0.1))],
    "glow_hue": (400, 400)}
BATCH["maelis"] = {**_STD,   # regenerated (2026-11-28): a slim trident upright beside him
    "joints": _hum((0.95, 1.05, 1.17, 1.30, 1.45, 1.52, 1.75), (0.26, 1.35),
                   ((0.32, 1.10), (0.40, 1.04), (0.45, 1.03)), ((-0.40, 1.05), (-0.42, 0.82), (-0.42, 0.72)),
                   (0.10, 0.90), ((0.13, 0.48), (-0.13, 0.48)), ((0.12, 0.10), (-0.18, 0.10)), ((0.10, -0.18, 0.02), (-0.22, -0.18, 0.02))),
    "rigid": [("hand_l", _near_line((0.40, 0.05, 0.0), (0.40, 0.05, 1.5), 0.05)), ("hand_l", lambda p: p.x > 0.22 and p.z > 1.45 and p.y > -0.1)],
    "glow_hue": (400, 400)}
BATCH["morvoren"] = {**_STD,
    "joints": _hum((0.98, 1.08, 1.20, 1.32, 1.52, 1.58, 1.80), (0.16, 1.42),
                   ((0.20, 1.12), (0.18, 0.90), (0.18, 0.82)), ((-0.24, 1.22), (-0.33, -0.30, 1.15), (-0.36, -0.36, 1.15)),
                   (0.08, 0.92), ((0.10, 0.50), (-0.10, 0.50)), ((0.08, 0.08), (-0.08, 0.08)), ((0.10, -0.15, 0.02), (-0.10, -0.15, 0.02))),
    "rigid": [("hand_r", _near_poly([(-0.32, -0.26, 0.0), (-0.32, -0.29, 0.9), (-0.40, -0.42, 1.4), (-0.42, -0.44, 1.88)], 0.055)), ("hand_r", _near((-0.42, -0.44, 1.72), 0.16))],
    "blend": [("pelvis", 0.75, lambda p: p.z < 0.95)],   # the floor-length gown
    "glow_hue": (160, 200)}
BATCH["caspian"] = {**_STD,
    "joints": _hum((0.88, 0.98, 1.10, 1.22, 1.38, 1.45, 1.75), (0.28, 1.25),
                   ((0.40, 1.0), (0.38, 0.78), (0.40, 0.66)), ((-0.40, 1.0), (-0.42, 0.78), (-0.42, 0.66)),
                   (0.12, 0.82), ((0.15, 0.45), (-0.15, 0.45)), ((0.17, 0.10), (-0.17, 0.10)), ((0.20, -0.20, 0.02), (-0.20, -0.20, 0.02))),
    "rigid": [("hand_r", lambda p: p.x < -0.3 and p.z < 0.85 and p.y < 0.15), ("hand_r", _near_line((-0.42, -0.75, 0.5), (-0.42, 0.0, 0.7), 0.08)),
              ("pelvis", lambda p: p.y > 0.2 and 0.35 < p.z < 0.85)],   # the air hose
    "glow_hue": (400, 400)}
BATCH["augustin"] = {**_STD,
    "joints": _hum((0.88, 0.98, 1.12, 1.26, 1.50, 1.58, 1.85), (0.35, 1.35),
                   ((0.48, 1.05), (0.48, 0.82), (0.50, 0.72)), ((-0.48, 1.05), (-0.52, 0.85), (-0.55, 0.75)),
                   (0.13, 0.80), ((0.20, 0.45), (-0.20, 0.45)), ((0.20, 0.10), (-0.20, 0.10)), ((0.25, -0.20, 0.02), (-0.25, -0.20, 0.02))),
    "rigid": [("hand_r", lambda p: p.x < -0.38 and 0.5 < p.z < 0.95 and p.y < 0.1),           # the mace
              ("pelvis", lambda p: p.y > 0.35 and 0.75 < p.z < 1.25),                    # the tail tuft
              ("spine_02", lambda p: p.y > 0.15 and 0.65 < p.z < 1.4), ("pelvis", lambda p: p.y > 0.15 and p.z <= 0.65)],   # the cape
    "glow_hue": (400, 400)}
BATCH["benedict"] = {**_STD,
    "joints": _hum((0.70, 0.80, 0.95, 1.10, 1.28, 1.32, 1.48), (0.35, 1.20),
                   ((0.48, 1.0), (0.52, 0.80), (0.52, 0.70)), ((-0.48, 0.0, 1.0), (-0.42, -0.25, 0.92), (-0.45, -0.38, 0.92)),
                   (0.17, 0.62), ((0.20, 0.33), (-0.20, 0.33)), ((0.22, 0.08), (-0.22, 0.08)), ((0.25, -0.20, 0.02), (-0.25, -0.20, 0.02))),
    "rigid": [("hand_r", _near_line((-0.47, -0.38, 0.0), (-0.47, -0.38, 1.55), 0.05)), ("hand_r", _near((-0.48, -0.38, 1.65), 0.24)),
              ("lowerarm_l", lambda p: p.x > 0.35 and 0.35 < p.z < 1.35)],   # the sundial shield
    "glow_hue": (400, 400)}
BATCH["poppy"] = {**_STD,   # third mesh (2026-11-28): background-removed input, legs apart, the sunflower at arm's length
    "joints": _hum((0.82, 0.90, 1.00, 1.10, 1.25, 1.30, 1.62), (0.15, 1.20),
                   ((0.28, 1.18), (0.38, 1.27), (0.41, 1.30)), ((-0.30, 1.05), (-0.55, 0.92), (-0.62, 0.90)),
                   (0.07, 0.77), ((0.15, 0.40), (-0.22, 0.40)), ((0.27, 0.07), (-0.38, 0.07)), ((0.30, -0.12, 0.02), (-0.40, -0.12, 0.02))),
    "rigid": [("hand_l", lambda p: p.x > 0.33 and p.z > 0.8), ("Head", lambda p: p.z > 1.32 and abs(p.x) < 0.42)],
    "blend": [("pelvis", 0.5, lambda p: 0.68 < p.z < 0.98)],
    "glow_hue": (400, 400)}
BATCH["jasper"] = {**_STD,
    "joints": _hum((0.95, 1.05, 1.17, 1.30, 1.48, 1.55, 1.72), (0.22, 1.40),
                   ((0.38, 1.15), (0.42, 1.30), (0.45, 1.38)), ((-0.38, 1.15), (-0.42, 1.30), (-0.45, 1.38)),
                   (0.10, 0.88), ((0.14, 0.48), (-0.14, 0.48)), ((0.14, 0.10), (-0.14, 0.10)), ((0.18, -0.20, 0.02), (-0.18, -0.20, 0.02))),
    "rigid": [("hand_l", lambda p: p.x > 0.38 and p.z > 1.33), ("hand_r", lambda p: p.x < -0.38 and p.z > 1.33),   # the cards
              ("Head", lambda p: p.z > 1.6)],                                                                     # the hat
    "blend": [("pelvis", 0.6, lambda p: p.z < 0.95 and (abs(p.x) > 0.17 or p.y > 0.1))],
    "glow_hue": (400, 400)}
BATCH["perpetua"] = {**_STD,
    "joints": _hum((1.00, 1.10, 1.22, 1.35, 1.55, 1.62, 1.85), (0.15, 1.48),
                   ((0.25, 1.22), (0.28, 0.98), (0.28, 0.90)), ((-0.30, 1.22), (-0.38, 1.02), (-0.40, 0.98)),
                   (0.08, 0.93), ((0.09, 0.50), (-0.09, 0.50)), ((0.08, 0.08), (-0.08, 0.08)), ((0.10, -0.15, 0.02), (-0.10, -0.15, 0.02))),
    "rigid": [("hand_r", lambda p: p.x < -0.38 and p.z < 1.15 and p.y < -0.2),          # the umbrella
              ("pelvis", lambda p: p.y > 0.18 and 0.25 < p.z < 1.0 and abs(p.x) < 0.15)],   # the tail
    "blend": [("pelvis", 0.75, lambda p: p.z < 0.98)],   # the floor-length gown
    "glow_hue": (400, 400)}
BATCH["lune"] = {**_STD,
    "joints": _hum((0.92, 1.02, 1.14, 1.26, 1.42, 1.48, 1.75), (0.20, 1.36),
                   ((0.28, 1.05), (0.30, 0.88), (0.30, 0.80)), ((-0.27, 1.15), (-0.32, -0.25, 1.24), (-0.34, -0.30, 1.26)),
                   (0.08, 0.88), ((0.10, 0.48), (-0.10, 0.48)), ((0.15, 0.08), (-0.12, 0.08)), ((0.22, -0.15, 0.02), (-0.15, -0.18, 0.02))),
    "rigid": [("hand_r", _near_line((-0.34, -0.28, 0.0), (-0.36, -0.30, 1.60), 0.05)), ("hand_r", _near((-0.38, -0.30, 1.72), 0.14)),
              # the feathered cloak-wings hang from the shoulders: chest and hips
              ("spine_02", lambda p: (abs(p.x) > 0.36 or p.y > 0.15) and 0.7 < p.z < 1.35), ("pelvis", lambda p: (abs(p.x) > 0.36 or p.y > 0.15) and p.z <= 0.7)],
    "blend": [("pelvis", 0.6, lambda p: p.z < 0.88)],
    "glow_hue": (400, 400)}
BATCH["rook"] = {**_STD,
    "joints": _hum((0.95, 1.05, 1.18, 1.32, 1.58, 1.65, 1.85), (0.26, 1.45),
                   ((0.30, 1.15), (0.36, -0.15, 1.10), (0.40, -0.22, 1.12)), ((-0.30, 1.10), (-0.28, 0.95), (-0.27, 0.88)),
                   (0.11, 0.88), ((0.15, 0.45), (-0.15, 0.45)), ((0.18, 0.10), (-0.15, 0.10)), ((0.22, -0.20, 0.02), (-0.22, -0.20, 0.02))),
    "rigid": [("hand_l", _near_poly([(0.92, -0.35, 1.45), (0.38, -0.25, 1.12), (-1.0, -0.05, 0.50)], 0.05)), ("hand_l", _near((0.85, -0.35, 1.38), 0.16)),
              ("hand_l", lambda p: p.x < -0.6 and p.z < 0.75)],
    "blend": [("pelvis", 0.5, lambda p: p.z < 0.9 and (abs(p.x) > 0.2 or p.y > 0.1))],
    "glow_hue": (400, 400)}
BATCH["nell"] = {**_STD,   # regenerated (2026-11-28): the scythe upright beside her, neat hat brim, short cloak
    "joints": _hum((0.85, 0.95, 1.07, 1.20, 1.33, 1.38, 1.55), (0.20, 1.28),
                   ((0.24, 0.0, 1.02), (0.29, -0.18, 1.10), (0.30, -0.25, 1.12)), ((-0.28, 0.95), (-0.30, 0.75), (-0.28, 0.66)),
                   (0.11, 0.78), ((0.17, 0.42), (-0.15, 0.42)), ((0.18, 0.10), (-0.18, 0.10)), ((0.20, -0.15, 0.02), (-0.25, -0.15, 0.02))),
    "rigid": [("hand_l", _near_line((0.36, -0.23, 0.0), (0.30, -0.23, 1.82), 0.05)), ("hand_l", lambda p: p.z > 1.57),
              ("Head", lambda p: p.z > 1.40)],
    "glow_hue": (400, 400)}
BATCH["aveline"] = {**_STD,
    "joints": _hum((1.00, 1.08, 1.20, 1.32, 1.50, 1.56, 1.78), (0.15, 1.40),
                   ((0.25, 1.20), (0.30, -0.20, 1.25), (0.30, -0.28, 1.27)), ((-0.22, 1.15), (-0.15, -0.20, 1.0), (-0.12, -0.25, 0.98)),
                   (0.08, 0.93), ((0.09, 0.50), (-0.09, 0.50)), ((0.08, 0.08), (-0.08, 0.08)), ((0.10, -0.15, 0.02), (-0.10, -0.15, 0.02))),
    "rigid": [("hand_l", lambda p: p.y < -0.2 and 0.45 < p.z < 1.32 and -0.2 < p.x < 0.5),            # the hoop
              ("spine_03", lambda p: (p.y > 0.22 and 0.3 < p.z < 1.0) or (p.y > 0.1 and p.z >= 1.0 and abs(p.x) > 0.12)),   # the wings
              ("spine_03", lambda p: abs(p.x) > 0.45 and p.z > 0.3)],
    "blend": [("pelvis", 0.75, lambda p: p.z < 0.95)],
    "glow_hue": (400, 400)}
BATCH["astrid"] = {**_STD,
    "joints": _hum((0.92, 1.02, 1.15, 1.28, 1.47, 1.53, 1.75), (0.20, 1.40),
                   ((0.32, 1.12), (0.38, -0.10, 1.12), (0.42, -0.15, 1.12)), ((-0.30, 1.12), (-0.33, 0.95), (-0.34, 0.86)),
                   (0.10, 0.88), ((0.18, 0.48), (-0.18, 0.48)), ((0.22, 0.10), (-0.22, 0.10)), ((0.27, -0.18, 0.02), (-0.25, -0.20, 0.02))),
    "rigid": [("hand_l", _near_line((0.33, 0.0, 0.05), (0.54, -0.27, 1.82), 0.05)), ("hand_l", _near((0.52, -0.26, 1.65), 0.11)),
              ("spine_03", lambda p: p.y > 0.12 and p.z > 0.15 and abs(p.x) > 0.15), ("spine_03", lambda p: abs(p.x) > 0.45)],   # the glider wings
    "glow_hue": (400, 400)}


def _digitigrade(entry, **legs):
    """Opt-in digitigrade legs (i3d_legs.py): the true knee, the hock, the ball of the foot and the toe tip per
    side, each (x, y, z) in the mesh's own pose. The hock doubles as the "ankle" the generic code reads."""
    j = dict(entry["joints"])
    for s in ("l", "r"):
        for k in ("knee", "hock", "ball", "toe"): j[f"{k}_{s}"] = legs[f"{k}_{s}"]
        j["ankle_" + s] = legs["hock_" + s]
    return {**entry, "joints": j, "legs": "digitigrade"}


# Animal-folk whose meshes stand on digitigrade legs (owner report 2026-11-28: their knees bent like a human's):
# the hare's long hind feet and the goat's hocked legs over hoof boots. Landmarks read off side views of each leg.
BATCH["tansy"] = _digitigrade(BATCH["tansy"],
    knee_l=(0.17, 0.15, 0.52), hock_l=(0.22, 0.28, 0.22), ball_l=(0.25, 0.16, 0.05), toe_l=(0.28, 0.07, 0.03),
    knee_r=(-0.11, 0.22, 0.48), hock_r=(-0.12, 0.305, 0.21), ball_r=(-0.19, 0.14, 0.05), toe_r=(-0.22, 0.03, 0.03))
BATCH["corran"] = _digitigrade(BATCH["corran"],
    knee_l=(0.20, 0.20, 0.55), hock_l=(0.20, 0.335, 0.22), ball_l=(0.22, 0.20, 0.05), toe_l=(0.24, 0.11, 0.015),
    knee_r=(-0.12, 0.09, 0.52), hock_r=(-0.13, 0.13, 0.21), ball_r=(-0.15, -0.10, 0.05), toe_r=(-0.17, -0.24, 0.015))

LANDMARKS.update(BATCH)
