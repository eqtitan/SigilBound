# Systems map

Which lessons belong to which system, and how systems connect. Read the rows for what you are about to touch.

| System | What it covers | Lessons |
|---|---|---|
| hero-models | GLBs, rigs, weights, meshes, materials | L002 L003 L004 L008 L010 L018 L019 L022 L023 |
| animation | clips, retargeting, Animation/Animator | L002 L003 L022 |
| rig-runtime | RigExtras, DigitigradeLegs, SpringBones, CreatureRig | L002 L003 L015 L022 |
| titan | Titan model, clips, TitanSpec, TitanBody | L003 L022 |
| art-pipeline | Gemini, cut-out, fal TRELLIS, artgen | L009 L012 |
| character-td | per-frame visual review method | L008 L018 L019 |
| build-pipeline | prepare-models, compress, credits | L001 L007 L010 |
| tests | Tests~, CompileCheck, gates | L001 L013 L026 L028 |
| ci | workflow, smoke gate, queue, artifacts | L004 L005 L016 L024 L027 |
| qa | in-house tester, checklists | L004 L016 L024 L025 L026 L027 L028 L032 |
| scenes | campaign scenery, cameras | L004 |
| balance | curves, pools, roles, probes | L006 L020 |
| campaign | stages, waves, eases | L006 |
| ui | IMGUI, shared components, overlays | L011 L017 L025 L028 |
| arena | ArenaReactor, effects | L015 |
| platform | Windows/Android specifics, app ids | L021 L031 |
| licensing | credits, licences | L007 |
| docs | changelog, PLAN, roadmap | L013 L029 L030 |
| process | who pushes, approvals | L014 L029 L030 L031 |
| tooling | keys, scripts | L012 |

```mermaid
graph LR
  L002[L002 exploded legs] --> L016[L016 pose guard]
  L003[L003 mangled Titan] --> L016
  L002 --> L022[L022 skeleton library]
  L003 --> L022
  L018[L018 spring cloth limits] --> L022
  L004[L004 previews != game] --> L005[L005 CI queue]
  L002 --> L004
  L003 --> L004
  L001[L001 stale models] --> L007[L007 credits overwrite]
  L001 --> L010[L010 mesh fix not in game]
  L010 --> L023[L023 missing maps fall back to shader defaults]
  L006[L006 Act II pool] --> L020[L020 balance ripples]
  L008[L008 frames vs averages] --> L019[L019 tight face locks]
  L008 --> L018
  L009[L009 i3d input rules] --> L018
  L011[L011 GUI.depth] --> L017[L017 shared UI]
  L015[L015 OnEnable/Start] --> L016
  L005 --> L024[L024 green run, no APK]
  L024 --> L027[L027 preview moves]
  L025[L025 96 dpi tours] --> L026[L026 shared prefs]
  L017 --> L028[L028 plates over text, solved]
  L025 --> L028
  L030 --> L031[L031 store id already taken]
  L027 --> L032[L032 model load mid-attack froze battle]
```
