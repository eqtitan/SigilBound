# Ulfgar Stormwall

| | |
|---|---|
| Element | Gale (wind green, reeds and storm sky, airy) |
| Rarity | Legendary |
| Role | Defense |
| Animation archetype | ShieldBlade (`SB_Anim_ShieldBlade_<state>`) |
| Build | tall, very broad, elder warrior |
| Files | `SB_Hero_ulfgar_LOD0.glb` (LOD1, LOD2), `T_Hero_ulfgar_Albedo/Normal/Mask.png`, `SB_Gear_ulfgar_Weapon.glb` |

## Concept

- **Silhouette:** a mountain: a horned helm, a fur mantle, a braided silver beard, a massive round shield, an axe, a storm-blue cape.
- **Outfit:** slate-blue plate with ice-blue trim, a white-grey fur mantle, a storm-blue cape, glowing ice-blue aura orbs at the shoulders.
- **Weapon / focus:** a massive round shield with an iron boss and storm-rune rim; a bearded war axe.
- **Colours:** main `#8a9aac` slate blue, accent `#bfe8ff` ice blue, glow `#bfe8ff` storm, skin tan, hair `#c8ccd0` silver, long, braided beard. Element rim: #3fb57a (added by the shader, don't paint it in).
- **Personality cues:** stoic, protective, ancient patience; level stern brows, flat mouth hidden by the beard.
- **Skills on screen:** Stormwall Bash rams with the shield; Hold the Line plants the shield and roars; Avalanche brings down a crash of ice and snow.
- **Shape language (Defense):** Squares and rectangles: wide shoulders, low centre of gravity, symmetrical front, the shield or armour reads first.
- **Rarity finish:** Legendary: a silhouette-breaker above the head (crown, halo, flame, wings), animated glow on large areas, one signature material (molten, starlit glass, sunmetal).
- **Phone check:** at 70 px tall the read is a mountain; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (a horned helm); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: cape 2x3 bones, beard braids 2 bones, mantle 1 bone |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandL: round shield
- Socket_HandR: war axe
- Socket_Back: cape root
- Socket_Head: horned helm
- Gear rarity variants (optional): `SB_Gear_ulfgar_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Stormwall Bash | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Hold the Line | all allies, no damage | Cast_Support |
| 3. Avalanche | all enemies | Cast_Ult |

| State | Loop | When the game plays it for Ulfgar | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: stoic, protective, ancient patience |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Stormwall Bash | sword slash from behind the shield |
| Attack_2 | once | strike rotation | shield bash |
| Attack_3 | once | strike rotation | shield-up thrust |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | Avalanche (camera zooms in) | the big moment: Avalanche brings down a crash of ice and snow |
| Cast_Support | once | Hold the Line | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (stoic, protective, ancient patience) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A tall very broad tan elder warrior with long silver hair and a braided silver beard under a horned steel helm, slate-blue plate armour with ice-blue trim, a thick grey-white fur mantle, a storm-blue cape, glowing ice-blue orbs above the shoulders. Weapon shown separately beside the figure, upright. Main colour slate blue, accent ice blue, glowing accents in storm, element mood wind green, reeds and storm sky, airy.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A tall very broad tan elder warrior with long silver hair and a braided silver beard under a horned steel helm, slate-blue plate armour with ice-blue trim, a thick grey-white fur mantle, a storm-blue cape, glowing ice-blue orbs above the shoulders. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour slate blue, accent ice blue, glowing accents in storm.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a massive round shield with an iron boss and storm-rune rim; a bearded war axe. Main colour slate blue, accent ice blue, glowing accents in storm.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour slate blue, accent ice blue, glowing accents in storm. Skin painted with warm subsurface tones. Frost-touched steel, fur painted in clumps, ice-blue rune trim in the glow mask.
```
