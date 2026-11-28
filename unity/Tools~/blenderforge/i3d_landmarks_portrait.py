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


_ODO_LOG = _near_poly([(-0.58, -0.55, 0.99), (-0.35, -0.27, 1.25), (-0.10, 0.07, 1.49), (0.10, 0.20, 1.66), (0.42, 0.45, 1.77)], 0.14)
_ODO_SHOULDER = _near_line((-0.30, -0.02, 1.26), (0.0, 0.0, 1.30), 0.12)   # the log rests here: keep the shoulder
_ODO_HEAD = _near((0.03, -0.08, 1.45), 0.19)                                # the hood under the log
_ODO_ARM_R = _near_line((-0.28, -0.05, 1.28), (-0.41, -0.09, 0.96), 0.13)
_ODO_ARM_L = _near_line((0.28, -0.05, 1.28), (0.45, -0.02, 1.0), 0.13)

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
    # frog-skin hood, a mossy log club over his right shoulder in his right hand
    "odo": {
        "joints": {
            "pelvis": (0, 0.68), "spine_01": (0, 0.82), "spine_02": (0, 0.97), "spine_03": (0, 1.12), "neck": (0, 1.30),
            "head": (0, 1.36), "head_top": (0, 1.60),
            "shoulder_l": (0.28, 1.28), "elbow_l": (0.45, 1.00), "wrist_l": (0.50, 0.66), "handend_l": (0.50, 0.55),
            "shoulder_r": (-0.28, 1.28), "elbow_r": (-0.41, -0.09, 0.96), "wrist_r": (-0.33, -0.28, 1.18), "handend_r": (-0.32, -0.35, 1.26),
            **_legs((0.12, 0.62), (0.17, 0.32), (-0.17, 0.32), (0.17, 0.10), (-0.14, 0.10), (0.21, -0.20, 0.02), (-0.17, -0.20, 0.02)),
        },
        "tear": 2.5, "fill_holes": True, "arm_reach": 0.15,
        "rigid": [("hand_r", lambda p: not _ODO_SHOULDER(p) and not _ODO_HEAD(p) and _ODO_LOG(p)),
                  ("hand_r", lambda p: p.z > 1.55 and p.y > 0.12 and p.x > 0.0),
                  # arms: rigid along the bones (the left hangs against the belly, the right is folded against the chest)
                  ("lowerarm_r", _limb((-0.41, -0.09, 0.96), (-0.33, -0.28, 1.18), 0.09, 0.06)), ("upperarm_r", _limb((-0.28, -0.05, 1.28), (-0.41, -0.09, 0.96), 0.10, 0.10)),
                  ("hand_l", _limb((0.50, -0.02, 0.66), (0.50, -0.02, 0.50), 0.08, 0.02)), ("lowerarm_l", _limb((0.45, -0.02, 1.0), (0.50, -0.02, 0.66), 0.09, 0.06)),
                  ("upperarm_l", _limb((0.28, -0.05, 1.28), (0.45, -0.02, 1.0), 0.10, 0.10)),
                  # the hood's mantle over the upper arms stays on the chest (the arms come out from under it)
                  ("spine_03", lambda p: p.z > 1.08 and abs(p.x) > 0.2 and not (_ODO_ARM_R(p) or _ODO_ARM_L(p))),
                  # the hood's cape down the back: torso
                  ("spine_02", lambda p: p.y > 0.20 and 0.65 < p.z < 1.25), ("pelvis", lambda p: p.y > 0.20 and p.z <= 0.65)],
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

# Clipping and face review (2026-11-28): faces symmetrised (i3d_rig.symmetrise_face), keeping the better half.
LANDMARKS["brannoc"]["sym_face"] = {"keep": "r"}
LANDMARKS["aurex"]["sym_face"] = {"keep": "l"}
# Odo's frog hood hangs to his shoulders and his face was already clean: the head lock only drove the hood into them.
LANDMARKS["odo"]["head_lock"] = False
