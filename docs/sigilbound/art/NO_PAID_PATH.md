# The no-paid-service path: honest assessment

**Question (owner, 2026-11-23):** can agents make acceptable hero art with free tools only, no paid
generator? **Test:** two heroes built entirely by script in Blender 5.2 (the free `bpy` Python
module, run headless in the agent container), with procedural "painted" materials baked into one
albedo atlas, a Mixamo-style humanoid rig, GLB export, and a preview render that copies the game's
shader. Inputs were only self-made geometry and code: no downloaded models, no textures, no packs.

## Results

| | Ysolde (slim duelist) | Gorrum (huge forge guardian) |
|---|---|---|
| Comparison sheet | [prototype/ysolde-compare.png](prototype/ysolde-compare.png) | [prototype/gorrum-compare.png](prototype/gorrum-compare.png) |
| Build time | about 2 min per run on 4 CPU cores (model, rig, 1024 bake, export, two renders) | same |
| Triangles | 58.5k (over the 30k LOD0 budget; the hair locks and subdivided shells are the cost; `heroforge lods` or fewer subdivisions bring it down) | 31k |
| Materials after bake | 1 (one 1024 atlas + a glow mask) | 1 |
| Bones | 22 (humanoid, no fingers) | 22 |
| GLB | 3.8 MB (uncompressed 1024 PNGs) | 2.3 MB |

Offline check (`dotnet run --project unity/Tools~/artcheck`) on both GLBs: the rig maps to all
required humanoid bones (so the shared CC0 clip library can drive them in Unity), 1 material, 1024
textures; they fail only on missing animations (none authored yet) and, for Ysolde, triangles.

Each sheet shows the current hero (code-built kit on the CC0 Quaternius model, web build) next to
the prototype in three ways: the hero-viewer framing, the battle camera, and at phone size (about
70 px tall).

## What the prototype gets right

- **Identity and readability.** At battle and phone size both heroes read as themselves: Ysolde's
  long red hair, circlet, single shoulder guard and trailing whip; Gorrum's horns, spiked pauldrons,
  glowing cracks and upright maul. The silhouette ideas from the hero sheets carry over.
- **A real game asset.** One material, a baked painted albedo with AO and top light, a glow mask
  that our shader and the Hollow tint use, a humanoid rig with Mixamo-style names that
  `HumanoidMap` maps, exported to the naming convention. It drops straight into the import pipeline.
- **Repeatable and cheap.** A hero is a recipe (`unity/Tools~/blenderforge/recipes.py`); parts
  (bodies, heads, hair locks, lathe-turned armour, straps, props) are shared, so heroes 3 to 100 get
  faster. Everything is original and code-made, with zero licensing risk.

## Where it falls short (the quality ceiling)

Being direct: **this is not the quality of a top mobile hero-collector, and scripted primitives
won't get there.** Today's prototype is roughly on par with the current code-built look. It wins
on hair volume and texture cohesion and loses on anatomy and charm. The gaps:

1. **Anatomy.** Limbs are tubes from a skin modifier, hands are mittens, there are no sculpted
   muscles, knees or collarbones. Top games use hand-sculpted bodies.
2. **Faces.** Primitive eyes and brows on an ellipsoid read at a distance but look like a doll up
   close. The hero viewer and summon screens show faces large.
3. **Cloth.** Armour is lathe-turned and box-built. There are no folds, layering or silhouette
   breaks from cloth. A cloth simulation pass is possible in Blender but slow, and hard to control
   by script.
4. **Textures.** The procedural "painting" (strokes, AO, top light, edge highlights) looks
   painterly from a distance. It has no designed detail: no stitching, ornament, wear placed by
   intent, or face painting.
5. **Animation.** No free source gives *unique* per-hero skill animations. CC0 libraries (the
   Quaternius Universal Animation Library) cover the generic states well. Hand-keyed motion by
   script is possible for simple swings but looks robotic.

## How far free tools can realistically go (recommended free path)

If the owner wants no paid service, the best quality per effort is not "more primitives". It is:

1. **CC0 base bodies instead of skin-modifier tubes.** Quaternius *Universal Base Characters*
   (CC0, humanoid rig, proper anatomy, male/female, several proportions) and *Modular Character
   Outfits - Fantasy* (CC0, 12 outfits, 62 parts) give sculpted anatomy, hands with fingers, UVs and
   a retargetable rig for free. Agents then add each hero's signature kit by script: hair, armour,
   props, colours and baked painted texture, as in this prototype. This is the biggest single jump.
   It still shares body shapes across heroes, so distinctness comes from silhouette kit and colour.
   That matches how the current game already works, at higher fidelity.
2. **Scripted kit on the base** (this prototype's parts library), fitted by shrinkwrap to the base
   body so armour hugs real anatomy.
3. **Baked painted textures** with curvature, AO and a top light (as here), plus hand-authored
   decals (emblems, stitching) made as vector shapes by script.
4. **Animation:** the CC0 Universal Animation Library for the shared states (idle, run, hit,
   dodge), retargeted in Unity. Unique skill motions come from simple scripted keyframe edits of
   CC0 clips (exaggerate, retime, add a spin, change the weapon arc) per hero. That gives
   "different", not "hand-animated quality". Report flags (`anim-not-unique`) keep the gap visible.

**Expected ceiling with that path:** a clean, consistent, stylized CC0-based roster. That's
clearly better than today and fine for a free game, but below the hand-sculpted, hand-painted look
of top commercial hero-collectors. Faces and unique motion stay the weakest points. Paid AI
generation (Meshy/Tripo) mainly buys sculpted variety per hero and text-to-motion animation. It
stays wired in heroforge as an optional plug-in (`--provider meshy|tripo`), off by default.

## Reproduce

```bash
python3 -m venv .bpy && .bpy/bin/pip install bpy==5.2.4      # Blender as a Python module (Python 3.13)
# EEVEE previews need EGL; in a container: apt-get install libegl1 libegl-mesa0 libgl1-mesa-dri libgbm1
EGL_PLATFORM=surfaceless .bpy/bin/python unity/Tools~/blenderforge/forge.py --hero ysolde --out out/ysolde --bake 1024 --render
EGL_PLATFORM=surfaceless .bpy/bin/python unity/Tools~/blenderforge/forge.py --hero gorrum --out out/gorrum --bake 1024 --render
```

Outputs: `SB_Hero_<id>_LOD0.glb` (ready for the Unity import tool or heroforge `stage`),
`albedo.png`, `glow.png`, `<id>_viewer.png`, `<id>_battle.png`. The comparison sheets come from
`unity/Tools~/blenderforge/battle-shot.cjs` (the current game, via the QA harness) and `compare.py`.
