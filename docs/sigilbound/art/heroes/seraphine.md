# Seraphine of the Seventh Bell

| | |
|---|---|
| Element | Light (dawn gold, warm ivory, bells and sunrise) |
| Rarity | Legendary |
| Role | Support |
| Animation archetype | Caster (`SB_Anim_Caster_<state>`) |
| Build | average height, soft, floating poise |
| Files | `SB_Hero_seraphine_LOD0.glb` (LOD1, LOD2), `T_Hero_seraphine_Albedo/Normal/Mask.png`, `SB_Gear_seraphine_Weapon.glb` |

## Concept

- **Silhouette:** a great bell: a very wide ivory robe hung with small golden bells, a radiant halo, two short bronze-feathered wings, a large golden handbell.
- **Outfit:** wide ivory robe with gold bell fringe, layered sleeves, a gold belt, short wings with bronze chime-feathers.
- **Weapon / focus:** a large golden handbell with a carved wooden handle.
- **Colours:** main `#f0e8d8` ivory, accent `#ffd870` bell gold, glow `#fff0a0` soft light, skin pale, hair `#fff4d8` cream-gold, long. Element rim: #e8c23a (added by the shader, don't paint it in).
- **Personality cues:** serene, compassionate, quietly powerful; soft kind brows, gentle smile.
- **Skills on screen:** Bell Toll rings a golden sound wave; Seventh Bell heals and shields with a ringing dome; Requiem lifts every ally with a chorus of bells.
- **Shape language (Support):** Vertical ovals and arches: tall, flowing cloth, upward lines, a light-source prop held high.
- **Rarity finish:** Legendary: a silhouette-breaker above the head (crown, halo, flame, wings), animated glow on large areas, one signature material (molten, starlit glass, sunmetal).
- **Phone check:** at 70 px tall the read is a great bell; one main colour, one accent, the focus visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, focus, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (a very wide ivory robe hung with small golden bells); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: wings 2x2 bones, robe hem 4 bones, bell fringe via texture |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: handbell
- Socket_Cast: bell mouth
- Socket_Back: wings (2 bones each)
- Socket_Head: halo
- Gear rarity variants (optional): `SB_Gear_seraphine_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a ranged hero: every attack is cast in place):

| Skill | Target | Plays |
|---|---|---|
| 1. Bell Toll | one enemy | Cast |
| 2. Seventh Bell | all allies, no damage | Cast_Support |
| 3. Requiem | all allies, no damage | Cast_Support |

| State | Loop | When the game plays it for Seraphine | Direction |
|---|---|---|---|
| Idle | loop | always | prop held up near the chest, light sway; matches the personality: serene, compassionate, quietly powerful |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | rarely (casters cast in place) | in place, root locked; the game moves the unit |
| Run_Back | loop | rarely | falls back to Run |
| Attack_1 | once | melee fallback only | prop swing (melee fallback, rarely used) |
| Attack_2 | once | - | - |
| Attack_3 | once | - | - |
| Cast | once | Bell Toll | bell thrust forward, release at about 0.3 s |
| Cast_Ult | once | not used by this hero's skills (falls back to Cast) | - |
| Cast_Support | once | Seventh Bell, Requiem | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (serene, compassionate, quietly powerful) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A gentle pale woman with long cream-gold hair and a radiant halo, a very wide ivory robe hung with small golden bells along the hem, layered sleeves, a gold belt, two short wings made of bronze chime-like feathers. Weapon shown separately beside the figure, upright. Main colour ivory, accent bell gold, glowing accents in soft light, element mood dawn gold, warm ivory, bells and sunrise.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A gentle pale woman with long cream-gold hair and a radiant halo, a very wide ivory robe hung with small golden bells along the hem, layered sleeves, a gold belt, two short wings made of bronze chime-like feathers. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour ivory, accent bell gold, glowing accents in soft light.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a large golden handbell with a carved wooden handle. Main colour ivory, accent bell gold, glowing accents in soft light.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour ivory, accent bell gold, glowing accents in soft light. Skin painted with warm subsurface tones. Soft ivory cloth, polished gold bells, bronze feather-chimes with engraved lines; halo in the glow mask.
```
