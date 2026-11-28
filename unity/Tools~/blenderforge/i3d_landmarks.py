"""Joint landmarks for the image-to-3D heroes (i3d_rig.py), read off orthographic front/side views of the
cleaned meshes (1.85 m tall, feet at z 0, facing -Y; the character's right is -X).
A joint is (x, z): y is the centre of the first solid layer from the front, or (x, y, z) to pin it.
rigid: (bone, test(p)) - vertices passing the test go wholly to that bone (held weapons, a halo), or to the nearest
tail bone ("nearest_tail"). trim: tests for faces to delete (generator guesses). tail: points for tail_01.. bones.
glow_hue: hue range (degrees) of bright saturated texels baked into the glow mask."""


def _near_line(a, b, r):
    """Test: within r of the segment a-b (a held staff or spear)."""
    from mathutils import Vector
    A, B = Vector(a), Vector(b)
    def t(p):
        ab = B - A; u = max(0.0, min(1.0, (p - A).dot(ab) / ab.length_squared))
        return (p - (A + ab * u)).length < r
    return t


def _legs(hip, knee_l, knee_r, ank_l, ank_r, toe_l, toe_r):
    return {"hip_l": (hip[0], hip[1]), "hip_r": (-hip[0], hip[1]), "knee_l": knee_l, "knee_r": knee_r,
            "ankle_l": ank_l, "ankle_r": ank_r, "toe_l": toe_l, "toe_r": toe_r}


LANDMARKS = {
    "vorgrim": {
        "joints": {
            "pelvis": (0, 0.92), "spine_01": (0, 1.02), "spine_02": (0, 1.14), "spine_03": (0, 1.27), "neck": (0, 1.47),
            "head": (0, 1.56), "head_top": (0, 1.74),
            "shoulder_l": (0.27, 1.36), "elbow_l": (0.42, 1.10), "wrist_l": (0.47, 0.88), "handend_l": (0.49, 0.80),
            "shoulder_r": (-0.27, 1.36), "elbow_r": (-0.40, 1.10), "wrist_r": (-0.47, -0.10, 0.90), "handend_r": (-0.50, -0.12, 0.82),
            **_legs((0.13, 0.90), (0.27, 0.46), (-0.24, 0.46), (0.33, 0.10), (-0.30, 0.10), (0.34, -0.20, 0.03), (-0.30, -0.20, 0.03)),
        },
        "rigid": [("hand_r", lambda p: p.x < -0.50 and p.z < 1.0), ("hand_r", lambda p: p.y < -0.30 and p.z < 0.85 and p.x < 0.0)],
        "glow_hue": (8, 45),
    },
    "talvo": {
        "joints": {
            "pelvis": (0, 0.95), "spine_01": (0, 1.05), "spine_02": (0, 1.17), "spine_03": (0, 1.30), "neck": (0, 1.48),
            "head": (0, 1.56), "head_top": (0, 1.74),
            "shoulder_l": (0.22, 1.42), "elbow_l": (0.30, 1.12), "wrist_l": (0.33, 0.86), "handend_l": (0.33, 0.77),
            "shoulder_r": (-0.22, 1.42), "elbow_r": (-0.40, 1.12), "wrist_r": (-0.48, 0.86), "handend_r": (-0.50, 0.78),
            **_legs((0.12, 0.92), (0.21, 0.50), (-0.18, 0.50), (0.22, 0.10), (-0.20, 0.10), (0.24, -0.20, 0.03), (-0.22, -0.20, 0.03)),
        },
        "tail": [(0.0, 0.15, 0.85), (0.05, 0.30, 0.62), (0.15, 0.45, 0.38), (0.30, 0.55, 0.25), (0.50, 0.62, 0.30), (0.65, 0.68, 0.48), (0.80, 0.75, 0.65)],
        "trim": [lambda p: p.z < 0.30 and p.x < -0.45, lambda p: p.z < 0.30 and p.y < -0.30, lambda p: p.z < 0.20 and p.y > 0.45],
        "rigid": [("hand_r", lambda p: p.x < -0.52), ("nearest_tail", lambda p: p.y > 0.22 and p.z < 0.90),
                  ("nearest_tail", lambda p: p.x > 0.38 and p.z < 0.75)],
        "glow_hue": (8, 45),
    },
    "wystan": {
        "joints": {
            "pelvis": (0, 0.85), "spine_01": (0, 0.95), "spine_02": (0, 1.07), "spine_03": (0, 1.20), "neck": (0, 1.44),
            "head": (0, 1.50), "head_top": (0, 1.78),
            "shoulder_l": (0.25, 1.33), "elbow_l": (0.38, 1.05), "wrist_l": (0.28, 0.85), "handend_l": (0.24, 0.80),
            "shoulder_r": (-0.25, 1.33), "elbow_r": (-0.36, 1.08), "wrist_r": (-0.36, -0.15, 0.95), "handend_r": (-0.36, -0.20, 0.88),
            **_legs((0.12, 0.80), (0.15, 0.42), (-0.15, 0.42), (0.24, 0.10), (-0.22, 0.10), (0.28, -0.15, 0.02), (-0.25, -0.15, 0.02)),
        },
        # staff in the right hand; the wide wing-sleeves swing with the forearms (no stretching between arm and robe)
        "tear": 3.5,
        "rigid": [("hand_r", lambda p: -0.47 < p.x < -0.27 and p.y < -0.19),
                  ("hand_r", lambda p: p.z > 0.04 and _near_line((-0.33, -0.16, 0.0), (-0.43, -0.27, 1.05), 0.05)(p)),
                  ("hand_r", _near_line((-0.43, -0.27, 1.05), (-0.50, -0.45, 1.50), 0.09)),
                  ("lowerarm_r", lambda p: p.x < -0.36 and 0.45 < p.z < 1.15), ("lowerarm_l", lambda p: p.x > 0.36 and 0.45 < p.z < 1.15)],
        "glow_hue": (400, 400),
    },
    # fal.ai TRELLIS from the owner's new front image (2026-11-27): the trident came out level, pointing forward
    # through the right claw (2.3 m long, so its butt behind the hand is cut back); the body sits ~0.17 m in front of y 0
    "crusk": {
        "joints": {
            "pelvis": (0, 0.66), "spine_01": (0, 0.80), "spine_02": (0, 0.95), "spine_03": (0, 1.10), "neck": (0, 1.30),
            "head": (0, 1.40), "head_top": (0, 1.66),
            "shoulder_l": (0.40, 1.22), "elbow_l": (0.55, -0.05, 0.95), "wrist_l": (0.57, -0.25, 0.72), "handend_l": (0.52, -0.40, 0.55),
            "shoulder_r": (-0.40, 1.22), "elbow_r": (-0.58, -0.05, 0.95), "wrist_r": (-0.62, -0.10, 0.70), "handend_r": (-0.58, -0.15, 0.50),
            **_legs((0.15, 0.62), (0.25, 0.40), (-0.25, 0.40), (0.27, 0.10), (-0.27, 0.10), (0.40, -0.38, 0.02), (-0.36, -0.38, 0.02)),
        },
        "trim": [lambda p: p.y > 0.45 and p.x < -0.45 and 0.6 < p.z < 0.95],
        "tear": 2.5, "fill_holes": True,  # 3.5+ leaves stretched sheets; 2.0 opens bigger holes
        "rigid": [  # trident and claw to the right hand, claw to the left hand, nautilus shield and the vambrace to the forearms
                  ("hand_r", _near_line((-0.6, -1.2, 0.53), (-0.6, 0.6, 0.75), 0.075)),
                  ("hand_r", lambda p: p.y < -0.70 and p.x < -0.35 and p.z < 0.72),
                  ("hand_r", lambda p: p.x < -0.44 and 0.30 < p.z < 0.75 and p.y < 0.15),
                  ("hand_l", lambda p: p.x > 0.44 and 0.45 < p.z < 0.80),
                  ("lowerarm_l", lambda p: p.x > 0.50 and 0.80 <= p.z < 1.20),
                  ("lowerarm_r", lambda p: p.x < -0.50 and 0.75 <= p.z < 1.12)],
        "glow_hue": (170, 215),
    },
    # fal.ai TRELLIS from the owner's front image (2026-11-27): the moth wings are rigid on wing bones on the upper
    # back (RigExtras flutters them about the vertical hinge), daggers rigid in the hands
    "nyssa": {
        "joints": {
            "pelvis": (0, 0.93), "spine_01": (0, 1.02), "spine_02": (0, 1.13), "spine_03": (0, 1.25), "neck": (0, 1.44),
            "head": (0, 1.49), "head_top": (0, 1.66),
            "shoulder_l": (0.15, 1.36), "elbow_l": (0.22, 1.10), "wrist_l": (0.25, -0.02, 0.88), "handend_l": (0.26, -0.03, 0.80),
            "shoulder_r": (-0.15, 1.36), "elbow_r": (-0.22, 1.10), "wrist_r": (-0.25, -0.04, 0.88), "handend_r": (-0.26, -0.06, 0.80),
            **_legs((0.09, 0.90), (0.10, 0.48), (-0.14, 0.48), (0.11, 0.10), (-0.19, 0.10), (0.26, -0.02, 0.02), (-0.27, -0.17, 0.02)),
        },
        "tear": 2.5, "fill_holes": True,  # 3.5 leaves a stretched sheet at the left hip (belt to forearm)
        "extra_bones": [("wing_l", "spine_02", (0.07, 0.10, 1.20), (0.07, 0.10, 1.40)),
                        ("wing_r", "spine_02", (-0.07, 0.10, 1.20), (-0.07, 0.10, 1.40))],
        "rigid": [("hand_r", lambda p: p.x < -0.20 and 0.64 < p.z < 0.87 and p.y < 0.07),
                  ("hand_l", lambda p: p.x > 0.20 and 0.64 < p.z < 0.87 and p.y < 0.07),
                  ("wing_l", lambda p: p.x > 0 and 0.74 < p.z < 1.43 and ((p.x >= 0.27 and p.y > 0.07) or (p.x > 0.13 and p.y > 0.11))),
                  ("wing_r", lambda p: p.x < 0 and 0.74 < p.z < 1.43 and ((p.x <= -0.27 and p.y > 0.07) or (p.x < -0.13 and p.y > 0.11))),
                  ("Head", lambda p: p.z > 1.62)],
        "glow_hue": (400, 400),
    },
    # regenerated by the owner from a halo-free front image (2026-11-27); halo added as a ring, cape back recoloured
    "hartwin": {
        "joints": {
            "pelvis": (0, 0.92), "spine_01": (0, 1.00), "spine_02": (0, 1.10), "spine_03": (0, 1.22), "neck": (0, 1.37),
            "head": (0, 1.42), "head_top": (0, 1.56),
            "shoulder_l": (0.27, 1.27), "elbow_l": (0.36, 1.00), "wrist_l": (0.42, 0.78), "handend_l": (0.42, 0.70),
            "shoulder_r": (-0.27, 1.27), "elbow_r": (-0.33, 1.00), "wrist_r": (-0.35, 0.78), "handend_r": (-0.33, 0.70),
            **_legs((0.11, 0.88), (0.15, 0.48), (-0.11, 0.48), (0.20, 0.10), (-0.13, 0.10), (0.22, -0.15, 0.02), (-0.15, -0.15, 0.02)),
        },
        "rigid": [("hand_r", lambda p: p.x < -0.33 and p.z < 0.88 and p.y < 0.22), ("hand_r", lambda p: p.x < -0.24 and p.z < 0.70 and p.y < -0.05),
                  ("Head", lambda p: p.y > 0.22 and p.z > 1.40),
                  # the cape hangs from the shoulders: torso, not arms or legs (no stretching between them)
                  ("spine_02", lambda p: p.y > 0.16 and 0.65 < p.z < 1.25), ("pelvis", lambda p: p.y > 0.16 and p.z <= 0.65),
                  ("spine_02", lambda p: p.x > 0.47 and p.z < 0.85)],
        "halo": {"center": (0.0, 0.28, 1.60), "radius": 0.17, "minor": 0.012, "color": (255, 214, 110)},
        "recolor": [
            # the cape's back was guessed black; the concept's is white with a gold edge
            {"faces": lambda c, n: n.y > 0.3 and c.y > 0.05 and 0.08 < c.z < 1.32 and abs(c.x) < 0.45,
             "dark": 0.32, "color": (236, 226, 205), "edge_color": (212, 170, 70)},
            # dark antler tips toward the gold of the antler bases
            {"faces": lambda c, n: c.z > 1.55, "dark": 0.36, "color": (198, 152, 82)},
        ],
        "glow_hue": (400, 400),
    },
}
