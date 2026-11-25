// Copies the web game's CC0 Quaternius hero models (public/game/models) into the Unity
// project's StreamingAssets, decoding their EXT_meshopt_compression so glTFast can load them
// without the optional meshopt package. KHR_mesh_quantization stays (glTFast supports it).
// Also copies the retargeted UAL clips and the credits file. Run before a Unity build
// (CI does this):  cd unity/Tools~/models && npm ci && node prepare-models.mjs
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { NodeIO } from "@gltf-transform/core";
import { ALL_EXTENSIONS } from "@gltf-transform/extensions";
import { MeshoptDecoder } from "meshoptimizer";

const here = path.dirname(fileURLToPath(import.meta.url));
const src = path.resolve(here, "../../../public/game/models");
const out = path.resolve(here, "../../Assets/StreamingAssets/Models");
fs.mkdirSync(out, { recursive: true });

await MeshoptDecoder.ready;
const io = new NodeIO().registerExtensions(ALL_EXTENSIONS).registerDependencies({ "meshopt.decoder": MeshoptDecoder });

for (const f of fs.readdirSync(src).filter((n) => n.endsWith(".glb"))) {
  const doc = await io.read(path.join(src, f));
  for (const ext of doc.getRoot().listExtensionsUsed()) {
    if (ext.extensionName === "EXT_meshopt_compression") ext.dispose(); // write plain buffers
  }
  const bytes = await io.writeBinary(doc);
  fs.writeFileSync(path.join(out, f), bytes);
  const anims = doc.getRoot().listAnimations().length, meshes = doc.getRoot().listMeshes().length;
  console.log(`${f}: ${(fs.statSync(path.join(src, f)).size / 1024).toFixed(0)} KB -> ${(bytes.length / 1024).toFixed(0)} KB, ${meshes} meshes, ${anims} clips`);
}
for (const f of ["anims_ual.json", "CREDITS.md"]) fs.copyFileSync(path.join(src, f), path.join(out, f));
// Unity-only CC0 bodies (phase E step 45: Quaternius Ultimate Modular Women), plain GLBs in godot/models.
const extra = path.resolve(here, "../../../godot/models");
for (const f of fs.readdirSync(extra).filter((n) => /^women_.*\.glb$/.test(n))) {
  fs.copyFileSync(path.join(extra, f), path.join(out, f));
  console.log(`${f}: copied (Unity-only body)`);
}
// Free-path heroes baked by unity/Tools~/blenderforge/quat.py (SB_Hero_<id>_LOD0.glb + _albedo/_glow
// PNGs): copied as they are, with an index the game reads (HeroModels) to know which exist.
const heroesSrc = path.resolve(here, "heroes");
if (fs.existsSync(heroesSrc)) {
  const heroesOut = path.join(out, "heroes");
  fs.mkdirSync(heroesOut, { recursive: true });
  const ids = [];
  for (const f of fs.readdirSync(heroesSrc)) {
    fs.copyFileSync(path.join(heroesSrc, f), path.join(heroesOut, f));
    const m = /^SB_Hero_(.+)_LOD0\.glb$/.exec(f);
    if (m) ids.push(m[1]);
  }
  fs.writeFileSync(path.join(heroesOut, "index.txt"), ids.join("\n") + "\n");
  console.log(`Baked heroes: ${ids.join(", ") || "none"}`);
}
// High-detail heroes (phase F, blenderforge/ubc.py) and the retargeted UAL clips for their rig.
const hdSrc = path.resolve(here, "heroes_hd");
if (fs.existsSync(hdSrc)) {
  const hdOut = path.join(out, "heroes_hd");
  fs.mkdirSync(hdOut, { recursive: true });
  const ids = [];
  for (const f of fs.readdirSync(hdSrc)) {
    fs.copyFileSync(path.join(hdSrc, f), path.join(hdOut, f));
    const m = /^SB_Hero_(.+)_HD\.glb$/.exec(f);
    if (m) ids.push(m[1]);
  }
  fs.writeFileSync(path.join(hdOut, "index.txt"), ids.join("\n") + "\n");
  console.log(`High-detail heroes: ${ids.join(", ") || "none"}`);
}
if (fs.existsSync(path.resolve(here, "ubc_anims.glb"))) fs.copyFileSync(path.resolve(here, "ubc_anims.glb"), path.join(out, "ubc_anims.glb"));
console.log(`Models ready in ${path.relative(process.cwd(), out)}`);
