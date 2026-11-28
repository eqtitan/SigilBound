# Skeleton library and Humanoid retargeting

Owner decision, 2026-11-28: *"I'm disappointed that we cannot have full range of motion on all skeletons; it has to be
skeletons we can use with some modifications to make each unique and carbon based."* Rebuild approved. Pilot heroes:
the Titan, Ysolde (human), Augustin (lion beast-folk), Corran (goat, animal legs), Nyssa (moth wings). Animations come
from free CC0 libraries plus our own (no Mixamo).

## In short (for the owner)

- **What was wrong.** Every hero was forced onto one human skeleton, and every animation was copied onto it bone by
  bone. Wherever a hero differed (longer arms, animal legs, a hunched giant), we patched it with a correction rule.
  The rules fought each other: stretched sheets, the exploded Tansy and Corran (build 180), the mangled Titan
  (build 190).
- **What we are building instead.** A small library of complete, standard skeletons: a **biped**, a **biped with
  animal legs**, a **brute** (long arms, short legs: the Titan, golems), a **many-legged** body (Vessa), and add-on
  chains (tails, wings, ears, hair, cloth) that clip onto any of them. Each hero is one of these skeletons with its
  own proportions.
- **How the animations reach every hero.** Unity's *Humanoid* system describes a pose as muscle movements ("bend
  the elbow 60%", "lift the arm") instead of bone angles. A move made once on our reference skeleton is turned into
  muscle movements while the game is built, and each hero's own skeleton plays those muscles with its own limb
  lengths. No correction rules.
- **How each hero stays unique.** A small profile per hero: which skeleton, a walking style (heavy, light,
  prowling, regal), joint limits, add-on chains, and its signature moves. The style bends the posture (a heavy
  hero carries weight low and forward with arms off the body; a regal one walks chest up, shoulders back), so two
  heroes playing the same clip still move like different creatures.
- **Physics on everything.** Every skeleton carries a standard set of body colliders (head, neck, chest, spine,
  hips, arms, thighs, calves) sized from the hero's own bones, physics chains for whatever should swing (hair, capes,
  skirts, tails, ears, soft wings...) and fixed sockets for weapons, shields, back gear and headgear.
- **More life, planned in phases.** Proper hand grips and two-handed holds (no more weapons through heads), separate
  accessory meshes from the image generator, twist bones and basic faces (blink, look, four expressions), and life
  layers (heads tracking the acting unit, directional hit reactions, breathing, a cheap partial ragdoll on
  knock-backs and deaths). Every hero's every move is checked automatically in CI.
- **Safety.** It is switched on per hero. The other heroes keep today's path until their batch is checked frame by
  frame in a real build. If anything is missing at runtime, a migrated hero falls back to today's path.
- **Proof.** Real game builds, with before and after frames of the same moves for each pilot hero (below, once the
  build is in).

## 1. Today (what this replaces)

| Piece | What it does | Why it breaks |
|---|---|---|
| One 65-joint UBC skeleton for every hero | Clips play by bone name with the legacy `Animation` component | A clip only fits the proportions it was made on |
| `RigExtras` | Re-expresses each clip relative to a hero's moved rest pose (`D*q`, `T1 + D*(p - T0)`) | Assumes a clip rewrote every bone each frame; compounded when it didn't (L002, L003) |
| `DigitigradeLegs` | Layers baked per-hero leg clips over the shared ones | Name collisions with glTFast's auto-added clips exploded two heroes (build 180) |
| `AnimStyle.HdOverrides`, aliases | A gentler clip per hero where a gown or prop can't follow | Shrinks the move set instead of fixing the motion |
| Embedded Titan clips | Its own moves on its own rest, plus the rest-fix path | Mangled Titan (build 190, L003) |

## 2. Architecture

```
Blender (reference rigs, CC0 + own clips)           CI build (game-ci runs the Unity editor)                 Phone
 ubc_anims.glb  (shared clips, reference biped)  ->  HumanoidClipBaker: GLB -> reference skeleton ->  ->   hero GLB (glTFast)
 SB_Hero_titan_HD.glb (signature moves, brute)       AvatarBuilder -> HumanPoseHandler per frame         -> HumanoidRig: avatar from its own
                                                     -> Humanoid .anim (muscle curves)                       skeleton + Animator + PlayableGraph
                                                     -> Resources/HumanoidClips/<set>/                    -> posture job, hock, flutter
                                                                                                          -> SpringBones, HandGrip, TitanBody
```

### 2.1 The skeleton library

| Skeleton | Human bones mapped (Unity Humanoid) | Extra (non-human) bones | Who |
|---|---|---|---|
| **Biped** | Hips, Spine, Chest, UpperChest, Neck, Head; Shoulder/UpperArm/LowerArm/Hand; UpperLeg/LowerLeg/Foot/Toes; 30 finger bones (optional) | leaf markers, sockets | humans, elves, gnomes, beast-folk with plantigrade legs |
| **Biped, digitigrade** | as Biped; `Foot` is the hoof/paw | `meta_l/r` (metatarsal) between calf and foot, folded after the clip by the knee's bend (`HockFold`) | goat-, hare-folk (Corran, Tansy) |
| **Brute** | as Biped, long arms, short legs | sockets (`chest_core`, `fist_*`), breakable crystals | the Titan, golems |
| **Multi-leg** | upper body as Biped | eight leg chains, body/abdomen; own gait (`creature_gait.py`, `CreatureRig`) | Vessa (stays on its creature rig in the pilot) |
| **Add-on chains** | never human bones | tail, wings, ears, hair, cloth (`sb_*` spring nodes or flutter bones) | any skeleton |

Why the UBC joint set is the library biped: every one of the 158 high-detail heroes is already rigged on it
(checked: 158 of 158 have `pelvis`, the UBC limb and finger names), it is a complete Mecanim-compatible set
(`HumanoidMap` maps all 52 biped + finger bones with every bone under its human parent), and every rest pose is a
T-pose (the avatar takes the rest pose as its T-pose; `RigLibraryTests` checks it per hero). The library variants
are the UBC biped plus the documented extra bones; new rigs (Blender, `i3d_rig.py`, `blenderforge`) must keep the
same names.

Natural joint limits: Unity's own Humanoid muscle ranges (anatomical degrees per muscle) plus per-skeleton clamps
in muscle space (normalized -1..1; `RigProfiles.BaseLimits`: a brute's stiff trunk and short legs that can't split,
less ankle roll on a digitigrade leg, neck and head turn for all).

### 2.2 Data model: the per-hero rig profile (`Core/Rig/RigProfiles.cs`)

| Field | Meaning | Example |
|---|---|---|
| `Skeleton` | Library skeleton | `Brute` |
| `Gait` | Walking style: muscle offsets + tempo (`RigProfiles.Gait`) | `Heavy`: spine and chest forward, arms off the body, knees soft, tempo 0.82 |
| `GaitAmount` | Scales the style for this hero | Augustin 1.2 (prouder) |
| `Limits` | Muscle clamps (natural range per hero) | Titan spine twist ±0.55 |
| `Addons` | Chains on the standard bones | Nyssa: `wing_l`, `wing_r` flutter 14 degrees at 1.4 Hz |
| `HockFold` | Digitigrade: metatarsal fold per degree of knee bend | Corran 0.6 |
| `ClipSets` | Baked clip sets, searched in order | Titan: `titan` (its own moves), then `biped` |
| `Bones` | Override of the automatic human-bone mapping | (none needed in the pilot) |

`RigProfiles.Migrated(id)` is the per-hero flag. `RigProfiles.ForceLegacy` turns every hero back to the old path
(used by the before/after tour).

### 2.3 Pipeline: Blender -> GLB -> Unity editor (Humanoid) during the CI build

1. **Author** clips on the reference rig in Blender (the UBC biped; the brute for brute-only moves). Export GLB:
   one root node `Armature`, metres, Y up, +Z front. Shared clips live in `unity/Tools~/models/ubc_anims.glb`;
   signature moves in the hero's own GLB (the Titan's 11 `Titan_*` clips from `titan_moves.py`).
2. **Bake** (`Editor/HumanoidClipBaker.cs`, first thing in every `BuildScript` build, so game-ci's editor does it):
   reads each source (`Core/Rig/GlbAnim.cs`, plain C#), rebuilds the reference skeleton as GameObjects with
   glTFast's axis conversion, builds its Humanoid avatar (`AvatarBuilder.BuildHumanAvatar`, mapping from
   `HumanoidMap`), samples each clip at 30 fps, reads the human pose (`HumanPoseHandler.GetHumanPose`: 95 muscles,
   body position and rotation) and writes a Humanoid `AnimationClip` (muscle curves on `Animator`, finger muscles
   spelled `LeftHand.Index.1 Stretched`; root motion baked into the pose, so clips stay in place as authored).
   It then plays each baked clip back on the reference avatar and logs hands/feet/head error (`SBHUMAN` lines in
   the build log). Output: `Assets/Resources/HumanoidClips/<set>/` (generated, git-ignored).
   Why not FBX + ModelImporter: it would need the clips re-exported to FBX (axis, scale and leaf-bone conversions)
   and a second source of truth; baking from the same GLBs the game ships keeps one source and one conversion
   (glTFast's), and needs no extra package.
3. **Ship**: the clips are ordinary assets in the player (Humanoid clips are small: muscle curves only).

### 2.4 Runtime (`Client/HumanoidRig.cs`)

- **Avatar per hero** from its own skeleton at load (glTFast instance; `AvatarBuilder` with the GLB rest pose as the
  T-pose). Proportions, rest orientations and limb lengths are absorbed by the avatar: nothing to re-express.
- **Playback: an `Animator` driven by a `PlayableGraph`** (no `AnimatorController`). Why: controllers can't be
  created at runtime (UnityEditor.Animations), and one prebuilt controller per hero/clip-set is a maintenance
  burden; the battle code already asks for clips by name with its own fades, loops, holds, reversed playback and
  queued idles (`HeroModel`), which map one to one onto a 4-input `AnimationMixerPlayable`. Time is set by hand
  each frame (`DirectorUpdateMode.Manual`), so hit-stop, battle speed, idle loops at 1x, and frozen frames for
  screenshots are exact. Works with glTFast-loaded models because the avatar is built from the instance.
- **Posture job** (`AnimationScriptPlayable` + `IAnimationJob`, built into Unity): adds the gait's muscle offsets
  (full on idle, ready and locomotion; partly during actions) and clamps the profile's joint limits.
- **After the clip**: digitigrade hock fold, wing/ear flutter; then the existing `SpringBones` (spring chains for
  cloth, tails, hair), `HandGrip`, `TitanBody`.
- **IK**: the baked clips carry no IK goals, so clip foot IK is off. Foot grounding, look-at and hand grip go in a
  second animation job using `AnimationHumanStream` goals (`SetGoalPosition`, `SetLookAtPosition`, `SolveIK`) -
  built into Unity, no package. The Animation Rigging package (free) is an alternative, but adds a package the
  compile checks would need stubs for; deferred until a hero needs two-bone constraints beyond Humanoid IK.
- **Retired for migrated heroes**: `RigExtras`, `DigitigradeLegs` (and their baked leg clips), per-hero clip
  overrides. Tests check they are never added with a humanoid rig.

## 3. Per-hero modifications ("unique and carbon based")

| Hero | Skeleton | Gait | Modifications |
|---|---|---|---|
| Ysolde | Biped | light | quick feet, upright chest |
| Augustin | Biped | regal x1.2 | chest up, shoulders back and down, measured steps (tempo 0.9) |
| Corran | Biped, digitigrade | light x0.8 | metatarsal folds against the knee (0.6), less ankle roll |
| Nyssa | Biped + wings | light | wings flutter, faster and wider in actions |
| Titan | Brute | heavy x0.8 | its own moves first; stiff trunk, short legs that can't split, slow tread (0.82) |

Next steps per hero (after the pilot): blend-tree style variants (two locomotion clips mixed by a style weight)
once the library has heavy and light walk cycles; spring chains on capes, tails and hair from rig-time landmarks.

## 4. Animation library

### 4.1 What we have (CC0 / own)

| Set | Clips | Source |
|---|---|---|
| UAL (Standard) | 46: idles (4), walk/formal walk/jog/sprint, crouch walk/idle, jump start/loop/land, roll (+RM), punch jab/cross/enter, sword idle/attack (+RM), spell enter/idle/shoot/exit, pistol aim/idle/shoot/reload, hit chest/head, death, interact, pick-up, push, sitting (4), swim (2), dance, driving, fixing | Quaternius Universal Animation Library, CC0 1.0 |
| Own (make_clips.py) | Defeat_Slump, Spear_Thrust, Hit_Kneel | project |
| Own ultimates | Ult_LeapSlam, Whirlwind, Overhead, DashStrike, Uppercut, GroundPound, Channel, Volley, Twirl, Blessing, TwinFlurry | project |
| Titan (titan_moves.py) | Titan_Idle, Walk, Roar, Slam, Sweep, Leap, Land, Breath, Hit, HitBig, Collapse | project |

### 4.2 Gaps for full range of motion

| Gap | Needed for | Plan |
|---|---|---|
| Turn in place (L/R 90, 180) | facing targets without sliding | UAL 2 (check tier and licence) or own |
| Strafe / back-step / side-step | dodge, repositioning | own on the reference biped |
| Heavy hits (gut, knock-back, knock-down + get-up) | big hits, Break | own: Hit_Kneel exists; add KnockDown, GetUp |
| Kneel / get up, revive | revives, defeat | own |
| Block / parry loop | Defense role | own |
| Two-handed weapon set (idle, 3 swings, heavy) | greatswords, hammers, polearms | own (grip on both hands via hand IK) |
| Bow draw / release | bows (today pistol clips) | own |
| Brute set: knuckle-walk, two-fist slam, leap, roar, grab, stomp | Titan, golems | Titan has slam, leap, roar; add knuckle-walk, stomp, grab on the brute |
| Quadruped / creature set | Vessa-like heroes | creature_gait (procedural) |

Sourcing: Quaternius Universal Animation Library 2 and other Quaternius packs are the first candidates, but their
licence and tier must be confirmed on quaternius.com / itch before import (quaternius.com is blocked in this agent's
network; the current UAL was taken from a GitHub mirror that carries the pack's CC0 LICENSE, and CREDITS.md asks
for a re-download before a store release). No Mixamo. Every new clip goes into `CREDITS.md` with its source.
Own clips are authored on the reference biped (or the brute) in Blender and baked like the rest.

## 5. Migration plan (158 heroes)

All 158 high-detail heroes already use the library biped's joint names, so migration is a profile entry plus a
frame-by-frame check in a real build (character-td rules), not a re-rig - except where a hero needs extra bones.

| Batch | Heroes | Work | Effort |
|---|---|---|---|
| 0 Pilot | Titan, Ysolde, Augustin, Corran, Nyssa | pipeline, runtime, tour | done in this branch |
| 1 Humans, kit heroes | ~60 plain bipeds | profile (gait), tour check, drop overrides | 1 day + 1 build |
| 2 Image-to-3D with moved joints (`rest_fit`, 38) | beast-folk, elves, gnomes | profile; watch hands/fingers and shoulders | 1-2 days + 2 builds |
| 3 Gowns, robes, capes (the `HdOverrides` list, kaelith, wystan...) | ~15 | spring chains instead of gentler clips; may need re-weighting | 2-3 days |
| 4 Animal legs, tails, wings (tansy, talvo, vorgrim, ...) | ~6 | digitigrade/add-on chains from rig-time landmarks | 1-2 days |
| 5 Brutes and golems | ~6 | brute profile; brute clip set | 1 day + clips |
| 6 Creatures (Vessa) | 1 | upper body Humanoid, legs stay procedural | 1 day |
| 7 Retire the legacy path | - | remove RigExtras, DigitigradeLegs, baked leg clips (APK -0.6 MB/digitigrade hero), overrides | 0.5 day |

Each batch: profile entries, gates, one dispatch, contact sheets, owner approval, merge. About 2 weeks of calendar
time with builds as the bottleneck (one CI build at a time, ~1 hour).

## 6. Risks

| Risk | Mitigation |
|---|---|
| glTFast + Humanoid at runtime: avatar build fails on a skeleton | Same checks as AvatarBuilder offline (`RigLibraryTests`); runtime falls back to legacy and logs `SBMODEL <id>: rig LEGACY` |
| Baked clips wrong (curve names, root handling) | Editor round-trip check logs error per clip; the game falls back if no clip set loads; real-build tour judges frames |
| Muscle offsets sign (front/back) wrong for a gait | small values, checked in the tour frames; table is data |
| APK size | 70 Humanoid clips = 6.4 MB uncompressed (keyed every frame; keyframe reduction can roughly halve it); APK 172.7 MB in run 208; digitigrade baked leg clips go away at batch 7 |
| Phone performance | One Animator + a 4-input mixer + one job per hero; Humanoid evaluation is a native multi-threaded job. 10 units in battle is well within mobile budgets; measured in the smoke `SBBUDGET` lines; Low quality can skip the posture job |
| Mixed roster during migration | Per-hero flag; legacy path untouched |
| Code stripping on IL2CPP removes Animator/Director code used only from scripts | `link.xml` keeps the Animation and Director modules |

## 7. Owner additions (2026-11-28): design and phasing

### 7.1 Physics standard: colliders and chains on every rig (in the pilot)

`Core/Rig/RigPhysics.cs` + `Client/BodyPhysics.cs`, bone-name based so it merges with the shared physics API
(feat/physics-standard, `Core/Physics/ColliderSet.cs`, `docs/sigilbound/art/PHYSICS.md`; when that lands the
profile's collider and chain lists feed it instead of BodyPhysics' own build, same names).
- **Body collider set** on every profile: head (sphere), neck, chest, spine, hips, upper/lower arms, thighs, calves
  (a digitigrade calf ends at the hock). Capsules from bone to child bone, radius = a fraction of that bone's length
  x skeleton girth (brute 1.45) x the hero's girth (Augustin 1.15, Titan 1.1). Unity `CapsuleCollider`s on the bones,
  triggers on one kinematic body: effects, hits and picks can query them, nothing gets pushed.
- **Chains** for everything that should move: hair/braids, capes, skirts, sashes, tails, ears, chains, pouches,
  banners, soft wings. Simulated by the existing `SpringSolver` (after UniVRM, MIT) with the body colliders as
  obstacles; the swing is added on top of whatever the bone holds (a clip key, a flutter), with a last-write guard so
  nothing compounds (L002). Low quality: kinematic.
- **Rigid binding** for weapons, shields, headgear (`RigidSpec`; geometry bound 100% by `blenderforge/prop_rigid.py`).
- Pilot: all five get the body set; Nyssa's wings are soft-wing chains on top of their flutter; the Titan's rock fists
  and crystal ridges are rigid. The pilot meshes have no hair/cape bones yet: those chains need rig-time bones
  (accessory meshes, 7.3, or `spring_rig.py`).

### 7.2 Sockets, grips, two-hand IK (sockets in the pilot; grips and IK next)

- **Standard sockets** on every rig (`RigPhysics.StandardSockets`, created as `socket_<name>` nodes): `hand_r`,
  `hand_l`, `back`, `hip_l`, `hip_r`, `shield_arm`, `head_top`. Items attach to sockets, never to raw mesh weights:
  no floating props or headgear.
- **Grip poses** per weapon type (`GripPose`: fist, one-hand sword, two-hand haft, staff, bow, open cast hand) as
  finger muscle values (Humanoid finger muscles, a hand mask layer in the mixer) replacing `HandGrip`'s single curl.
- **Off-hand IK** onto two-handed weapons: an animation job sets the off hand's IK goal to a grip point on the weapon
  (`AnimationHumanStream.SetGoalPosition/Rotation` + `SolveIK`); per-weapon grip offsets in data (socket offset +
  grip points). Weapons through heads (Tallow, Saoirse, Hobb, Hask): a swing-clearance check in 7.6 plus the weapon's
  collider kept out of the head/chest colliders by the same job (push the hand goal out), and two-hand clips for
  polearms.

### 7.3 Separate accessory meshes (pipeline change; proof on one pilot in stage 2)

Image-to-3D today fuses clothes, props and body into one shell; that is why props carry leg weights and capes can't
swing. New flow (`unity/Tools~/artgen`, `.claude/skills/art-pipeline/SKILL.md`):
1. Front image of the **base body without accessories** (prompt template: same character, no hat, no weapon, no
   shield, no cape, arms in T-pose, plain background).
2. One image per **accessory** (hat/helmet/crown, weapon, shield, cape, back gear), single object, plain background,
   matching the concept's design.
3. TRELLIS per image -> separate meshes. The body is rigged on the library skeleton; rigid items are snapped to their
   socket (`head_top`, `hand_r`, `shield_arm`, `back`); cloth items (capes) get a chain at rig time and are weighted
   onto it.
Tool: `unity/Tools~/blenderforge/accessory_attach.py` (one mesh, one atlas: body albedo on the left two thirds, up to
two accessory textures in the right third, so the HD loader is unchanged).

**Pilot, Augustin (2026-11-28, Blender previews only, not shipped, owner review pending).** Inputs: 4 Gemini images
(two base bodies, mace, cape; body B picked for its leg gap) + 3 TRELLIS models = 22 US cents. Body rigged as
`augustin_acc` (`i3d_landmarks_portrait.py`, tail rigid on the pelvis), 10.8k tris; with mace and cape 17.0k tris,
91 bones (66 + 5x5 cape chain nodes), atlas 1536x1024, GLB 2.7 MB. Findings:
- The mace is 100% on `hand_r` (every exported mace vertex) and stays in the fist in every clip checked (idle,
  sword attack, overhead ult, walk, hit): the props-follow-the-legs failure is gone by construction.
- The cape hangs from `spine_03` on 5 chains x 4 bones, top band blended into the spine, pushed off the back where
  it met the body. TRELLIS modelled the cape **worn by an invented mannequin** (a whole torso and legs inside) and
  as a back view; the tool now finds the cape's outer side (the smoothest first-hit surface) and drops every face
  hidden more than 2 cm behind it, and the cape prompt no longer says "as if hanging from the shoulders".
- Still open: the cape's inner side shows dark specks where mannequin texture stayed near the edges; the tail pokes
  out under the cape (it's part of the body mesh); the body itself has the usual image-to-3D armpit/shoulder tears
  (an i3d_rig landmark issue, not an accessory one). Swing is only visible in game (SpringBones), so the next CI
  run with this model is the real check; needs a `SpringRigs` entry and the `RigProfiles` rigid/chain lines.
Frames: scratchpad `acc_pilot/augustin_acc_compare.jpg`, `rev_sheet3.jpg`.

**Augustin wired in (owner approved the parts approach, 2026-11-28; branch feat/augustin-parts).** The shipped
`SB_Hero_augustin_HD.glb` is now the parts build: `i3d_rig` body (`augustin_acc` landmarks) + `accessory_attach.py
... mace.glb:hand_r cape.glb:back:chain --hem 0.65`, then `compress-hd.mjs` and `compress-textures.py`; 24.4k tris,
91 joints, 2.9 MB. The old `_mat.png` metal map no longer matches the atlas and is dropped (index_mat.txt too).
`SpringRigs["augustin"]` drives the cape (stiffness 1.2, drag 0.4, thigh/calf capsules, a hip sphere for the tail).
Fixes after the review:
- Dark specks inside the cape: no face-culling rule removed every scrap of the invented wearer, so the cape is now
  rebuilt as a sheet: a 44x76 grid of rays from behind samples the outer surface (depth + texture UV), a fresh grid
  mesh is built from the hits (outline relaxed), and the inside is the same sheet flipped 4 mm in, painted plain in
  the cape's own colour (the hero shader culls back faces, so the cloth needs both sides).
- Tail through and under the cape: the cape drapes over everything behind the body (a smooth outward offset carried
  down below obstacles, not a per-vertex push) and the hem is lowered to 65% of knee-to-ankle, so the tail stays
  under the cloth seen from behind; it still shows beside the right leg from the side and front, as a tail should.
Game frames come with the next build of this branch (cape swing is only visible in game).

### 7.4 Twist bones and facial basics (phase 2)

- Biped gains `upperarm_twist`, `lowerarm_twist` (Unity's twist distribution already spreads twist when they are
  missing; real bones give volume) and helper joints at shoulder, hip, elbow and knee (driven by the parent's bend,
  for volume preservation). Not human bones: the avatar ignores them, a small driver sets them after the clip.
- Facial layer: blink, eyes look at a target (the acting unit), four expressions (fierce, hurt, victory, KO) as blend
  shapes on image-to-3D faces where the mesh allows, else jaw/brow/eyelid face bones; driven by battle events
  (strike -> fierce, hit -> hurt, win -> victory, death -> KO).

### 7.5 Life layers and partial ragdoll (phase 2)

- Head/eye tracking of the acting unit (look-at goal in the IK job, weight by distance and angle).
- Directional additive hit reactions: a small additive pose from the hit direction (layer in the mixer), on top of
  the hit clip.
- Lean into turns (spine roll from the turn rate), breathing (chest/shoulder muscles, phase per hero).
- Partial ragdoll on knock-back and death: the body colliders become dynamic rigidbodies joined by character joints
  for about a second, blended with the clip (physics weight curve), then back to animation or held. Only at those
  moments; off on Low.

### 7.6 Per-move CI checks (phase 1.5)

An editor batch step in the CI build (the same place the clips are baked) plays every migrated hero x every clip it
can play on its runtime avatar (sampled every 2nd frame) and reports lines like SBBOUNDS:
`SBMOVE <hero> <clip> limits=<worst muscle over its range> slide=<cm a planted foot moves> intersect=<worst
limb/prop penetration of the body colliders, cm> ok|BAD <why>`; the smoke check fails CI on BAD or a regression
against the last accepted numbers (`docs/sigilbound/art/move_baseline.json`). The pilot tour's `SBPOSE` lines are the
first step (head height, top, width per frozen frame in the real player).

### 7.7 Phasing

| Phase | Content | Gate |
|---|---|---|
| 1 (this branch) | library, Humanoid bake + runtime, profiles, gait, limits, physics standard (colliders, chains, sockets), 5 pilots, pilot tour | real-build frames, owner approval |
| 1.5 | per-move CI checks (7.6), grip poses + off-hand IK (7.2), batch 1 migration | SBMOVE clean on migrated heroes |
| 2 | separate accessory meshes (7.3) on one pilot, then the image-to-3D roster as remodels come; twist/helper bones and faces (7.4) | frame-by-frame review |
| 3 | life layers + partial ragdoll (7.5), batches 2-6 | performance on the phone check |
| 4 | retire the legacy path | all heroes migrated |

## 8. Proof (real builds)

ShotsMode's **pilot tour** (end of the smoke tour; `-sbShotsNoPilot` skips it): each pilot alone on the arena under
a battle camera (33 degrees up, three-quarter front), in ten states (idle, walk, run, attack, cast, ultimate, hit,
heavy hit, death, victory), frozen at six points of each clip, shot first on the legacy path and then on the
library: `pilot-<legacy|library>-<hero>-<state>-<k>.jpg` with an `SBPOSE` line each. Contact sheets and GIFs are
built from those real frames. Results are recorded below per build.

### Build log

- **Run 208** (commit 4045b0a): the editor baked 70 Humanoid clips (59 shared + 11 Titan; 6.4 MB uncompressed in
  Resources); all five pilots loaded as `rig humanoid` in the player (52 human bones each; Corran
  BipedDigitigrade, Titan Brute), no exceptions, smoke passed, every gallery `SBBOUNDS` ok, APK 172.7 MB (budget 190).
  Gallery and Titan-fight shots show the pilots on the new path (scratchpad `skel_pilot/stage1/`). The pilot tour
  itself photographed the still-running Titan practice fight instead of the pilots (L033); fixed in ba8fde8.
- **Run 211** (ba8fde8, with the physics standard and the tour fix): cancelled to free the queue for main.
- **Run 46121700973** (cf9e479, stage 2): Android and Windows built; the smoke tour ran to the end (`DONE 750`, all
  `SBBUDGET`/`SBBOUNDS` ok, no exceptions) and failed only on the new per-move gate (below). The pilot tour now
  shoots the pilots themselves: 660 frames, five before/after sheets and GIFs (scratchpad `skel_pilot/stage2/`).
  - Motion: the library matches the legacy path state for state (same clips through the avatar), with one clear
    gain: Augustin's death falls in place on the library, where the legacy path dropped him out of frame. The
    mace stays in the fist through attack and physics on both paths.
  - Physics: frame 1 of each library physics row shows the body colliders (head, chest, spine, hips, arms, legs)
    sitting on the body for all five, the Titan's around its oversized arms.
  - SBMOVE, first real run: 306 hero-clip pairs, 272 BAD. 269 are "joint past its range" and not real: idles flag
    too (max |muscle| median 1.84, max 3.34), so the limit was miscalibrated; the check now looks at body muscles only
    (finger curls of the grip poses excluded), names the worst muscle and flags above 1.5; the next run confirms it.
    3 are real candidates: right forearm into the chest in Augustin `Ult_Volley` (8.7 cm), Titan `Titan_Slam`
    (9.5 cm) and `Titan_Sweep` (21 cm); to look at frame by frame before fixing or listing them in move_baseline.txt.
    Foot sliding stays under 4.2 cm in every stance clip (limit 10).
  - The three limb-through-body cases, frame by frame: **Titan_Sweep fixed** in `titan_moves.py` (the follow-through
    now ends out in front and the return to idle loops forward; the right forearm was 21 cm into the chest at the old
    end pose and on the way back, now 6.5 cm by the offline check, events and impact window unchanged); **Titan_Slam
    accepted** (the hunch over the fists around the overhead slam, not visible at battle distance); **Augustin
    Ult_Volley accepted** (never played by Augustin, whose ult is Ult_Blessing). Both accepts are in move_baseline.txt.
