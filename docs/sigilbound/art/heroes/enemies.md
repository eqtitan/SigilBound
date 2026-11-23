# Enemy variants

## Hollow enemies (campaign, dungeons, Trials)

Every campaign enemy is a **Hollow** copy of one of the 24 heroes (`Game.StageWaves` picks hero
templates). Players should recognise who they are fighting, so **Hollow enemies need no extra
models**: they reuse the hero prefab and change only how it is shaded. `HeroRig.ApplyLook` does
this at runtime on cached material copies:

| What | Hero | Hollow |
|---|---|---|
| Albedo | as painted | multiplied toward dark violet (`#3a2a44` at 55%) |
| Rim light (shader `_RimColor`) | element colour | red `#ff3355` |
| Glow mask (Mask B: eyes, runes, gems) | the hero's glow colour (`LOOKS.glow`); off for heroes without one | red `#ff2a40`, brighter |
| Gear | the player's equipped gear | plain gear that gets finer with level (`GearLook.ForEnemy`) |

What the art must provide for this to work:
- **Eyes in the glow mask** (Mask texture, blue channel, about 0.6-1.0 on the irises). This is
  what makes the red Hollow eyes read at phone size. Paint them in for every hero: heroes without a
  glow colour (most commons) get a black glow, so their eyes only light up on Hollow copies.
- Runes, gems and trim that should turn red on Hollow copies also go in the glow mask.
- Nothing else: no separate Hollow textures, so 24 heroes never become 48 asset sets.

Arena rivals are other players' heroes and use the normal hero look.

## Optional later: Hollow extras

If enemies need more threat than a tint, add one shared, swappable piece rather than new models:
a `SB_Gear_hollow_Back.glb` (ragged shadow cloak) and `SB_Gear_hollow_Head.glb` (cracked violet
crown shard), attached on `Socket_Back` / `Socket_Head` for Hollow units only. They fit every hero
because sockets are standard. Budget: 1,500 tris together, sharing one 512 px texture.

## Kharvok the Ashen Titan (daily boss)

Out of scope for this revamp: the Titan keeps today's model (CC0 yeti recoloured as basalt with
element seams, code-built horns and shards). It is not a hero (`CharacterView` never loads a prefab
for it). If it is redone later: a hulking stone giant, about 4.7 m in battle, 40k tris at LOD0,
seams painted into the glow mask, and its own clip set (Idle, Punch, Weapon, Jump, Jump_Land,
HitReact, Death, Yes/No/Wave roars) because its choreography differs from heroes
(`AnimStyle.TitanStyle`).
