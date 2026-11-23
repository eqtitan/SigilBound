# Ysolde Emberlash, the fire-whip duelist

| | |
|---|---|
| Element | Flame (ember orange-red, forge glow, ash and soot) |
| Rarity | Rare |
| Role | Attack |
| Animation archetype | Blade (`SB_Anim_Blade_<state>`) |
| Build | slim, athletic, tall-ish |
| Files | `SB_Hero_ysolde_LOD0.glb` (LOD1, LOD2), `T_Hero_ysolde_Albedo/Normal/Mask.png`, `SB_Gear_ysolde_Weapon.glb` |

## Concept

- **Silhouette:** an S-curve: long crimson hair, a lashing whip trailing in a curve on the ground, a coiled spare whip at the hip, thin gold circlet.
- **Outfit:** fitted dark-red leather duelling armour with ember-orange edging, one shoulder guard, high boots, wrapped wrists.
- **Weapon / focus:** a long braided leather whip with a glowing ember tip and a brass-capped handle.
- **Colours:** main `#5a1e1e` dark red, accent `#ff7a3a` ember orange, glow `#ffb347` ember, skin fair, hair `#c8281e` crimson, long. Element rim: #e8553a (added by the shader, don't paint it in).
- **Personality cues:** fierce, proud, thrill-seeking; sharp high brows, confident smirk, glowing amber eyes.
- **Skills on screen:** Lash is a double crack of the whip; Searing Coil wraps the target in fire; Wildfire sweeps a burning arc.
- **Shape language (Attack):** Triangles and diagonals: forward lean, asymmetric, the weapon line is the longest shape.
- **Rarity finish:** Rare: metal trim (bronze or steel) and one small glow detail (eyes, gem or weapon edge); simple pattern on the trim.
- **Phone check:** at 70 px tall the read is an S-curve; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (long crimson hair); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: whip 6 bones, hair 3 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: whip handle (whip lash as a 6-bone chain)
- Socket_Hip: coiled whip
- Socket_Head: circlet (part of the head mesh)
- Gear rarity variants (optional): `SB_Gear_ysolde_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Lash | one enemy, 2 hits | Attack_1-3 (run-in, strike, run back) |
| 2. Searing Coil | one enemy | Attack_1-3 (run-in, strike, run back) |
| 3. Wildfire | all enemies | Cast_Ult |

| State | Loop | When the game plays it for Ysolde | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: fierce, proud, thrill-seeking |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Lash, Searing Coil | diagonal slash, step in |
| Attack_2 | once | strike rotation | backhand slash |
| Attack_3 | once | strike rotation | short thrust or front kick |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | Wildfire (camera zooms in) | the big moment: Wildfire sweeps a burning arc |
| Cast_Support | once | not used by this hero's skills (falls back to Cast) | - |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (fierce, proud, thrill-seeking) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A slim fierce young woman with long crimson hair and a thin gold circlet, glowing amber eyes, fitted dark-red leather duelling armour with ember-orange edging, one shoulder guard, wrapped wrists, high boots, a coiled whip at the hip. Weapon shown separately beside the figure, upright. Main colour dark red, accent ember orange, glowing accents in ember, element mood ember orange-red, forge glow, ash and soot.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A slim fierce young woman with long crimson hair and a thin gold circlet, glowing amber eyes, fitted dark-red leather duelling armour with ember-orange edging, one shoulder guard, wrapped wrists, high boots, a coiled whip at the hip. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour dark red, accent ember orange, glowing accents in ember.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a long braided leather whip with a glowing ember tip and a brass-capped handle. Main colour dark red, accent ember orange, glowing accents in ember.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour dark red, accent ember orange, glowing accents in ember. Skin painted with warm subsurface tones. Tooled leather with ember-orange edge paint, scorch marks near the hands; eyes in the glow mask.
```
