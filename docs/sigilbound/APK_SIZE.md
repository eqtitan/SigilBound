# APK size (Unity build)

Google Play caps the base download at 200 MB. CI fails the Android build when the APK is over **190 MB**
(`android` job, step "APK size budget"), and the `checks` job estimates it before any build
(`Tests~/ModelPackTests.cs`: prepared `StreamingAssets/Models` + 48 MB for everything else).

## Where the size is (build 169, 194.7 MB, measured from the released APK)

| Part | MB |
|---|---|
| High-detail hero GLBs (`heroes_hd/*.glbz`, gzipped) | 94.7 |
| Hero albedo JPEGs | 30.3 |
| Hero normal-map JPEGs | 12.7 |
| Hero glow PNGs | 1.2 |
| Other `StreamingAssets/Models` (base models, Unity-only bodies stored plain, clip file) | 9.2 |
| IL2CPP, engine, dex, Resources and the rest | 46.3 |

Unity stores StreamingAssets uncompressed in the APK, so whatever prepare-models writes there is what ships.

## What the build does now (PLAN step 101)

- `prepare-models.mjs` snaps the high-detail heroes' positions to about 1/16384 of the model's size and skin
  weights to 1/256 steps (summing to exactly 1). Both stay float32: glTFast left skinned heroes in their
  bind pose when POSITION / WEIGHTS_0 were quantized, so they never are.
- Every generated GLB is packed into an "SBZ1" container (`Core/ModelPack.cs`: vertex and index buffers
  byte-transposed, lossless) and then gzipped. `HeroModels.ReadBytes` unzips and unpacks to the exact GLB.
- The Unity-only bodies (`women_*.glb`, 4.9 MB) are packed and gzipped too.
- Result: generated GLBs 96 MB to 66 MB; `StreamingAssets/Models` 148 MB to 117 MB.
  **Estimated APK: about 164 MB** (from 195 MB).

## Why the hero textures are not ASTC

The hero textures are loaded at runtime from StreamingAssets, so Unity's texture import rules
(`PortraitImportRules`-style ASTC) never see them. Moving them into imported assets with ASTC would cut
their memory about 7 times, but grow the download: ASTC 6x6 at 1024 px with mips is about 0.62 MB per
albedo against 0.19 MB as JPEG, roughly +65 MB for the albedos alone. The memory side is handled instead by
uploading the JPEGs non-readable and bounding the model cache (PLAN step 100). The way to get both small
downloads and GPU-compressed textures is KTX2 (Basis Universal, transcoded to ASTC/ETC2 on the phone)
through the KtxUnity package and glTFast's KTX support: an M-sized change that needs a device test.

## Getting under 150 MB

Not reached with lossless or invisible changes alone. The options, roughly in order of cost:

1. **Play Asset Delivery for the store build** (owner decision D8, recommended). Not implemented yet. What it
   would take:
   - Move `StreamingAssets/Models/heroes_hd` into an install-time asset pack. In Unity 6 that is either
     `PlayerSettings.Android.splitApplicationBinary = true` (all StreamingAssets go to an install-time
     pack named `UnityStreamingAssetsPack`) or a custom asset pack folder (`*.androidpack`) next to the
     other StreamingAssets.
   - Install-time packs are installed with the app, so `UnityWebRequest` reads from them as today on
     Android; check on a device that `Application.streamingAssetsPath` still resolves the hero files
     (the split moves them out of the base APK's `assets/`).
   - The store build is an AAB already (`BuildScript.AndroidStore`). The side-loaded test APK cannot use
     asset packs, so it stays one file and keeps the 190 MB gate.
   - Later, unowned heroes could move to a fast-follow or on-demand pack (needs the Play Asset Delivery
     API to request and wait for the pack before a hero loads).
2. **Texture quality** (art decision): normal maps at 512 px instead of 768 px save about 7 MB; albedo
   re-encoded at JPEG quality 72 instead of 82 saves about 7 MB (a second JPEG generation, so it should be
   judged on a phone first).
3. **LOD1 meshes** (performance review #3): a 6-8k triangle mesh per hero for Low/Medium and Hollow enemies;
   shipping LOD1 instead of the full mesh on phones would roughly halve the mesh data again.
