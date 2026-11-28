---
name: art-pipeline
description: Sigilbound's art concept workflow - generate concept/portrait/icon/landmark images with Gemini, pick with the owner, cut out the background, turn the pick into a 3D .glb with fal.ai TRELLIS, then rig/process it for the game. Use when asked to make a new hero, remodel a hero from its portrait, make a prop/landmark/icon, or "run the art pipeline".
---

# Sigilbound art pipeline (Gemini image -> owner pick -> fal TRELLIS 3D -> rig -> owner approval)

Scripts live in `unity/Tools~/artgen/`. Keys are **network secrets saved on the cloud environment** - the proxy
injects them; scripts send a placeholder. Never ask the owner to paste a key in chat, never print or commit one.
- Gemini: host `generativelanguage.googleapis.com` (Authorization: Bearer). Prepaid AI Studio credit; ~4 US cents/image.
- fal.ai: host `queue.fal.run` (Authorization: Key). ~2 US cents per TRELLIS model.
If a call fails with 401/403, the secret is missing on this environment: ask the owner to add it (request_credential
for fal: host queue.fal.run, prefix Key; Gemini must be added in the environment settings or via request_credential
without a header name, since the OpenAI-compatible endpoint takes Bearer). A 402/"credits depleted" or fal
"Exhausted balance" means the owner must top up - tell them, don't retry in a loop.

## 1. Concept images (Gemini)
Write the prompt to a file, then:
`python3 unity/Tools~/artgen/gemini_image.py gemini-2.5-flash-image prompt.txt out/<id>_a 1` (run 2 variants in parallel).
Prompt rules that make good 3D input (learned the hard way):
- "Full-body character concept art of <name>, <one-paragraph description>." + this block:
  `Plain flat light-grey background, no scenery, no floor, no cast shadow, no text, no border, a single character only,
  entire body visible from feet to top of head, nothing cropped. View: straight front view, slightly turned (about 15
  degrees), eye level. Pose: standing upright, feet apart at shoulder width, arms held slightly away from the body so
  hands, arms, weapon and torso do not overlap. Style: high-quality stylized fantasy game character, painterly but with
  clean solid forms and clear silhouettes, premium mobile hero-collector game, even soft studio lighting. Important: no
  glowing particles, no magic trails, no mist, no fire, no water splashes, no motion blur. Every part solid and attached.`
- Weapons/props held **down at the side or upright beside the body, clear of it** - never resting on a shoulder or
  across the chest (Odo and Marrow failed that way). Long gowns/robes and capes rig worst; prefer shorter hems.
- Legs clearly apart with a visible gap (fused legs can't be rigged - Poppy).
- Animal-folk: say whether legs are human-style (boots/robe) or animal (hocks/hooves) - animal legs need the
  digitigrade rig (`i3d_legs.py`, see docs/sigilbound/art/RIGGING.md).
- Portrait cards: "Fantasy hero portrait card illustration, vertical 3:4, head and upper body..." with an element-coloured
  background; crop to 3:4 and resize to 384x512 for `Assets/Resources/Portraits/portrait_<id>.jpg`.
- Single objects (landmarks/props): "A single <object>, three-quarter view, plain light-grey background, no scenery,
  no floor, no cast shadow, clean solid stylized painterly game asset".
- Originality: no real-world religious symbols (use the game's Sigil), avoid archetypes ART_BIBLE §11.4 lists.

## 2. Owner pick
`python3 unity/Tools~/artgen/sheet.py out/<id>_options.jpg A=out/<id>_a_1.png B=out/<id>_b_1.png` and send it with a
one-line recommendation per option. Wait for the pick (AskUserQuestion).

## 3. Cut-out + 3D (fal TRELLIS)
`python3 unity/Tools~/artgen/cutout.py pick.png pick_cut.png` (check the cut-out visually), then
`python3 unity/Tools~/artgen/fal_trellis.py pick_cut.png out/<id>.glb` (texture 2048 by default; it keeps the request id
in `<out>.request` so an unclear submit is resumed, never billed twice). Batch with at most 5 in parallel.

## 4. Into the game
- Heroes: rig with `unity/Tools~/blenderforge/i3d_prep.py`, `i3d_landmarks*.py` (add the hero's landmarks), `i3d_rig.py`
  (props rigid to the hand/forearm, capes/shells/wings to the spine, gowns partly to the pelvis); creatures use
  `creature_i3d.py`; animal legs `i3d_legs.py`. Blender python is a local venv (`bpy`) - create one if missing.
  **Physics standard (required for every new hero, L033):** after rigging and `compress-hd.mjs`, run
  `blenderforge/physics_classify.py` on the GLB, regenerate `Core/Rig/BodyColliderData.cs` (`--all`), enable the
  swinging parts (capes, long hair, tails, skirts that hang free) in `Tools~/models/physics/overrides.json`, add chains
  with `physics_rig.py` (last step), and verify them in the character-td frame sweep (docs/sigilbound/art/PHYSICS.md).
  Render `<id>_fal_compare.jpg` (input | raw | rigged front/side/back) and an anim GIF (idle, strike, cast/ult, hit).
  Anything with motion problems goes to the **character-td** agent (.claude/agents/character-td.md).
- Landmarks/props: `unity/Tools~/scenery/i3d_landmark.py` (see docs/sigilbound/art/CAMPAIGN_SCENES_PLAN.md).
- Credit every generated asset in `public/game/models/CREDITS.md` **and** `unity/Assets/StreamingAssets/Models/CREDITS.md`
  ("generated with Gemini + fal.ai TRELLIS (MIT)") - prepare-models copies the first over the second.
- Gates: `cd unity/Tests~ && dotnet run -c Release`, CompileCheck, prepare-models + APK budget < 190 MB.

## 5. Owner approval
Send the compare sheets/GIFs; nothing merges to main until the owner approves. Keep a spend log (images x 4c, models x 2c).
