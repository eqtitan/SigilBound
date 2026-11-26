// Shrinks the high-detail hero builds (blenderforge/ubc.py) for phones before they are committed:
// KHR_mesh_quantization on normals and texture coordinates only (see below) and JPEG albedo/normal textures
// (the game loads .jpg or .png). Usage: node compress-hd.mjs DIR   (rewrites the files in DIR)
// Pipeline for every high-detail build (PLAN.md step 58): blenderforge/ubc.py -> node compress-hd.mjs DIR
// -> python3 compress-textures.py DIR (albedo q82, normal 768 px q88). At build time prepare-models.mjs
// gzips the GLBs into StreamingAssets (lossless; Unity stores StreamingAssets uncompressed in the APK).
import fs from "node:fs";
import path from "node:path";
import { NodeIO } from "@gltf-transform/core";
import { ALL_EXTENSIONS } from "@gltf-transform/extensions";
import { quantize, dequantize, prune, dedup } from "@gltf-transform/functions";

const dir = process.argv[2];
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
for (const f of fs.readdirSync(dir).filter((n) => /_HD(_LOD1)?\.glb$/.test(n))) {
  const p = path.join(dir, f), before = fs.statSync(p).size;
  const doc = await io.read(p);
  // Only normals and texture coordinates are quantized. Positions and skin weights stay float: glTFast
  // left skinned meshes in their bind pose (T-pose) when POSITION / WEIGHTS_0 were quantized, so the
  // heroes stopped following their animated bones. dequantize() first undoes older builds.
  await doc.transform(dequantize(), dedup(), prune(), quantize({ pattern: /^(NORMAL|TEXCOORD_0)$/, quantizeNormal: 10, quantizeTexcoord: 12 }));
  await io.write(p, doc);
  console.log(`${f}: ${(before / 1024).toFixed(0)} -> ${(fs.statSync(p).size / 1024).toFixed(0)} KB`);
}
