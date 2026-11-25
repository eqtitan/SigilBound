# Morwenna Abyss-Crowned

| | |
|---|---|
| Element | Shadow (violet, night glass, starlit void) |
| Rarity | Legendary |
| Role | Attack |
| Animation archetype | Caster (`SB_Anim_Caster_<state>`) |
| Build | tall, elegant, still |
| Files | `SB_Hero_morwenna_LOD0.glb` (LOD1, LOD2), `T_Hero_morwenna_Albedo/Normal/Mask.png`, `SB_Gear_morwenna_Weapon.glb` |

## Concept

- **Silhouette:** a dark spire crowned with shards: a jagged violet crown, a high spiked collar, a black floor-length robe and cape, a star-filled orb in one hand.
- **Outfit:** abyssal black robe with violet light running in the folds, a high spiked collar, long cape, silver claw rings.
- **Weapon / focus:** a dark glass orb swirling with tiny stars, held in a claw-ring setting.
- **Colours:** main `#1a0f24` abyss black, accent `#a050ff` violet, glow `#d090ff` starlight, skin ash grey, hair `#120a1a` black, long. Element rim: #9a5ae8 (added by the shader, don't paint it in).
- **Personality cues:** cold, amused, sovereign; stern brows, faint smirk, glowing lilac eyes.
- **Skills on screen:** Abyssal Touch drains with a dark tendril; Crown of Night drops a ring of shadow; Drown in Dark engulfs one foe in the void.
- **Shape language (Attack):** Triangles and diagonals: forward lean, asymmetric, the weapon line is the longest shape.
- **Rarity finish:** Legendary: a silhouette-breaker above the head (crown, halo, flame, wings), animated glow on large areas, one signature material (molten, starlit glass, sunmetal).
- **Phone check:** at 70 px tall the read is a dark spire crowned with shards; one main colour, one accent, the focus visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, focus, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (a jagged violet crown); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: cape 2x3 bones, robe hem 4 bones, hair 3 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: star orb
- Socket_Cast: orb
- Socket_Back: cape root
- Socket_Head: jagged crown
- Gear rarity variants (optional): `SB_Gear_morwenna_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a ranged hero: every attack is cast in place):

| Skill | Target | Plays |
|---|---|---|
| 1. Abyssal Touch | one enemy | Cast |
| 2. Crown of Night | all enemies | Cast_Ult |
| 3. Drown in Dark | one enemy | Cast_Ult |

| State | Loop | When the game plays it for Morwenna | Direction |
|---|---|---|---|
| Idle | loop | always | prop held up near the chest, light sway; matches the personality: cold, amused, sovereign |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | rarely (casters cast in place) | in place, root locked; the game moves the unit |
| Run_Back | loop | rarely | falls back to Run |
| Attack_1 | once | melee fallback only | prop swing (melee fallback, rarely used) |
| Attack_2 | once | - | - |
| Attack_3 | once | - | - |
| Cast | once | Abyssal Touch | orb thrust forward, release at about 0.3 s |
| Cast_Ult | once | Crown of Night, Drown in Dark (camera zooms in) | the big moment: Crown of Night drops a ring of shadow |
| Cast_Support | once | not used by this hero's skills (falls back to Cast) | - |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (cold, amused, sovereign) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A tall elegant ash-grey-skinned queen with long black hair and a jagged violet crystal crown, glowing lilac eyes, a high spiked collar, an abyssal black floor-length robe with violet light in its folds, a long cape, silver claw rings. Weapon shown separately beside the figure, upright. Main colour abyss black, accent violet, glowing accents in starlight, element mood violet, night glass, starlit void.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A tall elegant ash-grey-skinned queen with long black hair and a jagged violet crystal crown, glowing lilac eyes, a high spiked collar, an abyssal black floor-length robe with violet light in its folds, a long cape, silver claw rings. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour abyss black, accent violet, glowing accents in starlight.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a dark glass orb swirling with tiny stars, held in a claw-ring setting. Main colour abyss black, accent violet, glowing accents in starlight.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour abyss black, accent violet, glowing accents in starlight. Skin painted with warm subsurface tones. Velvet black with violet light gradients in the folds, star flecks; crown and eyes in the glow mask.
```
