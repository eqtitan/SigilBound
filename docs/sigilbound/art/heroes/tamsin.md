# Tamsin Reedvane, the reed-field archer

| | |
|---|---|
| Element | Gale (wind green, reeds and storm sky, airy) |
| Rarity | Rare |
| Role | Support |
| Animation archetype | Archer (`SB_Anim_Archer_<state>`) |
| Build | average, light on her feet |
| Files | `SB_Hero_tamsin_LOD0.glb` (LOD1, LOD2), `T_Hero_tamsin_Albedo/Normal/Mask.png`, `SB_Gear_tamsin_Weapon.glb` |

## Concept

- **Silhouette:** a wide straw hat disc on top, a long braid down the back, a longbow taller than her shoulder, quiver of reed-fletched arrows.
- **Outfit:** moss-green tunic with straw-gold trim, leather bracer and chest guard, field trousers, wrapped boots.
- **Weapon / focus:** a recurved ash longbow wrapped with green cord; a quiver of reed arrows with gold fletching.
- **Colours:** main `#5f7f3a` moss green, accent `#e8d07a` straw gold, glow none, skin tan, hair `#8a6a2a` golden-brown, long braid. Element rim: #3fb57a (added by the shader, don't paint it in).
- **Personality cues:** cheerful, patient, sharp-eyed; relaxed brows, open smile.
- **Skills on screen:** Reed Dart is a quick shot; Tailwind sends a green gust over allies; Field Remedy scatters healing seeds.
- **Shape language (Support):** Vertical ovals and arches: tall, flowing cloth, upward lines, a light-source prop held high.
- **Rarity finish:** Rare: metal trim (bronze or steel) and one small glow detail (eyes, gem or weapon edge); simple pattern on the trim.
- **Phone check:** at 70 px tall the read is a wide straw hat disc on top, a long braid down the back, a longbow taller than her shoulder, quiver of reed-fletched arrows; one main colour, one accent, the focus visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, focus, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (headgear, weapon); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: braid 3 bones, hat brim 0 |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandL: longbow (bow hand)
- Socket_Back: quiver
- Socket_Head: straw hat
- Gear rarity variants (optional): `SB_Gear_tamsin_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a ranged hero: every attack is cast in place):

| Skill | Target | Plays |
|---|---|---|
| 1. Reed Dart | one enemy | Cast |
| 2. Tailwind | all allies, no damage | Cast_Support |
| 3. Field Remedy | all allies, no damage | Cast_Support |

| State | Loop | When the game plays it for Tamsin | Direction |
|---|---|---|---|
| Idle | loop | always | prop held up near the chest, light sway; matches the personality: cheerful, patient, sharp-eyed |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | rarely (casters cast in place) | in place, root locked; the game moves the unit |
| Run_Back | loop | rarely | falls back to Run |
| Attack_1 | once | melee fallback only | bow strike (melee fallback, rarely used) |
| Attack_2 | once | - | - |
| Attack_3 | once | - | - |
| Cast | once | Reed Dart | bow thrust forward, release at about 0.3 s |
| Cast_Ult | once | not used by this hero's skills (falls back to Cast) | - |
| Cast_Support | once | Tailwind, Field Remedy | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (cheerful, patient, sharp-eyed) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A tan young woman with a long golden-brown braid under a wide straw hat, moss-green tunic with straw-gold trim, leather bracer and chest guard, field trousers and wrapped boots, a quiver of reed arrows on her back. Weapon shown separately beside the figure, upright. Main colour moss green, accent straw gold, element mood wind green, reeds and storm sky, airy.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A tan young woman with a long golden-brown braid under a wide straw hat, moss-green tunic with straw-gold trim, leather bracer and chest guard, field trousers and wrapped boots, a quiver of reed arrows on her back. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour moss green, accent straw gold.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a recurved ash longbow wrapped with green cord; a quiver of reed arrows with gold fletching. Main colour moss green, accent straw gold.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour moss green, accent straw gold. Skin painted with warm subsurface tones. Woven straw pattern on the hat, worn leather bracer, linen tunic.
```
