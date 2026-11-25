# Brannoc Spearhand, the road spearman

| | |
|---|---|
| Element | Gale (wind green, reeds and storm sky, airy) |
| Rarity | Common |
| Role | Attack |
| Animation archetype | Polearm (`SB_Anim_Polearm_<state>`) |
| Build | lean, wiry, average height; long reach |
| Files | `SB_Hero_brannoc_LOD0.glb` (LOD1, LOD2), `T_Hero_brannoc_Albedo/Normal/Mask.png`, `SB_Gear_brannoc_Weapon.glb` |

## Concept

- **Silhouette:** a tall diagonal: the spear (longer than he is) slanting forward with a pennant snapping at the tip, bandana tails flicking behind his head, a small travel pack on one shoulder.
- **Outfit:** olive-green quilted leather jerkin with brass buckles over a rust-brown shirt, rolled sleeves, belted with pouches, wrapped shins, worn boots.
- **Weapon / focus:** an ash-wood spear with an iron leaf blade, leather grip wraps and a mint-green pennant tied below the blade.
- **Colours:** main `#4f7a34` olive green, accent `#d8b04a` brass, glow none, skin tan, hair `#5a3a22` brown, short, stubble. Element rim: #3fb57a (added by the shader, don't paint it in).
- **Personality cues:** easy grin, always moving, the friendly veteran of the road; raised brows, relaxed stance with weight on the back foot.
- **Skills on screen:** Spear Thrust is a fast lunge; Whirling Haft spins the spear overhead in a green wind ring.
- **Shape language (Attack):** Triangles and diagonals: forward lean, asymmetric, the weapon line is the longest shape.
- **Rarity finish:** Common: practical matte materials (linen, leather, iron), one accent colour, no glow, no cape.
- **Phone check:** at 70 px tall the read is a tall diagonal; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (the spear (longer than he is) slanting forward with a pennant snapping at the tip); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: bandana tails 2 bones, pennant 2 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: spear (grip at a third of the shaft)
- Socket_Back: travel pack
- Socket_Head: bandana tails (2 cloth bones)
- Gear rarity variants (optional): `SB_Gear_brannoc_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Spear Thrust | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Whirling Haft | all enemies | Cast_Ult |

| State | Loop | When the game plays it for Brannoc | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: easy grin, always moving, the friendly veteran of the road |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Spear Thrust | two-handed thrust at full reach |
| Attack_2 | once | strike rotation | sweeping haft strike |
| Attack_3 | once | strike rotation | spinning overhead chop |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | Whirling Haft (camera zooms in) | the big moment: Whirling Haft spins the spear overhead in a green wind ring |
| Cast_Support | once | not used by this hero's skills (falls back to Cast) | - |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (easy grin, always moving, the friendly veteran of the road) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A lean tan-skinned man in his thirties with short brown hair and stubble, knotted mint-green bandana with two trailing tails, olive-green quilted leather jerkin with brass buckles, rolled rust-brown sleeves, belt with pouches, wrapped shins and worn boots, small travel pack. Weapon shown separately beside the figure, upright. Main colour olive green, accent brass, element mood wind green, reeds and storm sky, airy.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A lean tan-skinned man in his thirties with short brown hair and stubble, knotted mint-green bandana with two trailing tails, olive-green quilted leather jerkin with brass buckles, rolled rust-brown sleeves, belt with pouches, wrapped shins and worn boots, small travel pack. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour olive green, accent brass.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. an ash-wood spear with an iron leaf blade, leather grip wraps and a mint-green pennant tied below the blade. Main colour olive green, accent brass.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour olive green, accent brass. Skin painted with warm subsurface tones. Worn leather with scuffed edges, brass buckles polished at the corners, faded cloth.
```
