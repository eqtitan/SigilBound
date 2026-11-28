# Physics standard: body colliders and spring chains for every hero

Owner, 2026-11-28: "Add colliders as a base standard for all models moving forward and game physics to everything;
start remediation of the existing models." This page is the standard. Lessons: L018 (where spring cloth fails),
L026 (this standard), L027 (classifier limits on fused image-to-3D meshes).

## 1. What every hero has

**A body collider set**, built at load for every high-detail hero (`HeroModels.BuildHd` adds `BodyColliders`),
defined in one place: `unity/Assets/Sigilbound/Core/Physics/ColliderSet.cs`.

| Shape | Bones (UBC biped names) | Group | Radius from |
|---|---|---|---|
| head (sphere) | `Head`, lifted along neck->head by `headUp` x r | Head | Head-weighted band above the head joint (hair/hats sit outside) |
| neck | `neck_01` -> `Head` | Neck | neck-weighted vertices |
| chest | `spine_02` -> `neck_01` | Torso | spine_02/03-weighted, 25th percentile |
| spine | `pelvis` -> `spine_02` | Torso | pelvis/spine_01/02-weighted, 25th percentile |
| hips | `thigh_l` -> `thigh_r` | Hips | pelvis-weighted band at the hip joints |
| upperarm_l/r | `upperarm` -> `lowerarm` | Arms | upper-arm-weighted |
| lowerarm_l/r | `lowerarm` -> `hand` | Forearms | forearm-weighted |
| thigh_l/r | `thigh` -> `calf` | Thighs | thigh-weighted |
| calf_l/r | `calf` -> `foot` | Calves | calf-weighted |
| tail (extra) | `tail_01` -> `tail_02` | Extra | 0.6 x calf |
| wing_l/r (extra) | `wing_l/r` sphere | Extra | 0.8 x upper arm |
| ground | holder y = 0 (a 1000 m sphere, `ChainKinds.Ground`) | Ground | - |

Radii are measured at build time from the hero's own mesh girth (30th percentile of the distance from the bone of the
vertices that bone dominates, middle of the segment only, so a robe or pauldron on the same bone sits outside it),
clamped to a band of the bone length, by `blenderforge/physics_classify.py --all`, which writes the generated
`Core/Physics/BodyColliderData.cs` (one line per hero). A hero without a line (a new model before it is classified)
gets the fallback ratios of its own bone lengths (medians over the shipped heroes). Creature rigs (`Creatures.Is`,
Vessa) have no humanoid body and are skipped.

**API (small, bone-name based - the skeleton library calls the same thing):**

```csharp
// Core (engine-free): pos = bone name -> model-space rest position, or null when missing
ColliderShape[] set = ColliderSet.Build(heroId, pos, missing /* optional List<string> */);
// Client: on the model holder, with the bones at rest (before the first clip)
var body = root.AddComponent<BodyColliders>(); body.Init(holder, heroId);
SCapsule c = body.Capsule(i, space);              // shape i in any space, current pose
if (SpringBones.Needed(holder)) root.AddComponent<SpringBones>().Init(holder, heroId, body);
```

For feat/skeleton-library: the rig profiles keep the UBC biped names, so a migrated hero needs exactly the three
Client lines above after its avatar/Animator is set up (at rest). `SpringBones` runs in `LateUpdate` at execution
order 200, after any animation job, and reads only transforms - it doesn't care whether `Animation` or an
`Animator`/Playables graph posed them. Profiles that add bones (tail, wings) get the extra colliders automatically.

## 2. Spring chains (what swings)

Chains are GLB nodes `sb_<k>_<i>` (last node = the tip), added in place to the shipped file (run **last**, after
`compress-hd.mjs`, which prunes empty leaf nodes). The chain number gives its kind (`ChainKinds.Of`):

| k | Kind | Collides with | Defaults (stiffness, drag, gravity, joint r, swing limit) |
|---|---|---|---|
| 0-19 | skirt / robe panel / coat tail | hips, thighs, calves, ground | 1.5, 0.45, 0, 3 cm, - |
| 20-29 | cape | torso, hips, thighs, calves, extras (no ground: lying on it squeezed it) | 2.2, 0.55, 0.3, 2.5 cm, 20 deg |
| 30-34 | tail | hips, thighs, calves, ground | 2.0, 0.35, 0, 4 cm, - |
| 35-39 | soft wing | torso, arms, head | 3.0, 0.5, 0, 3 cm, - |
| 40-49 | hair (long hair, braids, ponytails) | head, neck, torso, upper arms, ground | 2.5, 0.5, 0.1, 2 cm, - |
| 50-54 | ear | head | 4.0, 0.6, 0, 1.5 cm, - |
| 55-69 | flap (sash, belt flap, tassel, pouch, banner, chain) | hips, thighs, ground | 1.5, 0.5, 0.2, 2 cm, - |

Per-hero settings override the defaults in `Core/Kit/SpringRigs.cs` (`["id:kind"]`, then `["id"]`). Each kind runs
its own solver (`Core/SpringSolver.cs`, after UniVRM's VRMSpringBone, MIT, credited in `Models/CREDITS.md`).
The swing limit (`SpringRig.MaxAngle`, per joint from its animated rest direction) stops a clip whose root teleports
(a dash ult) from whipping a cape round the body.

**Rigid, never physics:** weapons, shields, staffs, crowns, helmets, hats, halos. The classifier lists them as RIGID
(islands on a hand, off a forearm, or on the head above the skull); `blenderforge/prop_rigid.py` binds props 100% to
their bone. Head-gear seating is fix/ctd-headgear's job.

## 3. Build-time pipeline (every new hero, part of rigging)

1. Rig the hero as usual (`i3d_rig.py` / `ubc.py` + `compress-hd.mjs`).
2. `python3 unity/Tools~/blenderforge/physics_classify.py heroes_hd/SB_Hero_<id>_HD.glb` - prints the measured radii
   and the classified parts (kind, vertex count, bounds, a no-physics severity = depth of the part inside the body
   over key battle frames). Then regenerate the data for everyone:
   `python3 physics_classify.py --all ../models/heroes_hd OUT.json --colliders-cs ../../Assets/Sigilbound/Core/Physics/BodyColliderData.cs`.
3. Decide which kinds swing: add `"<id>": {"chains": ["cape"]}` to `unity/Tools~/models/physics/overrides.json`
   (per kind optional `K` chains across, `J` bones, `anchor`, `top`).
4. `python3 physics_rig.py SRC.glb DST.glb <id>` into a scratch folder (never straight over the shipped file).
5. Frame sweep before/after (character-td rules, every 2nd frame of every clip, battle camera both sides, menu,
   face) with the preview mirror of this runtime (scratchpad `physics/tools/physim.py` + `sweep.py --spring`).
   Verdict SHIP / SHIP WITH NOTE / REVERT; any frame worse = REVERT. Only then copy DST over the shipped GLB.
6. Gates (prepare-models, Tests~ "All checks passed", CompileCheck, APK) and a real build (L004).

Classifier limits (L027): kit heroes (separate cloth islands) classify cleanly; fused image-to-3D meshes give noisy
parts (armour as skirt, robes as tails) - treat their proposals as hints and check the colour view
(scratchpad `physics/tools/partview.py`). Skirts and gowns on chains still follow L018.

## 4. Budget and quality

- At most `ChainKinds.MaxJointsPerHero` = 48 spring joints per hero (physics_rig refuses more; tests check every
  shipped hero), `ChainKinds.BattleBudget` = 480 simulated joints per battle: heroes enabled after the budget is
  full run kinematic. Measured (PhysicsTests): a 48-joint hero with 17 colliders ~21 us per frame on the CI machine,
  a full battle at budget ~0.2 ms.
- Low quality: kinematic (no swing, no history, cloth still pushed out of the body and above the ground).
- Batch 1 capes: 12-15 joints per hero.

## 5. Tests (`unity/Tests~/PhysicsTests.cs`, `SpringTests.cs`)

Body set built for every shipped humanoid hero (no missing bones, radii inside the bands of their bone lengths,
measured data line), chains well formed and in budget, every chain joint at rest outside its kind's colliders,
determinism, ground, cost budget, Low-quality behaviour, and the preview/game solver parity fixture.

## 6. Remediation log

Worst-first by the classifier's severity; skipped: Neve (other TD), Lazlo and Holt (being remodelled), the skeleton
pilot heroes (Titan, Ysolde, Augustin, Corran, Nyssa). Proof per hero: scratchpad `physics/<hero>/` (before/after
sweeps, review sheets, `before_after.jpg`, `motion.gif`), summary `physics/summary.json`.

| Batch | Heroes | Result |
|---|---|---|
| 1 | kit capes: wick, nocturne, imogen, sprig, aurelia, tallow, dunstan, kaija, hilde, bastian | see `physics/summary.json` |
