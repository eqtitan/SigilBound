# Creature heroes

Heroes whose body is not the 65-joint humanoid. The first is Vessa Silkmourne (spider-folk, owner concept `docs/sigilbound/concepts/grok/vessa.jpg`).

## Build: `unity/Tools~/blenderforge/creature.py`

```
bpyenv/bin/python creature.py -- --design docs/sigilbound/concepts/creatures.json --hero vessa --out DIR
node compress-hd.mjs DIR && python3 compress-textures.py DIR      # then copy into Tools~/models/heroes_hd
bpyenv/bin/python creature_review.py -- DIR/vessa_creature.blend REVIEW turn still:Attack:13 battle:<other HD glb>
```

1. **Upper body:** built with `ubc.py` (parts, kit, species), then the humanoid legs are cut away (`cut_lower`).
2. **Creature rig:** bones are added under `root`, with the UBC `pelvis` re-parented beneath them (`add_bones`):
   - spider: `spider_body`, then `abdomen` and `abdomen_02`, plus eight legs `leg_<L1..R4>_coxa/_femur/_tibia/_tarsus`, each ending in a `_tip` marker bone.
3. **Modelled parts:** built with `tube`, `ellip` and the egg shaping, and bound rigidly (`bind_rigid`). Then decimated and baked through the HD paint pass (`ubc.bake`: AO, worn edges, top light).
4. **Clips:** the model's own clips, baked to FK keys (`bake_clips`):
   - The legs and body come from `creature_gait.py`: an alternating tetrapod gait with per-leg timing and analytic leg IK.
   - The upper body comes from the UAL clips in `models/ubc_anims.glb`, copied by bone name (`Source`), with spine and head offsets on top.
   - Vessa's clips: Idle_Loop, Walk_Loop, Turn, Attack, Attack2, Cast, Ult_Pounce, Hit, Death, Win, Stun_Loop.

## Build from an image-to-3D mesh: `creature_i3d.py` (Vessa, 2026-11-27)

Vessa now ships the owner's image-to-3D mesh (TRELLIS via fal.ai, from an approved front image) on the same rig and clips:

```
bpyenv/bin/python i3d_prep.py -- vessa.glb vessa DIR          # weld, drop debris, 1.85 m tall, feet at z 0
bpyenv/bin/python creature_i3d.py -- --hero vessa --clean DIR/vessa_clean.blend --out OUT [--blend OUT/vessa_rig.blend]
node compress-hd.mjs OUT && python3 compress-textures.py OUT   # then copy into Tools~/models/heroes_hd
```

1. **Leg layout from the mesh:** `LAYOUTS` holds each leg's hip, knee, ankle and foot read off the cleaned mesh (left side, mirrored). `creature_gait.configure` takes them as per-leg attach points, rest feet, segment lengths and tarsus leans, so the gait's rest pose is the mesh's own stance (max 6 cm off) and the legs are not reshaped.
2. **Upper body** as `i3d_rig.py`: UBC joints moved to landmarks, the arms posed into the mesh's pose, bone heat on the upper-body bones only, then posed back to the T-pose; faces stretched more than 2.5x by that move are dropped.
3. **Lower body rigid:** each leg vertex goes wholly to the nearest segment of the nearest leg (no smearing between segments or legs); the cephalothorax to `spider_body`, the abdomen to `abdomen`.
4. **Clips:** `creature.bake_clips` with an identity map (gait metres = model metres), so the model carries the same `GAME_CLIPS`. No `rest_fit` marker: the clips are baked for this skeleton.

Limits: one albedo from the generator (no normal or glow map); the braid bones are gone (the mesh's hair is skinned to the head and spine).

## Game: `Core/Kit/Creatures.cs` and `Client/CreatureRig.cs`

- **`CreatureSpec`** fields:
  - `Scale`: fixed; no head-bone fitting.
  - `ModelHeight`
  - `SlotWidth`: lineup spacing.
  - `MeleeReach`: kept between centres in melee.
  - `KnockDownHoldsHit`
  - `BodyBone`, `Legs` (bone chains with tips)
  - Step tuning
  - `Style` and `Aliases`: humanoid battle names mapped to the creature's own clips.
- **`HeroModels.BuildHd`** calls `CreatureRig.Setup` for a creature: it plays the model's own clips and adds `CreatureLegs`. `CreatureLegs` keeps the feet the clip puts on the ground planted while battle code moves the unit, steps in two alternating groups, and fades out on fast dashes.
- **`CreatureTests`** check that every creature is a hero, ships its model with the bones its spec names, and has every clip its style and aliases ask for.

## Adding a creature

1. Add a design to `creatures.json` (`"creature": "<rig>"`, optional `"ult_clip"`).
2. Write a `<rig>` layout in `creature_gait.py`: legs, rest spots, groups.
3. Write the rig and parts functions in `creature.py`.
4. Add a `CreatureSpec` to `Creatures.Table` and an `AnimStyle` with aliases in `HeroAnim.cs`.
5. Run `docs/sigilbound/sigfx.py`, `ultfx.py` and `armourclass.py`; they read `creatures.json`.
