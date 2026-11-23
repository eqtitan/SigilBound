# Mira Saltwhistle, the dock warden

| | |
|---|---|
| Element | Tide (sea blue, sea-glass and foam, deep water) |
| Rarity | Common |
| Role | Defense |
| Animation archetype | ShieldBlade (`SB_Anim_ShieldBlade_<state>`) |
| Build | small and compact, sturdy legs, slightly larger head (young) |
| Files | `SB_Hero_mira_LOD0.glb` (LOD1, LOD2), `T_Hero_mira_Albedo/Normal/Mask.png`, `SB_Gear_mira_Weapon.glb` |

## Concept

- **Silhouette:** a rounded block: a big round shell-studded shield covering half her body, teal ponytail arcing behind, short sword peeking out low.
- **Outfit:** sea-blue padded gambeson with pale shell-white trim and toggles, deep-teal trousers tucked into rope-bound boots, a salt-white sash.
- **Weapon / focus:** a round wooden shield rimmed in pale bronze and studded with shells; a short broad sword.
- **Colours:** main `#3f7f9a` sea blue, accent `#e8e0c8` shell white, glow none, skin fair, hair `#2f8f8f` teal, high ponytail. Element rim: #3a8ee8 (added by the shader, don't paint it in).
- **Personality cues:** calm, steady, unflappable; flat mouth, brows slightly lowered, feet planted.
- **Skills on screen:** Shell Bash shoves with the shield; Saltwall raises a shimmering wall of salt spray.
- **Shape language (Defense):** Squares and rectangles: wide shoulders, low centre of gravity, symmetrical front, the shield or armour reads first.
- **Rarity finish:** Common: practical matte materials (linen, leather, iron), one accent colour, no glow, no cape.
- **Phone check:** at 70 px tall the read is a rounded block; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (a big round shell-studded shield covering half her body); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: ponytail 3 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandL: round shield (strap on the forearm)
- Socket_HandR: short sword
- Socket_Hip: scabbard
- Gear rarity variants (optional): `SB_Gear_mira_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Shell Bash | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Saltwall | all allies, no damage | Cast_Support |

| State | Loop | When the game plays it for Mira | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: calm, steady, unflappable |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Shell Bash | sword slash from behind the shield |
| Attack_2 | once | strike rotation | shield bash |
| Attack_3 | once | strike rotation | shield-up thrust |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | not used by this hero's skills (falls back to Cast) | - |
| Cast_Support | once | Saltwall | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (calm, steady, unflappable) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A small, sturdy young woman with a high teal ponytail, sea-blue padded gambeson with shell-white trim and toggles, white sash, deep-teal trousers tucked into rope-bound boots, a short scabbard at the hip. Weapon shown separately beside the figure, upright. Main colour sea blue, accent shell white, element mood sea blue, sea-glass and foam, deep water.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A small, sturdy young woman with a high teal ponytail, sea-blue padded gambeson with shell-white trim and toggles, white sash, deep-teal trousers tucked into rope-bound boots, a short scabbard at the hip. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour sea blue, accent shell white.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a round wooden shield rimmed in pale bronze and studded with shells; a short broad sword. Main colour sea blue, accent shell white.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour sea blue, accent shell white. Skin painted with warm subsurface tones. Quilted cloth with stitch lines, salt-bleached edges, small shells and pale bronze rivets.
```
