// Makes LOD1 and LOD2 from a hero's LOD0 .glb with meshoptimizer's simplifier (via glTF-Transform),
// keeping the skeleton, skin weights and UVs, aiming at the art bible's battle and lineup budgets.
// LOD files drop their textures: Unity gives them the LOD0 materials. Normals are dropped before
// welding (hard edges would otherwise split every vertex and block the simplifier) and rebuilt
// after: fine for small-on-screen LODs.
//   node unity/Tools~/heroforge/lods.mjs <SB_Hero_<id>_LOD0.glb> [--lod1 12000] [--lod2 5000]
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { NodeIO } from "@gltf-transform/core";
import { ALL_EXTENSIONS } from "@gltf-transform/extensions";
import { dequantize, weld, simplify, prune, normals } from "@gltf-transform/functions";
import { MeshoptSimplifier, MeshoptDecoder, MeshoptEncoder } from "meshoptimizer";
import { budgets } from "./budgets.mjs";

const countTris = (doc) => {
  let n = 0;
  for (const mesh of doc.getRoot().listMeshes())
    for (const p of mesh.listPrimitives()) {
      const idx = p.getIndices(), pos = p.getAttribute("POSITION");
      n += Math.floor((idx ? idx.getCount() : pos ? pos.getCount() : 0) / 3);
    }
  return n;
};

export async function makeLods(lod0Path, targets = {}) {
  await MeshoptSimplifier.ready; await MeshoptDecoder.ready; await MeshoptEncoder.ready;
  const b = budgets();
  const want = { 1: targets.lod1 ?? b.TrisLod1, 2: targets.lod2 ?? b.TrisLod2 };
  const io = new NodeIO().setLogger({ debug() {}, info() {}, warn: console.warn, error: console.error }).registerExtensions(ALL_EXTENSIONS).registerDependencies({ "meshopt.decoder": MeshoptDecoder, "meshopt.encoder": MeshoptEncoder });
  const out = {};
  for (const lod of [1, 2]) {
    const doc = await io.read(lod0Path);
    const before = countTris(doc);
    // Each LOD at least halves the work: budget, or 60% / 30% of LOD0 for models already under it.
    want[lod] = Math.min(want[lod], Math.round(before * (lod === 1 ? 0.6 : 0.3)));
    await doc.transform(dequantize(), weld());
    let ratio = Math.min(1, want[lod] / Math.max(1, countTris(doc)));
    // Simplification lands near, not on, the ratio: tighten until under budget (a few passes at most).
    for (let pass = 0; pass < 4; pass++) {
      const trial = await io.read(lod0Path);
      for (const mesh of trial.getRoot().listMeshes()) for (const p of mesh.listPrimitives()) { const n = p.getAttribute("NORMAL"); if (n) p.setAttribute("NORMAL", null); }
      await trial.transform(dequantize(), weld(), simplify({ simplifier: MeshoptSimplifier, ratio, error: 0.02 + pass * 0.03, lockBorder: false }), normals({ overwrite: true }));
      const got = countTris(trial);
      if (got <= want[lod] || pass === 3) {
        for (const t of trial.getRoot().listTextures()) t.dispose(); // LOD0's materials are used in Unity
        for (const a of trial.getRoot().listAnimations()) a.dispose(); // and LOD0's clips
        await trial.transform(prune());
        const dest = lod0Path.replace(/_LOD0\.glb$/i, `_LOD${lod}.glb`);
        await io.write(dest, trial);
        out[lod] = { file: path.basename(dest), before, after: got, target: want[lod] };
        break;
      }
      ratio *= (want[lod] / got) * 0.95;
    }
  }
  return out;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  const file = process.argv[2];
  if (!file || !fs.existsSync(file)) { console.error("usage: node lods.mjs <SB_Hero_<id>_LOD0.glb> [--lod1 N] [--lod2 N]"); process.exit(2); }
  const arg = (k) => { const i = process.argv.indexOf(k); return i > 0 ? Number(process.argv[i + 1]) : undefined; };
  const r = await makeLods(file, { lod1: arg("--lod1"), lod2: arg("--lod2") });
  for (const [lod, x] of Object.entries(r)) console.log(`LOD${lod}: ${x.file} ${x.before} -> ${x.after} tris (target ${x.target})`);
}
