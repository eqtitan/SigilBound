# Odo Mudlark, the marsh mudlark

| | |
|---|---|
| Element | Gale (wind green, reeds and storm sky, airy) |
| Rarity | Common |
| Role | HP |
| Animation archetype | Brawler (`SB_Anim_Brawler_<state>`) |
| Build | huge and round: barrel chest, thick arms and legs, small head |
| Files | `SB_Hero_odo_LOD0.glb` (LOD1, LOD2), `T_Hero_odo_Albedo/Normal/Mask.png`, `SB_Gear_odo_Weapon.glb` |

## Concept

- **Silhouette:** a big boulder: round shoulders under a shaggy moss mantle, a gnarled root club resting on one shoulder, bald head with a big black beard.
- **Outfit:** mud-brown hide vest laced over bare arms, moss-green mantle with hanging reeds, patched breeches, mud-caked foot wraps.
- **Weapon / focus:** a gnarled bog-oak root club, knobbed and moss-spotted, bound with rope at the grip.
- **Colours:** main `#4e3626` mud brown, accent `#9ab84a` moss green, glow none, skin brown, hair `#1e1610` black, bald with a full beard. Element rim: #3fb57a (added by the shader, don't paint it in).
- **Personality cues:** good-natured, slow to anger, immovable; kind brows, big smile under the beard.
- **Skills on screen:** Mud Slap is a heavy open-hand slap; Thick Hide hunches him into a mud-armoured stance.
- **Shape language (HP):** Circles and mass: round, heavy forms, thick limbs, soft big shapes that look hard to knock down.
- **Rarity finish:** Common: practical matte materials (linen, leather, iron), one accent colour, no glow, no cape.
- **Phone check:** at 70 px tall the read is a big boulder; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (round shoulders under a shaggy moss mantle); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: moss mantle 2 bones, reeds via texture alpha |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: root club
- Socket_Back: club rest pose on shoulder (optional)
- Gear rarity variants (optional): `SB_Gear_odo_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Mud Slap | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Thick Hide | self, no damage | Cast_Support |

| State | Loop | When the game plays it for Odo | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: good-natured, slow to anger, immovable |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Mud Slap | right hook or club swing |
| Attack_2 | once | strike rotation | left jab or backhand |
| Attack_3 | once | strike rotation | heavy kick or shoulder barge |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | not used by this hero's skills (falls back to Cast) | - |
| Cast_Support | once | Thick Hide | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (good-natured, slow to anger, immovable) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A huge broad dark-skinned bald man with a thick black beard, barrel chest and thick arms, mud-brown laced hide vest, shaggy moss-green mantle with hanging reeds, patched breeches and mud-caked foot wraps. Weapon shown separately beside the figure, upright. Main colour mud brown, accent moss green, element mood wind green, reeds and storm sky, airy.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A huge broad dark-skinned bald man with a thick black beard, barrel chest and thick arms, mud-brown laced hide vest, shaggy moss-green mantle with hanging reeds, patched breeches and mud-caked foot wraps. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour mud brown, accent moss green.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a gnarled bog-oak root club, knobbed and moss-spotted, bound with rope at the grip. Main colour mud brown, accent moss green.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour mud brown, accent moss green. Skin painted with warm subsurface tones. Dried mud cracks on the lower legs, mossy fibres on the mantle, oiled hide.
```
