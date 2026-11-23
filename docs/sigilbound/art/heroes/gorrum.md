# Gorrum Slagheart, the forge-born guardian

| | |
|---|---|
| Element | Flame (ember orange-red, forge glow, ash and soot) |
| Rarity | Rare |
| Role | Defense |
| Animation archetype | Heavy (`SB_Anim_Heavy_<state>`) |
| Build | massive, wide, heavy; small head on huge shoulders |
| Files | `SB_Hero_gorrum_LOD0.glb` (LOD1, LOD2), `T_Hero_gorrum_Albedo/Normal/Mask.png`, `SB_Gear_gorrum_Weapon.glb` |

## Concept

- **Silhouette:** a towering anvil: spiked dark pauldrons, a horned skull-less helm band with curved bone horns, an upright two-handed maul planted at his side, glowing seams in the armour.
- **Outfit:** soot-black plate with molten-orange seams, spiked pauldrons, a chain apron, huge armoured boots.
- **Weapon / focus:** an upright forge maul: a square iron head with glowing slag cracks on a thick haft.
- **Colours:** main `#3a2a22` soot black, accent `#ff5a1a` molten orange, glow `#ff7a2a` forge glow, skin deep brown, hair `#1a1410` black, bald, full beard. Element rim: #e8553a (added by the shader, don't paint it in).
- **Personality cues:** grim, silent, unbreakable; heavy stern brows, frown, ember-orange eyes.
- **Skills on screen:** Slag Fist punches with a burning gauntlet; Molten Hide makes the seams flare; Eruption slams the maul to crack the ground.
- **Shape language (Defense):** Squares and rectangles: wide shoulders, low centre of gravity, symmetrical front, the shield or armour reads first.
- **Rarity finish:** Rare: metal trim (bronze or steel) and one small glow detail (eyes, gem or weapon edge); simple pattern on the trim.
- **Phone check:** at 70 px tall the read is a towering anvil; one main colour, one accent, the weapon visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, weapon, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (spiked dark pauldrons); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: chain apron 2 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: forge maul
- Socket_Head: horn band
- Gear rarity variants (optional): `SB_Gear_gorrum_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a melee hero: single-target damage runs in and strikes):

| Skill | Target | Plays |
|---|---|---|
| 1. Slag Fist | one enemy | Attack_1-3 (run-in, strike, run back) |
| 2. Molten Hide | self, no damage | Cast_Support |
| 3. Eruption | all enemies | Cast_Ult |

| State | Loop | When the game plays it for Gorrum | Direction |
|---|---|---|---|
| Idle | loop | always | weapon ready, weight shifting; matches the personality: grim, silent, unbreakable |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | melee run-in, 0.26 s | in place, root locked; the game moves the unit |
| Run_Back | loop | backpedal home, 0.25 s | falls back to Run |
| Attack_1 | once | Slag Fist | overhead two-handed slam |
| Attack_2 | once | strike rotation | sideways sweep |
| Attack_3 | once | strike rotation | punch with the free hand |
| Cast | once | fallback for the casts below | weapon raised, a short charge, release |
| Cast_Ult | once | Eruption (camera zooms in) | the big moment: Eruption slams the maul to crack the ground |
| Cast_Support | once | Molten Hide | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (grim, silent, unbreakable) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A massive dark-skinned bald man with a black beard, small curved bone horns on an iron head band, ember-orange eyes, soot-black plate armour with glowing molten-orange seams, spiked dark pauldrons, a chain apron, huge armoured boots. Weapon shown separately beside the figure, upright. Main colour soot black, accent molten orange, glowing accents in forge glow, element mood ember orange-red, forge glow, ash and soot.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A massive dark-skinned bald man with a black beard, small curved bone horns on an iron head band, ember-orange eyes, soot-black plate armour with glowing molten-orange seams, spiked dark pauldrons, a chain apron, huge armoured boots. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour soot black, accent molten orange, glowing accents in forge glow.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. an upright forge maul: a square iron head with glowing slag cracks on a thick haft. Main colour soot black, accent molten orange, glowing accents in forge glow.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour soot black, accent molten orange, glowing accents in forge glow. Skin painted with warm subsurface tones. Soot-black metal with orange heat cracks (seams in the glow mask), ash dust on the upper surfaces.
```
