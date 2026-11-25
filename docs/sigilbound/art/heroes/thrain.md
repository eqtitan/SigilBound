# Thrain Ashforge, the ashforge smith

| | |
|---|---|
| Element | Flame (ember orange-red, forge glow, ash and soot) |
| Rarity | Epic |
| Role | HP |
| Animation archetype | Heavy (`SB_Anim_Heavy_<state>`) |
| Build | short and stocky (dwarf-like), very broad, big hands |
| Files | `SB_Hero_thrain_LOD0.glb` (LOD1, LOD2), `T_Hero_thrain_Albedo/Normal/Mask.png`, `SB_Gear_thrain_Weapon.glb` |

## Concept

- **Silhouette:** a compact square: a huge braided red beard, a smith's apron, a short cape, a big glowing forge hammer over the shoulder.
- **Outfit:** leather-and-iron smith armour with orange-hot rivets, heavy apron, rolled sleeves over thick forearms, a short rust cape, iron-capped boots.
- **Weapon / focus:** a stout forge hammer with a glowing orange core in the head.
- **Colours:** main `#5a3a2a` smith brown, accent `#ffa040` forge orange, glow `#ffa040` forge, skin tan, hair `#a8321a` red, short, braided beard. Element rim: #e8553a (added by the shader, don't paint it in).
- **Personality cues:** hearty, loud, generous; level brows, big grin in the beard.
- **Skills on screen:** Forge Hammer is a heavy overhead blow; Ashen Ward plants his feet behind a heat shield; Bellows pumps hot air to rally allies.
- **Shape language (HP):** Circles and mass: round, heavy forms, thick limbs, soft big shapes that look hard to knock down.
- **Rarity finish:** Epic: a cape or large ornament, gold or silver filigree, two or three glowing runes or gems, extra secondary motion.
- **Phone check:** at 70 px tall the read is a compact square; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (a huge braided red beard); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: beard braids 2 bones, cape 2x2 bones, apron 2 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: forge hammer
- Socket_Back: cape root
- Socket_Hip: tongs
- Gear rarity variants (optional): `SB_Gear_thrain_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Forge Hammer | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Ashen Ward | all allies, no damage | Cast_Support |
| 3. Bellows | all allies, no damage | Cast_Support |

| State | Loop | When the game plays it for Thrain | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: hearty, loud, generous |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Forge Hammer | overhead two-handed slam |
| Attack_2 | once | strike rotation | sideways sweep |
| Attack_3 | once | strike rotation | punch with the free hand |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | not used by this hero's skills (falls back to Cast) | - |
| Cast_Support | once | Ashen Ward, Bellows | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (hearty, loud, generous) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A short very stocky tan man with a huge braided red beard, leather-and-iron smith armour with glowing orange rivets, a heavy leather apron, rolled sleeves over thick forearms, a short rust-red cape, iron-capped boots, tongs at the belt. Weapon shown separately beside the figure, upright. Main colour smith brown, accent forge orange, glowing accents in forge, element mood ember orange-red, forge glow, ash and soot.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A short very stocky tan man with a huge braided red beard, leather-and-iron smith armour with glowing orange rivets, a heavy leather apron, rolled sleeves over thick forearms, a short rust-red cape, iron-capped boots, tongs at the belt. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour smith brown, accent forge orange, glowing accents in forge.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. a stout forge hammer with a glowing orange core in the head. Main colour smith brown, accent forge orange, glowing accents in forge.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour smith brown, accent forge orange, glowing accents in forge. Skin painted with warm subsurface tones. Scorched leather apron, sparks burned into the cloth, hot rivets in the glow mask.
```
