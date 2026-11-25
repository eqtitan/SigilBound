# Oriel Sunveil, the sun herald

| | |
|---|---|
| Element | Light (dawn gold, warm ivory, bells and sunrise) |
| Rarity | Epic |
| Role | Support |
| Animation archetype | Caster (`SB_Anim_Caster_<state>`) |
| Build | tall, graceful, long robes |
| Files | `SB_Hero_oriel_LOD0.glb` (LOD1, LOD2), `T_Hero_oriel_Albedo/Normal/Mask.png`, `SB_Gear_oriel_Weapon.glb` |

## Concept

- **Silhouette:** a sunrise: a floating golden halo, a sunburst disc behind the shoulders, a long orange-and-ivory robe, a staff topped by a small sun.
- **Outfit:** layered ivory and burnt-orange robes with gold borders, a wide sash, open sleeves, sandals.
- **Weapon / focus:** a slender gold staff topped with a small radiant sun disc.
- **Colours:** main `#e08a2c` sun orange, accent `#ffe27a` gold, glow `#ffe27a` sunlight, skin deep brown, hair `#f0e6c8` cream-white, swept back. Element rim: #e8c23a (added by the shader, don't paint it in).
- **Personality cues:** warm, joyful, generous; soft kind brows, smile.
- **Skills on screen:** Veil Light is a sunray; Sunveil heals and cleanses in a warm column; Ascend lifts allies in a rising sun burst.
- **Shape language (Support):** Vertical ovals and arches: tall, flowing cloth, upward lines, a light-source prop held high.
- **Rarity finish:** Epic: a cape or large ornament, gold or silver filigree, two or three glowing runes or gems, extra secondary motion.
- **Phone check:** at 70 px tall the read is a sunrise; one main colour, one accent, the focus visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, focus, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (a floating golden halo); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: robe hem 4 bones, sleeves 1 bone each |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: sun staff
- Socket_Cast: staff sun
- Socket_Head: halo (floating, 1 bone)
- Socket_Back: sunburst disc
- Gear rarity variants (optional): `SB_Gear_oriel_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a ranged hero: every attack is cast in place):

| Skill | Target | Plays |
|---|---|---|
| 1. Veil Light | one enemy | Cast |
| 2. Sunveil | all allies, no damage | Cast_Support |
| 3. Ascend | all allies, no damage | Cast_Support |

| State | Loop | When the game plays it for Oriel | Direction |
|---|---|---|---|
| Idle | loop | always | prop held up near the chest, light sway; matches the personality: warm, joyful, generous |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | rarely (casters cast in place) | in place, root locked; the game moves the unit |
| Run_Back | loop | rarely | falls back to Run |
| Attack_1 | once | melee fallback only | prop swing (melee fallback, rarely used) |
| Attack_2 | once | - | - |
| Attack_3 | once | - | - |
| Cast | once | Veil Light | staff thrust forward, release at about 0.3 s |
| Cast_Ult | once | not used by this hero's skills (falls back to Cast) | - |
| Cast_Support | once | Sunveil, Ascend | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (warm, joyful, generous) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A tall graceful dark-skinned herald with swept-back cream-white hair and a floating golden halo, layered ivory and burnt-orange robes with gold borders, a wide sash, open sleeves, sandals, a sunburst disc behind the shoulders. Weapon shown separately beside the figure, upright. Main colour sun orange, accent gold, glowing accents in sunlight, element mood dawn gold, warm ivory, bells and sunrise.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A tall graceful dark-skinned herald with swept-back cream-white hair and a floating golden halo, layered ivory and burnt-orange robes with gold borders, a wide sash, open sleeves, sandals, a sunburst disc behind the shoulders. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour sun orange, accent gold, glowing accents in sunlight.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a slender gold staff topped with a small radiant sun disc. Main colour sun orange, accent gold, glowing accents in sunlight.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour sun orange, accent gold, glowing accents in sunlight. Skin painted with warm subsurface tones. Warm silk with gold borders, sun motifs; halo and sun disc in the glow mask.
```
