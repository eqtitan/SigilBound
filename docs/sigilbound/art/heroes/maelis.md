# Maelis Deepcurrent, the deep-current queen

| | |
|---|---|
| Element | Tide (sea blue, sea-glass and foam, deep water) |
| Rarity | Epic |
| Role | Attack |
| Animation archetype | Polearm (`SB_Anim_Polearm_<state>`) |
| Build | tall, regal, long limbs |
| Files | `SB_Hero_maelis_LOD0.glb` (LOD1, LOD2), `T_Hero_maelis_Albedo/Normal/Mask.png`, `SB_Gear_maelis_Weapon.glb` |

## Concept

- **Silhouette:** a crowned trident: branching coral crown, shell pauldrons, a long cape, a three-pronged trident taller than she is.
- **Outfit:** deep-blue scale armour with bright aqua trim, nautilus-shell pauldrons, a sea-green cape, greaves with fin edges.
- **Weapon / focus:** a long trident with a coral-and-bronze head and aqua glowing tines.
- **Colours:** main `#1a4a6a` deep blue, accent `#4ad0ff` aqua, glow `#4ad0ff` aqua, skin brown, hair `#163a5a` navy, long. Element rim: #3a8ee8 (added by the shader, don't paint it in).
- **Personality cues:** proud, commanding, playful menace; arched brows, smirk.
- **Skills on screen:** Trident is a thrust; Riptide drags the foe in a water spiral; Maelstrom spins a whirlpool over all foes.
- **Shape language (Attack):** Triangles and diagonals: forward lean, asymmetric, the weapon line is the longest shape.
- **Rarity finish:** Epic: a cape or large ornament, gold or silver filigree, two or three glowing runes or gems, extra secondary motion.
- **Phone check:** at 70 px tall the read is a crowned trident; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (branching coral crown); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: cape 2x3 bones, hair 3 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: trident
- Socket_Back: cape root
- Socket_Head: coral crown
- Gear rarity variants (optional): `SB_Gear_maelis_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Trident | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Riptide | one enemy | Attack_1-3 (run-in, strike, run back) |
| 3. Maelstrom | all enemies | Cast_Ult |

| State | Loop | When the game plays it for Maelis | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: proud, commanding, playful menace |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Trident, Riptide | two-handed thrust at full reach |
| Attack_2 | once | strike rotation | sweeping haft strike |
| Attack_3 | once | strike rotation | spinning overhead chop |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | Maelstrom (camera zooms in) | the big moment: Maelstrom spins a whirlpool over all foes |
| Cast_Support | once | not used by this hero's skills (falls back to Cast) | - |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (proud, commanding, playful menace) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A tall regal dark-skinned woman with long navy hair and a branching coral crown, deep-blue scale armour with bright aqua trim, nautilus-shell pauldrons, a long sea-green cape, greaves with fin-shaped edges. Weapon shown separately beside the figure, upright. Main colour deep blue, accent aqua, glowing accents in aqua, element mood sea blue, sea-glass and foam, deep water.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A tall regal dark-skinned woman with long navy hair and a branching coral crown, deep-blue scale armour with bright aqua trim, nautilus-shell pauldrons, a long sea-green cape, greaves with fin-shaped edges. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour deep blue, accent aqua, glowing accents in aqua.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a long trident with a coral-and-bronze head and aqua glowing tines. Main colour deep blue, accent aqua, glowing accents in aqua.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour deep blue, accent aqua, glowing accents in aqua. Skin painted with warm subsurface tones. Overlapping scales with iridescent aqua edges, coral with soft pink-orange undertones.
```
