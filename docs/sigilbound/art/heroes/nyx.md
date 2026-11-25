# Nyx Hollowstep, the hollowstep blade

| | |
|---|---|
| Element | Shadow (violet, night glass, starlit void) |
| Rarity | Rare |
| Role | Attack |
| Animation archetype | Daggers (`SB_Anim_Daggers_<state>`) |
| Build | slim, light, crouching readiness |
| Files | `SB_Hero_nyx_LOD0.glb` (LOD1, LOD2), `T_Hero_nyx_Albedo/Normal/Mask.png`, `SB_Gear_nyx_Weapon.glb` |

## Concept

- **Silhouette:** a small sharp arrowhead: deep hood, cloth face-wrap, two long violet scarf tails streaming back, a dagger reversed in each hand.
- **Outfit:** close-fitting deep-violet wraps and hood, layered leather straps, a violet scarf, soft split-toe boots.
- **Weapon / focus:** two slim curved daggers with smoky-glass blades and wrapped grips.
- **Colours:** main `#26203a` night violet, accent `#b48cff` lilac, glow `#c9a3ff` lilac, skin pale, hair `#1e1a2a` black (hidden). Element rim: #9a5ae8 (added by the shader, don't paint it in).
- **Personality cues:** quiet, precise, unreadable; stern brows, only the glowing lilac eyes visible.
- **Skills on screen:** Hollow Cut is a three-hit flurry; Vanish blinks behind the target; Night Toll rings a shadow pulse.
- **Shape language (Attack):** Triangles and diagonals: forward lean, asymmetric, the weapon line is the longest shape.
- **Rarity finish:** Rare: metal trim (bronze or steel) and one small glow detail (eyes, gem or weapon edge); simple pattern on the trim.
- **Phone check:** at 70 px tall the read is a small sharp arrowhead; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (deep hood); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: scarf tails 2x3 bones, hood rim 1 bone |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: dagger
- Socket_HandL: dagger
- Socket_Hip: sheaths
- Gear rarity variants (optional): `SB_Gear_nyx_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Hollow Cut | one enemy, 3 hits | Attack_1-3 (run-in, strike, run back) |
| 2. Vanish | one enemy | Attack_1-3 (run-in, strike, run back) |
| 3. Night Toll | all enemies | Cast_Ult |

| State | Loop | When the game plays it for Nyx | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: quiet, precise, unreadable |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Hollow Cut, Vanish | crossing double slash |
| Attack_2 | once | strike rotation | reverse-grip stab |
| Attack_3 | once | strike rotation | spinning kick |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | Night Toll (camera zooms in) | the big moment: Night Toll rings a shadow pulse |
| Cast_Support | once | not used by this hero's skills (falls back to Cast) | - |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (quiet, precise, unreadable) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A slender figure in a deep-violet hood with a cloth wrap over the lower face, glowing lilac eyes, close-fitting violet wraps and layered leather straps, long violet scarf tails, soft split-toe boots, two dagger sheaths at the hips. Weapon shown separately beside the figure, upright. Main colour night violet, accent lilac, glowing accents in lilac, element mood violet, night glass, starlit void.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A slender figure in a deep-violet hood with a cloth wrap over the lower face, glowing lilac eyes, close-fitting violet wraps and layered leather straps, long violet scarf tails, soft split-toe boots, two dagger sheaths at the hips. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour night violet, accent lilac, glowing accents in lilac.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. two slim curved daggers with smoky-glass blades and wrapped grips. Main colour night violet, accent lilac, glowing accents in lilac.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour night violet, accent lilac, glowing accents in lilac. Skin painted with warm subsurface tones. Matte layered cloth, smoky violet gradient into black at the hems; eyes in the glow mask.
```
