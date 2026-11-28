# Battle scenery brief

The art director's brief for the battle arenas, written on 2026-11-26. The heroes are now semi-realistic
and hand-painted (ART_BIBLE.md), but the arenas still had flat, primitive props. This brief brings the
arenas up to the heroes' look. It covers:

- the target look
- palette, materials, silhouettes and lighting for each theme
- how the scenery is built
- the budgets
- the interactive backdrop on boss stages

Code: `Client/Scenery.cs`, `SceneryKit.cs`, `SceneryPaint.cs`, `ArenaReact.cs` and `Arena.cs`.
Tools: `unity/Tools~/scenery/`.

## 1. Audit (preview build 8351936)

- **Props were primitives.** Cubes, cylinders and spheres were tinted with one flat colour each. Trees
  were faceted blobs on a stick and crystals were plain boxes. Next to the painted heroes with their
  rim light, the arena read as a prototype.
- **No sky.** The sky was a two-colour gradient quad. The "mountains" were 18 flat dark rectangles,
  and most of them sat behind the sky quad, so only the side ones showed.
- **Water bug.** On every shore theme the water was a 10 m blue square floating behind the arena. A
  comment had swallowed the line that scaled it.
- **Ground.** Two-tone Perlin noise with a soft worn ellipse. There was no edge where feet wear the
  ground and nothing broke up the large areas.
- **Light and fog.** Every theme used the same white sun and the same fog range.

## 2. Target look

Show the same world as the heroes, a step back:

- Painted, not photographic.
- Chunky readable shapes with a soft volume.
- Local colour first.
- AO in the crevices.
- A warm highlight band on top and cool, darker undersides.

The ART_BIBLE painting guide applies to props as it does to heroes.

The scenery always stays quieter than the fight. It is darker and less saturated than the heroes, it
has no rim light, and the ring stays clear. The ring is the 4.2 x 3.95 m rune ellipse plus its HUD
margin.

Depth runs in three bands:

1. **Foreground scatter** (6.5–22 m from the centre). Full colour.
2. **Painted ridges** (36–44 m). Their colour shifts toward the fog with distance.
3. **Sky.** Painted clouds lit from the sun side, with a sun glow low on the horizon.

## 3. Per-theme palette and mood

Colours come from the theme table in `Scenery.cs`, so the arenas keep their identity. Each theme adds
a ridge silhouette, fog, a boss vent and storm flags.

| Theme | Ground / props | Shadows (Sil) | Sky (top / horizon) | Ridges | Fog near–far | Boss vent | Storm |
|---|---|---|---|---|---|---|---|
| dusk forest | moss green, autumn leaves (green, amber, rust) | `#2e2c46` | `#1f2b46` / `#c9805a` | forest crowns | 26–64 | dust | |
| teal shore | sand, grey-blue rock, driftwood | `#24404e` | `#16283a` / `#4f7c8a` | low sea hills | 26–64 | sea spray | |
| red badlands | rust earth, layered mesas | `#44201f` | `#2a1a1e` / `#a8483a` | flat-top mesas | 30–72 | dust | |
| green marsh | dark moss, reeds | `#223c36` | `#1a2e2a` / `#7fa088` | rolling hills | 20–56 | water | |
| gold ruins | olive grass, warm worked stone, flagstones | `#36304c` | `#28203a` / `#c8a870` | broken towers | 26–64 | crystal sparks | |
| blossom forest | spring grass, pink canopies | `#463858` | `#2a2438` / `#e0b0d0` | forest crowns | 26–64 | dust | |
| moonlit shore | pale sand, slate | `#1a2642` | `#101a2e` / `#5a7ab0` | sea hills | 26–64 | sea spray | |
| amethyst badlands | violet strata, crystals | `#221a36` | `#0e0a16` / `#3a2a5a` | mesas | 28–70 | crystal sparks | |
| glow marsh | teal moss, glowing caps | `#122838` | `#0e1a2a` / `#2a5a7a` | hills | 20–56 | spores | |
| burning ruins | scorched stone, embers, flagstones | `#381410` | `#1a0a0a` / `#ff6a2a` | broken towers | 26–64 | lava | |
| saltreach harbor | wet stone, posts, crates, lanterns | `#1e2c3a` | `#1a2436` / `#c89060` | sea hills | 26–64 | sea spray | |
| black fens | black water, dead trees, reeds | `#0e1812` | `#080e0a` / `#2a3a28` | dead tree line | 16–50 | spores | |
| wraithmoor barrows | grey moor, standing stones, mounds | `#202230` | `#1a1c26` / `#8a8e9a` | hills | 18–54 | dust | |
| ember groves | ash ground, leaves lit from within | `#2a0e0a` | `#140806` / `#c04a1a` | forest crowns | 26–64 | lava | |
| storm coast | slate, carved stones | `#0e1622` | `#0a1018` / `#3a4a5e` | jagged peaks | 20–58 | sea spray | yes |
| titan caldera | basalt, lava | `#1e0c0a` | `#1a0a0a` / `#c0401a` | volcano with a glowing crater | 26–64 | lava | yes |

**Materials**

Props use a gradient-map painting. Each prop face has a material row:

- bark, three leaf tones, rock, moss
- wood, worked stone, cloth, iron
- cap, stem, reed, earth, brass trim, coal

A per-theme ramp maps a baked shade value to a painted colour:

- **Shade end.** 30 % of the row's colour, cooled toward the theme's silhouette colour.
- **Middle.** The row's local colour.
- **Highlight end.** 130 % of the local colour, warmed toward the sun.
- **Banding.** A soft three-band step, the same idea as the hero shader's painterly ramp.

Moss is the theme's grass colour mixed 35 % with its rock. It sits on up-facing faces of rocks, ruins and
stones and is broken by noise. Glowing parts use the glow material:

- crystals and mushroom caps use the theme's mote colour
- brazier coals and lantern cores use warm fire

**Silhouettes**

- **Broadleaf tree:** a branching skinned trunk with root flare, and three canopy tiers (wide base,
  middle, crown). The canopy is lit as one soft volume and its leaves are dappled light and dark.
- **Pine:** five tiers with drooping, alternating tips.
- **Dead tree:** crooked, with forked branches.
- **Rock:** chiselled from random planes into big painted facets, half sunk into the ground.
- **Ruins:** fluted columns (one intact with its capital, the others broken with a fallen chunk),
  bevelled block walls with missing courses, and an arch with a missing voussoir.

**Lighting**

- The sun takes 35 % of the horizon's tint, so dusks are warm and storms cold. It stays close to white
  so the heroes' palette holds. Storm coast drops the sun to 0.9.
- Ambient light stays as before so the hero shader is not affected.
- Fog is linear, uses the horizon colour, and its range is set per theme. Ridges and sky are pre-hazed
  to match it.

## 4. How it is built

- **Prop kit.** `Tools~/scenery/make_props.py` (Blender's bpy) builds 42 meshes in code with
  deterministic seeds: 3 oaks, 2 pines, 2 dead trees, 4 rocks, 2 standing stones, a barrow mound,
  3 columns, 2 walls, an arch, a banner pole and its cloth, a brazier, 2 crystal clusters,
  2 mushroom clusters, 2 reed clumps, 3 grass tufts, 2 bushes, a lantern post, 2 posts, a crate,
  2 mesas and 2 debris chunks (17.7k triangles in all).
  - **Bake.** Every vertex gets ray-cast ambient occlusion (28 rays, including the ground) and a top
    light. Painterly noise is added and the result is stored as a shade byte, next to the face's
    material row.
  - **Output.** `Resources/Scenery/props.bytes` (about 295 KB): quantized positions, int8 normals
    and indices per submesh. `SceneryKit.cs` loads it synchronously.
  - **Fallback.** If the file is missing, the old primitive scatter runs instead.
- **Painted textures.** `SceneryPaint.cs` paints them in code at arena build time. `Tools~/scenery/paint.py`
  mirrors it in numpy for the review renders.
  - **Ramp:** 64 x 64.
  - **Sky:** 384 x 160. Domain-warped fbm clouds lit from above, plus the sun glow and horizon haze.
  - **Water and lava strips:** 512 x 96 each (lava adds a 512 x 96 glow mask).
  - **Ridges:** three 512 x 96 alpha strips, one per style, with a sun-side rim and haze at the foot.
  - **Ground:** 256², mipmapped. Broad two-tone strokes, then the worn patch with its trodden rim,
    pebbles, flagstones in the ruins and wet sand toward water.
  - **Crack decal:** 128².
  - **Cache.** Textures are kept until the arena theme changes. Painting takes about 50 ms per arena on
    desktop .NET; expect 3–5 times that on a phone, once, during the battle's loading.
- **Backdrop** (revised after release build 132). The sky and the three ridges are unlit painted
  strips: a straight front card with chamfered corners and side cards running back past the cameras.
  - **Placement:** the near ridge at z 28 (x ±30), the others at z 33 and 38, the sky at z 44
    (50 m tall).
  - **Why straight and close:** the menu/gallery and battle cameras look down about 30° and never see
    the true horizon; the top of their frame meets the ground about 30 m out. A ridge any farther is
    hidden behind the ground, and the first pass's 36–46 m arcs never showed there. Straight cards also
    stay level lines from a high camera, where a curved arc bowed into an arch.
  - **Ground fade:** the ground fades to a horizon colour where it meets the near card.
  - **Low cameras:** the Titan's views see all three ranges and the sky.
  - **Draw order:** far to near (render queue 2996–2999). The strips are never hidden as occluders
    (their names start with "Sky"), and a plain far field runs under them.
- **Water** is a painted strip: shallows, wave streaks and a foam line at the shore, deep water further
  out, then haze. It uses low gloss. The first pass's glossy plane mirrored Unity's default blue sky and
  showed a hard sun streak, which the build-132 tour mistook for the sky.
- **Lava** (Titan) is dark crusted basalt split by glowing veins. The veins widen and open pools of glow
  appear only far out, with emission ×1.1 from a vein mask. Before, a flat plane glowed at full
  emission and read as an overexposed yellow band.
- Both water and lava run 90 × 16 m from a shore line at z 10, with wet sand before the water.
- **Caldera** (Titan, art review H3). The crater ridge is a basalt wall ring centred at (0, 6), with its foot at 17 m,
  crest at 19.5 m (4–7 m high) and back at 22 m. It runs from −15° to 195°, so it stays open toward the camera side.
  A thin glowing lava line runs along its foot. It is built as 14 chunks so the occluder pass can hide a chunk that
  blocks a camera. Two lava-vein strips (the lake's crust-and-vein material, 15 × 2.2 m) run in from the flanks at
  x ±9.6 toward the lake. Scatter keeps off both. Code: `Scenery.Caldera`.

## 5. Budgets and quality tiers

> Campaign stops now use the generator's budgets (section 8.3: 16k / 26k / 38k / 44k scenery triangles per tier, terrain included); the numbers below describe the older scatter, still used by the Titan.

- Scatter is still 70 × density, with density 0.4 / 0.7 / 1.0 / 1.3 for Low / Medium / High / Ultra.
  Grass tufts at the ring edge are 60 × density.
- Triangles per prop: broadleaf tree about 1000–1250, rock 320, wall 700–840, tuft 72, pine 300.
  - A High arena is about 30–40k triangles; a Low one about 12–16k.
- Draw calls: all ramp props share one material and are static-batched. Glow parts share a second
  material.
- On boss stages, props that move (trees, banners, braziers) are left out of the static batch.
- Boss extras by tier:
  - **Low:** half the particles, at most 3 debris rocks, no brazier lights.
  - **Medium:** at most 6 debris rocks.
  - **High and Ultra:** at most 9 debris rocks, plus a warm point light on each of the 2 braziers.
- APK: about +295 KB raw for the kit (it compresses in the APK). Every texture is generated, so none are added.
- `Arena.HideOccluders` works unchanged. Kit props are ordinary renderers, the backdrop is skipped by
  name, and the ground and far field are skipped by size.

## 6. Interactive backdrop (boss stages and the Titan)

**Which battles.** Campaign boss stages on Normal and Hard (`Game.IsBossStage`) and the Titan get an
`ArenaReactor`.

**How it is wired.** `BattleController` raises presentation-only events from `ArenaEvents`. Nothing
reads them back into the battle:

- `RaiseHit(pos, share of max HP, crit, heavy)` on every damage number. "Heavy" means an ultimate or a
  boss source.
- `RaiseKill(pos)` on every death.
- `RaiseShock(pos, strength, slam)` on:
  - ultimates and boss attacks
  - the Titan's basic slam
  - Earthsplitter, Faultline Leap and Bedrock Hammer
  - the roar at the start of Heartfire Torrent

`SkillFx.cs` and `Sound.cs` are untouched.

| Trigger | Response |
|---|---|
| any shock, crit or heavy hit | Trees, bushes, reeds and dead trees tilt away from the blow on damped springs, then settle. Banner cloths swing on the crossbar. A gentle idle breeze keeps them alive. |
| kill | Both braziers flare: a flame burst, and on High and Ultra the brazier lights jump and settle. |
| big hit (over 18 % of max HP) or heavy | The nearest vent erupts. Lava fountains, sea spray, crystal sparks, glowing spores or dust, by theme. On a shock, all three vents erupt. |
| crit, heavy hit or shock | Debris falls from the sky onto the far half or the flanks of the ring, never between the camera and the heroes. It bounces once, kicks up dust, then settles into the ground and is reused. |
| storm themes (storm coast, Titan caldera): crit, heavy or shock, plus an idle strike every 7–14 s | A jagged bolt hits the far ridges and the sun and ambient light flash. With Settings > Screen shake off, there is no flash. |
| warlord ultimate (slam) | The ground cracks under the target. The crack is dark, or glows on lava themes, and fades over 4 s. The Titan's own moves already crack the ground in SkillFx, so the reactor skips cracks there. |

**Boss-stage dressing.** Two braziers flank the enemy side (x ±6.7, z 4.4). Two war banners stand behind
the foes, further back and wider for the Titan. Three vents sit beyond them, at the shore line where
there is water or lava.

**Tap flourish: left out.** Every tap in a battle can be a target or skill choice, so a background tap
effect would compete with battle input.

## 7. Review and checks

- `python unity/Tools~/scenery/make_props.py` rebuilds `props.bytes` and prints the triangle count per prop.
- `python unity/Tools~/scenery/review.py OUT [theme ...]` renders, per theme:
  - a prop lineup
  - a diorama from the battle camera, with grey stand-ins on the ring
  The sheets are in `$S/scenery_check/`.
- `painted_textures_csharp.jpg` comes from the C# painter itself, compiled outside Unity with a thin
  shim. It shows sky, ridges and ground for each theme.

**In-game checks still needed**

- How the transparent backdrop sorts against motes and VFX on device.
- How long painting takes on the Note20 baseline.
- Brazier lights under the URP additional-light limit.
- That kit props sit correctly with HideOccluders on the Cinematic camera preset.

## 8. Procedural campaign scenes and camera rules (2026-11-28)

Plan and spend: [CAMPAIGN_SCENES_PLAN.md](CAMPAIGN_SCENES_PLAN.md). Code: `Core/SceneCameras.cs` (cameras and rules),
`Core/SceneGen.cs` (generator, rules check, light), `Core/Biomes.cs` (the theme table, moved from `Scenery.cs`, plus the
generator's tables), `Core/SceneMeshes.Generated.cs` (every scenery mesh's footprint, from `Tools~/scenery/mesh_bounds.py`).
`Client/Scenery.cs` builds the layout; `Tests~/SceneGenTests.cs` checks every stage.

### 8.1 Cameras (camera first)

Every camera a player can see a stage from is in `SceneCameras`:

| Camera | Where it comes from | Aspects | Rule |
|---|---|---|---|
| Classic, Overhead, Cinematic, Three-quarter (default) | `BattleLayout.View` (the View button), fitted per aspect | portrait 390x844 (0.46), 16:9, 19.5:9 | **Guarded**: nothing may block the hero lane |
| Hero view (yaw 180) | same | same | Looks back over the enemies: whatever stands behind them in the other views is in front of it, so it is faded at run time (`Arena.HideOccluders`), not refused |
| Lair reveal, keys 0 / mid / 1 | `SceneCameras.RevealKeys` (BattleController's path, now in Core) | portrait, 16:9 | Nothing but the landmark may stand in its sight lines |
| Battle start and victory | the framed battle view (no separate camera) | | covered by the views above |

`SceneCameras.Footprint` gives each camera's ground footprint (the four corner rays on y = 0); the previews draw them.

**Hero lane:** the rim ellipse (4.2 x 3.95 m round (0, 0.6)) grown by 1.3 m, up to 3.6 m (HP tags). Sample points: the
centre and two rings (inside the rim and at the lane edge) at 0.3, 1.4, 2.4 and 3.6 m.

### 8.2 Placement rules (enforced: `SceneRules.Check`, every placement, every stage)

1. No footprint inside the lane.
2. No sight line from a guarded camera to a lane point passes through a placement (each mesh is a low cylinder for its
   whole footprint and a tall one for the part above 35 % of its height), and nothing within 1.5 m of a camera.
3. Foreground: any part nearer a guarded camera than the lane's nearest point must sit in the outer bands of that
   screen (|x| >= 0.6 in NDC), so near pieces only frame the edges.
4. Seen: every placement is inside some camera's frustum within the fog's end; nothing is built where no one looks.
5. Lair reveal: nothing but the landmark and its parts in the reveal keys' sight lines to the landmark.
6. World bounds: inside the near ridge card (z 27.5, |x| 27); only story pieces (boats, gates, the mill) stand in water.
7. Spacing: big pieces keep apart; scatter keeps off set-pieces.
8. **Landmark framing** (`SceneRules.FrameError`): the landmark's body (35 % of its height, at most 2.5 m up) projects
   in the default view's background band at every aspect: NDC y 0 to 0.76 (under the turn-order bar; a far copy may
   reach 0.92), |x| <= 0.86. The default view looks down 30 degrees, so tall towers rise past the HUD; the lair reveal
   shows them whole. **Boss stages** (stage 6, Normal and Hard; owner decision 2026-11-28) use the tilted views
   (`BattleLayout.BossView`: Classic, Overhead, Cinematic and Three-quarter look up `BossTilt` = 6 degrees about the
   camera's right axis, then refit the distance so the rim, heroes and tags stay in frame; Hero view and the Titan are
   unchanged). The rules run on those tilted cameras, and on a boss stage the lair's base must be on screen and the
   landmark up to `SceneCameras.LairShown` (6 m) under NDC 0.92: whole for every lair but the bell tower, the
   Stormcrown crag, the burning tree and the sunken cathedral, whose tips still rise past the HUD in landscape. The generator searches x across the band and z a little nearer or farther for the legal spot
   nearest the authored one; the landmark's parts and copies move with it. Other story pieces keep their authored spot
   when it is legal, else the nearest legal spot (spiral search); a piece with no legal spot fails the tests.

### 8.3 The generator

Stage id -> seed (`SceneGen.Seed`) -> layout, in this order (each step through the rules):

- **Light** (`SceneGen.LightOf`): the stop's mood sets the sun's height and side (day 50 degrees ... dusk 24), fog and a
  world tint (day 1.0 ... night 0.7) painted into the ground and ramps. The sun (at least 0.85) and ambient keep a floor
  so heroes read on every stop; the ring keeps more value (tint^0.4) and very dark biomes get a lane lift.
- **Landmark**: the lair's main piece, or its copy ahead (synthesised at 24.5 m minus 5 m x progress when a stop has none).
- **Road**: from the camera side through the ring to the landmark's foot, painted into the ground with ruts.
- **Story set-pieces** (StoryScenes), **boss dressing** (braziers, banners), **night lanterns**.
- **Skyline** (21.5-26 m, far ramp), **mid-ground** (10-17 m), **framing** (near flanks, edges only),
  **approach props** (debris and dead wood along the road, thickening toward the lair), **scatter** (density falls off
  over 4.5 m from the lane), **edge tufts**. Tables per biome in `Biomes.cs`.
- **Terrain** (`SceneGen.TerrainHeight`): flat in and around the lane, on the road, at the shore and under the
  landmark; rolling outside (biome relief 0.6-2.4 m) and rising toward the ridges; low on the camera side. Built as a
  grid (24/32/48 per tier) with the Plane's texture mapping.
- **Budget**: placements stop when the tier's triangle budget is reached (16k / 26k / 38k / 44k, terrain included).

### 8.4 Image-to-3D landmarks

`Tools~/scenery/i3d_landmark.py` turns a TRELLIS GLB into `Resources/Scenery/Landmarks/<name>.bytes` (SBL1): scaled to
the landmark's height, pivot at the footprint centre, decimated to its budget (1,600-3,800 triangles), and its texture
split into a gradient map (per texel a material row clustered from its colour, and a shade). The game paints the texture
from the chapter's ramp at build time (`SceneryPaint.LandmarkTexture`), so a generated building wears the biome palette,
the far haze and the mood like every procedural prop (art review H4). Bright saturated texels (a rose window, a shard)
become glow texels with an emission mask that the boss-stage pulse drives. Moving parts stay procedural and hang on
sockets (`SceneMeshes.Sockets`: the mill wheel, the bell, the lighthouse lamp). A generated landmark replaces the
procedural mesh of the same name; deleting its file restores the procedural one.

Generated so far: drowned mill, miller's cottage, wagon, stilt hut, bell tower, Stormcrown crag, lighthouse, sea throne,
sunken cathedral, king's barrow, toll house, warehouse, chapel, shipwreck, gatehouse, cairn.

### 8.5 Previews and checks

- `dotnet run --project unity/Tests~ -- scenes OUT.json` dumps every stage's layout (items, landmark, road, light,
  terrain heights, cameras and footprints).
- `python Tools~/scenery/scene_preview.py OUT.json DIR 2 [3 ...]` (Blender) renders each stop from the default view at
  16:9 and portrait, the lair reveal's first key, and a plan with the lane, road, placements by layer and the cameras'
  footprints, then a contact sheet per chapter. These are previews, not game screenshots.
- `-- scenetests` runs only the scene checks (about 4 s); `-- scenedebug CHAPTER` prints each stop's landmark framing.
