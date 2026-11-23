# Pell Cinderbrook, the candle-keeper's apprentice

| | |
|---|---|
| Element | Flame (ember orange-red, forge glow, ash and soot) |
| Rarity | Common |
| Role | Support |
| Animation archetype | Caster (`SB_Anim_Caster_<state>`) |
| Build | short, slight, big head and hands (a youth) |
| Files | `SB_Hero_pell_LOD0.glb` (LOD1, LOD2), `T_Hero_pell_Albedo/Normal/Mask.png`, `SB_Gear_pell_Weapon.glb` |

## Concept

- **Silhouette:** a small triangle: a knee-length apprentice tunic flaring out, messy copper hair, both hands cupping a glowing candle in a brass holder.
- **Outfit:** rust-brown apprentice tunic with amber piping and wax-dripped sleeves, dark belt with a tinder pouch and spare candles, soft boots.
- **Weapon / focus:** a stubby beeswax candle in a brass dish holder with a ring handle; the flame is the spell focus.
- **Colours:** main `#8a4a2a` rust brown, accent `#ffb347` amber, glow `#ffb347` candle amber, skin pale, freckled, hair `#c8642a` copper, messy. Element rim: #e8553a (added by the shader, don't paint it in).
- **Personality cues:** gentle, eager, a little shy; kind brows, small smile, shoulders slightly hunched over the flame.
- **Skills on screen:** Ember Flick tosses a spark; Kindle spreads warm healing light from the candle.
- **Shape language (Support):** Vertical ovals and arches: tall, flowing cloth, upward lines, a light-source prop held high.
- **Rarity finish:** Common: practical matte materials (linen, leather, iron), one accent colour, no glow, no cape.
- **Phone check:** at 70 px tall the read is a small triangle; one main colour, one accent, the focus visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, focus, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (a knee-length apprentice tunic flaring out); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: tunic hem 2 bones front and back |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: candle holder
- Socket_Cast: flame point at the wick
- Socket_Hip: candle bundle
- Gear rarity variants (optional): `SB_Gear_pell_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a ranged hero: every attack is cast in place):

| Skill | Target | Plays |
|---|---|---|
| 1. Ember Flick | one enemy | Cast |
| 2. Kindle | all allies, no damage | Cast_Support |

| State | Loop | When the game plays it for Pell | Direction |
|---|---|---|---|
| Idle | loop | always | prop held up near the chest, light sway; matches the personality: gentle, eager, a little shy |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | rarely (casters cast in place) | in place, root locked; the game moves the unit |
| Run_Back | loop | rarely | falls back to Run |
| Attack_1 | once | melee fallback only | prop swing (melee fallback, rarely used) |
| Attack_2 | once | - | - |
| Attack_3 | once | - | - |
| Cast | once | Ember Flick | candle thrust forward, release at about 0.3 s |
| Cast_Ult | once | not used by this hero's skills (falls back to Cast) | - |
| Cast_Support | once | Kindle | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (gentle, eager, a little shy) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A slight freckled teenage boy with messy copper hair, rust-brown knee-length apprentice tunic with amber piping and wax-dripped sleeves, dark belt with a tinder pouch and a bundle of spare candles, soft boots. Weapon shown separately beside the figure, upright. Main colour rust brown, accent amber, glowing accents in candle amber, element mood ember orange-red, forge glow, ash and soot.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A slight freckled teenage boy with messy copper hair, rust-brown knee-length apprentice tunic with amber piping and wax-dripped sleeves, dark belt with a tinder pouch and a bundle of spare candles, soft boots. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour rust brown, accent amber, glowing accents in candle amber.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a stubby beeswax candle in a brass dish holder with a ring handle; the flame is the spell focus. Main colour rust brown, accent amber, glowing accents in candle amber.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour rust brown, accent amber, glowing accents in candle amber. Skin painted with warm subsurface tones. Wax drips on the sleeves, soot smudges on the fingertips, warm amber piping.
```
