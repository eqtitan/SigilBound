# Kettle Gravejaw, the gravedigger brawler

| | |
|---|---|
| Element | Shadow (violet, night glass, starlit void) |
| Rarity | Common |
| Role | Attack |
| Animation archetype | Brawler (`SB_Anim_Brawler_<state>`) |
| Build | brawny arms and hands, slightly hunched, average height |
| Files | `SB_Hero_kettle_LOD0.glb` (LOD1, LOD2), `T_Hero_kettle_Albedo/Normal/Mask.png`, `SB_Gear_kettle_Weapon.glb` |

## Concept

- **Silhouette:** a forward-leaning hook shape: long coat tails behind, long black hair, a rusted iron hook held low and forward.
- **Outfit:** charcoal long coat with violet stitching and frayed tails, open collar, bandaged forearms, heavy work boots, a shovel-strap across the chest.
- **Weapon / focus:** a rusted grave hook on a short iron haft with a leather wrap.
- **Colours:** main `#5a5560` ash grey, accent `#9a5ae8` violet stitching, glow none, skin ash grey, hair `#2a2a30` black, long. Element rim: #9a5ae8 (added by the shader, don't paint it in).
- **Personality cues:** sly, scrappy, gallows humour; stern brows, crooked smirk, a scar across one eye.
- **Skills on screen:** Grave Hook yanks with the hook; Rattle Bones is a big overhead hook slam.
- **Shape language (Attack):** Triangles and diagonals: forward lean, asymmetric, the weapon line is the longest shape.
- **Rarity finish:** Common: practical matte materials (linen, leather, iron), one accent colour, no glow, no cape.
- **Phone check:** at 70 px tall the read is a forward-leaning hook shape; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (long coat tails behind); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: coat tails 2x2 bones, hair 2 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: grave hook
- Socket_Back: strap
- Gear rarity variants (optional): `SB_Gear_kettle_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Grave Hook | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Rattle Bones | one enemy | Attack_1-3 (run-in, strike, run back) |

| State | Loop | When the game plays it for Kettle | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: sly, scrappy, gallows humour |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Grave Hook, Rattle Bones | right hook or club swing |
| Attack_2 | once | strike rotation | left jab or backhand |
| Attack_3 | once | strike rotation | heavy kick or shoulder barge |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | not used by this hero's skills (falls back to Cast) | - |
| Cast_Support | once | not used by this hero's skills (falls back to Cast) | - |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (sly, scrappy, gallows humour) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A brawny ash-grey-skinned man with long black hair and a scar across one eye, charcoal long coat with violet stitching and frayed tails, open collar, bandaged forearms, heavy work boots, a leather strap across the chest. Weapon shown separately beside the figure, upright. Main colour ash grey, accent violet stitching, element mood violet, night glass, starlit void.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A brawny ash-grey-skinned man with long black hair and a scar across one eye, charcoal long coat with violet stitching and frayed tails, open collar, bandaged forearms, heavy work boots, a leather strap across the chest. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour ash grey, accent violet stitching.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a rusted grave hook on a short iron haft with a leather wrap. Main colour ash grey, accent violet stitching.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour ash grey, accent violet stitching. Skin painted with warm subsurface tones. Frayed coat hems, grave-dirt on the boots, rust streaks on metal.
```
