// Copies the web game's CC0 Quaternius hero models (public/game/models) into the Godot demo
// (godot/models), decoding EXT_meshopt_compression and KHR_mesh_quantization so Godot's glTF
// importer reads them as plain GLBs. Also copies the credits file.
// Run after changing the web models (the decoded GLBs are committed):
//   cd godot/tools/models && npm ci && node prepare-models.mjs
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { NodeIO } from "@gltf-transform/core";
import { ALL_EXTENSIONS } from "@gltf-transform/extensions";
import { dequantize } from "@gltf-transform/functions";
import { MeshoptDecoder } from "meshoptimizer";

const here = path.dirname(fileURLToPath(import.meta.url));
const src = path.resolve(here, "../../../public/game/models");
const out = path.resolve(here, "../../models");
fs.mkdirSync(out, { recursive: true });

await MeshoptDecoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ "meshopt.decoder": MeshoptDecoder });

// The three hero bases (the Titan's yeti is not in the demo).
for (const f of ["adventurer.glb", "hooded_adventurer.glb", "monarch.glb"]) {
  const doc = await io.read(path.join(src, f));
  await doc.transform(dequantize());
  for (const ext of doc.getRoot().listExtensionsUsed()) {
    if (ext.extensionName === "EXT_meshopt_compression" || ext.extensionName === "KHR_mesh_quantization") ext.dispose();
  }
  const bytes = await io.writeBinary(doc);
  fs.writeFileSync(path.join(out, f), bytes);
  const root = doc.getRoot();
  const anims = root.listAnimations().map((a) => a.getName());
  const mats = root.listMaterials().map((m) => m.getName());
  console.log(`${f}: ${(fs.statSync(path.join(src, f)).size / 1024).toFixed(0)} KB -> ${(bytes.length / 1024).toFixed(0)} KB`);
  console.log(`  nodes: ${root.listNodes().filter((n) => n.getMesh()).map((n) => n.getName()).join(", ")}`);
  console.log(`  materials: ${mats.join(", ")}`);
  console.log(`  clips: ${anims.join(", ")}`);
}
fs.copyFileSync(path.join(src, "CREDITS.md"), path.join(out, "CREDITS.md"));
console.log(`Models ready in ${path.relative(process.cwd(), out)}`);
