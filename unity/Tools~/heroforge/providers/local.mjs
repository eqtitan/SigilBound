// Offline stand-in provider for testing the batch flow without any account: "generates" each hero
// by copying the CC0 Quaternius model the game already uses for that hero's base (already rigged,
// with clips), so every later step (stage, LODs, validation, Unity import) runs for real.
import fs from "node:fs";
import path from "node:path";
import { ROOT } from "../prompts.mjs";

export const info = { name: "local", keyEnv: null, hosts: [], rigs: true, terms: "CC0 (unity/Assets/StreamingAssets/Models/CREDITS.md)" };
const BASE = { // game3d/three3d.mjs BASE
  hask: "monarch", ulfgar: "monarch", gorrum: "monarch", thrain: "monarch", corvan: "monarch", odo: "monarch", aurelia: "monarch", maelis: "monarch",
  mira: "hooded_adventurer", wren: "hooded_adventurer", ysolde: "hooded_adventurer", nyx: "hooded_adventurer", tamsin: "hooded_adventurer",
  velka: "hooded_adventurer", sable: "hooded_adventurer", morwenna: "hooded_adventurer", seraphine: "hooded_adventurer",
};

export function create(opts) {
  const src = (id) => path.join(ROOT, "unity/Assets/StreamingAssets/Models", `${BASE[id] ?? "adventurer"}.glb`);
  return {
    info,
    async generate(hero, prompts, dir) {
      const file = path.join(dir, "raw", "model.glb");
      fs.mkdirSync(path.dirname(file), { recursive: true });
      fs.copyFileSync(src(hero), file);
      opts.log(`  copied ${path.basename(src(hero))} (CC0) as ${hero}`);
      return { taskId: `local-${hero}`, files: [file] };
    },
    generateProp: null,
    async rig(hero, gen, dir) {
      const dest = path.join(dir, `SB_Hero_${hero}_LOD0.glb`);
      fs.copyFileSync(gen.files[0], dest); // already rigged and animated
      return { taskId: gen.taskId, files: [dest] };
    },
  };
}
