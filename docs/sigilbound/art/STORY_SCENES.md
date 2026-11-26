# Story scenes brief

Status: **brief and mock-ups for approval.** Nothing here is wired into the game yet.

The owner asked: "Let's work on scene art and flesh it out more based on the story build", and
refined it: "Each level of campaign progression should show different parts of the world scene,
reaching the boss's lair at the last stage."

This brief builds on the arena scenery pass in [SCENERY.md](SCENERY.md):

- the prop kit
- the painted sky, ridges and ground
- the boss-stage `ArenaReactor`

Today the scenery is chosen per *theme*, so "Thornwick Road" and "The Drowned Mill" look like any
forest or shore. Here every chapter becomes a short journey. Each of its 6 stages is its own stop on a
route through the chapter's world, and stage 6 is the boss's lair.

## 1. Rules for every chapter

**One palette per chapter.** Each stop keeps the chapter's theme colours (SCENERY.md §3). What changes
along the route is the time of day and the weather:

- the sun's strength
- a drift of the horizon colour toward the theme's shadow colour, by up to 35 %
- fog

The arena never jumps to another palette mid-chapter.

**The lair can be seen from the road.**

- Stops 1–4 show the chapter's landmark in the distance, against the near ridge (z 20–25). It is
  usually off-centre, so the route visibly heads toward it.
- Stop 5 sees it looming close.
- Stage 6 is fought at the landmark's foot.

**The lair is the arrival.** It is the chapter's strongest set-piece: the landmark at full size behind
the enemies (z 11–15) or on a flank. The boss dressing comes with it (braziers and war banners), and the
landmark joins the boss interactions.

**Staging and budget**

- **Placement.** Set-pieces sit behind the enemy arc or on the flanks, never between the camera and the
  ring. The ring and its HUD margin stay clear, and `HideOccluders` still covers the low camera presets.
- **Stop set-pieces:** about 300–2,500 triangles each.
- **Lair landmark:** at most 4,000 triangles. A distant copy reuses the same mesh, so it adds no bytes.

**Each stop is built from:**

- the theme's normal scatter, with its weights nudged per stop (more reeds at the millpond, more crates
  at the yard)
- one or two stop set-pieces
- 2–3 small story props, which tell the story through details rather than a set-piece

## 2. The chapters

The act 1 story lines below are **drafts for the owner**. They use the voice of the act 2 lines in
`GameData.ChapterStory` and lead into `Act2Intro`. "Theme" is today's `ThemeOf` theme.

Lighting shorthand used in the stop lists:

| Mood | Sun strength | Horizon drift toward the shadow colour |
|---|---|---|
| day | 1.0 | none |
| late | 0.95 | 10 % |
| golden | 0.9 | 20 % |
| dusk | 0.85 | 35 % |
| night | 0.7 | 35 %, fog closer |

### 1. Thornwick Road — dusk forest

> *The Sigil cracked in the night, and the Hollow came down Thornwick Road with the dark. Follow the
> wagon tracks: whatever overturned them is still close.*

**Route**

1. **The crossroads.**
   - A waystone with a hanging sign, the cart road painted into the ground, a lantern post.
   - day
2. **The hedge lane.** A thorn hedge both sides, pressing in.
   - Far away, smoke rises over the treeline where the wreck is.
   - late
3. **The tollhouse.**
   - An empty toll hut, with its barrier pole snapped.
   - late
4. **The ambush.**
   - Scattered sacks, a lost wheel, arrows in the trees.
   - golden
5. **The camp.**
   - Hollow banners on stakes, a cold fire ring.
   - The wreck is visible ahead.
   - dusk
6. **Lair: the broken wagon.**
   - The overturned wagon is a barricade between thorn hedges, with the waystone behind.
   - dusk

**Story props:** the waystone ("Thornwick 2 leagues"), spilled grain sacks, a child's doll on the
wagon bed.

**Lair interactions**
- The hedges thrash with shockwaves.
- The wagon's upturned wheel spins on crits.
- The lantern on the waystone flares on kills.

### 2. The Drowned Mill — teal shore (river and millpond)

> *The river rose the night the Sigil broke. The old mill went under, yet its wheel still turns, and the
> Hollow gather where it groans.*

**Route** (mocked in full)

1. **The flooded road.**
   - Road posts standing in the water, an abandoned rowboat.
   - The mill sits on the far water, off to the right.
   - day
2. **The millpond reeds.**
   - Thick reeds, a skiff in the shallows.
   - The mill is closer, with its wheel turning.
   - late
3. **The sluice gate.**
   - A stone sluice with its gate half raised and an iron winding wheel.
   - The mill rises behind it.
   - late
4. **The miller's cottage.**
   - A whitewashed cottage, thatch, a picket fence, a well.
   - The mill fills the right flank.
   - golden
5. **The mill yard.**
   - A loaded cart, sacks, millstones, a hoist.
   - The mill looms over it.
   - dusk
6. **Lair: the foot of the half-sunk mill.**
   - The mill tilts in the race and its paddle wheel churns beside the ring.
   - dusk

**Story props:** millstones dumped on the bank, flour sacks gone grey with water, the broken weir.

**Lair interactions**
- The wheel turns slowly, spins up on crits, then slows back down.
- The race throws spray (Water vents) on big hits.
- The mill's timbers creak and lean, swaying with shocks.

### 3. Ashgrave Pass — red badlands

> *Ash falls like snow over the pass. The mourners fled and left their dead unburied, but the graves are
> not quiet.*

**Route**

1. **The foothills.**
   - Mesas, a cairn marking the trail.
   - The pass gate's twin cairns stand far off on the ridge.
   - day
2. **The ash slope.**
   - Pale ash drifts on the ground and dead trees.
   - late
3. **The mourners' camp.**
   - Abandoned tents, a cold pyre, prayer flags.
   - golden
4. **The mourners' bell.**
   - A post with an iron bell beside the trail.
   - golden
5. **The gate cairns.**
   - Ash-capped cairns on both flanks, the graveyard ahead.
   - dusk
6. **Lair: the graveyard in the pass.**
   - Leaning headstones and burial mounds between the gate cairns.
   - dusk, with ash falling (ember motes go pale)

**Story props:** mourners' lanterns, a dropped shovel, fresh mounds.

**Lair interactions**
- Ash plumes puff from the cairns on kills (Dust vents).
- The mourners' bell tolls on kills.
- The ground cracks over the graves under boss slams.

### 4. Saltmarsh Reaches — green marsh

> *The marsh folk have pulled up their ladders. From their stilt huts they watch lights moving in the
> reeds, lights that aren't theirs.*

**Route**

1. **The causeway.**
   - A plank walk over the reeds.
   - Distant stilt-hut roofs.
   - day
2. **The eel weirs.**
   - Wicker eel traps, drying nets.
   - late
3. **The drowned boat.**
   - A sunken punt among the reeds.
   - golden
4. **The salt pans.**
   - Raked white pans, salt heaps.
   - golden
5. **The outer huts.**
   - A single hut on stilts with its ladder up.
   - dusk
6. **Lair: the stilt village.**
   - Two huts joined by a walkway, nets and lanterns.
   - night, with fireflies

**Story props:** raised ladders, a hanging lantern, fishing nets.

**Lair interactions**
- The huts sway on their stilts with shocks.
- The lanterns swing.
- The water erupts (Water vents) on big hits.

### 5. Bellspire Ruins — gold ruins

> *The Bellspire has not rung in a hundred years. Tonight it tolls by itself, and every toll calls more
> Hollow to the ruins.*

**Route**

1. **The old road.**
   - Paving breaking through the grass.
   - The leaning tower is far off.
   - day
2. **The broken wall.**
   - The town wall, with gaps and fallen blocks.
   - day
3. **The market cross.**
   - A toppled market cross and stalls.
   - late
4. **The cloister.**
   - An arcade of arches.
   - golden
5. **The fallen buttress.**
   - A buttress across the path.
   - The tower fills the sky.
   - golden
6. **Lair: the foot of the leaning bell tower.**
   - Its belfry is open, with the bell visible.
   - golden into dusk

**Story props:** a bell rope trailing from the tower, pigeons (motes), a cracked bell lip in the grass.

**Lair interactions**
- The bell swings and tolls on every kill. A visual swing; the audio agent can add the toll as an event.
- Crystals spark (Crystal vents) on big hits.
- Debris falls from the belfry on crits.

### 6. The Glass Wastes — today: blossom forest (see open question 1)

> *The first Sigil war burned these sands to glass. The spires sing in the wind, and something Hollow
> sings back.*

**Route**

1. **The edge of the waste.**
   - The last trees, grass going to sand.
   - day
2. **The fused dunes.**
   - Glassy crust and small shards.
   - day
3. **The shattered caravan.**
   - A wagon frozen in glass.
   - late
4. **The fulgurite field.**
   - Twisted glass tubes from old lightning.
   - golden
5. **The spire gate.**
   - Two great spires.
   - dusk
6. **Lair: the singing spires.**
   - A forest of glass spires around a fulgurite arch.
   - dusk, so the spires glow

**Story props:** a glass-crusted shield, bleached bones, a melted helmet.

**Lair interactions**
- The spires flash and chime on crits.
- Crystal-spark vents.
- Shards fall as debris.

### 7. Stormcrown Heights — today: moonlit shore (see open question 1)

> *Lightning never leaves the Stormcrown. Climb to the crown of rock, where a splinter of the old storm
> still answers the Hollow.*

**Route**

1. **The switchback.**
   - A cliff path, wind-bent pines.
   - The crown crag is on the skyline.
   - day
2. **The rope bridge.**
   - A bridge over a gap, on the flank.
   - late
3. **The shepherds' hut.**
   - A stone bothy.
   - golden
4. **The scree field.**
   - Loose rock, a snow line.
   - dusk
5. **The storm altar.**
   - Iron rods and scorched stones.
   - night
6. **Lair: the crown crag.**
   - A ring of rock points with lightning rods.
   - night storm

**Story props:** iron lightning rods, a shepherd's crook, scorched cairns.

**Lair interactions**
- Lightning strikes the crown points on crits and shocks (ArenaReactor storm mode).
- Snow and debris fall from the tips.

### 8. Hollow Deep — amethyst badlands

> *The Hollow did not come from nowhere. Under the badlands a cavern breathes violet light. Go down, and
> find where they are made.*

**Route**

1. **The mine track.**
   - Rails, an overturned ore cart.
   - day
2. **The prospectors' camp.**
   - Tents, a winch.
   - late
3. **The crystal seam.**
   - Violet crystals breaking out of the rock.
   - golden
4. **The sinkhole.**
   - A collapse on the flank, glowing from below.
   - dusk
5. **The Hollow-touched.**
   - Crystals everywhere, the ground cracked.
   - night
6. **Lair: the cavern mouth.**
   - An arch of slabs with Hollow light pulsing in its throat.
   - night

**Story props:** miners' lamps, an ore cart, a winch frame.

**Lair interactions**
- The cavern's glow pulses on kills and surges on ultimates.
- Crystal vents.
- The cavern breathes Hollow spores.

### 9. Sunken Cathedral — glow marsh

> *The cathedral sank with its choir still singing. Its rose window glows under the water, and the
> drowned congregation is waking.*

**Route**

1. **The pilgrims' road.**
   - Waymarker shrines.
   - The cathedral's gable is far off over the water.
   - late
2. **The drowned graves.**
   - Headstones in the shallows.
   - golden
3. **The bell in the reeds.**
   - A fallen bell, half sunk.
   - golden
4. **The chapter house.**
   - A roofless side chapel.
   - dusk
5. **The nave steps.**
   - Steps descending into the water.
   - night
6. **Lair: the flooded nave.**
   - Arcades wading into the water and the west front's rose window glowing above the boss.
   - night, glow mushrooms

**Story props:** floating candles, pews in the water, a fallen bell.

**Lair interactions**
- The rose window brightens on ultimates.
- The water stirs (spore and water vents) on big hits.
- The arches shed stones on crits.

### 10. The Last Pyre — burning ruins

> *At the Last Pyre the Hollow mean to burn what's left of the Sigil. Reach the fire before it takes the
> shards, or the night never ends.*

**Route**

1. **The burning road.**
   - Embers, a ruined gatehouse.
   - The pyre's glow is on the horizon.
   - dusk
2. **The ash fields.**
   - Burnt-out braziers, ash.
   - dusk
3. **The torch avenue.**
   - Torch posts lining the way.
   - night
4. **The Hollow host.**
   - Banners, war camp.
   - night
5. **The dais steps.**
   - Stepped stone, the heat haze.
   - night
6. **Lair: the Last Pyre.**
   - A stepped dais carrying a giant log pyre, ringed by torches.
   - night

**Story props:** shards of the Sigil in iron cages, torch posts, Hollow banners.

**Lair interactions**
- The pyre flares on kills and roars on ultimates.
- The torch ring brightens one torch per kill.
- Lava vents.

### 11. Saltreach Harbor — saltreach harbor (act 2)

> *Saltreach's lamps still burn, but the docks are full of Hollow. A street urchin swears the Flame shard
> sleeps in the old lighthouse.*

**Route**

1. **The city gate.** The lighthouse is far off over the roofs. day
2. **The fish market.** Crates, nets. late
3. **The warehouses.** Cranes, rope coils. golden
4. **The breakwater.** Mooring posts, a sunk skiff. dusk
5. **The pier's end.** The lighthouse rock looms. dusk
6. **Lair: the old lighthouse.**
   - The Flame shard is its lamp, and its beam sweeps the arena.
   - dusk into night

**Story props:** the urchin's chalk marks, lanterns, fishing floats.

**Lair interactions**
- The beam sweeps continuously and flashes on crits.
- Spray vents.
- The lamp flares to full on kills.

### 12. The Black Fens — black fens

> *Beyond the city walls the fens are black and still. Something drowned here has learned to walk, and it
> carries the Shadow shard's chill.*

**Route**

1. **The last dyke.** Dead trees, reeds. late
2. **The sunken track.** A plank path, half drowned. golden
3. **The hanging trees.** Ropes, spirit lanterns. dusk
4. **The bog cottage.** A drowned hovel. dusk
5. **The shrine path.** Spirit lanterns on stakes lead in. night
6. **Lair: the drowned shrine.**
   - A sunken pavilion, its roof slid askew, the Shadow shard glowing violet in its niche.
   - night

**Story props:** spirit lanterns, offerings on stakes, a drowned bell.

**Lair interactions**
- The spirit lanterns gutter out one by one as the boss is hurt.
- Spores rise.
- The shard pulses with ultimates.

### 13. Wraithmoor Barrows — wraithmoor barrows

> *The barrows of Wraithmoor hum at night. The dead kings beneath them are restless, and the Tide shard
> sings under the stones.*

**Route**

1. **The moor road.** Standing stones in a line. day
2. **The lesser barrows.** Mounds, kerb stones. late
3. **The stone row.** A long avenue. golden
4. **The opened grave.** A robbed barrow, spoil heaps. dusk
5. **The king's avenue.** The great mound fills the view. night
6. **Lair: the king's barrow.**
   - A lintelled portal, a ring of stones, the Tide shard blazing blue on the mound's crown.
   - night

**Story props:** grave goods, a robber's spade, offerings at the portal.

**Lair interactions**
- The standing stones glow in turn on kills.
- The shard pulses with ultimates.
- Dust vents from the portal.

### 14. The Ember Groves — ember groves

> *The ember groves never stop burning. A Light shard fell among the trees, and the fire answered it.*

**Route**

1. **The grove edge.** Smouldering stumps. dusk
2. **The charcoal burners' clearing.** Kilns. dusk
3. **The burning arch.** Two trees grown together. night
4. **The cinder stream.** A lava trickle. night
5. **The heartwood ring.** Huge roots. night
6. **Lair: the burning great tree.**
   - Split open at the heart, the Light shard inside.
   - night

**Story props:** charcoal kilns, singed banners, a fallen burner's axe.

**Lair interactions**
- The canopy sheds burning leaves on kills.
- Lava vents.
- The heart glows brighter with each ultimate.

### 15. The Storm Coast — storm coast (act 2)

> *On the storm coast the last bearer waits, and so does the Hollow King. The Gale shard screams in the
> lightning. End it here.*

**Route** (mocked in full)

1. **The cliff path.**
   - Wind-bent grass, standing stones.
   - The Hollow King's throne cliff is on the far headland.
   - day, storm building
2. **The wreck.**
   - A ship's bare ribs on the shingle, a torn sail, crates washed up.
   - late
3. **The dead beacon.**
   - A broken coastal beacon tower, its fire basket cold.
   - golden
4. **The sea stacks.**
   - Stacks and a Hollow watch post.
   - The first lightning hits the throne cliff.
   - dusk
5. **The Hollow camp.**
   - War banners, braziers.
   - The throne cliff looms over the camp.
   - dusk storm
6. **Lair: the sea-cliff throne.**
   - The Hollow King's throne is carved into the cliff, chains run down to the sea, and the Gale shard is
     bound to the seat.
   - night storm

**Story props:** drowned chains, a cold beacon, the King's banners.

**Lair interactions**
- Lightning strikes the throne and its spires on crits.
- Spray vents.
- The chains clank and swing with shocks.

## 3. Data structure (proposed)

The existing per-theme build stays as the base. A small, per-chapter table of stage *sets* sits on top
of it:

```csharp
// Core (data only, testable): Core/StoryScenes.cs
public sealed class StageSet {
    public string Name;               // "The sluice gate" (could also feed the stage list)
    public float Dusk, Light = 1, Fog = 1;   // time of day within the chapter palette
    public Piece[] Pieces;            // set-pieces and story props: (mesh, x, z, y, yaw, scale, glow)
    public (string prefix, float w)[] Scatter; // extra weights on top of the theme's picks
}
public static StageSet[][] Sets;      // [chapter][stage 0..5]; stage 5 = the lair
public static Piece Landmark(int chapter, int stage); // the lair landmark's far/near placement
```

- `Scenery.Build` already knows the chapter. It would also take the stage-in-chapter
  (`stage % StagesPerChapter`).
  - **Overlay:** after the theme scatter, Build places the stop's pieces. The pieces' keep-out circles
    remove scatter under them.
  - **Mood:** the stop's Dusk/Light/Fog adjust the horizon colour, sun and fog.
- The arena's paint cache key becomes theme + chapter + dusk step, so the sky is repainted only when the
  mood changes.
- **Moving parts** are separate kit meshes named `<landmark>_<part>` (`drowned_mill_wheel`,
  `bellspire_bell`, `lighthouse_lamp`). A small `LandmarkReact` step in the existing `ArenaReactor` maps
  each part to a behaviour:
  - spin
  - toll/swing
  - beam
  - flare
  - pulse
  - strike target
  These run on the same `ArenaEvents`, with no battle-logic change.
- **Tests (Core):**
  - every chapter has 6 sets
  - every piece's mesh exists in the kit
  - no piece intrudes on the ring or the camera side (the same ellipse test the scatter uses)
  - every lair is the last set
- **Arena gallery, Titan and dungeons** are unchanged: no stage index means the theme only. Dungeons
  could later borrow a chapter's stops.

## 4. Budget and costs (measured on the mock-up meshes)

| Item | Triangles | props.bytes |
|---|---|---|
| 15 lair landmarks | 442–3,944 each (mean about 1,800) | 520 KB |
| 3 moving parts (wheel, bell, lamp) | 132–336 each | 13 KB |
| 8 stop set-pieces for the 2 mocked strips | 104–2,488 each | 122 KB |
| **Mocked set, total** | 32.9k | **614 KB** |

**Full rollout estimate**

- About 45 more stop set-pieces across the other 13 chapters, many shared (cottage, cart, boat, fence,
  tents, banners, beacon) → about +600 KB.
- About 1.2 MB raw added to props.bytes (it is 295 KB today). The APK's compression roughly halves that.

**Cuts before shipping**

- Thin the three heaviest pieces (hedge thorns, barrow mound, cavern slabs), about −120 KB.
- Share stop pieces across chapters.
- Split the file per act (`props.bytes`, `story_act1.bytes`, `story_act2.bytes`) and parse only the act
  in play.
- Target: ≤ 900 KB raw in all.

**Frame cost.** A stop adds one or two pieces (≤ 4k triangles) on top of today's scatter (30–40k on
High). Pieces use the same ramp material and static-batch with the scatter. On boss stages the moving
parts stay out of the batch.

**Load cost**

- Parsing all story meshes at startup would add an estimated 10–20 ms on a phone.
- With per-act files loaded on demand it is about 5–10 ms once per act.
- Repainting the sky for a mood change is about 10 ms on desktop.

## 5. Mock-ups

Made with `unity/Tools~/scenery/make_landmarks.py` and `story_review.py`. These are battle-camera
Cycles renders in the same style as the arena sheets, with grey cylinders standing in for the heroes.

- `scratchpad/story_check/sheet_lairs.jpg`: the lair of all 15 chapters.
- `scratchpad/story_check/strip_drowned_mill.jpg`: stops 1–6 of The Drowned Mill.
- `scratchpad/story_check/strip_storm_coast.jpg`: stops 1–6 of The Storm Coast.
- The individual renders are `scratchpad/story_check/c*.png`.

**How the mock-ups differ from the game**

- Fog is approximated per material.
- There is no post-processing colour grade.
- The glows are brighter than the game's bloom.
- Moving parts are shown at rest.
- Lightning bolts are frozen meshes; in the game they are the reactor's LineRenderer.

**Seen in the mock-ups**

- Far landmarks in heavy-fog themes (Storm Coast stops 1–4) fade to a pale ghost.
  - In the game they should be drawn darker (silhouette tint) or fog-exempt so the destination reads.
  - Alternatively, they could be painted into the near ridge card as a skyline feature.
- These pieces are still blockier than the kit and need a modelling pass before shipping:
  - the cliff under the Hollow King's throne
  - the cathedral arcades
  - the Thornwick wagon

## 6. Open questions for the owner

1. **Theme fit.**
   - *The Glass Wastes* (chapter 6) uses the **blossom forest** theme and *Stormcrown Heights*
     (chapter 7) uses **moonlit shore** (`ThemeOf` = chapter index).
   - The mock-ups keep those palettes, but a pink forest under glass spires and a seashore under a
     mountain crag fight the names.
   - Proposal: give the two chapters their own themes, a pale glass desert and a cold high crag. That
     adds 2 rows to the theme table and new colour grades (PostFx presets).
   - Alternatively, keep the palettes and lean the stops into them, for example spring blossom at the
     edge of the waste.
2. **Approve or rewrite the act 1 story lines** (§2). If approved, `ChapterStory[0..9]` gets them and
   the campaign screen shows them as it does for act 2.
3. **Stop names in the stage list?** The stop names ("The sluice gate") could title the stages in the
   campaign list instead of "2-3". This is optional.
4. **Bell and wheel sounds.** The bell toll, wheel creak and pyre roar would need cues from the audio
   agent; the scenery would raise them as events only.
5. **Dungeons, Hard mode and arena rivals** reuse chapter themes today. Should Hard stages reuse the
   same stops with a night mood, so Hard reads as "the same road, at night"?
