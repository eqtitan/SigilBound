# Velka Driftmourn, the frost-tide mourner

| | |
|---|---|
| Element | Tide (sea blue, sea-glass and foam, deep water) |
| Rarity | Rare |
| Role | HP |
| Animation archetype | Caster (`SB_Anim_Caster_<state>`) |
| Build | tall and slim, long neck |
| Files | `SB_Hero_velka_LOD0.glb` (LOD1, LOD2), `T_Hero_velka_Albedo/Normal/Mask.png`, `SB_Gear_velka_Weapon.glb` |

## Concept

- **Silhouette:** a tall narrow spire: floor-length icy robe, faceted ice pauldrons, long white hair, a short frozen sea-glass staff.
- **Outfit:** layered icy-blue robe with frost-white edging, ice-crystal pauldrons, a mourning veil-scarf, silver circlet.
- **Weapon / focus:** a short staff of frozen sea-glass with a spiral of ice at the head.
- **Colours:** main `#3a6a8a` icy blue, accent `#bfefff` frost white, glow `#bfefff` frost, skin pale blue, hair `#e8f4ff` white, long. Element rim: #3a8ee8 (added by the shader, don't paint it in).
- **Personality cues:** melancholy, gentle, distant; sad slanted brows, small frown.
- **Skills on screen:** Drift Strike is a cold staff tap; Mournful Wave sends a slow frost wave; Deep Mending wraps allies in cool regeneration.
- **Shape language (HP):** Circles and mass: round, heavy forms, thick limbs, soft big shapes that look hard to knock down.
- **Rarity finish:** Rare: metal trim (bronze or steel) and one small glow detail (eyes, gem or weapon edge); simple pattern on the trim.
- **Phone check:** at 70 px tall the read is a tall narrow spire; one main colour, one accent, the focus visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, focus, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (floor-length icy robe); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: robe hem 4 bones, hair 3 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: frost staff
- Socket_Cast: ice spiral
- Socket_Head: circlet
- Gear rarity variants (optional): `SB_Gear_velka_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a ranged hero: every attack is cast in place):

| Skill | Target | Plays |
|---|---|---|
| 1. Drift Strike | one enemy | Cast |
| 2. Mournful Wave | all enemies | Cast_Ult |
| 3. Deep Mending | all allies, no damage | Cast_Support |

| State | Loop | When the game plays it for Velka | Direction |
|---|---|---|---|
| Idle | loop | always | prop held up near the chest, light sway; matches the personality: melancholy, gentle, distant |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | rarely (casters cast in place) | in place, root locked; the game moves the unit |
| Run_Back | loop | rarely | falls back to Run |
| Attack_1 | once | melee fallback only | prop swing (melee fallback, rarely used) |
| Attack_2 | once | - | - |
| Attack_3 | once | - | - |
| Cast | once | Drift Strike | staff thrust forward, release at about 0.3 s |
| Cast_Ult | once | Mournful Wave (camera zooms in) | the big moment: Mournful Wave sends a slow frost wave |
| Cast_Support | once | Deep Mending | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (melancholy, gentle, distant) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A tall slender pale-blue-skinned woman with long white hair and a silver circlet, layered floor-length icy-blue robe with frost-white edging, faceted ice-crystal pauldrons, a thin mourning scarf. Weapon shown separately beside the figure, upright. Main colour icy blue, accent frost white, glowing accents in frost, element mood sea blue, sea-glass and foam, deep water.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A tall slender pale-blue-skinned woman with long white hair and a silver circlet, layered floor-length icy-blue robe with frost-white edging, faceted ice-crystal pauldrons, a thin mourning scarf. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour icy blue, accent frost white, glowing accents in frost.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a short staff of frozen sea-glass with a spiral of ice at the head. Main colour icy blue, accent frost white, glowing accents in frost.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour icy blue, accent frost white, glowing accents in frost. Skin painted with warm subsurface tones. Frost patterns creeping up from the hem, translucent-looking ice painted with internal highlights.
```
