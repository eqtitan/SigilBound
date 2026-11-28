# Rigging notes

How the high-detail heroes are rigged and animated, and the exceptions to the humanoid rig.

## The humanoid rig (every hero)

- One skeleton for everyone: the UBC 65-joint humanoid (`blenderforge/ubc_parts.py`), plantigrade legs
  `thigh > calf > foot > ball`. Single root node `Armature` (glTFast then makes no Scene object and the
  clip paths resolve from the holder; `HdClipFileTests`).
- One clip library for everyone: `models/ubc_anims.glb` (59 clips: the CC0 UAL clips retargeted, three
  hand-made ones, the signature ultimates). Battle code plays them by name (`AnimStyle.Hd`, `HdAliases`).
- Image-to-3D heroes (`blenderforge/i3d_rig.py`): the UBC joints are moved to the mesh's landmarks
  (`i3d_landmarks*.py`), the mesh is skinned in its own pose and moved to the rest pose. The game's
  `RigExtras` re-expresses each shared clip relative to the hero's rest (rotation `D*q`, position
  `T1 + D*(p - T0)`), as Blender does when it previews the same clips.
- Kit creature extras (`kit_rig.py`, main branch): tail chains, jaws, longer necks, wing bones, driven
  procedurally by `RigExtras`.

## Digitigrade legs (animal-folk; owner report 2026-11-28)

"Some legs had knees for humans but not the animal they represent." A hare or a goat stands on its toes:
a short thigh, a shin slanting back, a long raised metatarsal ending in a hock that points backwards,
then the toes or hoof. With the humanoid rig the shin and the metatarsal were one rigid bone, so the
hock never folded: the hero crouched and knelt like a human on stilts.

### Which heroes

Checked from the portraits, the concept images (`gemini/batch/*_1.png`), the TRELLIS meshes and the
roster species, and in walk, run, crouch, attack and hit frames from the side at the battle camera's
elevation:

| Rank | Hero | Species | Legs in portrait and mesh | Result |
|---|---|---|---|---|
| 1 | `tansy` | hare-folk | long hind feet, hock high and back | digitigrade rig |
| 2 | `corran` | goat-folk | hocked legs over cloven-hoof boots | digitigrade rig |
| - | `nettle` | fox-folk (hedge-runner) | tall boots, straight legs | humanoid, unchanged |
| - | `fennick` | cat-folk | short legs in boots, paw feet | humanoid, unchanged |
| - | `rook` | raven-folk | trousers and boots, talon toes | humanoid, unchanged |
| - | `augustin` | lion-folk | full armour greaves, paw sabatons | humanoid, unchanged |
| - | `lune` | owl-folk | floor-length robe, talons at the hem | humanoid, unchanged |
| - | `holt` | bear-folk | bears are plantigrade | humanoid, unchanged |
| - | `jasper`, `perpetua`, `wenna`, `lazlo`, `odo`, `brannoc` | fox, cat, turtle, shark, frog hood, boar pelt | coat and boots, gown, plantigrade | humanoid, unchanged |

The other 130-odd heroes are humans, elves, gnomes, golems, spirits and kit-built bodies with human legs.
The kit concepts that ask for "digitigrade foot covers" (holt, tansy, corran, augustin in
`concepts/NEW_HEROES.md`) are now built from their portraits (image-to-3D), so the table above covers them.

### The approach

Researched options (free for a free game):

- **Rigify** (Blender, GPL): its `limbs.paw` / `limbs.rear_paw` leg types are a three-segment leg with an
  IK chain and a metatarsal held at an angle. Only the concept is used; no Rigify code is copied.
- **Runtime IK** (a two-bone solver plus a metatarsal offset in `LateUpdate`): works with any clip, but
  would run on the phone for every animal hero and must match the Blender previews by hand.
- **Baked FK (chosen)**: solve offline, once per clip, and ship plain keyframes. The game needs no IK,
  the previews are the shipped data, and the extra data is small (about 0.63 MB per hero raw, 0.3 MB gzipped in
  the APK).

Implementation, all self-written project code:

1. **Skeleton** (`i3d_legs.rest`): the leg becomes `thigh > calf > meta > foot > ball` (femur, tibia, the
   new metatarsal bone `meta_l`/`meta_r`, the toes in two parts). The rest pose is the mesh's own leg,
   swung about the hip in the side view so the ball of the foot sits over the humanoid ball (a stepping leg
   in the mesh does not become the stance). The mesh is skinned with bone heat as before, so fur, cloth and
   boots around the hock follow the new bone; `split_limbs` treats `meta_` as leg.
2. **Clips** (`i3d_legs.bake`): each shared clip is played on a virtual plantigrade leg: the UBC
   reference leg scaled to the hero's hip height, i.e. the proportions the clips' hip heights and foot
   plants were made for. Per key, the digitigrade leg is solved to it:
   - the ball of the foot and the hock ride the humanoid foot (planted when it is planted), the toes ride
     the humanoid toe bone;
   - the hock folds toward the ground by 0.85 x the humanoid ankle's bend (crouch, kneel) and lifts when
     the clip stretches the leg further than femur + tibia (heel rise);
   - thigh and calf are a two-bone IK from the clip's hip to the hock, the knee on the humanoid knee's side;
   - bone lengths never change (if the hock falls short, the foot follows; the bake reports how far: up to
     ~5 cm on full strides and rolls, 0 on stances).
   The result is keyed as FK (rotation per key at 30 per second, translation as two constant keys) on the
   ten leg bones and written into the hero's GLB, one animation per shared clip with the same name and
   length.
3. **Game** (`Client/DigitigradeLegs.cs`): for a model with `meta_l`/`meta_r`, each shared clip state gets
   a twin `<name>~legs` on layer 1, limited to the leg chains (`AddMixingTransform` on the thighs), copying
   the shared state's enabled flag, weight, time, speed and wrap mode every frame. Legacy Animation gives
   the higher layer its weight first, so the legs follow the baked clip and the body the shared one through
   cross-fades, queued clips, hit-stop and battle speed. `RigExtras` skips the leg bones of these heroes.
   Clip names, aliases and battle code are unchanged; a clip without a baked twin falls back to the
   shared legs.

### Runtime pitfall (build 180)

glTFast adds an `Animation` with a file's own clips to the object it instantiates under (for the high-detail
files, the holder). A digitigrade hero's GLB carries its baked leg clips under the shared clip names, and
`HeroModels.BuildHd` only adds a shared clip whose name is free (so an embedded Titan keeps its own). The two
together left the leg-only clips in the shared clips' place: no curve moved the body, and `RigExtras`, which
assumes a clip rewrote each bone every frame, re-applied its rest fix to its own output (`D*q`, `T1 + D*(p-T0)`
compounding: Corran's pelvis orbited 3 m up, Tansy's arms and head spun into a sheet). Now `BuildHd` clears the
auto-added clips first (`ClearClips`), `RigExtras` leaves alone a bone that still holds what it wrote last frame,
and the smoke tour's gallery logs `SBBOUNDS` per hero (Head bone height and offset, skinned bounds, in units of
the figure's height), which `tools/unity-smoke-check.sh` fails on. Offline renders of the clips alone cannot see
this class of bug: check a real build's gallery.

### Adding a hero

In the hero's landmark entry (`i3d_landmarks_portrait.py`), wrap it with `_digitigrade(...)` and give, per
side, `knee`, `hock`, `ball` and `toe` as `(x, y, z)` in the mesh's own pose (read them off a side view of
each leg with a 5 cm grid). Then rebuild as usual:

    bpyenv/bin/python i3d_rig.py -- --hero ID --clean ID_clean.blend --out OUT
    node models/compress-hd.mjs OUT        # then copy SB_Hero_ID_HD.glb into models/heroes_hd

`i3d_rig.py` prints `digitigrade legs, 59 clips baked` and one line per clip. `DigitigradeTests` checks
the metatarsal chain, a baked clip for every shared clip with the same length, and keys on all ten leg
bones and nothing else; add the hero to its `Expected` list.

### Merging `i3d_rig.py`

The change is three small hooks that call `i3d_legs.py` behind the `"legs": "digitigrade"` flag (canon
joints after `orig`, `rest()` after the leg bones are placed, the leg aims, `meta` in the leg tag lists,
`bake()` after the export). Heroes without the flag build exactly as before. Copy `i3d_rig.py` and
`i3d_legs.py` together to `feat/new-heroes-6`, `feat/titan-model` and `main`.

### Proof

Before/after side views at the battle camera's elevation (walk, crouch, crouch idle, kneel, jog) and walk
and crouch GIFs per hero, plus a contact sheet: scratchpad `reviews/legs/`.

## Spring bones and per-hero clip overrides (character TD pilot, 2026-11-28)

Owner report on the earlier clipping pass: "some of those look good in some frames but horrible in others". The
pilot replaced weight rules with tools that hold on every frame, and checked every 2nd frame of every clip each hero
plays (battle camera at 33 degrees from both sides, a menu front view and a face close-up; scratchpad `ctd/`).

**Spring bones** (`Core/SpringSolver.cs`, `Client/SpringBones.cs`, `Core/Kit/SpringRigs.cs`; algorithm after
UniVRM's VRMSpringBone, MIT, credited in `Models/CREDITS.md`):
- Chains are GLB nodes `sb_<chain>_<bone>` (last node = tip), added in place to the shipped file by
  `blenderforge/spring_rig.py` (no rebuild, nothing else changes). Run it **last**: `compress-hd.mjs` prunes empty
  leaf nodes, which would drop the chain tips (`SpringTests` catches that).
- The cloth is found by a layer test (a ray from the leg bone to the vertex hits nothing first = the leg's own
  surface: trousers, boots; anything with another surface in between = robe/coat), the legs are re-skinned by
  projection onto thigh/calf/foot, and the cloth goes onto a ring of chains round the pelvis (angular blend between
  two chains, tent weights along them).
- Runtime: fixed 1/60 s steps in the model holder's space (after the clip, `RigExtras` and `DigitigradeLegs`),
  capsule colliders on thighs and calves (optional feet, hip), at most 4 catch-up steps, a long gap resets to rest.
  Low quality: kinematic (rest direction pushed out of the legs once per frame, no swing). About 10 us per hero
  per frame on a desktop core for 36 joints.
- The preview solver (scratchpad `ctd/tools/springsim.py`) mirrors the C# line by line; `SpringTests` replays its
  scripted run through the game solver (worst quaternion gap 6e-8), plus determinism, colliders, Low mode, reset,
  catch-up and cost.

**Clip overrides** (`AnimStyle.HdOverrides`, `HdAliasesFor`): a hero whose gown, robe or prop can't follow a shared
clip plays a gentler one: kaelith and wystan move with `Walk_Formal_Loop` and take heavy hits with `Hit_Head`
(wystan also idles with `Idle_Loop`), hask strikes with `Sword_Attack` only.

**Shield slide** (`blenderforge/shield_slide.py`): the clipping review's slide toward the fist, capped (hask 20 cm;
uncapped it was 33 cm and left the tower shield held by its top edge).

What did not work, so other heroes don't repeat it: spring chains on a full kit gown (kaelith: holes in the idle,
legs out of the skirt in Death01); chains on image-to-3D meshes whose cloth carries arm/hand weights from bone heat
(wystan's robe, vorgrim's cape on the right upper arm and tail on the right thigh) - those need a re-rig of the
upper body first. Results per hero: scratchpad `ctd/summary.json`.
