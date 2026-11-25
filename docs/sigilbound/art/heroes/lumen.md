# Lumen Varro, the radiant blade

| | |
|---|---|
| Element | Light (dawn gold, warm ivory, bells and sunrise) |
| Rarity | Rare |
| Role | Attack |
| Animation archetype | Blade (`SB_Anim_Blade_<state>`) |
| Build | tall, heroic, slightly smaller head |
| Files | `SB_Hero_lumen_LOD0.glb` (LOD1, LOD2), `T_Hero_lumen_Albedo/Normal/Mask.png`, `SB_Gear_lumen_Weapon.glb` |

## Concept

- **Silhouette:** a clean heroic V: broad gold-trimmed pauldrons, a bright breastplate, swept golden hair, a longsword that glows along the edge.
- **Outfit:** silver-white breastplate with a sunburst ridge, gold pauldrons, slate-blue padded sleeves and trousers, polished greaves.
- **Weapon / focus:** a straight longsword with a gold crossguard and a softly glowing pale-gold edge.
- **Colours:** main `#c8d0dc` silver-white, accent `#ffcc33` gold, glow `#fff2b0` pale gold, skin fair, hair `#f2d27a` golden, swept back. Element rim: #e8c23a (added by the shader, don't paint it in).
- **Personality cues:** confident, upbeat, a little showy; level brows, bright smile, glowing pale-gold eyes.
- **Skills on screen:** Radiant Edge is a clean slash; Judgment drives a pillar of light down; Dawnbreak flashes a sunrise wave.
- **Shape language (Attack):** Triangles and diagonals: forward lean, asymmetric, the weapon line is the longest shape.
- **Rarity finish:** Rare: metal trim (bronze or steel) and one small glow detail (eyes, gem or weapon edge); simple pattern on the trim.
- **Phone check:** at 70 px tall the read is a clean heroic V; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (broad gold-trimmed pauldrons); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: none (tight silhouette) |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: longsword
- Socket_Hip: scabbard
- Gear rarity variants (optional): `SB_Gear_lumen_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Radiant Edge | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Judgment | one enemy | Attack_1-3 (run-in, strike, run back) |
| 3. Dawnbreak | all enemies | Cast_Ult |

| State | Loop | When the game plays it for Lumen | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: confident, upbeat, a little showy |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Radiant Edge, Judgment | diagonal slash, step in |
| Attack_2 | once | strike rotation | backhand slash |
| Attack_3 | once | strike rotation | short thrust or front kick |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | Dawnbreak (camera zooms in) | the big moment: Dawnbreak flashes a sunrise wave |
| Cast_Support | once | not used by this hero's skills (falls back to Cast) | - |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (confident, upbeat, a little showy) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A tall heroic fair-skinned man with swept-back golden hair and glowing pale-gold eyes, silver-white breastplate with a sunburst ridge, broad gold pauldrons, slate-blue padded sleeves and trousers, polished greaves, a scabbard at the hip. Weapon shown separately beside the figure, upright. Main colour silver-white, accent gold, glowing accents in pale gold, element mood dawn gold, warm ivory, bells and sunrise.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A tall heroic fair-skinned man with swept-back golden hair and glowing pale-gold eyes, silver-white breastplate with a sunburst ridge, broad gold pauldrons, slate-blue padded sleeves and trousers, polished greaves, a scabbard at the hip. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour silver-white, accent gold, glowing accents in pale gold.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a straight longsword with a gold crossguard and a softly glowing pale-gold edge. Main colour silver-white, accent gold, glowing accents in pale gold.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour silver-white, accent gold, glowing accents in pale gold. Skin painted with warm subsurface tones. Polished silver with warm gold trim and a faint sunburst etch; eyes and edge in the glow mask.
```
