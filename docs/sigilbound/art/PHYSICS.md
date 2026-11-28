# Physics standard: body colliders and spring chains for every hero

Owner, 2026-11-28: "Add colliders as a base standard for all models moving forward and game physics to everything;
start remediation of the existing models." This page is the standard. Lessons: L018 (where spring cloth fails),
L033 (this standard and its tuning), L034 (classifier limits on fused image-to-3D meshes).

## 1. What every hero has

**The library's body collider set** (feat/skeleton-library: `Core/Rig/RigPhysics.BodySet`, `Client/BodyPhysics.cs` -
one standard, bone-name based). Library rigs get it from their profile; every legacy hero gets it at load too
(`HeroModels.BuildHd`: `BodyPhysics.Init(holder, RigProfiles.Legacy(id))` - a plain biped profile, no chains).
Shapes: head (sphere on `Head`), neck, chest (`spine_03`->`neck_01`), spine (`spine_01`->`spine_03`), hips
(`pelvis`->`spine_01`), upper/lower arms, thighs, calves - capsules on the bones (triggers on a kinematic body, so
hits and effects can query them too).

**Sizes from each hero's own girth (extension, this branch).** The library sizes a collider as fraction x bone length
x profile girth. `blenderforge/physics_classify.py --all` measures every shipped hero (30th percentile of the
distance from the bone of the vertices that bone dominates, middle of the segment, so a robe or pauldron sits outside
it; hips 15th, since gowns are pelvis-weighted), clamped to 0.5-1.8x the library size, into the generated
`Core/Rig/BodyColliderData.cs`; `RigPhysics.Radius(heroId, spec, len, girth)` returns the measured radius when the hero
has a line, else the library's own size (so nothing changes for a rig without data). Creature rigs (Vessa) are skipped.

**Extensions to the library API (compatible; say so when merging):** `RigPhysics.Radius`, `RigPhysics.RefLength`,
`RigPhysics.Group` + `BodyGroup` (collider groups), `RigProfiles.Legacy(id)`, `Core/Rig/RigChains.cs` (`ChainKinds`),
`SpringRig.MaxAngle` / `Mask` / `ThighR`... overrides, `BodyColliderData`. `RigLibraryTests`' legacy-block marker now
ends at the `BodyPhysics` line.

## 2. Spring chains (what swings)

Library rigs list chains by bone name (`ChainSpec`, simulated by `BodyPhysics`). Legacy heroes carry them in the GLB:
nodes `sb_<k>_<i>` (last node = the tip), added in place to the shipped file (run **last**, after `compress-hd.mjs`,
which prunes empty leaf nodes), simulated by `Client/SpringBones.cs` against the same `BodyPhysics` bodies.
The chain number gives the library's `ChainKind` (`ChainKinds.Of`, `Core/Rig/RigChains.cs`):

| k | Kind | Collides with | Defaults (stiffness, drag, gravity, joint r, swing limit) |
|---|---|---|---|
| 0-19 | Skirt (robe panel, coat tail) | hips, thighs, calves, ground | 1.5, 0.45, 0, 3 cm, - |
| 20-29 | Cape | torso, hips, thighs, calves (no ground: lying on it squeezed it, L033) | 2.2, 0.55, 0.3, 2.5 cm, 20 deg |
| 30-34 | Tail | hips, thighs, calves, ground | 2.0, 0.35, 0, 4 cm, - |
| 35-39 | SoftWings | torso, arms, head | 3.0, 0.5, 0, 3 cm, - |
| 40-49 | Hair | head, neck, torso, upper arms, ground | 2.5, 0.5, 0.1, 2 cm, 35 deg |
| 50-54 | Ears | head | 4.0, 0.6, 0, 1.5 cm, 25 deg |
| 55-59 / 60-63 / 64-66 | Sash / Pouch / Chain | hips, thighs, ground | 1.5, 0.5, 0.2, 2 cm, 40 deg |
| 67-69 | Banner | torso, hips | same |

Per-hero settings override the defaults in `Core/Kit/SpringRigs.cs` (`["id:kind"]`, then `["id"]`). Each kind runs
its own solver (`Core/SpringSolver.cs`, after UniVRM's VRMSpringBone, MIT, credited in `Models/CREDITS.md`).
The swing limit (`SpringRig.MaxAngle`, per joint from its animated rest direction) stops a clip whose root teleports
(a dash ult) from whipping a cape round the body.

**Rigid, never physics:** weapons, shields, staffs, crowns, helmets, hats, halos (`RigidSpec` in the library). The
classifier lists them as RIGID; `blenderforge/prop_rigid.py` binds props 100% to their bone. Head-gear seating is
fix/ctd-headgear's job.

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

Classifier limits (L034): kit heroes (separate cloth islands) classify cleanly; fused image-to-3D meshes give noisy
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
