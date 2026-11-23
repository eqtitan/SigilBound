# Vaelith, the Last Pyre

| | |
|---|---|
| Element | Flame (ember orange-red, forge glow, ash and soot) |
| Rarity | Legendary |
| Role | Attack |
| Animation archetype | Blade (`SB_Anim_Blade_<state>`) |
| Build | tall, lean, imposing |
| Files | `SB_Hero_vaelith_LOD0.glb` (LOD1, LOD2), `T_Hero_vaelith_Albedo/Normal/Mask.png`, `SB_Gear_vaelith_Weapon.glb` |

## Concept

- **Silhouette:** a living flame: hair that rises as fire, a black-and-gold crown, a ragged ember cape, glowing shoulder orbs, a burning longsword.
- **Outfit:** charred crimson plate with molten-gold edges, a black-and-gold crown, an ember-shredded cape, glowing aura orbs at the shoulders.
- **Weapon / focus:** a long blackened sword with a molten-gold fuller and flames along the edge.
- **Colours:** main `#3a1010` charred crimson, accent `#ffb020` molten gold, glow `#ffd060` blaze, skin fair, hair `#ff5a2a` living flame. Element rim: #e8553a (added by the shader, don't paint it in).
- **Personality cues:** grieving, relentless, regal fury; sharp stern brows, frown, blazing golden eyes.
- **Skills on screen:** Pyre Brand brands with fire; Cinder Rain calls burning ash on all foes; Last Pyre is a huge overhead flaming strike.
- **Shape language (Attack):** Triangles and diagonals: forward lean, asymmetric, the weapon line is the longest shape.
- **Rarity finish:** Legendary: a silhouette-breaker above the head (crown, halo, flame, wings), animated glow on large areas, one signature material (molten, starlit glass, sunmetal).
- **Phone check:** at 70 px tall the read is a living flame; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (hair that rises as fire); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: flame hair 4 bones (or VFX), cape 2x3 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: burning sword
- Socket_Back: cape root
- Socket_Head: crown
- Socket_Cast: sword tip
- Gear rarity variants (optional): `SB_Gear_vaelith_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Pyre Brand | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Cinder Rain | all enemies, 2 hits | Cast_Ult |
| 3. Last Pyre | one enemy | Attack_1-3 (run-in, strike, run back) |

| State | Loop | When the game plays it for Vaelith | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: grieving, relentless, regal fury |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Pyre Brand, Last Pyre | diagonal slash, step in |
| Attack_2 | once | strike rotation | backhand slash |
| Attack_3 | once | strike rotation | short thrust or front kick |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | Cinder Rain (camera zooms in) | the big moment: Cinder Rain calls burning ash on all foes |
| Cast_Support | once | not used by this hero's skills (falls back to Cast) | - |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (grieving, relentless, regal fury) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A tall lean fair-skinned figure with hair that rises like living flame, a black-and-gold crown, blazing golden eyes, charred crimson plate armour with molten-gold edges, glowing ember orbs above the shoulders, a ragged ember-shredded cape. Weapon shown separately beside the figure, upright. Main colour charred crimson, accent molten gold, glowing accents in blaze, element mood ember orange-red, forge glow, ash and soot.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A tall lean fair-skinned figure with hair that rises like living flame, a black-and-gold crown, blazing golden eyes, charred crimson plate armour with molten-gold edges, glowing ember orbs above the shoulders, a ragged ember-shredded cape. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour charred crimson, accent molten gold, glowing accents in blaze.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a long blackened sword with a molten-gold fuller and flames along the edge. Main colour charred crimson, accent molten gold, glowing accents in blaze.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour charred crimson, accent molten gold, glowing accents in blaze. Skin painted with warm subsurface tones. Charred metal with molten-gold cracks, ember-eaten cape edges; hair, eyes and cracks in the glow mask.
```
