# Aurelia Dawnwarden

| | |
|---|---|
| Element | Light (dawn gold, warm ivory, bells and sunrise) |
| Rarity | Epic |
| Role | Defense |
| Animation archetype | ShieldBlade (`SB_Anim_ShieldBlade_<state>`) |
| Build | tall, strong, balanced |
| Files | `SB_Hero_aurelia_LOD0.glb` (LOD1, LOD2), `T_Hero_aurelia_Albedo/Normal/Mask.png`, `SB_Gear_aurelia_Weapon.glb` |

## Concept

- **Silhouette:** a cathedral door: a huge tower shield, a winged helm, a long royal-blue cape, gold-and-white plate.
- **Outfit:** gold-and-ivory plate with a white tabard bearing a gold sunrise emblem, royal-blue cape, gauntlets and sabatons.
- **Weapon / focus:** a radiant tower shield with a sunrise relief, and a short broad sword.
- **Colours:** main `#d9b24a` dawn gold, accent `#f5f0e0` ivory, glow `#fff2b0` dawn light, skin fair, hair `#e8b84a` golden, long, under the helm. Element rim: #e8c23a (added by the shader, don't paint it in).
- **Personality cues:** steadfast, noble, warm under the armour; level brows, calm mouth, glowing pale-gold eyes.
- **Skills on screen:** Warden's Blow is a shield-edge strike; Aegis of Dawn raises a golden dome; Sunfall slams the shield down in a ring of light.
- **Shape language (Defense):** Squares and rectangles: wide shoulders, low centre of gravity, symmetrical front, the shield or armour reads first.
- **Rarity finish:** Epic: a cape or large ornament, gold or silver filigree, two or three glowing runes or gems, extra secondary motion.
- **Phone check:** at 70 px tall the read is a cathedral door; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (a huge tower shield); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: cape 2x3 bones, tabard 2 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandL: tower shield
- Socket_HandR: short sword
- Socket_Back: cape root
- Socket_Head: winged helm
- Gear rarity variants (optional): `SB_Gear_aurelia_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Warden's Blow | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Aegis of Dawn | all allies, no damage | Cast_Support |
| 3. Sunfall | all enemies | Cast_Ult |

| State | Loop | When the game plays it for Aurelia | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: steadfast, noble, warm under the armour |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Warden's Blow | sword slash from behind the shield |
| Attack_2 | once | strike rotation | shield bash |
| Attack_3 | once | strike rotation | shield-up thrust |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | Sunfall (camera zooms in) | the big moment: Sunfall slams the shield down in a ring of light |
| Cast_Support | once | Aegis of Dawn | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (steadfast, noble, warm under the armour) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A tall strong fair-skinned woman with long golden hair under a winged steel helm, glowing pale-gold eyes, gold-and-ivory plate armour, a white tabard with a gold sunrise emblem, a long royal-blue cape, gauntlets and sabatons. Weapon shown separately beside the figure, upright. Main colour dawn gold, accent ivory, glowing accents in dawn light, element mood dawn gold, warm ivory, bells and sunrise.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A tall strong fair-skinned woman with long golden hair under a winged steel helm, glowing pale-gold eyes, gold-and-ivory plate armour, a white tabard with a gold sunrise emblem, a long royal-blue cape, gauntlets and sabatons. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour dawn gold, accent ivory, glowing accents in dawn light.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a radiant tower shield with a sunrise relief, and a short broad sword. Main colour dawn gold, accent ivory, glowing accents in dawn light.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour dawn gold, accent ivory, glowing accents in dawn light. Skin painted with warm subsurface tones. Burnished gold with ivory enamel, sunrise relief on the shield (emissive rays in the glow mask).
```
