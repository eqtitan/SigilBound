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
| APK size | Humanoid clips are muscle curves only (~70 clips); digitigrade baked leg clips go away at batch 7 |
| Phone performance | One Animator + a 4-input mixer + one job per hero; Humanoid evaluation is a native multi-threaded job. 10 units in battle is well within mobile budgets; measured in the smoke `SBBUDGET` lines; Low quality can skip the posture job |
| Mixed roster during migration | Per-hero flag; legacy path untouched |
| Code stripping on IL2CPP removes Animator/Director code used only from scripts | `link.xml` keeps the Animation and Director modules |

## 7. Proof (real builds)

ShotsMode's **pilot tour** (end of the smoke tour; `-sbShotsNoPilot` skips it): each pilot alone on the arena under
a battle camera (33 degrees up, three-quarter front), in ten states (idle, walk, run, attack, cast, ultimate, hit,
heavy hit, death, victory), frozen at six points of each clip, shot first on the legacy path and then on the
library: `pilot-<legacy|library>-<hero>-<state>-<k>.jpg` with an `SBPOSE` line each. Contact sheets and GIFs are
built from those real frames. Results are recorded below per build.

### Build log

(filled in from the real builds)
