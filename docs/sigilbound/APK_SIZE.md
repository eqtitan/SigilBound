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

## Hero textures as KTX2 and LOD1 meshes (PLAN step 116)

Prepared with the batch of 158 high-detail heroes on main (2026-11-28): `StreamingAssets/Models` 117.9 MB to
127.8 MB, **estimated APK 165.9 MB to 175.8 MB** (+9.8 MB; budget 190).

| Part (prepared, 158 heroes) | Before (MB) | After (MB) |
|---|---|---|
| Hero GLBs (`*_HD.glbz`) | 72.1 | 72.1 |
| Albedo: JPEG q82 -> KTX2 ETC1S (quality 160) | 29.3 | 31.1 |
| Normal maps: JPEG q88 -> KTX2 ETC1S normal-map mode (quality 192) | 10.0 | 10.7 |
| Glow + metal PNGs -> one prebuilt mask, KTX2 UASTC + zstd | 1.1 | 1.3 |
| LOD1 (`*_HD_LOD1.sbl`, 119 heroes) | 0 | 7.1 |

- **Textures.** `prepare-models.mjs` encodes every albedo, normal map and mask to KTX2 with the Basis Universal
  WASM encoder bundled in `ktx2-encoder` (MIT; Basis Universal is Apache-2.0), on all CPU threads (about 3.5 minutes
  for all 414 maps on 4 threads). Results are cached by a hash of the inputs and settings in
  `Tools~/models/.ktx-cache` (gitignored; CI restores it with `actions/cache`), so only new or changed maps are
  encoded. The JPEG/PNG sources stay in `heroes_hd` and do not ship. The game loads the maps with
  **KTX for Unity** (`com.unity.cloud.ktx` 3.7.1, Apache-2.0, native transcoder for Android, Windows, Linux),
  which transcodes on a worker thread: ETC1S to ETC2/ETC1 RGB on Android and BC1 on Windows (0.5 byte per pixel),
  UASTC to ASTC 4x4 / ETC2 RGBA / BC7 (1 byte per pixel). The mask is built at prepare time (`ktx-worker.mjs`
  `buildMask`, the same rule as `HeroModels.MaskFromGlow`). The Hollow tint is the shader's `_Hollow` switch.
- **GPU memory per hero's maps** (estimate from formats, with mips): albedo 1024 5.6 -> 0.70 MB, normal 768
  3.1 -> 0.39 MB, mask 512 1.4 -> 0.35 MB: **10.1 -> 1.44 MB** for a hero with all three (Low, no normal map:
  7.0 -> 1.05 MB). Averaged over all 158 heroes (some have no normal map or mask): 8.75 -> 1.24 MB.
- **LOD1.** meshoptimizer simplifies the shipped mesh to about 42% of the triangles (error at most 1% of the
  model's size; attribute seams kept). No vertex is moved, so the file is just the kept vertex ids and the new
  triangles (`Core/HdLod.cs`, gzipped, about 60 KB per hero); `HeroModels.BuildLodMesh` builds the Unity mesh from
  the full one at load (positions, normals, UVs and skin weights copied; bind poses and bones are the same file's)
  after checking the vertex and index counts and the first triangle. Result: 44% of the triangles and 57% of the
  vertices on average. Used on Low and Medium and for Hollow enemies on every tier; the LOD is part of the model
  cache key (`|l1`). A full second GLB per hero would have added about 45 MB. 38 image-to-3D heroes are flat-shaded
  triangle soups (every edge is a seam, so nothing can collapse without changing their look) and keep the full
  mesh; they are 6-17k triangles already. The Titan always uses its full mesh.

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
3. **LOD1 only on low-end phones**: LOD1 now ships next to the full mesh (step 116). Shipping only LOD1 in a
   store build for low-memory devices would cut about 40 MB of mesh data, at a visible cost in the hero viewer.
