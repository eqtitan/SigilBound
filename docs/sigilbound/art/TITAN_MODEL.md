# Swapping in a new Titan model

The Titan's model hookup is data, not code. Everything is in `unity/Tools~/models/heroes_hd/titan.json`,
which `prepare-models.mjs` ships as `StreamingAssets/Models/heroes_hd/titan.json`. The game reads it with
the HD index (`HeroModels.LoadHdIndex`, parsed by `Core/TitanSpec.cs`). If the file is missing or a value is
bad, the default for the current armoured knight is used.

## Drop-in steps

1. **Export the GLB on the hero layout:** one root node named `Armature`, metres, Y up, facing +Z (glTF front),
   feet near y 0, one skinned mesh. Name it `SB_Hero_<id>_HD.glb`, for example `SB_Hero_basalt_HD.glb`.
   Add `_albedo.jpg` (run `compress-textures.py`), `_normal.jpg` and `_glow.png`.
2. **Glow mask = molten seams.** In `_glow.png`, the seams, chest core and eyes are white, everything
   else is black. Brightness is what counts, not colour. The game tints the mask per element (`seams`)
   and multiplies it by `glowGain`. Keep `glowGain` at or below about 1.5. Above that, Flame clips to white
   under bloom, which is what made the old chest letter read as a white "U". The material is single-sided
   (back faces culled), so seams never show through the far side of a plate.
3. **Copy the files** into `unity/Tools~/models/heroes_hd/`, set `"model": "<id>"` in `titan.json`, and run
   `node unity/Tools~/models/prepare-models.mjs`. This lists the id in `index.txt`.
4. **Tune the numbers below** and check the Titan in practice mode (Titan screen, Practice). Look at the
   arrival, all three moves, the final blow and the name tag.
5. **Run the gates:** `cd unity/Tests~ && dotnet run -c Release` checks that `titan.json` parses and names
   a shipped model, that there is a seam colour for every element and a sane glow gain, and that the
   arrival and the jump camera frame a knight-sized Titan and a hunched one.

## titan.json fields

| Field | Default (knight) | Meaning |
|---|---|---|
| `model` | `titan` | File stem: `heroes_hd/SB_Hero_<model>_HD.glb` |
| `head` | 6.24 | Head height in battle, in metres from the soles (heroes are 1.85). The model is scaled so `headBone` lands here. |
| `headBone` | `Head` | The bone used for scale and the name tag. If it is missing, the mesh top is taken as `hornTop`. |
| `viewScale` | 1.3 | The battle view's own Titan scale. The model is built to `head / viewScale` inside it. |
| `bulk` | 1.18 | Extra width and depth (x and z) over the uniform scale. |
| `face` | 0.97 | Face height as a fraction of `head`. Cameras look the Titan in the eye here. Use about 0.75–0.85 for a hunched colossus whose face sits below its shoulders. |
| `hornTop` | 1.27 | Horn tips as a fraction of `head`. The name tag sits 0.6 m above them. |
| `chest` | 0.72 | Chest core height as a fraction of `head`, where Heartfire Torrent gathers its fire (fallback when there is no `chestBone`). |
| `autoGround` | true | Puts the mesh's lowest point on the ground. `footOffset` (in metres, positive is up) is added after that. |
| `back` | 1.2 | How far behind the far rim it stands. |
| `reach` | 3.6 | Bedrock Hammer stops this far from its target. Set it to where the fists land (`titan_clips.json` `contactZ` x `head` / `headBoneY`; 3.1 for the basalt colossus). |
| `clips` | `ubc` | `ubc` uses the shared retargeted UAL clips (hero rig). `embedded` uses the GLB's own clips first, for a separately rigged model. |
| `aliases` | `{}` | Maps the names the battle asks for (`Idle`, `Jump`, `Jump_Land`, `Walk_Loop`, `Ult_Overhead`, `Ult_Channel`, `HitReact`, `Hit_Chest`, `Hit_Head`, `Death01`, `Yes`, ...) to the model's clip names. |
| `moves` | `{}` | Per move (`idle`, `walk`, `roar`, `fists`, `leap`, `land`, `breath`, `sweep`, `collapse`): its `clip` and event times as normalized clip time (`impact`, `top`, `takeoff`, `release`, ...). Missing: the shared clips and the old timings. |
| `chestBone`, `fistL`, `fistR` | `chest_core`, `fist_l`, `fist_r` | Sockets: the breath pours from the chest core; impacts land at the fists. |
| `ridgeStages` | `[0.66, 0.33]` | Health at which the `ridge_NN_s1` and `ridge_NN_s2` crystals break off (health = the top damage tier still to deal). |
| `flareGain`, `ridgeGlow` | 2.2, 1.6 | The wind-up flare on the seams and core (x `glowGain`) and the ridge glow (x the seam colour). |
| `seams` | element colours | Seam and rim colour per element (`Flame`, `Gale`, `Tide`, `Light`, `Shadow`), as hex. |
| `glowGain` | 1.35 | Multiplier on the seam colour. |
| `glowCut` | the knight's chest letter | Boxes in model space (Unity axes, which are glTF with X negated, in metres before scaling) as `[x0,y0,z0,x1,y1,z1]`. Triangles inside them take the `glowCutUv` texel and stop glowing. Use `[]` for a clean model. |

## Cameras

All the Titan cameras read `head`, `face`, `hornTop` and `back` through `BattleLayout`: the arrival keys
(`TitanIntroKeys`), Worm's-eye and the other views, the tag and the cinematics. A lower, wider, hunched
silhouette needs `head` and `face` set, and nothing else. Every Titan cinematic is clamped so the bottom
of the frame meets the ground within 30 m (`BattleLayout.MaxPitchForGround`, applied in
`Arena.ComposeCamera`).

## Current model: the hunched basalt colossus (2026-11-27)

An image-to-3D mesh (TRELLIS via fal.ai) rigged to the hero UBC skeleton, so `clips` stays `ubc`:

```
bpyenv/bin/python i3d_prep.py -- titan.glb titan DIR
bpyenv/bin/python i3d_rig.py -- --hero titan --clean DIR/titan_clean.blend --out OUT
node compress-hd.mjs OUT && python3 compress-textures.py OUT
```

Landmarks are in `blenderforge/i3d_landmarks_titan.py`: the hands end at the fists (wrist above the fist,
hand bone through it), the fists are rigid rocks, the back and shoulder crystal ridges are rigid on
`spine_03`. The glow mask is the orange seams, chest core and eyes (dilated a little), and the albedo under
it is darkened to neutral (`glow_neutral`) so the seam colour is the element's alone. The joints moved off
the UBC proportions carry the `rest_fit` marker, so `RigExtras` re-expresses the shared clips for them.
`titan.json`: the Head bone sits at the head's base (1.42 of 1.85 m), so `face` is 1.04 and `hornTop` 1.23;
`head` 6.0 keeps the horn tips near the knight's height; `bulk` 1.0 (the mesh is already broad); `reach` 4.5
for the long arms; no `glowCut`.

## Its own moves (2026-11-28)

The remodel brought its own clips, sockets and breakable crystal ridges (`blenderforge/titan_moves.py`, run on the
rigged model AFTER `compress-hd.mjs`, whose prune would drop the socket nodes):

```
python3 titan_moves.py RIGGED.glb RIGGED_glow.png SB_Hero_titan_HD_albedo.jpg heroes_hd/
bpyenv/bin/python titan_preview.py -- heroes_hd/ OUT fists|leap|breath|sweep|collapse|ridges   # review renders
python3 titan_gif.py OUT                                                                         # GIFs + contact sheet
```

It writes the GLB (11 `Titan_*` clips, `chest_core`, `fist_l`/`fist_r`, 22 `ridge_NN_sK` crystal bones), the glow PNG
with the ridge mask in its alpha, and `titan_clips.json` (lengths, events, measured fist contact windows). Copy the event
times into `titan.json` `moves`; `TitanMechanicsTests` checks they match and sit in the contact windows. Timings and
effects: `docs/sigilbound/MECHANICS.md` section 13.

