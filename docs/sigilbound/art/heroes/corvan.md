# Corvan Tidecaller, the old tidecaller

| | |
|---|---|
| Element | Tide (sea blue, sea-glass and foam, deep water) |
| Rarity | Rare |
| Role | Support |
| Animation archetype | Caster (`SB_Anim_Caster_<state>`) |
| Build | elderly, slightly short, upright |
| Files | `SB_Hero_corvan_LOD0.glb` (LOD1, LOD2), `T_Hero_corvan_Albedo/Normal/Mask.png`, `SB_Gear_corvan_Weapon.glb` |

## Concept

- **Silhouette:** a column with a crook: a knee-length robe, a cyan stole hanging long, white hair and beard, a driftwood staff topped by a glowing blue orb.
- **Outfit:** deep-blue knee-length priest's robe with pale cyan trim and wave embroidery, cyan stole, sandals, a net-bag of shells at the belt.
- **Weapon / focus:** a twisted driftwood staff with a sea-glass orb caught in its roots.
- **Colours:** main `#20507a` deep blue, accent `#7fd4ff` pale cyan, glow `#7fd4ff` sea-glass, skin brown, hair `#d0e6f0` white, long, full beard. Element rim: #3a8ee8 (added by the shader, don't paint it in).
- **Personality cues:** kind, wise, unhurried; soft lowered brows, calm mouth hidden by the beard.
- **Skills on screen:** Undertow pulls with a water bolt; Rising Tide heals with a wave of light; Tidal Hymn raises the staff to rally allies.
- **Shape language (Support):** Vertical ovals and arches: tall, flowing cloth, upward lines, a light-source prop held high.
- **Rarity finish:** Rare: metal trim (bronze or steel) and one small glow detail (eyes, gem or weapon edge); simple pattern on the trim.
- **Phone check:** at 70 px tall the read is a column with a crook; one main colour, one accent, the focus visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, focus, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (a knee-length robe); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: stole 2x2 bones, beard 1 bone |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: driftwood staff
- Socket_Cast: orb
- Socket_Hip: shell net
- Gear rarity variants (optional): `SB_Gear_corvan_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a ranged hero: every attack is cast in place):

| Skill | Target | Plays |
|---|---|---|
| 1. Undertow | one enemy | Cast |
| 2. Rising Tide | all allies, no damage | Cast_Support |
| 3. Tidal Hymn | all allies, no damage | Cast_Support |

| State | Loop | When the game plays it for Corvan | Direction |
|---|---|---|---|
| Idle | loop | always | prop held up near the chest, light sway; matches the personality: kind, wise, unhurried |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | rarely (casters cast in place) | in place, root locked; the game moves the unit |
| Run_Back | loop | rarely | falls back to Run |
| Attack_1 | once | melee fallback only | prop swing (melee fallback, rarely used) |
| Attack_2 | once | - | - |
| Attack_3 | once | - | - |
| Cast | once | Undertow | staff thrust forward, release at about 0.3 s |
| Cast_Ult | once | not used by this hero's skills (falls back to Cast) | - |
| Cast_Support | once | Rising Tide, Tidal Hymn | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (kind, wise, unhurried) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A dark-skinned elder with long white hair and a full white beard, deep-blue knee-length priest's robe with pale cyan wave embroidery, a long cyan stole, a net bag of shells at the belt, sandals. Weapon shown separately beside the figure, upright. Main colour deep blue, accent pale cyan, glowing accents in sea-glass, element mood sea blue, sea-glass and foam, deep water.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A dark-skinned elder with long white hair and a full white beard, deep-blue knee-length priest's robe with pale cyan wave embroidery, a long cyan stole, a net bag of shells at the belt, sandals. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour deep blue, accent pale cyan, glowing accents in sea-glass.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a twisted driftwood staff with a sea-glass orb caught in its roots. Main colour deep blue, accent pale cyan, glowing accents in sea-glass.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour deep blue, accent pale cyan, glowing accents in sea-glass. Skin painted with warm subsurface tones. Salt-faded robe, embroidered wave motifs, barnacle and shell details.
```
