# Hero art pipeline (Unity)

How new hero models get from a generator, a pack or any other source into the game, step by step.
The visual rules are in `docs/sigilbound/art/ART_BIBLE.md`; the choice of source is in
`docs/sigilbound/art/ROUTES.md`; each hero's brief and prompts are in `docs/sigilbound/art/heroes/`.

```
hero-prompts.json --heroforge--> generate (API) -> GLB -> rig/animate (API) -> LOD1/LOD2
   -> incoming/ (convention names + PROVENANCE.json) -> artcheck (offline report)
   -> Unity: Sigilbound > Art > Import Hero Folder  (or batch mode)
   -> Assets/Sigilbound/Art/Resources/Heroes/<id>.prefab + Reports/HeroImport_latest.md
   -> the game loads the prefab; without one it keeps today's hero
```

## What's where

| Path | What |
|---|---|
| `Assets/Sigilbound/Core/Art/` | the rules in plain C#: names, budgets, battle states, sockets (`ArtSpec.cs`), rig mapping (`HumanoidMap.cs`), `.glb` reader (`GlbInfo.cs`), validation and report |
| `Assets/Sigilbound/Editor/Art/HeroImportPipeline.cs` | the import tool (menu **Sigilbound > Art**, batch entry `ImportFromCommandLine`) |
| `Assets/Sigilbound/Editor/Art/HeroArtPostprocessor.cs` | import settings applied automatically to files under `Assets/Sigilbound/Art` |
| `Assets/Sigilbound/Client/HeroRig.cs` | runtime: on every generated prefab; scales, colours, gear, hands the prefab to the battle code |
| `Assets/Sigilbound/Client/Shaders/SigilCharacterPainted.shader` | the hero shader for textured art |
| `Assets/Sigilbound/Art/` | created by the tool: `Incoming/<id>/`, `Animations/`, `Generated/`, `Resources/Heroes/`, `Rejected/`, `Reports/`, `Test/` |
| `Tools~/heroforge/` | the agent batch flow (Node): prompts, providers, LODs, staging |
| `Tools~/artcheck/` | the offline validator (dotnet, same rules as Unity) |

## 1. Files the tool accepts

Names per `ART_BIBLE.md` section 10 (anything else is reported and ignored):

```
SB_Hero_<id>_LOD0.glb|fbx        required; LOD1/LOD2 optional (missing ones are flagged)
T_Hero_<id>_Albedo.png           optional (else textures embedded in the model are used)
T_Hero_<id>_Mask.png / _Normal.png / _Emission.png
SB_Gear_<id>_Weapon.glb          optional, attached to Socket_HandR; _Offhand, _Helm, _Back, _Chest, _Boots
SB_Gear_<id>_Helm_r3.glb         optional rarity variant (shown up to the equipped rarity)
SB_Anim_<State>.fbx              shared Humanoid clip library (Idle, Run, Attack_1, Cast, Hit, Death, ...)
SB_Anim_<Archetype>_<State>.fbx  per weapon family: Blade, Polearm, Brawler, Daggers, ShieldBlade, Heavy, Caster, Archer
SB_Anim_<id>_<State>.fbx|glb     per hero (wins over the two above)
```

`<id>` is the hero id from `public/game/data.js` (`ysolde`, `hask`, ...). States are listed in
`ART_BIBLE.md` section 8 and in each hero sheet.

## 2. Automated batch (agents): heroforge

Needs Node 22 and the .NET 8 SDK. No Unity needed until the last step.

```bash
cd unity/Tools~/heroforge && npm ci && cd -        # glTF-Transform + meshoptimizer for LODs

# See what would be generated (prompts, files, cost estimate); checks prompts for banned names:
node unity/Tools~/heroforge/heroforge.mjs plan --provider meshy --heroes ysolde,gorrum

# Print every API request without sending it (no key needed):
node unity/Tools~/heroforge/heroforge.mjs generate --provider meshy --plan-tier paid --dry-run --heroes ysolde

# The real run, once the owner has saved the key (see "Keys" below):
node unity/Tools~/heroforge/heroforge.mjs all --provider meshy --plan-tier paid --proxy-auth --heroes ysolde,gorrum
```

`all` runs these steps (each can be run alone; state is kept in `<out>/state.json`, so a rerun
skips finished heroes unless `--force`):

| Step | Does | Output |
|---|---|---|
| `plan` | builds prompts from `hero-prompts.json` + game data, checks banned terms | console |
| `generate` | text-to-3D (and the weapon as its own mesh) via the provider API, downloads the GLB | `<out>/<id>/raw/model.glb`, `weapon.glb` |
| `rig` | provider auto-rig + animation presets (Rodin: `--rig-with meshy|tripo`) | `<out>/<id>/SB_Hero_<id>_LOD0.*`, `SB_Anim_<id>_<State>.*` |
| `lods` | LOD1/LOD2 with meshoptimizer to the bible's budgets | `SB_Hero_<id>_LOD1.glb`, `_LOD2.glb` |
| `stage` | copies convention-named files into one folder, writes provenance | `<out>/incoming/`, `PROVENANCE.json` |
| `validate` | `Tools~/artcheck` on the staged files | `<out>/reports/artcheck.md`, exit 1 if a hero fails |
| `import` | runs the Unity tool in batch mode if `UNITY_PATH`/`--unity` is set, else prints the command | Unity report and prefabs |

Defaults: `--out .heroforge-out` (git-ignored), all 24 heroes, T-pose.

**Keys.** Never in the repo or chat. Either an environment variable (`MESHY_API_KEY`,
`TRIPO_API_KEY`, `RODIN_API_KEY`), or in a Claude Code cloud session: the agent asks the owner to
save the key through the session's secure credential form for the API host, then runs with
`--proxy-auth` (the script sends a placeholder that the network proxy replaces). The session's
network must allow the API host and the download host (the first response shows it).

**Plan tier.** `--plan-tier paid|free` is required for real providers; free tiers are refused
unless `--allow-free-tier`, because their outputs aren't owned (see ROUTES.md section 3).

**Test without an account:** `--provider local` "generates" each hero by copying the CC0 model
the game already uses for it, so every later step runs for real:

```bash
node unity/Tools~/heroforge/heroforge.mjs all --provider local --heroes brannoc,hask,wren --out .heroforge-selftest
```

## 3. In Unity (interactive)

1. Open `unity/` in Unity 6000.0.58f1 (glTFast 6.14 and URP 17 are in the manifest; glTFast imports
   `.glb` files as assets).
2. If you use the shared animation library, put the `SB_Anim_*.fbx` files in
   `Assets/Sigilbound/Art/Animations/` (or in the folder you import; the tool moves them there).
3. **Sigilbound > Art > Import Hero Folder...** and pick the folder (for example
   `.heroforge-out/incoming`).
4. Read the dialog and `Assets/Sigilbound/Art/Reports/HeroImport_latest.md`.
5. Press Play: heroes that passed use the new prefab in battle, in the hero viewer and in the menu
   lineup; failing ones are in `Art/Rejected/` and the game keeps today's model for them.

Other menu items:
- **Rebuild Heroes From Incoming**: re-runs the build for everything already in `Art/Incoming`
  (after changing a texture or a setting).
- **Validate Hero Prefabs**: re-measures the prefabs in `Resources/Heroes` and writes a report.
- **Run Pipeline Test (CC0 model)**: imports the shipped Quaternius adventurer as Brannoc into
  `Art/Test/` (not Resources, so the game is unchanged) and logs PASS/FAIL. Expected result for
  this model: prefab built with legacy clips, 14/15 states, sockets created; the report fails the
  material budget (the model has 11 flat-colour materials) and warns that its IK feet prevent a
  Humanoid avatar. That is the correct verdict for a placeholder model.

## 4. Batch mode (CI or a build machine)

```bash
<Unity> -batchmode -nographics -projectPath unity \
  -executeMethod Sigilbound.EditorTools.HeroImportPipeline.ImportFromCommandLine \
  -sbArtIn /abs/path/to/incoming [-sbArtKeepFailing] -logFile -
# self-test instead of an import:
<Unity> -batchmode -nographics -projectPath unity -executeMethod Sigilbound.EditorTools.HeroImportPipeline.ImportFromCommandLine -sbArtSelfTest -logFile -
```

Exit code 0 = every hero passed, 1 = a hero failed validation, 2 = usage or I/O error. Batch mode
needs an activated Unity license (the repo's CI already activates one from the `UNITY_EMAIL` /
`UNITY_PASSWORD` secrets for APK builds; an import job would reuse that, see open questions).

## 5. What the import tool does per hero

1. **Copies** the files under `Assets/Sigilbound/Art` (`Incoming/<id>/`, `Animations/`); the
   postprocessor sets FBX import options (Humanoid, file scale, 4 bone weights, mesh compression),
   clip options (renamed to the state, in place, looping per state) and texture options (2048 max,
   Android 1024 ASTC, sRGB/linear per map).
2. **Avatar:** maps the skeleton with `HumanoidMap` and checks Unity's hierarchy rules. FBX: Unity's
   own Humanoid avatar, or ours if Unity's mapping fails. GLB: builds the avatar with
   `AvatarBuilder` (`Generated/AV_Hero_<id>.asset`). Rigs that can't be Humanoid stay Generic.
3. **Animation:** with a Humanoid avatar and a complete shared library, an Animator controller
   (`Generated/AC_Hero_<id>.controller`) with one state per battle state, a `Speed` parameter, and
   transitions back to Idle (Death holds). Otherwise the file's own clips (legacy Animation for
   glTFast imports). States without a clip map to their fallback (Attack_3 -> Attack_1, Cast_Ult ->
   Cast, Victory -> Idle, ...).
4. **Materials:** `Generated/M_Hero_<id>_Body|Alpha.mat` on `Sigilbound/CharacterPainted` (URP Lit
   if the shader is missing), using `T_Hero_<id>_*` or the textures embedded in the file.
5. **Sockets:** `Socket_HandR/HandL/Back/Head/Hip/Cast` from the mapped bones (authored sockets in
   the file are kept). **Gear** files attach to their slot's socket; skinned gear binds by bone name.
6. **LODs:** LOD1/LOD2 files are rebound to the LOD0 skeleton and put in a `LODGroup` (45% / 12% /
   1% screen height); missing ones are flagged.
7. **Prefab:** `Resources/Heroes/<id>.prefab` with a `HeroRig` (or `Rejected/` if validation fails).
8. **Report:** `Reports/HeroImport_<date>.md` and `_latest.md`: tris per LOD, bones, materials,
   skinned meshes, texture sizes, resolved states, sockets, animation backend, every issue.

## 6. Runtime behaviour

`CharacterView.Create` (battle units, hero viewer, menu lineup) loads `Resources/Heroes/<id>`:
- with a `HeroRig` that can animate (all required states and an Animator or legacy clips): scaled
  to 2.05 m x the hero's build, element rim and glow set (red rim and glow plus a dark tint on Hollow
  enemies), weapon scaled by gear rarity, rarity variants shown, then driven by the same
  choreography as today (run-in, strikes, casts, ultimate cast, support cast, hits, dodge, death,
  victory);
- with a `HeroRig` that can't animate: the prefab is dropped and today's GLB hero is built;
- with an old hand-made prefab (Animator, no HeroRig): the trigger behaviour as before;
- with no prefab: exactly today's behaviour (CC0 GLB hero, placeholder while loading).
The Titan never loads a prefab. `HeroRig.Enabled = false` turns the new art off for comparisons.

## 7. Shared animation library (recommended)

One Humanoid clip set for all heroes keeps motion consistent across 24 generated models:
1. Get the CC0 Quaternius Universal Animation Library (quaternius.com or its itch page; the repo
   already credits a mirror in `Assets/StreamingAssets/Models/CREDITS.md`). FBX, Humanoid-compatible.
2. Rename the clips to states: `SB_Anim_Idle.fbx`, `SB_Anim_Run.fbx`, `SB_Anim_Cast.fbx`,
   `SB_Anim_Cast_Support.fbx`, `SB_Anim_Hit.fbx`, `SB_Anim_Hit_Big.fbx`, `SB_Anim_Death.fbx`,
   `SB_Anim_Victory.fbx`, and per family `SB_Anim_Blade_Attack_1.fbx`, `SB_Anim_Polearm_Attack_1.fbx`...
3. Record the source in `Assets/Sigilbound/Art/Animations/CREDITS.md`.
Per-hero clips from the generator's animation API (`SB_Anim_<id>_<State>`) override library clips.

## 8. Troubleshooting

| Report says | Fix |
|---|---|
| `humanoid: no bone found for X` | rename the bone, or re-rig with the provider's auto-rig; GLB rigs need unique bone names |
| `humanoid: LeftFoot is not below LeftLowerLeg` | IK-style feet: re-rig, or rely on the file's own clips (warning only if it has every required state) |
| `anim-missing` | add the clip to the shared library, the archetype set or as `SB_Anim_<id>_<State>` |
| `tris` / `lod-missing` | `node unity/Tools~/heroforge/lods.mjs SB_Hero_<id>_LOD0.glb`, or regenerate with a lower polycount |
| `materials` | merge to one atlas (generators usually output one material; packs may need a texture bake) |
| `.glb did not import as a model` | glTFast missing or the file is broken: open it in a glTF viewer |
| Prefab in `Rejected/` | fix and use **Rebuild Heroes From Incoming**, or `-sbArtKeepFailing` to test a failing hero in game |

## 9. What has been verified (2026-11-23)

- `dotnet run --project unity/Tests~`: passes, including the art checks (naming, states, humanoid
  mapping on Mixamo-style, UE-style, Rigify and the shipped Quaternius rigs, validation rules, a
  dry-run import of the three CC0 models, prompts covering 24 heroes with no banned names).
- Compile check: the editor tool, postprocessor and runtime compile against the stubs and against
  the real Unity 6000.0.58f1 editor and engine assemblies (`Tools~/unity-refs`); URP and glTFast
  are still stubbed.
- `SigilCharacterPainted.shader`: all passes and variants compile with DXC against URP 17's
  ShaderLibrary (`Tools~/shadercheck`), desktop and `SHADER_API_MOBILE`.
- heroforge with `--provider local` end to end (generate, rig, LODs 10.2k -> 5.8k / 2.8k tris,
  stage, artcheck report); Meshy, Tripo and Rodin request flows in `--dry-run`.
- **Not verified:** the Unity editor run itself (no Unity editor or license in the prep
  environment): importing, avatar building, controller creation, prefab saving and the in-game
  look are untested until someone runs **Run Pipeline Test (CC0 model)** in Unity. No real API
  call has been made (no keys, by design).
