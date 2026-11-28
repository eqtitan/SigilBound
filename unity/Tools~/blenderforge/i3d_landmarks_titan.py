"""Joint landmarks for the Titan's image-to-3D mesh (TRELLIS via fal.ai, cleaned by i3d_prep.py), rigged by
i3d_rig.py to the UBC skeleton the Titan's battle clips use. Same format as i3d_landmarks.py: 1.85 m tall, feet at
z 0, facing -Y, the character's right is -X; a joint is (x, z) or (x, y, z).

Design (owner, 2026-11-27): a hunched basalt colossus, very long arms with huge fists at the knees, short legs,
crystal ridges on the back and shoulders, an open molten chest core, a small horned head. The hands end at the
fists (wrist above the fist, hand bone through it), not mid-forearm. The glow mask covers the orange seams and the
core, and the albedo under it is neutralised so the game's per-element glow colour (Flame orange, Tide cyan, Gale
green, Light gold, Shadow violet) is the seam colour."""


def _legs(hip, knee_l, knee_r, ank_l, ank_r, toe_l, toe_r):
    return {"hip_l": (hip[0], hip[1]), "hip_r": (-hip[0], hip[1]), "knee_l": knee_l, "knee_r": knee_r,
            "ankle_l": ank_l, "ankle_r": ank_r, "toe_l": toe_l, "toe_r": toe_r}


LANDMARKS = {
    "titan": {
        "joints": {
            "pelvis": (0, 0.60), "spine_01": (0, 0.75), "spine_02": (0, 0.92), "spine_03": (0, 1.12),
            "neck": (0, -0.05, 1.38), "head": (0, -0.22, 1.42), "head_top": (0, -0.28, 1.64),
            "shoulder_l": (0.52, 0.10, 1.32), "elbow_l": (0.70, 0.12, 0.92), "wrist_l": (0.75, 0.07, 0.50), "handend_l": (0.76, 0.05, 0.14),
            "shoulder_r": (-0.52, 0.10, 1.32), "elbow_r": (-0.70, 0.12, 0.92), "wrist_r": (-0.75, 0.07, 0.50), "handend_r": (-0.76, 0.05, 0.14),
            **_legs((0.18, 0.55), (0.27, 0.30), (-0.27, 0.30), (0.30, 0.08), (-0.30, 0.08), (0.33, -0.12, 0.02), (-0.33, -0.12, 0.02)),
        },
        "tear": 2.5, "fill_holes": True, "arm_reach": 0.22,
        "rigid": [  # the fists are single rocks; the back and shoulder crystal ridges ride the upper spine
                  ("hand_l", lambda p: p.x > 0.46 and p.z < 0.50), ("hand_r", lambda p: p.x < -0.46 and p.z < 0.50),
                  ("spine_03", lambda p: p.y > 0.24 and p.z > 0.85 and abs(p.x) < 0.45),
                  ("spine_03", lambda p: p.z > 1.42 and 0.35 < abs(p.x) < 0.66)],
        "glow_hue": (8, 48), "glow_sv": (110, 150), "glow_dilate": 3, "glow_neutral": True,
    },
}
