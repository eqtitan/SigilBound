# Kaito Stormrend, the storm swordsman

| | |
|---|---|
| Element | Gale (wind green, reeds and storm sky, airy) |
| Rarity | Epic |
| Role | Attack |
| Animation archetype | Blade (`SB_Anim_Blade_<state>`) |
| Build | athletic, light, forward-leaning |
| Files | `SB_Hero_kaito_LOD0.glb` (LOD1, LOD2), `T_Hero_kaito_Albedo/Normal/Mask.png`, `SB_Gear_kaito_Weapon.glb` |

## Concept

- **Silhouette:** a lightning zig-zag: spiky black hair, mint scarf and headband tails whipping back, one jade pauldron, a curved sword low and ready, scabbard angled behind.
- **Outfit:** dark-jade lamellar vest over a fitted shirt, one jade shoulder plate, wrapped forearms, split hakama-like trousers, a mint scarf.
- **Weapon / focus:** a curved single-edged sword with a mint-wrapped hilt and a faint wind-glow on the edge; a black lacquered scabbard.
- **Colours:** main `#2f5a4a` dark jade, accent `#7affc0` mint, glow `#a8ffdf` wind, skin fair, hair `#1a1a1a` black, spiky. Element rim: #3fb57a (added by the shader, don't paint it in).
- **Personality cues:** intense, focused, few words; sharp high brows, flat mouth, glowing mint eyes.
- **Skills on screen:** Rending Gust is a double slash; Thousand Cuts is a four-hit blur; Stormrend unleashes a wind cyclone.
- **Shape language (Attack):** Triangles and diagonals: forward lean, asymmetric, the weapon line is the longest shape.
- **Rarity finish:** Epic: a cape or large ornament, gold or silver filigree, two or three glowing runes or gems, extra secondary motion.
- **Phone check:** at 70 px tall the read is a lightning zig-zag; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (spiky black hair); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: scarf 2x3 bones, headband tails 2 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: curved sword
- Socket_Hip: scabbard
- Socket_Head: headband tails
- Gear rarity variants (optional): `SB_Gear_kaito_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Rending Gust | one enemy, 2 hits | Attack_1-3 (run-in, strike, run back) |
| 2. Thousand Cuts | one enemy, 4 hits | Attack_1-3 (run-in, strike, run back) |
| 3. Stormrend | all enemies | Cast_Ult |

| State | Loop | When the game plays it for Kaito | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: intense, focused, few words |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Rending Gust, Thousand Cuts | diagonal slash, step in |
| Attack_2 | once | strike rotation | backhand slash |
| Attack_3 | once | strike rotation | short thrust or front kick |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | Stormrend (camera zooms in) | the big moment: Stormrend unleashes a wind cyclone |
| Cast_Support | once | not used by this hero's skills (falls back to Cast) | - |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (intense, focused, few words) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. An athletic young man with spiky black hair, a green headband with trailing tails, glowing mint eyes, dark-jade lamellar vest over a fitted shirt, one jade shoulder plate, wrapped forearms, wide split trousers, a long mint scarf, a black scabbard at the hip. Weapon shown separately beside the figure, upright. Main colour dark jade, accent mint, glowing accents in wind, element mood wind green, reeds and storm sky, airy.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. An athletic young man with spiky black hair, a green headband with trailing tails, glowing mint eyes, dark-jade lamellar vest over a fitted shirt, one jade shoulder plate, wrapped forearms, wide split trousers, a long mint scarf, a black scabbard at the hip. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour dark jade, accent mint, glowing accents in wind.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a curved single-edged sword with a mint-wrapped hilt and a faint wind-glow on the edge; a black lacquered scabbard. Main colour dark jade, accent mint, glowing accents in wind.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour dark jade, accent mint, glowing accents in wind. Skin painted with warm subsurface tones. Lacquered lamellar plates with mint cord lacing, crisp edge highlights.
```
