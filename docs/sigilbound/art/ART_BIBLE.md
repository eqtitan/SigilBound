# Sigilbound art bible: hero revamp

The visual target for the hero revamp in the Unity version (the single engine from now on; the
web/WebGL and Godot versions are frozen). It extends `docs/sigilbound/ART_DIRECTION.md`, which
still describes the current code-built heroes and the element palette. Where they disagree for
new hero art, this document wins.

The rules here are also in code, so tools and people can't drift apart:
`unity/Assets/Sigilbound/Core/Art/ArtSpec.cs` (names, budgets, battle states, sockets) and
`Core/Art/HumanoidMap.cs` (rigs). The Unity import tool and the offline checker enforce them.

Contents: 1 Pillars · 2 Proportions · 3 Shape language · 4 Element palette · 5 Rarity language ·
6 Materials and textures · 7 Mobile budgets · 8 Rig and animation · 9 Gear on the model ·
10 Naming · 11 Staying original · 12 Review checklist

## 1. Style pillars

1. **Stylized, hand-painted, fully textured.** The look of a premium mobile hero-collector, made
   our own: chunky readable forms, painted albedo with soft light and crisp edge highlights, not
   photoreal and not flat-shaded low-poly. The shader adds the rest (wrap light, a three-band
   painterly ramp, element rim), so textures don't paint strong lighting.
2. **Readable at 70 px.** In battle a hero is about 70 px tall on a phone. Each hero needs one
   silhouette idea that survives that size (a spear diagonal, a bell-shaped robe, a tower shield),
   one main colour, one accent and a visible role prop.
3. **Element first, rarity second.** The element colour lives in the rim light and the glows, so
   matchups read at a glance. Rarity shows in finish and silhouette extras, never by changing the
   hero's palette.
4. **Warm, heroic fantasy.** Saturated but not neon. Grime and wear are allowed; grimdark is not:
   no skull piles, no gore, no spikes everywhere. Villainy (Kettle, Morwenna) comes from posture,
   colour and costume, not horror.
5. **One hero, one identity.** Every hero keeps the identity players already know from the current
   game: name, element, role, colours from `public/game/art.js` LOOKS, props and personality. The
   revamp raises fidelity; it doesn't redesign who they are.
6. **Original.** See section 11.

## 2. Proportions and stylization

| | Target |
|---|---|
| Head-to-body | about **1 : 6** (heroic stylized). Youths (Pell, Mira) 1 : 5.5; huge builds (Odo, Gorrum) 1 : 6.5 with a small head on big shoulders |
| Hands and feet | 10-15% larger than realistic, so grips and stances read |
| Weapons and props | 10-20% larger than realistic; thick handles; the blade/head shape is the silhouette |
| Height | authored about **1.85-2.05 m**; the game rescales every prefab to 2.05 m x the hero's build factor (`HeroArt.Body(id).H`, 0.88 for Thrain to 1.1 for Gorrum) |
| Faces | large readable eyes and brows; brow angle carries the mood (stern = angled down, kind = up); simple mouth shapes; no fine wrinkles |
| Stylization | simplified anatomy, big clear planes, bevelled edges with painted highlights, cloth in a few large folds |

Rest pose for delivery: T-pose (arms horizontal, palms down) or A-pose (arms 30-45 degrees down).
Fingers apart, legs slightly apart, facing +Z, feet on the ground at the origin.

## 3. Shape language by role

| Role | Shapes | Silhouette rules | Examples |
|---|---|---|---|
| Attack | triangles, diagonals | forward lean, asymmetric, the weapon is the longest line, sharp tips | Brannoc's spear diagonal, Ysolde's whip curve, Kaito's zig-zag scarf |
| Defense | squares, rectangles | wide shoulders, low centre of gravity, symmetrical front, the shield or armour reads first | Hask's kite-shield wall, Aurelia's tower shield, Ulfgar's mountain |
| HP | circles, mass | round heavy forms, thick limbs, soft big shapes | Odo's boulder, Velka's long rounded robe, Thrain's compact square-round build |
| Support | vertical ovals, arches | tall, flowing cloth, upward lines, a light-source prop held high | Wren's lantern crook, Oriel's sunburst, Seraphine's bell robe |

Within a role, keep heroes apart by **silhouette idea** (the "silhouette read" column in
`heroes/README.md`), never only by colour.

## 4. Element palette

The element colour goes in the **rim light** (set by the shader per unit, never painted), glowing
details (glow mask), spell effects and UI. Painted materials lean toward the element's family.

| Element | Rim / glow | Supporting hues | Material motifs | Avoid |
|---|---|---|---|---|
| Flame `#e8553a` | ember orange-red | soot black, molten gold, brick | scorched leather, heat cracks, forge iron, embers | pure fire-truck red over the whole body |
| Gale `#3fb57a` | wind green | moss, straw gold, slate | quilted leather, reeds, light steel, feathers | neon lime |
| Tide `#3a8ee8` | sea blue | teal, shell white, coral | scales, shells, sea-glass, salt-faded cloth | flat primary blue |
| Light `#e8c23a` | dawn gold | ivory, warm white, sky blue | burnished gold, enamel, silk, bells | chrome yellow, overexposed white |
| Shadow `#9a5ae8` | violet | night blue, black, silver | smoky glass, velvet, star flecks, silver | pure black (keep a violet or blue tint so forms read) |

Hollow enemies swap the rim and glow to red and tint the albedo dark violet in code
(`heroes/enemies.md`).

## 5. Rarity language

Rarity is finish and silhouette extras. The hero's main and accent colours stay the same.

| Rarity | Trim and materials | Glow | Silhouette extras |
|---|---|---|---|
| Common | practical matte: linen, leather, iron, wood; one accent colour | none | none (bandana tails, a pack: practical items only) |
| Rare | metal trim (bronze, steel), a simple pattern on trim | one small detail: eyes, a gem or a weapon edge | one signature piece (circlet, pauldrons, hat) |
| Epic | gold or silver filigree, enamel, a cape or large ornament | 2-3 runes or gems | cape or large shoulder/back piece with secondary motion |
| Legendary | one signature material (molten metal, starlit glass, sunmetal) | animated glow over larger areas (hair, crown, cracks) | a breaker above the head (crown, halo, living flame, wings) and an aura |

Equipped **gear** adds its own rarity on top (section 9), from Worn (1) to Mythic (5).

## 6. Materials and textures

**Approach: hand-painted albedo plus a light PBR mask.** One shader for every hero:
`Sigilbound/CharacterPainted` (`unity/Assets/Sigilbound/Client/Shaders/SigilCharacterPainted.shader`),
with URP Lit as the fallback.

| Map | File | Content | Colour space |
|---|---|---|---|
| Albedo | `T_Hero_<id>_Albedo.png` | hand-painted colour with soft baked AO and a gentle top-down gradient; no strong cast shadows, no rim, no specular highlights except small edge glints on metal | sRGB |
| Mask | `T_Hero_<id>_Mask.png` | R metallic, G ambient occlusion, B **glow mask** (eyes, runes, gems, hot cracks), A smoothness | linear |
| Normal | `T_Hero_<id>_Normal.png` (optional) | tangent space, OpenGL (+Y) convention; large forms only, keep it soft | linear |
| Emission | `T_Hero_<id>_Emission.png` (optional) | only for the URP Lit fallback; the painted shader uses the glow mask | sRGB |

- **Glow mask:** paint the irises into Mask B for every hero (0.6-1.0). The glow colour comes from
  the hero's LOOKS glow (or none), red on Hollow copies.
- **Sizes:** author at **2048 x 2048** (one atlas for body, hair and worn gear). Shipped at 1024 on
  Android (ASTC 6x6; normals ASTC 5x5), 2048 kept for a future Ultra tier. 512 minimum. Power of
  two only.
- **Texel density:** about **10 px/cm** at 2048 for a 2 m hero (5 px/cm shipped). Give the face and
  hands 2x density (face about 15% of the sheet); weapon on its own 1024 sheet or 20% of the atlas.
- **UVs:** one UV set, no overlaps except mirrored symmetric limbs (allowed to save space; never
  mirror the face, emblems or text), 4 px padding at 1024.
- **Alpha:** only on a second material for hair cards, feathers and fringe (alpha clip, two-sided).
  No alpha blending on characters.
- **Material count:** 1 opaque body material + optional 1 alpha-clip material. The weapon uses the
  body atlas or its own material (that's the +1 the budget allows).
- **Painting guide:** light from the top-front; local colour first; AO in crevices; a warm
  highlight band on upper surfaces; cool, slightly darker undersides; crisp 1-2 px highlights on
  metal edges; wear on edges, not noise everywhere. Skin with warm subsurface tones (red in ears,
  nose, knuckles).

## 7. Mobile budgets (Samsung Galaxy Note20 baseline)

The Note20 (Exynos 990 / Snapdragon 865+ class GPU, 8 GB) is the baseline; budgets leave room for
up to 10 units, the arena, VFX and post at 60 fps (30 on the Cooler/Low tiers). Enforced by
`ArtBudget` in `Core/Art/ArtSpec.cs`; over budget is a warning, over 1.5x is an error.

| Per hero | LOD0 (showcase) | LOD1 (battle) | LOD2 (lineup, low tier) |
|---|---|---|---|
| Triangles, all included | **30,000** | **12,000** | **5,000** |
| Used for | hero viewer, summon reveal, card photo | battle (up to 10 on screen) | menu lineup, back row, Low tier |
| LODGroup switch (screen height) | above 45% | 12-45% | below 12% (never culled) |

| Per hero | Budget |
|---|---|
| Bones | **75** max: humanoid with fingers (about 65) + up to 10 secondary (cape, hair, tails, tabard) |
| Bone weights | 4 per vertex (2 on the Low tier via quality settings) |
| Skinned meshes | 2 per LOD (body + optional alpha cards) |
| Materials | **2** (+1 for a rigid weapon on a socket) |
| Draw calls | **3** per pass before VFX; shadows: body only |
| Textures | 2048 source, 1024 shipped; total under 3 MB per hero on Android |
| Blend shapes | allowed for faces (blink, 3-4 mouth shapes), not required |

Battle frame with 10 units at LOD1: 120k character tris, 30 draw calls before shadows, which fits
next to the arena (measured today at about 195 calls and 660k tris for the whole frame on the web
build with the code-built heroes).

## 8. Rig and animation

- **Skeleton:** humanoid, mappable to Unity's Humanoid avatar. Mixamo-style naming is ideal; any
  rig works if `HumanoidMap` finds the limbs (it reads Mixamo, UE-style, Rigify and Quaternius
  names, and finds the trunk from the hierarchy). Feet must be children of the shins (no IK feet
  under the root), or the hero can't use the shared library.
- **Secondary bones** for capes, hair, tails and robes hang off the humanoid bones and count in
  the 75.
- **Battle states** (`HeroClips` in `ArtSpec.cs`; each hero sheet lists when each one plays):

| State | Loop | Required | Plays when |
|---|---|---|---|
| Idle | loop | yes | standing in battle, viewer, lineup |
| Idle_Fidget | once | | occasional idle variation |
| Run / Run_Back | loop | Run | melee run-in and return (in place: the game moves the unit) |
| Attack_1 / _2 / _3 | once | Attack_1 | single-target damage, melee heroes (rotation) |
| Cast | once | yes | ranged attacks and every all-enemies skill |
| Cast_Ult | once | | third skill or all-enemies skill |
| Cast_Support | once | | heals, buffs, shields, taunts |
| Hit / Hit_Big | once | Hit | damage; crit or a hit over 14% of max HP |
| Dodge | once | | glancing blow, 35% chance |
| Death | hold | yes | HP reaches 0 |
| Victory | loop | | after the last wave |

- **Clips are in place** (root locked), 24-30 fps, short: strikes 0.5-0.9 s with the impact at
  about 0.3 s; casts release at about 0.3 s; hits 0.3-0.5 s.
- **Shared library first.** One retargeted clip set serves every Humanoid hero; per-archetype
  sets (`Blade`, `Polearm`, `Brawler`, `Daggers`, `ShieldBlade`, `Heavy`, `Caster`, `Archer`)
  give each weapon family its own attacks; a hero override wins over both. A hero's own
  embedded clips are the fallback.

## 9. Gear on the model (sockets and swappable meshes)

The game passes equipped gear as `{ weapon, helm, chest, boots: { rarity 1-5, set } }`. On a
revamped hero it shows through **sockets** and **swappable meshes**, never by editing the body:

| Socket | Bone | Holds |
|---|---|---|
| `Socket_HandR` | right hand | main weapon or focus (grip at the origin, blade along +Y) |
| `Socket_HandL` | left hand | shield, off-hand weapon, bow |
| `Socket_Back` | upper chest | cape root, quiver, wings, back ornaments |
| `Socket_Head` | head (top) | helms, crowns, halos, hats |
| `Socket_Hip` | hips (right side) | scabbard, coils, pouches |
| `Socket_Cast` | right hand, forward | spell origin for VFX |

- The import tool creates these from the mapped bones; nodes named `Socket_*` in the file win, so
  artists can place them precisely.
- **Weapon rarity:** the weapon socket scales with gear rarity (+4% Fine to +18% Mythic), as the
  current code-built kit does.
- **Rarity variants:** optional meshes `SB_Gear_<id>_<Slot>_r<1-5>` are attached under the slot's
  socket; the best one at or below the equipped rarity shows. Skinned gear (chest, boots) binds to
  the hero skeleton by bone name. Variants never hide the face or replace the hero's signature
  headgear: helms add a band, crest or gem around it.
- **Set colours** (Fury red, Vigor green, Bulwark stone grey, Haste teal, Keen gold) belong on a
  gem area of the gear meshes; until tinting by set is wired, paint gems neutral.
- **Card faces ignore gear** on purpose (identity first), as today.

## 10. Naming convention

| What | Name | Example |
|---|---|---|
| Hero model | `SB_Hero_<heroId>_LOD<0-2>.glb` or `.fbx` | `SB_Hero_ysolde_LOD0.glb` |
| Texture | `T_Hero_<heroId>_<Albedo|Normal|Mask|Emission>.png` | `T_Hero_ysolde_Mask.png` |
| Gear / weapon mesh | `SB_Gear_<heroId>_<Weapon|Offhand|Helm|Chest|Boots|Back>[_r<1-5>].glb` | `SB_Gear_hask_Helm_r4.glb` |
| Shared clip | `SB_Anim_<State>.fbx` | `SB_Anim_Hit_Big.fbx` |
| Archetype clip | `SB_Anim_<Archetype>_<State>.fbx` | `SB_Anim_Polearm_Attack_1.fbx` |
| Hero clip | `SB_Anim_<heroId>_<State>.fbx` or `.glb` | `SB_Anim_kaito_Cast_Ult.fbx` |
| Sockets (in a file) | `Socket_<HandR|HandL|Back|Head|Hip|Cast>` | `Socket_HandR` |
| Generated in Unity | `M_Hero_<id>_<Body|Alpha>`, `AC_Hero_<id>`, `AV_Hero_<id>`, prefab `Resources/Heroes/<id>` | |

Hero ids are the lower-case ids from `public/game/data.js`. Files not matching are reported and
ignored.

## 11. Staying original

Sigilbound must never copy or imitate another game's characters, names or art, including the big
hero-collectors. How we make sure:

1. **Identity comes from our data.** Each hero's look derives from its own name, skills, element,
   colours and lore in this repo (the hero sheets cite them), not from a reference character.
2. **No names in prompts or briefs.** Never put another game, studio, artist or character name in
   a prompt, brief or file name. `hero-prompts.json` carries a banned-terms list and the core
   tests fail if any appears. No "in the style of"; describe the qualities instead (stylized,
   hand-painted, chunky readable shapes).
3. **No reference images of other games' characters** as image-to-3D inputs or style references.
   Image inputs are our own concept sheets only.
4. **Silhouette test against archetypes, not franchises.** Our heroes use our own silhouette ideas
   (a bell robe with bell fringe, chime-feather wings, a lantern crook, a living-flame crown).
   When a design starts to read as a famous character (for example a winged armoured woman with a
   halo and a spear, a hooded green archer, a horned dark lord in spiked black plate), change the
   defining element: prop, silhouette idea or colour block.
5. **Stylization as identity:** our warm hand-painted look, about 1:6 heads, big readable props and
   element rims differ deliberately from the grim semi-realistic heavy-armour look common in the
   genre.
6. **Review:** every generated hero is reviewed against this list before it ships (section 12).
   If unsure, generate again with a changed silhouette idea.
7. **Provenance:** keep the generator, plan tier, prompt and date for every file
   (`PROVENANCE.json` from heroforge), and credit sources in `CREDITS.md`.

## 12. Review checklist (every new hero)

- [ ] Reads at 70 px: silhouette idea, main colour, accent and prop are clear in a battle screenshot.
- [ ] Matches the hero sheet: colours from LOOKS, prop, personality; nothing from another game.
- [ ] Import report passes (tris, bones, materials, textures, required states, sockets).
- [ ] All battle states play: idle, run-in, strike, cast, support cast, hit, big hit, dodge, death,
      victory. Nobody T-poses; feet don't slide during strikes.
- [ ] Hollow copy: dark, red rim, red eyes visible.
- [ ] Gear at Worn, Superior and Mythic keeps the face and signature headgear visible.
- [ ] Back view: cape, hair and back items attached; no floating parts.
- [ ] Draw calls about 3 per hero in battle (Settings > Performance overlay).
- [ ] Provenance recorded; output owned under the plan used (see `ROUTES.md`).
