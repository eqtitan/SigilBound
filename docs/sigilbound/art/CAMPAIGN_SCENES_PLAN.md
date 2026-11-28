# Campaign scenes plan

Owner request (2026-11-28): "PMO to have the art director build out all of the campaign scenes; keep camera
locations in mind when producing; use procedural. The art director gets access to the image-to-3D process we have
set up."

Owned by the pmo agent (plan, tracking, spend) with the art-director agent (the build). Rules for the build live in
[SCENERY.md](SCENERY.md) section 8; the story of each stop is [STORY_SCENES.md](STORY_SCENES.md). Branch
`feat/campaign-scenes`.

## 1. Scope

- **All campaign stages:** 15 chapters x 6 stages = 90 stops, Act I (chapters 1-10) and Act II (11-15), Normal and
  Hard (Hard is the same stop by night, with lanterns lit).
- **Every boss lair** (stage 6 of each chapter), including the lair reveal camera path.
- **Other arenas** (arena PvP, trials, menus) use the same generator for their chapter's ground, without a stop.
- **Out of scope:** the Titan's caldera (rebuilt on `fix/titan-scene`, kept as is), hero models, VFX, the HUD, and
  the battle cameras themselves (the scenes are built around the existing cameras). One exception, by owner decision
  (2026-11-28): boss stages tilt the battle views up 6 degrees (SCENERY.md section 8, rule 8).

## 2. Milestones

| # | Milestone | Done when | Status |
|---|---|---|---|
| M0 | Camera rules and generator | Every camera extracted into code; seeded per-stage generator; placement rejects anything that blocks the hero lane or that no camera sees; tests for every stage | Done |
| M1 | Reference chapter end to end | Chapter 2, The Drowned Mill: all 6 stops and the lair reveal, image-to-3D mill and cottage, Blender previews from the game cameras | Done (previews: `scratchpad/scene_previews/chapter02_preview.jpg`) |
| M2 | Remaining chapters | Same generator and rules for chapters 1, 3-15; image-to-3D signature landmarks where the procedural ones were primitive | Done in code (12 more generated landmarks); previews for all 15 chapters |
| M3 | In-game check | Screenshot tour of a Unity build of this branch: every chapter's stops 1, 3 and 6 in battle plus the lair reveal, two Hard lairs (`-sbShotsScenes`, workflow input `scenes`) | See section 7 |
| M4 | Second landmark pass | Landmarks that rely on glowing parts (glass spires, the Hollow cavern, the last pyre, the drowned shrine, the burning tree) and the remaining primitive props | Not started |

M1 was the gate: "do one chapter fully and stop to report, unless the previews clearly meet the acceptance criteria".
The chapter 2 previews met every automated criterion below and the mill read as a textured, painted building, so the
art director continued with the other chapters in the same style. The visual criteria (4, 6) still need the owner's
eye on the previews and, after M3, on real screenshots.

## 3. Acceptance criteria

Automated (`unity/Tests~`, `SceneGenTests.cs`, every stage x every quality tier, Normal and Hard):

1. **Hero lane clear.** No placement inside the ring plus a 1.3 m margin, and none that cuts a sight line from Classic,
   Overhead, Cinematic or Three-quarter (portrait 390x844, 16:9, 19.5:9) to the lane at foot, chest, head or tag height.
2. **Foreground only at the edges.** Anything nearer a camera than the lane stays in the outer 40 % of the screen width.
3. **Nothing wasted.** Every placement is inside at least one camera's view (within the fog).
4. **Landmark framed.** Each stop's landmark (the lair or its copy ahead) sits in the default view's background band
   (between the enemies' heads and the turn-order bar, away from the side edges) at every aspect.
5. **Lair reveal clear.** Nothing but the landmark stands between the reveal camera's three keys and the landmark.
6. **Travels toward the lair.** The landmark is far on the first stops, close on stop 5, at its foot in the lair.
7. **Budget.** Scenery triangles (terrain included) under 16k / 26k / 38k / 44k for Low / Medium / High / Ultra.
8. **Readability.** The lit lane is never pitch black (L* 14 or more, night included) and differs from a hero's
   mid-tone by at least 12 L*; the heroes' light keeps a floor on every stop (art review M2).
9. **Assets.** Every generated landmark is within 4,000 triangles; generated landmarks total under 8 MB; the APK
   estimate (model pack tests) stays under 190 MB.

By eye (owner, previews now and screenshots after M3): set-pieces look painted, not like untextured primitives
(art review H4); the landmark is visible in battle (M1); dusk and night stops keep the heroes readable (M2).

## 4. Risks

| Risk | Mitigation |
|---|---|
| **Performance per tier.** More props and a terrain mesh on a Note20-class phone. | Triangle budgets per tier (above), static batching unchanged (one ramp material plus one per generated landmark), terrain grid 24/32/48 per tier. Measured heroes cost ~150k triangles on Ultra; scenery was 3-10k and now tops out at 44k. Needs a frame-time check on device (M3). Generation itself runs in ~5 ms per arena. |
| **APK size (190 MB limit).** | Generated landmarks ship as compact gradient-map meshes (~170-215 KB each, 16 files, ~3.1 MB raw, compressed in the APK); the source GLBs stay in `Tools~` (not shipped). |
| **Hero readability against backgrounds.** | Constant hero light; the mood darkens the world's albedo instead; the ring keeps more value at dusk and night and very dark biomes get a lift (black fens). Tested per stage. |
| **The default camera crops tall landmarks.** It looks down 30 degrees, so in landscape it sees only a few metres up at 20 m out. | Frame on the landmark's body (35 % of its height, at most 2.5 m up) and let towers rise past the HUD; the lair reveal shows them whole. Boss stages now tilt the camera up 6 degrees (owner decision 2026-11-28), so most lairs show whole. |
| **Generated meshes off-model** (blurred texture, missing parts). | Gradient mapping repaints them in the biome palette; moving parts stay procedural on sockets; concepts reviewed before 3D; each landmark has a procedural fallback (delete its `Landmarks/*.bytes`). |
| **Previews are not the game.** No Unity here. | Blender renders from the game's camera parameters with the shipped meshes; M3 confirms in game. |
| **Originality.** | Prompts describe the object only (no game or artist names); one concept (a spiked throne) was regenerated because it echoed a famous design. |

## 5. Image-to-3D budget and spend

Budget: up to **$5.00 total** without asking (owner default). Prices: Gemini image about $0.04, fal.ai TRELLIS $0.02 per model.

| Item | Images | Models | Cost |
|---|---|---|---|
| M1: drowned mill, miller's cottage | 2 | 2 | $0.12 |
| M2: wagon, stilt hut, bell tower, Stormcrown crag, lighthouse, sea throne (2 concepts), sunken cathedral, king's barrow, toll house, warehouse, chapel, shipwreck, gatehouse, cairn | 15 | 14 | $0.88 |
| **Total** | **17** | **16** | **$1.00** |

No failed or repeated paid jobs. Sources and prompts: `unity/Tools~/models/landmarks/` (GLB, concept JPG, prompt).

## 6. Who does what next

- **Owner:** look at the chapter contact sheets from real screenshots (section 7) and say whether the look is right.
- **Art director:** M4 (glow-aware landmarks), then a frame-time check with the PMO on device.

## 7. Real screenshots (M3)

Owner decision 2026-11-28: real screenshots before the scenes ship. The Unity workflow, run by hand on
`feat/campaign-scenes` with the `scenes` input, builds the Linux player and runs the screenshot tour with
`-sbShotsScenes` (1280x720, software GL): every chapter's stops 1, 3 and 6 in battle, the lair reveal's middle key,
and the Hard lairs of chapters 2 and 15. Contact sheets per chapter: scratchpad `scene_real/`.

**Build 182** (run 46102985276, commit 0e5e033): 146 shots, the tour took 9 min 18 s (5 min 16 s without the
scenes), smoke passed. Seen in every chapter: heroes, enemies and tags readable and in frame; the lane clear; no
pink or missing textures, floating props or black stages; the landmark behind the enemies on stop 6 with the tilt
(camera pitch 24 degrees against 30), whole for all but the bell tower and the Stormcrown crag; every lair reveal
shows its landmark. Problems, fixed in the next commit:
- The crystals of the gold ruins, amethyst and burning-ruins lands (chapters 5, 8, 10) were burned to flat white
  or yellow cut-outs by the bloom (emission 1.2); now a lit body in the mote colour with a soft glow (0.3).
- The two Hard lairs never started (locked in the tour's throwaway save) and three late stops (10-3, 14-1, 15-1)
  showed the defeat screen (a level-2 team); the tour now opens every stage and puts the team at level 60.
- Not fixed (by design, for the owner): on stops 1-3 the landmark far ahead is small and often lost in the fog.

The fixes (commit de232b7) are not yet confirmed in a build: three later dispatches (runs 185, 187, 197) were
cancelled while waiting, because the workflow's license-seat concurrency groups keep only the newest pending job
and other branches kept queueing. Next: dispatch again (`scenes` on) when the Unity queue is quiet and check
chapters 5, 8 and 10 (crystals), 2-6H and 15-6H (night tilt), and that no stop shows the defeat screen.
