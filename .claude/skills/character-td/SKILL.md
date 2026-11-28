---
name: character-td
description: The character TD's living playbook for Sigilbound hero models - review checklist, failure-mode catalogue with detectors, tool guide (what each blenderforge patch tool is for), what worked and what was rejected, and the owner's preferences. Load FIRST at the start of any hero model / rig / clipping / face / accessory / material task; APPEND what you learned at the end.
---

# Character TD playbook (load first, append last)

Role: `.claude/agents/character-td.md`. Brain: `docs/sigilbound/brain/` (L004, L008, L010, L018, L019, L022-L026).
**Rule:** read this file before touching a hero; before you report, add what you learned (new failure mode, a tool
result, an owner preference) to the sections below with the date and branch. Keep entries one or two lines.

## 1. Review checklist (every hero, every change)
1. Prepare: work on your own branch; `cd unity/Tools~/models && node prepare-models.mjs` before tests (L001).
2. Sweep **every 2nd frame of every clip the hero plays** (idle/ready, strikes, walk/run as aliased, cast, support,
   ult, hit, big hit, death, defeat, victory, fidgets, block) - the list comes from `AnimStyle.Hd` + `HdOverrides`.
   Views: battle camera both sides (el 33, az +-70), menu 3/4 (az 20, el 8), face close-up, and a
   **head TOP + SIDE check** (az 90 and el 85) for headgear and hair.
3. Look at the sheets yourself. Numbers only find candidates (L008). One worse frame = not fixed.
4. Compare before/after on the same frames; sheets in `$S/ctd/<hero>/before_after.jpg` + `summary*.json`.
5. Verdict per hero: SHIP / SHIP WITH NOTE (name the frame) / REVERT.
6. Prove visual fixes in a real build (CI smoke gallery; workflow_dispatch on your branch, wait - L004, L005).
   Previews must feed the game's material inputs (mask defaults included - L023).
7. Gates: last test line "All checks passed.", CompileCheck 0 errors, APK < 190 MB; brain lessons updated.

## 2. Failure modes and how to detect them
| Failure | Detect | Usual cause | Fix |
|---|---|---|---|
| Cloth split by the legs (robes, coats, gowns) | stretch metric (faces > 1.7x rest edge), Jog/Sword_Attack/Hit_Kneel frames | cloth skinned to the nearest thigh | spring coat + leg re-skin only if the cloth hangs free (L018); else clip override or remodel |
| Legs through a kit gown | front view, Jog/Hit_Kneel | kit leg-following skirt weights | clip override: Run=Walk_Formal_Loop, BigHit=Hit_Head |
| Prop stretching / bending with the arm or legs | audit.py stretch99 of hand/forearm pieces | blended weights (fingers, upper arm, bone heat to legs) | prop_rigid.py |
| Prop lying on the floor / pointing at the next hero | gallery shot; the prop's PCA axis vs forearm | i3d prop held mid-haft, pointing forward | prop_turn.py |
| Weapon through the head | headhit.py (prop verts inside a head sphere per frame) | Punch_Cross / Punch_Enter with long props | HdOverrides Strike=Sword_Attack, NoBlock |
| Shield through helmet/arm | shieldmetric | shield centred on the forearm | shield_slide.py (cap 20 cm) + prop_rigid |
| Floating head gear | head FRONT/SIDE/BACK/TOP close-ups WITH back-face culling + labelled cross-sections (ubc.py SB_HEADDUMP, ctd2/tools/xsec.py, hgrow.py); real build gallery | single-sheet hat crowns culled (L026); hats sized by a bbox-centred ellipse; rings fitted to one hair height / high on the dome; full hair under hats | kit: REBUILD with kit_head.py seating (compress_hair, seat/seat_zone, crown_height, _crown_out, clear_hover HALO_GAP) + post.sh re-patches; i3d: sockets (L025) |
| Gear/hair shown inside a closed piece | opensheets.py on the head dump | lathe sheet faces point inward | orient outward or solidify (L026) |
| Face warp / face slides on the head | face drift vs Head bone; face close-up frames | i3d faces weighted to neck/clavicles; Head-lock on a neck-weighted head slides (L024) | face_lock.py (tight mask; "avg" mode for i3d) |
| Lower layer showing through (shirt under armour) | detect.py inner-face metric | layered kit pieces | usually leave (tiny at battle distance) |
| Dark, metallic, sparkling hero | game render / maskproof | no _glow and no _mat map -> default white mask (L023) | neutral mask in HeroModels (shipped on fix/ctd-roster-3) |
| Metal reads as cloth | close-ups of blades/rims | mask metal 0 everywhere | hd_materials.py _mat maps + shader metal reflection |
| Specks floating off i3d meshes | component scan (comps.py), gallery close-up | TRELLIS islands | mesh_fix.py islands (check each: some pieces are intentional) - but first rule out L023 |
| Holes from behind | rimsim.py (back faces) | inside-out patches, thin shells | winding fix tried, rejected (exposes unpainted insides); open |
| Mirrored mesh (prop in the wrong hand) | front grid of the prep render | generator mirrored the image | flip x and recentre before rigging (rend.py "mirror", "shift") |
| Tail glued to a leg / pelvis | domcol.py dominant-bone colours | bone heat | needs rig-time tail landmarks (spring chains on these tore) |

## 3. Tools (unity/Tools~/blenderforge unless noted; all patch the shipped GLB in place unless noted)
- `glb_io.py` - read/skin/pose shipped GLBs (numpy), write accessors (incl. normalized), save.
- `prop_rigid.py SRC DST` - bind hand/shield-forearm pieces 100% to their bone.
- `prop_turn.py SRC DST BONE [GRIP]` - turn a long prop to run along the forearm.
- `mesh_fix.py SRC DST [--orient] [--no-islands]` - remove small loose pieces; winding fix is opt-in (rejected).
- `hd_materials.py DIR [ids]` - `_mat.png` metal/smoothness for steel/gold prop texels + kit helms/crowns (hair-coloured skipped) + `index_mat.txt`.
- `spring_rig.py SRC DST HERO` - spring chains (skirt ring, cape, tail) + leg re-skin; run LAST (compress-hd prunes tips). Only Jasper ships.
- `shield_slide.py SRC DST MAX`, `face_lock.py face_lock.json SRC DST [ids]` (faces branch), `hg_seat.py SRC DST HERO` (headgear, superseded by the kit rebuild).
- Kit rebuild of a hero (headgear pass 2): `ubc.py` (SB_HEADDUMP=DIR dumps labelled head triangles, SB_HEADONLY=1 stops after the head pass, 2-5 s) then
  scratchpad `ctd2/post.sh OUT STAGE ids` = compress-hd + compress-textures + the in-place patches the shipped kit GLBs carry
  (prop_rigid for dunstan gorrum hask hobb ivo merrit oriel oswin sable tallow, shield_slide 0.20 hask, face_lock for face_lock.json ids, hd_materials).
  A fresh build reproduces the shipped base exactly (checked Dunstan, Quill); always re-run those patches or they are lost.
- Runtime: `Core/SpringSolver.cs`, `Client/SpringBones.cs`, `Core/Kit/SpringRigs.cs`, `AnimStyle.HdOverrides`, `HeroModels.MaskFromGlow`, shader "metal reflection" block.
- Headgear review (scratchpad `ctd2/tools/`): `xsec.py`/`xsec_lib.py` (labelled sections), `hgrow.py` (renders+sections), `combo.py` (owner faces+headgear sheet: battle cam + framed close-ups + motion),
  `hgsweep.py` (head close-up every 2nd frame before/after), `gearbody.py` (per-frame rigid-head vs body contacts, before/after), `headrigid.py`, `opensheets.py`, `i3dscan.py`/`topbones2.py` (i3d head gear weights), `hgclip.py`.
- Review scripts (scratchpad `ctd/tools/`, not in the repo): `ctdlib.py` + `raster.so` (fast z-buffer renders,
  `render_lit` = game light model incl. mask/metal), `sweep.py` (every frame contact sheets), `cmp.py`/`cmpall.sh`/`stats.sh`
  (before/after + stretch), `mkba.py` (owner before/after sheets), `springsim.py` (preview solver = C#),
  `audit.py` (prop/headgear stretch, glow, holes, metal), `headview.py` (head front/side/top), `hgsil.py` (headgear gap),
  `headhit.py`, `comps.py`, `domcol.py`, `maskproof.py`, `rimsim.py`. The older `$S/clip/` (glbio, detect, clipset) is the pre-pilot set.
- Art: `.claude/skills/art-pipeline` (Gemini options -> owner pick -> cutout -> fal TRELLIS -> i3d_prep/i3d_rig).

## 4. What worked / what was rejected
- Worked: spring coat on Jasper; clip overrides for gowned kit heroes; prop_rigid; shield slide capped; neutral mask;
  remodels with shorter cloth (Wystan tunic, Vorgrim half-cloak); metal reflection limited to steel/gold props.
- Worked (headgear 2): rebuilding kit gear with hair compressed under covers + conform-to-hair seating; outward hat crowns; crowns placed where hair is near-vertical.
- Rejected: per-height conform of crowns high on the dome (band slid down the slope and sank at the back) - taper capped at 6 mm.
- Rejected: spring chains on kit gowns (holes, legs out), on cloth carrying arm/hand weights (Wystan old, Vorgrim cape),
  on Lazlo/Neve/Holt/Perpetua; tail chains on pelvis/leg-weighted tails; rigid head gear by height ramp on i3d heads
  (stretched more); double-sided / winding fix for i3d holes; uncapped shield slide (held by the top edge);
  whole-head lock (manes turned with the head, L019); head gear in metal maps by height (gold hair turned metal).

## 4b. Physics notes for the physics TD (feat/physics-standard owns colliders now)
- Simulate in the model holder's space at fixed 1/60 s (deterministic, matches previews); Low = kinematic + collisions.
- Two collision passes per joint; joint radius ~3-4 cm; thigh 9-13 cm, calf 7-11 cm capsules for robes; feet capsule for gowns.
- Cloth that hugs the legs or sits on layered meshes tears from the waist when put on chains; only free-hanging cloth worked (Jasper).
- Chains added after export: compress-hd prunes empty leaf nodes, so add tips last (SpringTests catches it).
- Tails/capes need rig-time landmarks; bone-heat weights on arms/legs make chains tear at the boundary.

## 5. Owner preferences (learned)
- A visual verdict beats any metric; "good in some frames, horrible in others" = not done (L008).
- Faces are not approved while head gear floats (2026-11-28); present faces and headgear together.
- Game physics (spring chains + colliders) is a standard for all heroes; show it in action (GIFs from the battle camera).
- Wants weapons' full move sets kept (NoBlock/Sword_Attack-only rejected; wait for the skeleton rebuild).
- Remodel picks: Lazlo A, Neve B with the FULL braid on a spring chain, Holt A; "make those look real" before fal.
- Prefers remodels with shorter cloth over weight hacks on long robes; options sheet first, fal only after the pick.
- Changelog must not claim a fix the tester can still see (L010).
- Favour fixes that carry over to the skeleton-library rebuild (mesh/material clean-up) over weight tweaks.

- Art: the proxy key only works on Gemini's OpenAI-compatible images/generations (text-to-image); image-conditioned edits are not available, so a "make it real" pass is a re-generation from a precise description of the pick - compare side by side and stop for the owner if it drifts. gemini-3-pro-image-preview gives the high-detail realistic look (n=1 per call).

## 6. Log (append: date, branch, one line)
- 2026-11-28 fix/ctd-pilot: spring bones, Jasper coat, Hask shield, gown clip overrides.
- 2026-11-28 fix/ctd-roster-1/2: rigid props, islands, metal maps + reflection, Wystan/Vorgrim remodels.
- 2026-11-28 fix/ctd-headgear: ring fit shipped for Quill/Vaelith/Ondessa/Pell; lowering hats/helms and tight rings rejected (rings sink out of sight); L025.
- 2026-11-28 fix/ctd-roster-3: L010 root cause = missing mask (L023); Obrecht pick turned; Nell idle; NoBlock weapons.
