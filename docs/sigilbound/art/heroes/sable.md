# Sable Nightglass, the nightglass witch

| | |
|---|---|
| Element | Shadow (violet, night glass, starlit void) |
| Rarity | Epic |
| Role | Support |
| Animation archetype | Caster (`SB_Anim_Caster_<state>`) |
| Build | average height, poised, elegant |
| Files | `SB_Hero_sable_LOD0.glb` (LOD1, LOD2), `T_Hero_sable_Albedo/Normal/Mask.png`, `SB_Gear_sable_Weapon.glb` |

## Concept

- **Silhouette:** a dark bell: wide-brimmed veiled hat, midnight robe and cape, a hand mirror raised, mirror shards floating around her.
- **Outfit:** midnight-blue robe with silver-lavender trim, a short cape, a sheer black veil falling from the hat brim, silver rings.
- **Weapon / focus:** an oval silver hand mirror whose glass shows violet light.
- **Colours:** main `#232a48` midnight blue, accent `#c8d8ff` silver-lavender, glow `#b48cff` violet, skin pale, hair `#3a2050` dark violet, swept. Element rim: #9a5ae8 (added by the shader, don't paint it in).
- **Personality cues:** enigmatic, amused, in control; arched stern brows, knowing smirk.
- **Skills on screen:** Glass Shard flings a shard; Mirror Veil fans reflections over the foes; Night Grace heals with silvery light from the mirror.
- **Shape language (Support):** Vertical ovals and arches: tall, flowing cloth, upward lines, a light-source prop held high.
- **Rarity finish:** Epic: a cape or large ornament, gold or silver filigree, two or three glowing runes or gems, extra secondary motion.
- **Phone check:** at 70 px tall the read is a dark bell; one main colour, one accent, the focus visible.

## Model, LODs and attachments

| | Budget | Notes |
|---|---|---|
| LOD0 (showcase) | 30,000 tris | everything included: body, hair, focus, worn gear |
| LOD1 (battle) | 12,000 tris | keep the silhouette pieces (wide-brimmed veiled hat); drop inner folds and small straps |
| LOD2 (lineup, low tier) | 5,000 tris | silhouette only, details painted into the albedo |
| Bones | 75 max | humanoid with fingers (about 65) + secondary: veil 2 bones, cape 2x2 bones, robe hem 4 bones |
| Materials | 2 max | body atlas (opaque) + optional alpha-clip cards |
| Textures | 2048 source, 1024 shipped | Albedo (hand-painted), Mask (R metal, G AO, B glow, A smooth), Normal optional |

Attachments (sockets the import tool creates; authored `Socket_*` nodes win):
- Socket_HandR: hand mirror
- Socket_Cast: mirror glass
- Socket_Back: orbiting shards anchor (VFX or 3 rigid shards on a spinner bone)
- Gear rarity variants (optional): `SB_Gear_sable_Helm_r3.glb` etc. shown up to the equipped rarity; the weapon grows with rarity (+4% Fine to +18% Mythic).

## Animations

Skills and the state each one plays (from `public/game/data.js`; a ranged hero: every attack is cast in place):

| Skill | Target | Plays |
|---|---|---|
| 1. Glass Shard | one enemy | Cast |
| 2. Mirror Veil | all enemies | Cast_Ult |
| 3. Night Grace | all allies, no damage | Cast_Support |

| State | Loop | When the game plays it for Sable | Direction |
|---|---|---|---|
| Idle | loop | always | prop held up near the chest, light sway; matches the personality: enigmatic, amused, in control |
| Idle_Fidget | once | optional | short look-around or prop twirl (web plays one every 8-20 s) |
| Run | loop | rarely (casters cast in place) | in place, root locked; the game moves the unit |
| Run_Back | loop | rarely | falls back to Run |
| Attack_1 | once | melee fallback only | prop swing (melee fallback, rarely used) |
| Attack_2 | once | - | - |
| Attack_3 | once | - | - |
| Cast | once | Glass Shard | mirror thrust forward, release at about 0.3 s |
| Cast_Ult | once | Mirror Veil (camera zooms in) | the big moment: Mirror Veil fans reflections over the foes |
| Cast_Support | once | Night Grace | prop or weapon raised high, open chest, upward motion |
| Hit | once | taking damage | small flinch, back to idle in about 0.4 s |
| Hit_Big | once | crit or a hit over 14% of max HP | bigger stagger, one step back |
| Dodge | once | glancing blow, 35% chance | short side-step or roll; skipped if missing |
| Death | hold last frame | HP reaches 0 | falls backward or kneels then collapses; the unit then sinks |
| Victory | loop | after the last wave | a short pose in character (enigmatic, amused, in control) then a loopable hold |

Required: Idle, Run, Attack_1, Cast, Hit, Death. Everything else falls back (see `Core/Art/ArtSpec.cs`).

## Prompts

Paste as-is. They name no other game, studio or artist; keep it that way.

**a) 2D concept turnaround** (for image-to-3D; use the front view as the input image):

```text
Original character design sheet for a stylized fantasy hero-collector game. Full-body front view and back view side by side, T-pose with arms straight out to the sides, palms down, legs slightly apart, neutral relaxed face, plain flat light-grey background, even soft studio light, no cast shadows. Hand-painted stylized look, chunky readable shapes, slightly enlarged head, hands and weapon, about six heads tall. A poised pale woman with swept dark-violet hair under a wide-brimmed hat with a sheer black veil, midnight-blue robe with silver-lavender trim, a short cape, silver rings, a knowing smile. Weapon shown separately beside the figure, upright. Main colour midnight blue, accent silver-lavender, glowing accents in violet, element mood violet, night glass, starlit void.
```

Negative: `photorealistic, 3d render, multiple characters, cropped, cut-off feet, perspective distortion, dramatic lighting, cast shadows, busy background, scenery, text, logo, watermark, signature, frame, extra limbs, extra fingers, fused fingers, gore`

**b) Text-to-3D, rig-friendly (T-pose).** Use the A-pose variant if the rigger prefers it (swap "T-pose" for "A-pose").

```text
Stylized hand-painted fantasy hero, game-ready full-body character, T-pose, arms straight out from the shoulders, legs apart, fingers spread and separated, neutral face, symmetrical, standing on the ground plane, facing forward. A poised pale woman with swept dark-violet hair under a wide-brimmed hat with a sheer black veil, midnight-blue robe with silver-lavender trim, a short cape, silver rings, a knowing smile. Chunky readable silhouette, about six heads tall, clean separated limbs, empty hands (no weapon held), cloth and hair clear of the arms and legs. Main colour midnight blue, accent silver-lavender, glowing accents in violet.
```

Negative:

```text
realistic, photorealistic, photo scan, horror, gore, skull pile, extra limbs, extra fingers, fused fingers, missing hands, merged legs, cape fused to legs, skirt fused to legs, hair fused to shoulders, weapon in hands, holding object, floating parts, base, pedestal, stand, text, logo, watermark, signature, multiple characters, A-pose with bent elbows, crouching, sitting, asymmetric pose, noisy surface, low quality, blurry texture
```

Weapon / focus as its own mesh (attached to the socket, so it never fuses to the hand):

```text
Stylized hand-painted fantasy prop, game-ready, single object, upright on the ground plane, centered, no character, no base or stand. an oval silver hand mirror whose glass shows violet light. Main colour midnight blue, accent silver-lavender, glowing accents in violet.
```

**c) Texture style** (refine / texturing step):

```text
Hand-painted stylized texture, soft painterly brush strokes, baked ambient occlusion and a gentle top-down light gradient, no hard cast shadows, no photographic detail, clean colour blocks with crisp edge highlights on metal and trim, warm rim on the upper surfaces, saturated but not neon. Main colour midnight blue, accent silver-lavender, glowing accents in violet. Skin painted with warm subsurface tones. Satin sheen on the robe, star-like silver flecks in the trim; mirror glass in the glow mask.
```
