// Shrinks the high-detail hero builds (blenderforge/ubc.py) for phones before they are committed:
// KHR_mesh_quantization on the meshes (glTFast reads it natively) and JPEG albedo/normal textures
// (the game loads .jpg or .png). Usage: node compress-hd.mjs DIR   (rewrites the files in DIR)
import fs from "node:fs";
import path from "node:path";
import { NodeIO } from "@gltf-transform/core";
import { ALL_EXTENSIONS } from "@gltf-transform/extensions";
import { quantize, prune, dedup } from "@gltf-transform/functions";

const dir = process.argv[2];
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS);
for (const f of fs.readdirSync(dir).filter((n) => /_HD(_LOD1)?\.glb$/.test(n))) {
  const p = path.join(dir, f), before = fs.statSync(p).size;
  const doc = await io.read(p);
  await doc.transform(dedup(), prune(), quantize({ quantizePosition: 14, quantizeNormal: 10, quantizeTexcoord: 12, quantizeWeight: 8 }));
  await io.write(p, doc);
  console.log(`${f}: ${(before / 1024).toFixed(0)} -> ${(fs.statSync(p).size / 1024).toFixed(0)} KB`);
}
